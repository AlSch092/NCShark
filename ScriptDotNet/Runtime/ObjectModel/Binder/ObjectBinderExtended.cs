﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace ScriptNET.Runtime
{
  /// <summary>
  /// Default object binder used by Runtime
  /// </summary>
  public class ObjectBinderExtended : ObjectBinder, IObjectBinder
  {
    IObjectBind IObjectBinder.BindToMethod(object target, string methodName, Type[] genericParameters, object[] arguments)
    {
      IScriptable scriptable = target as IScriptable;
      if (scriptable != null)
      {
        IObjectBind bind = scriptable.GetMethod(methodName, null);
        if (bind == null)
          bind = base.BindToMethod(scriptable.Instance, methodName, genericParameters, arguments);
        else
          return new DynamicMethodBind(scriptable, bind, arguments);

        if (bind != null) return bind;
      }

      return base.BindToMethod(target, methodName, genericParameters, arguments);
    }

    IObjectBind IObjectBinder.BindToIndex(object target, object[] arguments, bool setter)
    {
      IScriptable scriptable = target as IScriptable;
      if (scriptable != null)
      {
        IObjectBind bind = base.BindToIndex(scriptable.Instance, arguments, setter);
        if (bind != null) return bind;
      }

      return base.BindToIndex(target, arguments, setter);
    }

    #region DynamicMethodBind
    protected class DynamicMethodBind : IObjectBind
    {
      IScriptable scriptable;
      IObjectBind dynamicMethod;
      object[] arguments;

      public DynamicMethodBind(IScriptable scriptable, IObjectBind dynamicMethod, object[] arguments)
      {
        this.scriptable = scriptable;
        this.dynamicMethod = dynamicMethod;
        this.arguments = arguments;
      }

      #region IInvokable Members

      public bool CanInvoke()
      {
        return scriptable != null && dynamicMethod != null;
      }

      public object Invoke(IScriptContext context, object[] args)
      {
        context.CreateScope();
        context.SetItem("me", scriptable.Instance);
        context.SetItem("body", scriptable);

        object rez = RuntimeHost.NullValue;
        try
        {
          rez = dynamicMethod.Invoke(context, arguments);
        }
        finally
        {
          context.RemoveLocalScope();
        }

        if (rez != RuntimeHost.NullValue)
        {
          return rez;
        }
        else
        {
          throw new ScriptException(string.Format("Dynamic method call failed in object {0}", scriptable.ToString()));
        }
      }

      #endregion
    }
    #endregion
  }
}
