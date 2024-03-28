﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace ScriptNET.Runtime
{
  internal class LateBoundMethod : IObjectBind
  {
    public string MethodName { get; private set; }
    public object Target { get; private set; }
    public MethodInfo MethodInfo { get; private set; }

    public LateBoundMethod(string methodName, object target)
    {
      MethodName = methodName;
      Target = target;
    }

    public LateBoundMethod(MethodInfo methodInfo, object target)
    {
      MethodInfo = methodInfo;
      MethodName = methodInfo.Name;
      Target = target;
    }

    #region IInvokable Members

    public bool CanInvoke()
    {
      return Target != null;
    }

    public virtual object Invoke(IScriptContext context, object[] args)
    {
      object result = RuntimeHost.NullValue;

      IObjectBind bind = null;

      if (MethodInfo == null)
        bind = RuntimeHost.Binder.BindToMethod(Target, MethodName, null, args);
      else
        bind = RuntimeHost.Binder.BindToMethod(Target, MethodInfo, args);
      
      if (bind != null)
        result = bind.Invoke(context, null);
      else
        throw new ScriptMethodNotFoundException(MethodName);

      context.Result = result;
      return result;
    }
    #endregion

#if !PocketPC && !SILVERLIGHT
    //TODO: Review this approach
    public static implicit operator IntPtr(LateBoundMethod invokableMethod)
    {
      if (invokableMethod == null) throw new ArgumentNullException();
      if (invokableMethod.MethodName == null) throw new NotSupportedException();
      if (invokableMethod.Target == null) throw new NotSupportedException();

      return invokableMethod.Target.GetType().GetMethod(invokableMethod.MethodName).MethodHandle.GetFunctionPointer();
    }
#endif
  }
}
