#region License
/* **********************************************************************************
 * Copyright (c) Roman Ivantsov
 * This source code is subject to terms and conditions of the MIT License
 * for Irony. A copy of the license can be found in the License.txt file
 * at the root of this distribution. 
 * By using this source code in any fashion, you are agreeing to be bound by the terms of the 
 * MIT License.
 * You must not remove this notice from this software.
 * **********************************************************************************/
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Reflection;


namespace Irony.Compiler {
  // Parser class implements LALR(1) parser DFM. Its behavior is controlled by the state transition graph
  // with root in Data.InitialState. Each state contains a dictionary of parser actions indexed by input 
  // element (token or non-terminal node). 
  public class Parser {

    #region Constructors
    public Parser(GrammarData data) {
      Data = data;
    }
    #endregion

    #region Properties and fields: Data, Stack, _context, Input, CurrentState, LineCount, TokenCount
    public readonly GrammarData Data;
    public readonly ParserStack Stack = new ParserStack();

    private CompilerContext _context;
    private bool _caseSensitive;

    public IEnumerator<Token> Input {
      [System.Diagnostics.DebuggerStepThrough]
      get { return _input; }
    } IEnumerator<Token> _input;

    public Token CurrentToken  {
      [System.Diagnostics.DebuggerStepThrough]
      get { return _currentToken; }
    } Token  _currentToken;

    public ParserState CurrentState {
      [System.Diagnostics.DebuggerStepThrough]
      get { return _currentState; }
    } ParserState  _currentState;


    public int LineCount {
      [System.Diagnostics.DebuggerStepThrough]
      get { return _currentLine; }
    } int  _currentLine;

    public int TokenCount  {
      [System.Diagnostics.DebuggerStepThrough]
      get { return _tokenCount; }
    } int  _tokenCount;

    #endregion

    #region Events: ParserAction, TokenReceived
    public event EventHandler<ParserActionEventArgs> ActionSelected;
    public event EventHandler<TokenEventArgs> TokenReceived;
    TokenEventArgs _tokenArgs = new TokenEventArgs(null); //declare as field and reuse it to avoid generating garbage

    protected void OnTokenReceived(Token token) {
      if (TokenReceived == null) return;
      _tokenArgs.Token = token;
      TokenReceived(this, _tokenArgs);
    }
    #endregion

    #region Parsing methods
    private void Reset() {
      Stack.Reset();
      _currentState = Data.InitialState;
      _currentLine = 0;
      _tokenCount = 0;
      _context.Errors.Clear();
    }


    TokenList _previewBuffer = new TokenList();
    private Token ReadToken() {
      while (_input.MoveNext()) {
        Token result = _input.Current;
        _tokenCount++;
        _currentLine = result.Span.Start.Line + 1;
        if (TokenReceived != null)
          OnTokenReceived(result);
        if (result.Terminal.IsSet(TermOptions.IsNonGrammar))
          continue;
        return result;
      }//while
      //Return EOF
      return null; 
    }

    private void NextToken() {
      if (_previewBuffer.Count > 0) {
        _currentToken = _previewBuffer[0];
        _previewBuffer.RemoveAt(0);
        return; 
      }
      _currentToken = ReadToken();
      //  if null, we reached end of file; return EOF token.
      if (_currentToken == null) 
        _currentToken = Token.Create(Grammar.Eof, _context, new SourceLocation(0, _currentLine - 1, 0), string.Empty); 
    }//method

    public Token PreviewSymbols(StringList symbols) {
      //First check the preview buffer
      foreach (Token token in _previewBuffer) {
        if (symbols.Contains(token.Text)) return token; 
      }
      //Now read from input while saving all tokens in preview buffer
      Token tkn;
      while((tkn = ReadToken()) != null) {
        _previewBuffer.Add(tkn);
        if (symbols.Contains(tkn.Text)) return tkn; 
      } 
      return null;
    }//method

