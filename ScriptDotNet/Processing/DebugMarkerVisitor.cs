﻿using System;
using System.Collections.Generic;
using System.Text;
using Irony.Compiler;
using ScriptNET.Ast;

namespace ScriptNET.Processing
{
  //Script s = Script.Compile(@"1+1; 2+2;", new ScriptNET.Processing.IPostProcessing[] { new ScriptNET.Processing.DebugMarkerVisitor() }, false);
  //Console.Write(s.SyntaxTree);
  internal class DebugMarkerVisitor : IPostProcessing
  {
    #region IPostProcessing Members

    public void BeginProcessing(Script script)
    {
      
    }

    public void EndProcessing(Script script)
    {
      
    }

    #endregion

    #region IAstVisitor Members

    public void BeginVisit(AstNode node)
    {
      ScriptElements elements = node as ScriptElements;
      if (elements == null) return;

      List<AstNode> modified = new List<AstNode>();
      foreach (AstNode child in elements.ChildNodes)
      {
        modified.Add(new DebugNode());
        modified.Add(child);
      }

      elements.ChildNodes.Clear();
      elements.ChildNodes.AddRange(modified);
    }

    public void EndVisit(AstNode node)
    {
      
    }

    #endregion
  }

  internal class DebugNode : AstNode
  {
    public DebugNode()
      : base(new AstNodeArgs(new BnfTerm("[Debug]", "DEBUG"),null, new SourceSpan(), null))
    {
      
    }
  }
}
