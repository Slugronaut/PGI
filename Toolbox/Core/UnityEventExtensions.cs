/**********************************************
* Ancient Craft Games
* Copyright 2014-2017 James Clark
**********************************************/
using UnityEngine;
using UnityEngine.Events;
using System;

namespace Toolbox
{
    /// <summary>
    /// Implements virtual methods for UnityEvent. Based on Rob's design from 'MurderHobos' project.
    /// </summary>
    /// <remarks>
    /// These virtual implementations will not work if this object is cast to type of UnityEvent
    /// </remarks>
    [Serializable]
    public class EventAdaptor : UnityEvent
    {
        public new virtual void AddListener(UnityAction callback)
        {
            base.AddListener(callback);
        }

        public new virtual void RemoveListener(UnityAction callback)
        {
            base.RemoveListener(callback);
        }

        public new virtual void RemoveAllListeners()
        {
            base.RemoveAllListeners();
        }

        public new virtual void Invoke()
        {
            base.Invoke();
        }
    }


    /// <summary>
    /// Implements new virtual methods for UnityEvent.
    /// </summary>
    /// <remarks>
    /// These virtual implementations will not work if this object is cast to type of UnityEvent
    /// </remarks>
    [Serializable]
    public abstract class EventAdaptor<T> : UnityEvent<T>
    {
        public new virtual void AddListener(UnityAction<T> callback)
        {
            base.AddListener(callback);
        }

        public new virtual void RemoveListener(UnityAction<T> callback)
        {
            base.RemoveListener(callback);
        }

        public new virtual void RemoveAllListeners()
        {
            base.RemoveAllListeners();
        }

        public new virtual void Invoke(T t0)
        {
            base.Invoke(t0);
        }
    }


    /// <summary>
    /// Implements new virtual methods for UnityEvent.
    /// </summary>
    /// <remarks>
    /// These virtual implementations will not work if this object is cast to type of UnityEvent
    /// </remarks>
    [Serializable]
    public abstract class EventAdaptor<T0, T1> : UnityEvent<T0, T1>
    {
        public new virtual void AddListener(UnityAction<T0, T1> callback)
        {
            base.AddListener(callback);
        }

        public new virtual void RemoveListener(UnityAction<T0, T1> callback)
        {
            base.RemoveListener(callback);
        }

        public new virtual void RemoveAllListeners()
        {
            base.RemoveAllListeners();
        }

        public new virtual void Invoke(T0 arg0, T1 arg1)
        {
            base.Invoke(arg0, arg1);
        }
    }


    /// <summary>
    /// Implements new virtual methods for UnityEvent.
    /// </summary>
    /// <remarks>
    /// These virtual implementations will not work if this object is cast to type of UnityEvent
    /// </remarks>
    [Serializable]
    public abstract class EventAdaptor<T0, T1, T2> : UnityEvent<T0, T1, T2>
    {
        public new virtual void AddListener(UnityAction<T0, T1, T2> callback)
        {
            base.AddListener(callback);
        }

        public new virtual void RemoveListener(UnityAction<T0, T1, T2> callback)
        {
            base.RemoveListener(callback);
        }

        public new virtual void RemoveAllListeners()
        {
            base.RemoveAllListeners();
        }

        public new virtual void Invoke(T0 arg0, T1 arg1, T2 arg2)
        {
            base.Invoke(arg0, arg1, arg2);
        }
    }


    /// <summary>
    /// Implements virtual methods for UnityEvent.
    /// </summary>
    /// <remarks>
    /// These virtual implementations will not work if this object is cast to type of UnityEvent
    /// </remarks>
    [Serializable]
    public abstract class EventAdaptor<T0, T1, T2, T3> : UnityEvent<T0, T1, T2, T3>
    {
        public new virtual void AddListener(UnityAction<T0, T1, T2, T3> callback)
        {
            base.AddListener(callback);
        }

        public new virtual void RemoveListener(UnityAction<T0, T1, T2, T3> callback)
        {
            base.RemoveListener(callback);
        }

        public new virtual void RemoveAllListeners()
        {
            base.RemoveAllListeners();
        }

        public new virtual void Invoke(T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            base.Invoke(arg0, arg1, arg2, arg3);
        }
    }


    /// <summary>
    /// Simple event for toggle states.
    /// </summary>
    [Serializable]
    public class ToggleEvent : EventAdaptor<bool>
    {
        bool State = false;

        /// <summary>
        /// Toggles the state and invokes the event with the newly toggled state.
        /// </summary>
        public virtual void Invoke()
        {
            State = !State;
            base.Invoke(State);
        }

        /// <summary>
        /// Explicitly sets the state and invokes the event with it.
        /// </summary>
        /// <param name="state"></param>
        public override void Invoke(bool state)
        {
            State = state;
            base.Invoke(State);
        }
    }


    /// <summary>
    /// Event that fires exactly once and is buffered for all future subscribers.
    /// </summary>
    /// <remarks>
    /// After invoking, all listeners will be removed since it will never happen again.
    /// As well, future listeners will simply be invoked immediately if this was already invoked.
    /// </remarks>
    [Serializable]
    public class OneShotBufferedEvent : EventAdaptor
    {
        bool Invoked = false;