    public AstNode Parse(CompilerContext context, IEnumerable<Token> tokenStream) {
      _context = context;
      _caseSensitive = _context.Compiler.Grammar.CaseSensitive;
      Reset();
      _input = tokenStream.GetEnumerator();
      NextToken();
      while (true) {
        if (_currentState == Data.FinalState) {
          AstNode result = Stack[0].Node;
          Stack.Reset();
          return result;
        }
        //check for scammer error
        if (_currentToken.Terminal.Category == TokenCategory.Error) {
          ReportScannerError();
          if (!Recover()) 
            return null; 
          continue;
        }
        //Get action
        ActionRecord action = GetCurrentAction();
        if (action == null) {
          ReportParserError();
          if (!Recover())
            return null; //did not recover
          continue;
        }//action==null

        if (action.HasConflict())
          action = Data.Grammar.OnActionConflict(this, _currentToken, action);
        this.OnActionSelected(_currentToken, action);
        switch (action.ActionType) {
          case ParserActionType.Operator:
            if (GetActionTypeForOperation(_currentToken) == ParserActionType.Shift)
              goto case ParserActionType.Shift;
            else
              goto case ParserActionType.Reduce;

          case ParserActionType.Shift:
            ExecuteShiftAction(action);
            break;

          case ParserActionType.Reduce:
            ExecuteReduceAction(action);
            break;
        }//switch
      }//while
    }//Parse
    #endregion

    #region Error reporting and recovery
    private void ReportError(SourceLocation location, string message, params object[] args) {
      if (args != null && args.Length > 0)
        message = string.Format(message, args);
      _context.AddError(location, message, _currentState);
    }

    private void ReportScannerError() {
      _context.AddError(_currentToken.Location, _currentToken.Text, _currentState);
    }
    
    private void ReportParserError() {
      if (_currentToken.Terminal == Grammar.Eof) {
        ReportError(_currentToken.Location, "Unexpected end of file.");
        return;
      }
      StringList expectedList = GetCurrentExpectedSymbols();
      string message = this.Data.Grammar.GetSyntaxErrorMessage(_context, expectedList); 
      if (message == null) 
        message = "Syntax error" + (expectedList.Count == 0 ? "." : ", expected: " + TextUtils.Cleanup(expectedList.ToString(" ")));
      ReportError(_currentToken.Location, message);
    }

    #region Comment
    //TODO: This needs more work. Currently it reports all individual symbols most of the time, in a message like
    //  "Syntax error, expected: + - < > = ..."; the better method is to group operator symbols under one alias "operator". 
    // The reason is that code picks expected key list at current(!) state only, 
    // slightly tweaking it for non-terminals, without exploring Reduce roots
    // It is quite difficult to discover grouping non-terminals like "operator" in current structure. 
    // One possible solution would be to introduce "ExtendedLookaheads" in ParserState which would include 
    // all NonTerminals that might follow the current position. This list would be calculated at start up, 
    // in addition to normal lookaheads. 
    #endregion
    private StringList GetCurrentExpectedSymbols() {
      BnfTermList inputElements = new BnfTermList();
      StringSet inputKeys = new StringSet();
      inputKeys.AddRange(_currentState.Actions.Keys);
      //First check all NonTerminals
      foreach (NonTerminal nt in Data.NonTerminals) {
        if (!inputKeys.Contains(nt.Key)) continue; 
        //nt is one of our available inputs; check if it has an alias. If not, don't add it to element list;
        // and we have already all its "Firsts" keys in the list. 
        // If yes, add nt to element list and remove
        // all its "fists" symbols from the list. These removed symbols will be represented by single nt alias. 
        if (string.IsNullOrEmpty(nt.DisplayName))
          inputKeys.Remove(nt.Key);
        else {
          inputElements.Add(nt);
          foreach(string first in nt.Firsts) 
            inputKeys.Remove(first);
        }
      }
      //Now terminals
      foreach (Terminal term in Data.Terminals) {
        if (inputKeys.Contains(term.Key))
          inputElements.Add(term);
      }
      StringList result = new StringList();
      foreach(BnfTerm term in inputElements)
        result.Add(string.IsNullOrEmpty(term.DisplayName)? term.Name : term.DisplayName);
      result.Sort();
      return result;
    }

