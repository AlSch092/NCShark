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
  //This terminal allows to declare a set of constants in the input language
  // It should be used when constant symbols do not look like normal identifiers; e.g. in Scheme, #t, #f are true/false
  // constants, and they don't fit into Scheme identifier pattern.
  public class ConstantsTable : Dictionary<string, object> { }
  public class ConstantTerminal : Terminal {
    public ConstantTerminal(string name)     : base(name) {
      base.SetOption(TermOptions.IsConstant);
    }
    public readonly ConstantsTable Table = new ConstantsTable();
    public void Add(string lexeme, object value) {
      this.Table[lexeme] = value;
    }
    public override Token TryMatch(CompilerContext context, ISourceStream source) {
      string text = source.Text;
      foreach (string lexeme in Table.Keys) {
        if (source.Position + lexeme.Length > text.Length) continue;
        if (!source.MatchSymbol(lexeme, !Grammar.CaseSensitive)) continue; 
        Token tkn = Token.Create(this, context, source.TokenStart, lexeme, Table[lexeme]);
        source.Position += lexeme.Length;
        return tkn;
      }
      return null;
    }
    public override IList<string> GetFirsts() {
      string[] array = new string[Table.Count];
      Table.Keys.CopyTo(array, 0);
      return array;
    }

  }//class  



}
