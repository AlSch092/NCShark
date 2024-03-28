﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;

using ScriptNET;

namespace ScriptNET.Runtime
{
  /// <summary>
  /// Manages event subscriptions from the script
  /// </summary>
  [Bindable(false)]
  public abstract class EventBroker
  {
    public IInvokable Target
    {
      get;
      set;
    }

    public static readonly string UnsubscribeAllEvents = "UnsubscribeAllEvents";

    public static Dictionary<object, Dictionary<EventInfo, Delegate>> subscriptions = new Dictionary<object, Dictionary<EventInfo, Delegate>>();

    private static void AddInvokation(object target, EventInfo ei, Delegate deleg)
    {
      if (!subscriptions.ContainsKey(target))
        subscriptions.Add(target, new Dictionary<EventInfo, Delegate>());

      if (subscriptions[target].ContainsKey(ei))
        throw new ScriptEventException("Duplicate event subscription");

      subscriptions[target].Add(ei, deleg);
    }

    public static void AssignEvent(EventInfo ei, object targetObject, IInvokable function)
    {
      Type eventHelper = typeof(EventHelper<>);
      Type actualHelper = eventHelper.MakeGenericType(
        ei.GetAddMethod().GetParameters()[0].ParameterType.GetMethod("Invoke").GetParameters()[1].ParameterType);
      MethodInfo eventHandler = actualHelper.GetMethod("EventHandler");
      EventBroker target = (EventBroker)Activator.CreateInstance(actualHelper);
      target.Target = function;
#if PocketPC || SILVERLIGHT
      Delegate deleg = Delegate.CreateDelegate(ei.EventHandlerType, target, eventHandler);
#else
      Delegate deleg = Delegate.CreateDelegate(ei.EventHandlerType, target, eventHandler, false);
#endif
      ei.AddEventHandler(targetObject, deleg);
      AddInvokation(targetObject, ei, deleg);
    }

    public static void RemoveEvent(EventInfo ei, object targetObject, IInvokable function)
    {
      if (subscriptions[targetObject].ContainsKey(ei))
      {
        ei.RemoveEventHandler(targetObject, subscriptions[targetObject][ei]);
        subscriptions[targetObject].Remove(ei);
      }
    }

    public static void ClearAllEventsIfNeeded()
    {
      if (RuntimeHost.GetSettingsItem<bool>(UnsubscribeAllEvents))
        ClearAllEvents();
    }

    public static void ClearAllEvents()
    {
      if (subscriptions == null) return;
      foreach (object o in subscriptions.Keys)
      {
        List<EventInfo> keys = new List<EventInfo>();
        foreach (EventInfo ei in subscriptions[o].Keys)
          keys.Add(ei);

        foreach (EventInfo ei in keys)
        {
          RemoveEvent(ei, o, null);
        }
      }
      subscriptions.Clear();
    }
  }

  internal class EventOperatorHandler : IOperatorHandler
  {
    bool subscribe;

    public EventOperatorHandler(bool subscribe)
    {
      this.subscribe = subscribe;
    }

    #region IOperatorHandler Members

    public object Process(HandleOperatorArgs args)
    {
      if (args == null) throw new NotSupportedException();
      
      EventInfo @event = args.Arguments.FirstOrDefault() as EventInfo;
      if (@event == null) return null;

      args.Cancel = true;
      if (subscribe)
        return args.Arguments[1];
      else
        return new RemoveDelegate((IInvokable)args.Arguments[1]);
    }

    #endregion
  }


  [ComVisible(true)]
  internal class EventHelper<T> : EventBroker
  {   
    public void EventHandler(object sender, T e)
    {
      if (Target == null) return;
      
      IScriptContext context = new ScriptContext();
      context.CreateScope(
        RuntimeHost.ScopeFactory.Create(ScopeTypes.Event, context.Scope, context));
      
      Target.Invoke(context, new object[] { sender, e });
    }
  }

  internal class RemoveDelegate : IInvokable
  {
    public IInvokable OriginalMethod
    {
      get;
      private set;
    }

    public RemoveDelegate(IInvokable original)
    {
      OriginalMethod = original;
    }

    #region IInvokable Members

    public bool CanInvoke()
    {
      return false;
    }

    public object Invoke(IScriptContext context, object[] args)
    {
      throw new NotImplementedException();
    }

    #endregion
  }
}