    //TODO: need to rewrite, looks ugly
    private bool Recover() {
      if (_currentToken.Category != TokenCategory.Error)
        _currentToken = Grammar.CreateSyntaxErrorToken(_context, _currentToken.Location, "Syntax error.");
      //Check the current state and states in stack for error shift action - this would be recovery state.
      ActionRecord action = GetCurrentAction();
      if (action == null || action.ActionType == ParserActionType.Reduce) {
        while(Stack.Count > 0) {
          _currentState = Stack.Top.State;
          Stack.Pop(1);
          action = GetCurrentAction();
          if (action != null && action.ActionType != ParserActionType.Reduce) 
            break; //we found shift action for error token
        }//while
      }//if
      if (action == null || action.ActionType == ParserActionType.Reduce) 
        return false; //could not find shift action, cannot recover
      //We found recovery state, and action contains ActionRecord for "error shift". Lets shift it.  
      ExecuteShiftAction(action);//push the error token
      // Now shift all tokens from input that can be shifted. 
      // These are the ones that are found in error production after the error. We ignore all other tokens
      // We stop when we find a state with reduce-only action.
      while (_currentToken.Terminal != Grammar.Eof) {
        //with current token, see if we can shift it. 
        action = GetCurrentAction();
        if (action == null) {
          NextToken(); //skip this token and continue reading input
          continue; 
        }
        if (action.ActionType == ParserActionType.Reduce || action.ActionType == ParserActionType.Operator) {
          //we can reduce - let's reduce and return success - we recovered.
          ExecuteReduceAction(action);
          return true;
        }
        //it is shift action, let's shift
        ExecuteShiftAction(action);
      }//while
      return false; // 
    }
    #endregion

    protected void OnActionSelected(Token input, ActionRecord action) {
      Data.Grammar.OnActionSelected(this, _currentToken, action);
      if (ActionSelected != null) {
        ParserActionEventArgs args = new ParserActionEventArgs(this.CurrentState, input, action);
        ActionSelected(this, args);
      }
    }

    #region Misc private methods
    private ActionRecord GetCurrentAction() {
      ActionRecord action = null;
      if (_currentToken.MatchByValue) {
        string key = CurrentToken.Text;
        if (!_caseSensitive)
          key = key.ToLower();
        if (_currentState.Actions.TryGetValue(key, out action))
          return action;
      }
      if (_currentToken.MatchByType && _currentState.Actions.TryGetValue(_currentToken.Terminal.Key, out action))
        return action;
      return null; //action not found
    }
    private ParserActionType GetActionTypeForOperation(Token current) {
      Terminal thisTerm = current.Terminal;
      for (int i = Stack.Count - 2; i >= 0; i--) {
        if (Stack[i].Node == null) continue;
        BnfTerm term = Stack[i].Node.Term;
        if (!term.IsSet(TermOptions.IsOperator)) continue;
        Terminal prevTerm = term as Terminal;
        //if previous operator has the same precedence then use associativity
        if (prevTerm.Precedence == thisTerm.Precedence) 
          return thisTerm.Associativity == Associativity.Left ? ParserActionType.Reduce : ParserActionType.Shift;
        ParserActionType result = prevTerm.Precedence > thisTerm.Precedence ? ParserActionType.Reduce : ParserActionType.Shift;
        return result;
      }
      //If no operators found on the stack, do simple shift
      return ParserActionType.Shift;
    }
    private void ExecuteShiftAction(ActionRecord action) {
      Stack.Push(_currentToken, _currentState);
      _currentState = action.NewState;
      NextToken();
    }
    private void ExecuteReduceAction(ActionRecord action) {
      ParserState oldState = _currentState;
      int popCnt = action.PopCount;

      //Get new node's child nodes - these are nodes being popped from the stack 
      AstNodeList childNodes = new AstNodeList();
      for (int i = 0; i < action.PopCount; i++) {
        AstNode child = Stack[Stack.Count - popCnt + i].Node;
        if (!child.Term.IsSet(TermOptions.IsPunctuation)) 
          childNodes.Add(child);
      }
      //recover state, location and pop the stack
      SourceSpan newNodeSpan;
      if (popCnt == 0) {
        newNodeSpan = new SourceSpan(_currentToken.Location, 0);
      } else {
        SourceLocation firstPopLoc = Stack[Stack.Count - popCnt].Node.Location;
        int lastPopEndPos = Stack[Stack.Count - 1].Node.Span.EndPos;
        newNodeSpan = new SourceSpan(firstPopLoc, lastPopEndPos - firstPopLoc.Position);
        _currentState = Stack[Stack.Count - popCnt].State;
        Stack.Pop(popCnt);
      }
      //Create new node
      AstNode node = CreateNode(action, newNodeSpan, childNodes);
      // Push node/current state into the stack 
      Stack.Push(node, _currentState);
      //switch to new state
      ActionRecord gotoAction;
      if (_currentState.Actions.TryGetValue(action.NonTerminal.Key, out gotoAction)) {
        _currentState = gotoAction.NewState;
      } else 
        //should never happen
        throw new CompilerException( string.Format("Cannot find transition for input {0}; state: {1}, popped state: {2}", 
              action.NonTerminal, oldState, _currentState));
    }//method

