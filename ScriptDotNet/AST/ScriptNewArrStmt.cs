#region using
using System;

using Irony.Compiler;
using ScriptNET.Runtime;
using System.Globalization;
#endregion

namespace ScriptNET.Ast
{
  /// <summary>
  /// 
  /// </summary>
  internal class ScriptNewArrStmt : ScriptExpr
  {
    private ScriptTypeExpr constrExpr;
    private ScriptArrayResolution arrRank;

    public ScriptNewArrStmt(AstNodeArgs args)
      : base(args)
    {
      constrExpr = ChildNodes[1] as ScriptTypeExpr;
      arrRank = ChildNodes[2] as ScriptArrayResolution;
    }

    //TODO: Refactor
    public override void Evaluate(IScriptContext context)
    {
      constrExpr.Evaluate(context);
      Type type = (Type)context.Result;

      arrRank.Evaluate(context);
      int rank = (int)Convert.ChangeType(((object[])context.Result)[0], typeof(int), CultureInfo.CurrentCulture.NumberFormat);

      context.Result = Array.CreateInstance(type, rank);
    }
  }
}
