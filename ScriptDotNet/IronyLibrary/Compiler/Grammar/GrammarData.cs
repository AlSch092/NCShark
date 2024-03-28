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
  // GrammarData is a container for all information used by Parser and Scanner in input processing.
  // The state graph entry is InitialState state; the state graph encodes information usually contained 
  // in what is known in literature as transiton/goto tables.
  // The graph is built from the language grammar by GrammarDataBuilder instance. 
  // See Dragon book or other book on compilers on details of LALR parsing and parsing tables construction. 
  public class GrammarData {
    public Grammar Grammar;
    public NonTerminal AugmentedRoot;
    public ParserState InitialState;
    public ParserState FinalState;
    public readonly NonTerminalList NonTerminals = new NonTerminalList();
    public readonly TerminalList Terminals = new TerminalList();
    public readonly TerminalLookupTable TerminalsLookup = new TerminalLookupTable(); //hash table for fast terminal lookup by input char
    public readonly TerminalList FallbackTerminals = new TerminalList(); //terminals that have no explicit prefixes
    public readonly ProductionList Productions = new ProductionList();
    public readonly ParserStateList States = new ParserStateList();
    public readonly StringSet Errors = new StringSet();
    public string ScannerRecoverySymbols = "";
    public bool AnalysisCanceled;  //True if grammar analysis was canceled due to errors
  }

  public class TerminalLookupTable : Dictionary<char, TerminalList> { }

  public partial class ParserState {
    public readonly string Name;
    public readonly ActionRecordTable Actions = new ActionRecordTable();
    public readonly LRItemList Items = new LRItemList();

    public ParserState(string name, LRItem item) {
      Name = name;
      Items.Add(item);
    }
    public ParserState(string name, LR0ItemList coreItems) {
      Name = name;
      foreach (LR0Item coreItem in coreItems)
        Items.Add(new LRItem(this, coreItem));
    }
    public override string ToString() {
      return Name;
    }
  }//class

  public class ParserStateList : List<ParserState> { }
  public class ParserStateTable : Dictionary<string, ParserState> { } //hash table

  public class ActionRecord {
    //public BnfElement Input;
    public string Key;
    public ParserActionType ActionType = ParserActionType.Shift;
    public ParserState NewState;
    public ProductionList ReduceProductions = new ProductionList(); //may be more than one, in case of conflict

/*    //used for shift actions
    internal ActionRecord(string key, ParserState newState) {
      //Input = input;
      Key = key; // input.Key;
      NewState = newState;
      ActionType = ParserActionType.Shift;
    }
    //used for reduce actions
    internal ActionRecord(string key, Production reduceProduction) {
      //Input = input;
      Key = key; // input.Key;
      ReduceProductions.Add(reduceProduction);
      ActionType = ParserActionType.Reduce;
    }
 */ 
    internal ActionRecord(string key, ParserActionType type, ParserState newState, Production reduceProduction) {
      this.Key = key;
      this.ActionType = type;
      this.NewState = newState; 
      if (reduceProduction != null)
        ReduceProductions.Add(reduceProduction);
    }
    public ActionRecord CreateDerived(ParserActionType type, Production reduceProduction) {
      return new ActionRecord(this.Key, type, this.NewState, reduceProduction);
    }

    public Production Production { 
      get {return ReduceProductions.Count > 0? ReduceProductions[0] : null;}
    }
    public NonTerminal NonTerminal {
      get { return Production == null? null: Production.LValue; }
    }
    public int PopCount {
      get { return Production.RValues.Count;}
    }
    public bool HasConflict() {
      switch (ActionType) {
        case ParserActionType.Shift:
          return ReduceProductions.Count > 0;
        case ParserActionType.Reduce:
          return ReduceProductions.Count > 1;
        case ParserActionType.Operator:
          return true;
      }//switch
      return false;
    }
    public override string ToString() {
      string result = ActionType.ToString();
      if (ActionType == ParserActionType.Reduce && ReduceProductions.Count > 0)
        result += " on " + ReduceProductions[0];
      return result;
    }

  }//class ActionRecord

  public class ActionRecordTable : Dictionary<string, ActionRecord> { }

  public class Production {
    public readonly bool IsInitial;
    public readonly bool HasTerminals;
    public readonly bool IsError;                                  //means contains Error terminal
    public readonly NonTerminal LValue;                            // left-side element
    public readonly BnfTermList RValues = new BnfTermList(); //the right-side elements sequence
    public readonly LR0ItemList LR0Items = new LR0ItemList();      //LR0 items based on this production 
    public Production(bool isInitial, NonTerminal lvalue, BnfTermList rvalues) {
      LValue = lvalue;
      //copy RValues skipping Empty pseudo-terminal
      foreach (BnfTerm rv in rvalues)
        if (rv != Grammar.Empty)
          RValues.Add(rv);
      //Calculate flags
      foreach (BnfTerm term in RValues) {
        Terminal terminal = term as Terminal;
        if (terminal == null) continue;
        HasTerminals = true;
        if (terminal.Category == TokenCategory.Error) IsError = true;
      }//foreach
      //Note that we add an extra LR0Item with p = RValues.Count
      for (int p = 0; p <= RValues.Count; p++)
        LR0Items.Add(new LR0Item(this, p));
    }//constructor

    public bool IsEmpty() {
      return RValues.Count == 0; 
    }

    public override string ToString() {
      return ToString(-1); //no dot
    }

    //Utility method used by Production and LR0Item
    internal string ToString(int dotPosition) {
      char dotChar = '\u00B7'; //dot in the middle of the line
      StringBuilder bld = new StringBuilder();
      bld.Append(LValue.Name);
      bld.Append(" -> ");
      for (int i = 0; i < RValues.Count; i++) {
        if (i == dotPosition)
          bld.Append(dotChar);
        bld.Append(RValues[i].Name);
        bld.Append(" ");
      }//for i
      if (dotPosition == RValues.Count)
        bld.Append(dotChar);
      return bld.ToString();
    }

  }//Production class

  public class ProductionList : List<Production> { }

  public class LRItem {
    public readonly ParserState State;
    public readonly LR0Item Core;
    public readonly LRItemList PropagateTargets = new LRItemList(); //used for lookaheads propagation
    public readonly StringSet Lookaheads = new StringSet();
    public readonly StringSet NewLookaheads = new StringSet();
    public LRItem(ParserState state, LR0Item core) {
      State = state;
      Core = core;
    }
    public override string ToString() {
      return Core.ToString() + "  LOOKAHEADS: " + TextUtils.Cleanup(Lookaheads.ToString(" "));
    }
  }//LRItem class

  public class LRItemList : List<LRItem> { }

  public partial class LR0Item {
    public readonly Production Production;
    public readonly int Position;
    public readonly StringSet TailFirsts = new StringSet(); //tail is a set of elements after the "after-dot-element"
    public bool TailIsNullable = false;
    //automatically generated IDs - used for building key for list of kernel LR0Items
    // which in turn are used to quickly lookup parser states in hash
    internal int ID;
    internal static int _maxID;
    private string _toString;

    public LR0Item(Production production, int position) {
      Production = production;
      Position = position;
      ID = _maxID++;
      _toString = Production.ToString(Position);
    }
    public BnfTerm NextElement {
      get {
        if (Position < Production.RValues.Count)
          return Production.RValues[Position];
        else
          return null;
      }
    }
    public bool IsKernel {
      get { return Position > 0 || (Production.IsInitial && Position == 0); }
    }
    public override string ToString() {
      return _toString;
    }
  }//LR0Item

  public class LR0ItemList : List<LR0Item> { }


}//namespace