    private AstNode CreateNode(ActionRecord reduceAction, SourceSpan sourceSpan, AstNodeList childNodes) {
      NonTerminal nt = reduceAction.NonTerminal;
      AstNode result;

      AstNodeArgs args = new AstNodeArgs(nt, _context, sourceSpan, childNodes);
      result = nt.InvokeNodeCreator(args);
      if (result != null) return result;

      Type defaultNodeType = _context.Compiler.Grammar.DefaultNodeType;
      Type ntNodeType = nt.NodeType ?? defaultNodeType ?? typeof(AstNode);

      // Check if NonTerminal is a list
      // List nodes are produced by .Plus() or .Star() methods of BnfElement
      // In this case, we have a left-recursive list formation production:   
      //     ntList -> ntList + delim? + ntElem
      //  We check if we have already created the list node for ntList (in the first child); 
      //  if yes, we use this child as a result directly, without creating new list node. 
      //  The other incoming child - the last one - is a new list member; 
      // we simply add it to child list of the result ntList node. Optional "delim" node is simply thrown away.
      bool isList = nt.IsSet(TermOptions.IsList);
      if (isList && childNodes.Count > 1 && childNodes[0].Term == nt) {
        result = childNodes[0];
        AstNode newChild = childNodes[childNodes.Count - 1];
        newChild.Parent = result; 
        result.ChildNodes.Add(newChild);
        return result;
      }
      //Check for StarList produced by MakeStarList; in this case the production is:  ntList -> Empty | Elem+
      // where Elem+ is non-empty list of elements. The child list we are actually interested in is one-level lower
      if (nt.IsSet(TermOptions.IsStarList) && childNodes.Count == 1) {
        childNodes = childNodes[0].ChildNodes;
      }
      // Check for "node-bubbling" case. For identity productions like 
      //   A -> B
      // the child node B is usually a subclass of node A, 
      // so child node B can be used directly in place of the A. So we simply return child node as a result. 
      // TODO: probably need a grammar option to enable/disable this behavior explicitly
      if (!isList && !nt.IsSet(TermOptions.IsPunctuation) && childNodes.Count == 1) {
        Type childNodeType = childNodes[0].Term.NodeType ?? defaultNodeType ?? typeof(AstNode);
        if (childNodeType == ntNodeType || childNodeType.IsSubclassOf(ntNodeType))
          return childNodes[0];
      }
      // Try using Grammar's CreateNode method
      result = Data.Grammar.CreateNode(_context, reduceAction, sourceSpan, childNodes);
      if (result == null) {
        //Finally create node directly. For perf reasons we try using "new" for AstNode type (faster), and
        // activator for all custom types (slower)
        if (ntNodeType == typeof(AstNode))
          result = new AstNode(args);
        else
#if PocketPC || SILVERLIGHT
        {
          ConstructorInfo ctor = ntNodeType.GetConstructor(new Type[] { typeof(AstNodeArgs) });
          if (ctor == null)
            throw new Exception("Failed to located constructor: " + ntNodeType.ToString() + "(AstNodeArgs args)");
          result = (AstNode)ctor.Invoke(new object[] { args });
        }
#else
        {
          result = (AstNode)Activator.CreateInstance(ntNodeType, args);
        }
#endif
      }
      if (result != null)
        nt.OnNodeCreated(result);
      return result;
    }
    #endregion

  }//class

}//namespace
