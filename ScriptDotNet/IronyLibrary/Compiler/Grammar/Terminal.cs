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

  public class TerminalList : List<Terminal> { }
  

  public class Terminal : BnfTerm {

    public Terminal(string name)  : base(name) {
      Nullable = false; 
      this.NodeType = typeof(Token);
    }
    public Terminal(string name, TokenCategory category)  : this(name) {
      Category = category;
    }
    public Terminal(string name, TokenMatchMode matchMode) : this(name) {
      this.MatchMode = matchMode;
    }

    #region fields and properties
    public TokenMatchMode MatchMode = TokenMatchMode.ByValueThenByType;
    public TokenCategory Category = TokenCategory.Content;
    public int Precedence = int.MaxValue;
    public Associativity Associativity = Associativity.Neutral;
    public Terminal IsPairFor;
    // Priority is used when more than one terminal may match the input char. 
    // It determines the order in which terminals will try to match input for a given char in the input.
    // For a given input char the scanner uses the hash table to look up the collection of terminals that may match this input symbol. 
    // It is the order in this collection that is determined by Priority property - the higher the priority, 
    // the earlier the terminal gets a chance to check the input. 
    public int Priority; //default is 0

    #endregion

    #region virtuals
    public virtual Token TryMatch(CompilerContext context, ISourceStream source) {
      return null;
    }
    //"Firsts" (chars) collections are used for quick search for possible matching terminal(s) using current character in the input stream.
    // A terminal might declare no firsts. In this case, the terminal is tried for match for any current input character. 
    public virtual IList<string> GetFirsts() {
      return null;
    }
    #endregion

    #region static comparison methods
    public static int ByName(Terminal x, Terminal y) {
      return string.Compare(x.ToString(), y.ToString());
    }
    public static int ByPriorityReverse(Terminal x, Terminal y) {
      if (x.Priority > y.Priority)
        return -1;
      if (x.Priority == y.Priority)
        return 0;
      return 1;
    }
    #endregion

  }//class



}
