﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace ScriptNET.Runtime
{
  public partial class ObjectBinder
  {
    protected abstract class BaseHandler
    {
      IObjectBinder binder;

      public BaseHandler(IObjectBinder parent)
      {
        binder = parent;
      }

      protected bool CanBind(MemberInfo member)
      {
        return binder.CanBind(member);
      }
    }

    protected class PropertyHandler : BaseHandler, IGetter, ISetter, IHandler
    {
      public PropertyHandler(IObjectBinder parent):base(parent)
      {
      }

      #region IGetter Members
      public object Get(string name, object instance, Type type, params object[] arguments)
      {
        PropertyInfo pi = GetProperty(type, name);
        if (pi == null) return NoResult;
        if (!CanBind(pi)) return NoResult;

        return pi.GetValue(instance, arguments);
      }
      #endregion

      #region ISetter Members

      public object Set(string name, object instance, Type type, object value, params object[] arguments)
      {
        PropertyInfo pi = GetProperty(type, name);
        if (pi == null) return NoResult;
        if (!CanBind(pi)) return NoResult;

        if (value != null && pi.PropertyType.IsAssignableFrom(value.GetType()))
        {
          pi.SetValue(instance, value, null);
        }
        else
        {
          pi.SetValue(instance, RuntimeHost.Binder.ConvertTo(value, pi.PropertyType), arguments);
        }
        return value;
      }

      private PropertyInfo GetProperty(Type type, string name)
      {
        return type.GetProperties(ObjectBinder.PropertyFilter).Where(pi => pi.Name == name).FirstOrDefault();
      }

      #endregion
    }

    protected class FieldHandler : BaseHandler, IGetter, ISetter, IHandler
    {
      public FieldHandler(IObjectBinder parent)
        : base(parent)
      {
      }

      #region IGetter Members
      public object Get(string name, object instance, Type type, params object[] arguments)
      {
        FieldInfo fi = type.GetField(name, ObjectBinder.FieldFilter);
        if (fi == null) return NoResult;
        if (!CanBind(fi)) return NoResult;

        return fi.GetValue(instance);
      }
      #endregion

      #region ISetter Members

      public object Set(string name, object instance, Type type, object value, params object[] arguments)
      {
        FieldInfo fi = type.GetField(name, ObjectBinder.FieldFilter);
        if (fi == null) return NoResult;
        if (!CanBind(fi)) return NoResult;

        fi.SetValue(instance, RuntimeHost.Binder.ConvertTo(value, fi.FieldType));
        return value;
      }

      #endregion
    }

    protected class EventHandler : BaseHandler, IGetter, ISetter, IHandler
    {
      public EventHandler(IObjectBinder parent)
        : base(parent)
      {
      }

      #region IGetter Members
      public object Get(string name, object instance, Type type, params object[] arguments)
      {
        EventInfo ei = type.GetEvent(name, ObjectBinder.PropertyFilter);
        if (ei == null) return NoResult;
        if (!CanBind(ei)) return NoResult;

        //Type eventHelper = typeof(EventHelper<>);
        //Type actualHelper = eventHelper.MakeGenericType(ei.EventHandlerType);
        return ei;
      }
      #endregion

      #region ISetter Members

      public object Set(string name, object instance, Type type, object value, params object[] arguments)
      {
        EventInfo ei = type.GetEvent(name, ObjectBinder.PropertyFilter);
        if (ei == null) return NoResult;
        if (!CanBind(ei)) return NoResult;

        if (!(value is RemoveDelegate))
          EventBroker.AssignEvent(ei, instance, (IInvokable)value);
        else
          EventBroker.RemoveEvent(ei, instance, (IInvokable)value);

        return value;
      }

      #endregion
    }

    protected class MethodGetter : BaseHandler, IGetter
    {
      public MethodGetter(IObjectBinder parent)
        : base(parent)
      {
      }

      #region IGetter Members
      public object Get(string name, object instance, Type type, params object[] arguments)
      {
        MethodInfo[] methods = type.GetMethods(ObjectBinder.MethodFilter).Where(m => m.Name == name && CanBind(m)).ToArray();
        if (methods == null || methods.Length == 0) return NoResult;

        return new LateBoundMethod(name, instance);
      }
      #endregion
    }

    //protected class MutantHandler : IGetter, ISetter, IHandler
    //{
    //  #region IGetter Members
    //  public object Get(string name, object instance, Type type, params object[] arguments)
    //  {
    //    IMutant dm = instance as IMutant;
    //    if (dm == null) return NoResult;

    //    return dm.Get(name, null);
    //  }
    //  #endregion

    //  #region ISetter Members

    //  public object Set(string name, object instance, Type type, object value, params object[] arguments)
    //  {
    //    IMutant dm = instance as IMutant;
    //    if (dm == null) return NoResult;
    //    dm.Set(name, value, null);

    //    return value;
    //  }

    //  #endregion
    //}

    protected class ScriptableHandler : BaseHandler, IGetter, ISetter, IHandler
    {
      public ScriptableHandler(IObjectBinder parent)
        : base(parent)
      {
      }

      #region IGetter Members
      public object Get(string name, object instance, Type type, params object[] arguments)
      {
        IScriptable dm = instance as IScriptable;
        if (dm == null) return NoResult;

        return dm.GetMember(name, arguments).GetValue();
      }
      #endregion

      #region ISetter Members

      public object Set(string name, object instance, Type type, object value, params object[] arguments)
      {
        IScriptable dm = instance as IScriptable;
        if (dm == null) return NoResult;

        dm.GetMember(name, arguments).SetValue(value);
        return value;
      }

      #endregion
    }


    protected class NestedTypeGetter : BaseHandler, IGetter
    {
      public NestedTypeGetter(IObjectBinder parent)
        : base(parent)
      {
      }

      #region IGetter Members
      public object Get(string name, object instance, Type type, params object[] arguments)
      {
        Type nested = type.GetNestedType(name, ObjectBinder.NestedTypeFilter);
        if (nested == null) return NoResult;

        return nested;
      }
      #endregion
    }

    //protected class NameSpaceGetter : IGetter
    //{
    //    #region IGetter Members
    //    public object Get(string name, object instance, Type type, params object[] arguments)
    //    {
    //        NameSpaceMutant nameSpace = NameSpaceCache.Get(type.Namespace); // new NameSpaceMutant(type.Namespace);
    //        object rez = nameSpace.Get(name, null);
    //        if (rez == null) return NoResult;

    //        return rez;
    //    }
    //    #endregion
    //}
  }
}