        public override void Invoke()
        {
            if (!Invoked)
            {
                Invoked = true;
                base.Invoke();
                RemoveAllListeners(); //we can safely remove all listeners, this will never be called again
            }
        }

        public override void AddListener(UnityAction callback)
        {
            if(Invoked) callback.Invoke();
            else base.AddListener(callback);
        }
    }


    /// <summary>
    /// Event that fires exactly once and is buffered for all future subscribers.
    /// </summary>
    /// <remarks>
    /// After invoking, all listeners will be removed since it will never happen again.
    /// As well, future listeners will simply be invoked immediately if this was already invoked.
    /// </remarks>
    [Serializable]
    public class OneShotBufferedEvent<T> : EventAdaptor<T>
    {
        bool Invoked = false;
        T Arg;

        public override void Invoke(T arg)
        {
            if (!Invoked)
            {
                Arg = arg;
                Invoked = true;
                base.Invoke(arg);
                //we can safely remove all listeners, this will never be called again
                RemoveAllListeners();
            }
        }

        public override void AddListener(UnityAction<T> callback)
        {
            if (Invoked) callback.Invoke(Arg);
            else base.AddListener(callback);
        }
    }


    /// <summary>
    /// Event that fires exactly once and is buffered for all future subscribers.
    /// </summary>
    /// <remarks>
    /// After invoking, all listeners will be removed since it will never happen again.
    /// As well, future listeners will simply be invoked immediately if this was already invoked.
    /// </remarks>
    [Serializable]
    public class OneShotBufferedEvent<T0, T1> : EventAdaptor<T0, T1>
    {
        bool Invoked = false;
        T0 Arg0;
        T1 Arg1;

        public override void Invoke(T0 arg0, T1 arg1)
        {
            if (!Invoked)
            {
                Arg0 = arg0;
                Arg1 = arg1;
                Invoked = true;
                base.Invoke(arg0, arg1);
                //we can safely remove all listeners, this will never be called again
                RemoveAllListeners();
            }
        }

        public override void AddListener(UnityAction<T0, T1> callback)
        {
            if (Invoked) callback.Invoke(Arg0, Arg1);
            else base.AddListener(callback);
        }
    }


    /// <summary>
    /// Event that fires exactly once and is buffered for all future subscribers.
    /// </summary>
    /// <remarks>
    /// After invoking, all listeners will be removed since it will never happen again.
    /// As well, future listeners will simply be invoked immediately if this was already invoked.
    /// </remarks>
    [Serializable]
    public class OneShotBufferedEvent<T0, T1, T2> : EventAdaptor<T0, T1, T2>
    {
        bool Invoked = false;
        T0 Arg0;
        T1 Arg1;
        T2 Arg2;

        public override void Invoke(T0 arg0, T1 arg1, T2 arg2)
        {
            if (!Invoked)
            {
                Arg0 = arg0;
                Arg1 = arg1;
                Arg2 = arg2;
                Invoked = true;
                base.Invoke(arg0, arg1, arg2);
                //we can safely remove all listeners, this will never be called again
                RemoveAllListeners();
            }
        }

        public override void AddListener(UnityAction<T0, T1, T2> callback)
        {
            if (Invoked) callback.Invoke(Arg0, Arg1, Arg2);
            else base.AddListener(callback);
        }
    }


    /// <summary>
    /// Event that fires exactly once and is buffered for all future subscribers.
    /// </summary>
    /// <remarks>
    /// After invoking, all listeners will be removed since it will never happen again.
    /// As well, future listeners will simply be invoked immediately if this was already invoked.
    /// </remarks>
    [Serializable]
    public class OneShotBufferedEvent<T0, T1, T2, T3> : EventAdaptor<T0, T1, T2, T3>
    {
        bool Invoked = false;
        T0 Arg0;
        T1 Arg1;
        T2 Arg2;
        T3 Arg3;

        public override void Invoke(T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            if (!Invoked)
            {
                Arg0 = arg0;
                Arg1 = arg1;
                Arg2 = arg2;
                Arg3 = arg3;
                Invoked = true;
                base.Invoke(arg0, arg1, arg2, arg3);
                //we can safely remove all listeners, this will never be called again
                RemoveAllListeners();
            }
        }

        public override void AddListener(UnityAction<T0, T1, T2, T3> callback)
        {
            if (Invoked) callback.Invoke(Arg0, Arg1, Arg2, Arg3);
            else base.AddListener(callback);
        }
    }

    /// <summary>
    /// General-purpose GameObject event.
    /// </summary>
    [Serializable]
    public class GameObjectEvent : EventAdaptor<GameObject>
    { }

    #if ACG_FULL_TOOLBOX
    /// <summary>
    /// General-purpose AutonomousEntity event.
    /// </summary>
    [Serializable]
    public class EntityEvent : EventAdaptor<EntityRoot>
    { }
    #endif
}
