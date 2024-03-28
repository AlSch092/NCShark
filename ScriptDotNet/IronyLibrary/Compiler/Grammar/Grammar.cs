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

namespace Irony.Compiler {

  public class Grammar {

    #region properties: CaseSensitive, WhitespaceChars, Delimiters ExtraTerminals, Root, TokenFilters
    public bool CaseSensitive = true;
    //List of chars that unambigously identify the start of new token. 
    //used in scanner error recovery, and in quick parse path in Number literals 
    public string Delimiters = ",;[](){}";

    public string WhitespaceChars = " \t\r\n\v";
    public string LineTerminators = "\n\r\v";

    //Terminals not present in grammar expressions and not reachable from the Root
    // (Comment terminal is usually one of them)
    // Tokens produced by these terminals will be ignored by parser input. 
    public readonly TerminalList NonGrammarTerminals = new TerminalList();

    //Terminals that either don't have explicitly declared Firsts symbols, or can start with chars not covered by these Firsts 
    // For ex., identifier in c# can start with a Unicode char in one of several Unicode classes, not necessarily latin letter.
    //  Whenever terminals with explicit Firsts() cannot produce a token, the Scanner would call terminals from this fallback 
    // collection to see if they can produce it. 
    // Note that IdentifierTerminal automatically add itself to this collection if its StartCharCategories list is not empty, 
    // so programmer does not need to do this explicitly
    public readonly TerminalList FallbackTerminals = new TerminalList();

    //Default node type; if null then GenericNode type is used. 
    public Type DefaultNodeType = typeof(AstNode);

    public NonTerminal Root  {
      [System.Diagnostics.DebuggerStepThrough]
      get { return _root; }
      set { _root = value;  }
    } NonTerminal _root;

    public TokenFilterList TokenFilters = new TokenFilterList();
    #endregion 

    #region Register methods
    public void RegisterPunctuation(params string[] symbols) {
      foreach (string symbol in symbols) {
        SymbolTerminal term = SymbolTerminal.GetSymbol(symbol);
        term.SetOption(TermOptions.IsPunctuation);
      }
    }
    
    public void RegisterPunctuation(params BnfTerm[] elements) {
      foreach (BnfTerm term in elements) 
        term.SetOption(TermOptions.IsPunctuation);
    }

    public void RegisterOperators(int precedence, params string[] opSymbols) {
      RegisterOperators(precedence, Associativity.Left, opSymbols);
    }

    public void RegisterOperators(int precedence, Associativity associativity, params string[] opSymbols) {
      foreach (string op in opSymbols) {
        SymbolTerminal opSymbol = SymbolTerminal.GetSymbol(op);
        opSymbol.SetOption(TermOptions.IsOperator, true);
        opSymbol.Precedence = precedence;
        opSymbol.Associativity = associativity;
      }
    }//method

    public void RegisterBracePair(string openBrace, string closeBrace) {
      SymbolTerminal openS = SymbolTerminal.GetSymbol(openBrace);
      SymbolTerminal closeS = SymbolTerminal.GetSymbol(closeBrace);
      openS.SetOption(TermOptions.IsOpenBrace);
      openS.IsPairFor = closeS;
      closeS.SetOption(TermOptions.IsCloseBrace);
      closeS.IsPairFor = openS;
    }
    #endregion

    #region virtual methods: TryMatch, CreateNode, GetSyntaxErrorMessage
    //This method is called if Scanner failed to produce token
    public virtual Token TryMatch(CompilerContext context, ISourceStream source) {
      return null;
    }
    // Override this method in language grammar if you want a custom node creation mechanism.
    public virtual AstNode CreateNode(CompilerContext context, ActionRecord reduceAction, 
                                      SourceSpan sourceSpan, AstNodeList childNodes) {
      return null;      
    }
    public virtual string GetSyntaxErrorMessage(CompilerContext context, StringList expectedList) {
      return null; //Irony then would construct default message
    }
    public virtual void OnActionSelected(Parser parser, Token input, ActionRecord action) {
    }
    public virtual ActionRecord OnActionConflict(Parser parser, Token input, ActionRecord action) {
      return action;
    }
    #endregion

    #region Static utility methods used in custom grammars: Symbol(), ToElement, WithStar, WithPlus, WithQ
    protected static SymbolTerminal Symbol(string symbol) {
      return SymbolTerminal.GetSymbol(symbol);
    }
    protected static SymbolTerminal Symbol(string symbol, string name) {
      return SymbolTerminal.GetSymbol(symbol, name);
    }
    protected static BnfTerm ToElement(BnfExpression expression) {
      string name = expression.ToString();
      return new NonTerminal(name, expression);
    }
    protected static BnfTerm WithStar(BnfExpression expression) {
      return ToElement(expression).Star();
    }
    protected static BnfTerm WithPlus(BnfExpression expression) {
      return ToElement(expression).Plus();
    }
    protected static BnfTerm WithQ(BnfExpression expression) {
      return ToElement(expression).Q();
    }
    public static Token CreateSyntaxErrorToken(CompilerContext context, SourceLocation location, string message, params object[] args) {
      if (args != null && args.Length > 0)
        message = string.Format(message, args);
      return Token.Create(Grammar.SyntaxError, context, location, message);
    }
    #endregion


    public BnfExpression MakePlusRule(NonTerminal listNonTerminal, BnfTerm delimiter, BnfTerm listMember) {
      listNonTerminal.SetOption(TermOptions.IsList);
      if (delimiter == null)
        listNonTerminal.Rule = listMember | listNonTerminal + listMember;
      else
        listNonTerminal.Rule = listMember | listNonTerminal + delimiter + listMember;
      return listNonTerminal.Rule;
    }
    public BnfExpression MakeStarRule(NonTerminal listNonTerminal, BnfTerm delimiter, BnfTerm listMember) {
      if (delimiter == null) {
        //it is much simpler case
        listNonTerminal.SetOption(TermOptions.IsList);
        listNonTerminal.Rule = Empty | listNonTerminal + listMember;
        return listNonTerminal.Rule;
      }
      NonTerminal tmp = new NonTerminal(listMember.Name + "+");
      MakePlusRule(tmp, delimiter, listMember);
      listNonTerminal.Rule = Empty | tmp;
      listNonTerminal.SetOption(TermOptions.IsStarList);
      return listNonTerminal.Rule;
    }

    #region Standard terminals: EOF, Empty, NewLine, Indent, Dedent
    // Empty object is used to identify optional element: 
    //    term.Rule = term1 | Empty;
    public readonly static Terminal Empty = new Terminal("EMPTY");
    // The following terminals are used in indent-sensitive languages like Python;
    // they are not produced by scanner but are produced by CodeOutlineFilter after scanning
    public readonly static Terminal NewLine = new Terminal("LF", TokenCategory.Outline);
    public readonly static Terminal Indent = new Terminal("INDENT", TokenCategory.Outline);
    public readonly static Terminal Dedent = new Terminal("DEDENT", TokenCategory.Outline);
    // Identifies end of file
    // Note: using Eof in grammar rules is optional. Parser automatically adds this symbol 
    // as a lookahead to Root non-terminal
    public readonly static Terminal Eof = new Terminal("EOF", TokenCategory.Outline);

    //End-of-Statement terminal
    public readonly static Terminal Eos = new Terminal("EOS", TokenCategory.Outline);

    public readonly static Terminal SyntaxError = new Terminal("SYNTAX_ERROR", TokenCategory.Error);
    #endregion

        
  }//class

}//namespace
