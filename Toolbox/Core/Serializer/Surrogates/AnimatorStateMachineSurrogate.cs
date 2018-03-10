/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using System.Runtime.Serialization;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;


#if UNITY_EDITOR
using UnityEditor.Animations;
namespace Pantagruel.Serializer.Surrogate
{
    /// <summary>
    /// Handles the data preperation for serializing
    /// an AnimatorState whereby all values are copied
    /// </summary>
    public class AnimatorChildCopySurrogate : SurrogateBase
    {
        /// <summary>
        /// Collects all fields that will be serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            if (obj == null) return;
            if (obj is ChildAnimatorStateMachine)
            {
                ChildAnimatorStateMachine fsm = (ChildAnimatorStateMachine)obj;
                info.AddValue("position", (object)fsm.position);
                info.AddValue("stateMachine", (object)fsm.stateMachine);
            }
            else if (obj is ChildAnimatorState)
            {
                ChildAnimatorState state = (ChildAnimatorState)obj;
                info.AddValue("position", (object)state.position);
                info.AddValue("state", (object)state.state);
            }
        }

        /// <summary>
        /// Sets all fields and properties that have been deserialized.
        /// </summary>
        /// <param name="obj">The GameObject that will be used to initialize this component.</param>
        /// <param name="info">The fields that were deserialized already.</param>
        /// <param name="context">A context container that stores a <see cref="XmlDeserializer.DeserializeContext"/> object within.</param>
        /// <param name="selector">Always null.</param>
        /// <returns>The component that was deserialized.</returns>
        public override object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            //just deserialize as normal in-place object
            ReplaceState(obj, info);
            return obj;
        }
    }


    /// <summary>
    /// Handles the data preperation for serializing an AnimatorState
    /// where the motion should not be serialized.
    /// </summary>
    public class AnimatorControllerSurrogate : SurrogateBase
    {
        /// <summary>
        /// Collects all fields that will be serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            if (obj == null) return;
            AnimatorController con = obj as AnimatorController;

            info.AddValue("layers", (object)con.layers);
            info.AddValue("hideFlags", (int)con.hideFlags);
            info.AddValue("name", con.name);
            info.AddValue("parameters", (object)con.parameters);
            //serialize in place as a unityengine object.
            //GatherFieldsAndProps(obj, info, context);
        }

        /// <summary>
        /// Sets all fields and properties that have been deserialized.
        /// </summary>
        /// <param name="obj">The GameObject that will be used to initialize this component.</param>
        /// <param name="info">The fields that were deserialized already.</param>
        /// <param name="context">A context container that stores a <see cref="XmlDeserializer.DeserializeContext"/> object within.</param>
        /// <param name="selector">Always null.</param>
        /// <returns>The component that was deserialized.</returns>
        public override object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            //just deserialize as normal in-place object
            //ReplaceState(obj, info);
            if (obj == null) return null;
            AnimatorController con = obj as AnimatorController;
            var layers = info.GetValue("layers", typeof(AnimatorControllerLayer[])) as AnimatorControllerLayer[];
            //con.layers = layers as AnimatorControllerLayer[];
            if (con.layers == null && layers != null) Debug.LogError("Failed to deserialize 'AnimatorController.layers' properly.");
            con.hideFlags = (HideFlags)info.GetInt32("hideFlags");
            con.name = info.GetString("name");
            con.parameters = info.GetValue("parameters", typeof(AnimatorControllerParameter[])) as AnimatorControllerParameter[];
            
            int i = 0;
            foreach(var layer in layers)
            {
                Debug.Log("Add layer: " + layer.name);
                con.AddLayer(layer.name);
                foreach (var state in layer.stateMachine.states)
                    con.layers[i].stateMachine.AddState(state.state.name, state.position);
                
                i++;
            }
            return obj;
        }
    }


    /// <summary>
    /// Handles the data preperation for serializing
    /// an AnimatorState whereby all values are copied
    /// </summary>
    public class AnimatorControllerLayerSurrogate : SurrogateBase
    {
        /// <summary>
        /// Collects all fields that will be serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            if (obj == null) return;
            var layer = obj as AnimatorControllerLayer;
            info.AddValue("avatarMask", layer.avatarMask);
            info.AddValue("blendingMode", (int)layer.blendingMode);
            info.AddValue("defaultWeight", layer.defaultWeight);
            info.AddValue("iKPass", layer.iKPass);
            info.AddValue("name", layer.name);
            info.AddValue("stateMachine", (object)layer.stateMachine);
            info.AddValue("syncedLayerAffectsTiming", layer.syncedLayerAffectsTiming);
            info.AddValue("syncedLayerIndex", layer.syncedLayerIndex);
        }

        /// <summary>
        /// Sets all fields and properties that have been deserialized.
        /// </summary>
        /// <param name="obj">The GameObject that will be used to initialize this component.</param>
        /// <param name="info">The fields that were deserialized already.</param>
        /// <param name="context">A context container that stores a <see cref="XmlDeserializer.DeserializeContext"/> object within.</param>
        /// <param name="selector">Always null.</param>
        /// <returns>The component that was deserialized.</returns>
        public override object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            //just deserialize as normal in-place object
            //ReplaceState(obj, info);
            if (obj == null) return null;
            var layer = obj as AnimatorControllerLayer;
            layer.avatarMask = info.GetValue("avatarMask", typeof(AvatarMask)) as AvatarMask;
            layer.blendingMode = (AnimatorLayerBlendingMode)info.GetInt32("blendingMode");
            layer.defaultWeight = info.GetSingle("defaultWeight");
            layer.iKPass = info.GetBoolean("iKPass");
            layer.name = info.GetString("name");
            var fsm = info.GetValue("stateMachine", typeof(AnimatorStateMachine));

            //TODO: need to manually add states and substates here
            layer.stateMachine = fsm as AnimatorStateMachine;


            if (layer.stateMachine == null && fsm != null) Debug.LogError("Failed to deserialize 'AnimatorControllerLayer.stateMachine' properly.");
            
            layer.syncedLayerAffectsTiming = info.GetBoolean("syncedLayerAffectsTiming");
            layer.syncedLayerIndex = info.GetInt32("syncedLayerIndex");
            return obj;
        }
    }


    /// <summary>
    /// Handles the data preperation for serializing
    /// an AnimatorState whereby all values are copied
    /// </summary>
    public class AnimatorStateMachineSurrogate : SurrogateBase
    {
        /// <summary>
        /// Collects all fields that will be serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            if (obj == null) return;
            AnimatorStateMachine fsm = obj as AnimatorStateMachine;
            //serialize in place as a unityengine object.
            GatherFieldsAndProps(obj, info, context, "defaultState");
            info.AddValue("defaultState", (object)fsm.defaultState); //will get serialized as a bool due to implicit cast
        }

        /// <summary>
        /// Sets all fields and properties that have been deserialized.
        /// </summary>
        /// <param name="obj">The GameObject that will be used to initialize this component.</param>
        /// <param name="info">The fields that were deserialized already.</param>
        /// <param name="context">A context container that stores a <see cref="XmlDeserializer.DeserializeContext"/> object within.</param>
        /// <param name="selector">Always null.</param>
        /// <returns>The component that was deserialized.</returns>
        public override object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            //just deserialize as normal in-place object
            ReplaceState(obj, info, "stateMachines", "states");

            AnimatorStateMachine fsm = obj as AnimatorStateMachine;
            var subFsms = info.GetValue("stateMachines", typeof(ChildAnimatorStateMachine[])) as ChildAnimatorStateMachine[];
            var subStates = info.GetValue("states", typeof(ChildAnimatorState[])) as ChildAnimatorState[];

            //NOTE: this will no handle nested states properly!
            foreach(var subFsm in subFsms)
            {
                //Debug.Log("Adding sub FSM " + subFsm.stateMachine.name);
                fsm.AddStateMachine(subFsm.stateMachine, subFsm.position);
            }

            foreach(var state in subStates)
            {
                //Debug.Log("Adding State " + state.state);
                fsm.AddState(state.state, state.position);
            }

            return obj;
        }


    }


    /// <summary>
    /// Handles the data preperation for serializing
    /// an AnimatorState whereby all values are copied
    /// </summary>
    public class AnimatorControllerParameterSurrogate : SurrogateBase
    {
        /// <summary>
        /// Collects all fields that will be serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            if (obj == null) return;
            var param = obj as AnimatorControllerParameter;
            info.AddValue("defaultBool", param.defaultBool);
            info.AddValue("defaultFloat", param.defaultFloat);
            info.AddValue("defaultInt", param.defaultInt);
            info.AddValue("name", param.name);
            info.AddValue("type", (int)param.type);
        }

        /// <summary>
        /// Sets all fields and properties that have been deserialized.
        /// </summary>
        /// <param name="obj">The GameObject that will be used to initialize this component.</param>
        /// <param name="info">The fields that were deserialized already.</param>
        /// <param name="context">A context container that stores a <see cref="XmlDeserializer.DeserializeContext"/> object within.</param>
        /// <param name="selector">Always null.</param>
        /// <returns>The component that was deserialized.</returns>
        public override object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            //just deserialize as normal in-place object
            ReplaceState(obj, info,"type");
            var param = obj as AnimatorControllerParameter;
            param.type = (AnimatorControllerParameterType)info.GetInt32("type");
            return obj;
        }
    }


    /// <summary>
    /// Handles the data preperation for serializing an AnimatorState
    /// where the motion should not be serialized.
    /// </summary>
    public class AnimatorStateWithNoMotionSurrogate : SurrogateBase
    {
        /// <summary>
        /// Collects all fields that will be serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            if (obj == null) return;
            //serialize in place as a unityengine object.
            GatherFieldsAndProps(obj, info, context,
                "motion");//don't want to store animations or blendtrees
        }

        /// <summary>
        /// Sets all fields and properties that have been deserialized.
        /// </summary>
        /// <param name="obj">The GameObject that will be used to initialize this component.</param>
        /// <param name="info">The fields that were deserialized already.</param>
        /// <param name="context">A context container that stores a <see cref="XmlDeserializer.DeserializeContext"/> object within.</param>
        /// <param name="selector">Always null.</param>
        /// <returns>The component that was deserialized.</returns>
        public override object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            //just deserialize as normal in-place object
            ReplaceState(obj, info);
            return obj;
        }
    }


    /// <summary>
    /// Used by asset copier to avoid serialing motion data.
    /// </summary>
    public class IgnoreMotionDataSurrogate : ISerializationSurrogate
    {

        public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            //do nothing - we don't want to serialize this stuff
        }

        public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            //we don't want to do anything with this data
            return null;
        }
    }
}
#endif