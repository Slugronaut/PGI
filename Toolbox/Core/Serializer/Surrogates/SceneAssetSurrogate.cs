/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System;

namespace Pantagruel.Serializer.Surrogate
{
    /// <summary>
    /// Handles the data preperation for 
    /// serializing <see cref="UnityEngine.SceneAsset"/> objects.
    /// 
    /// This surrogate can only be used in the editor!
    /// 
    /// </summary>
    public class SceneAssetSurrogate : UnityEngineObjectSurrogate
    {
        /// <summary>
        /// Collects all fields that will be serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            //No need for reflection here, we know exactly what we want to store.
            SceneAsset asset = obj as SceneAsset;
            if (asset != null)
            {
                info.AddValue("Name", asset.name);
                info.AddValue("HideFlags", (int)asset.hideFlags);
                info.AddValue("Path", AssetDatabase.GetAssetPath(asset));
            }
            else Debug.LogWarning("Failed to process surrogate for SceneAsset datatype.");
        }

        /// <summary>
        /// Sets all fields that have been deserialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public override object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            string name, path;
            HideFlags flags;
            try
            {
                name = (string)info.GetValue("Name", typeof(string));
                path = (string)info.GetValue("Path", typeof(string));
                flags = (HideFlags)info.GetValue("HideFlags", typeof(int));
            }
            catch(Exception e)
            {
                Debug.LogWarning("Failed to deserialized SceneAsset in surrogate handler.\n" + e.Message);
                return null;
            }
            if (!string.IsNullOrEmpty(name))
            {
                SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (asset != null)
                {
                    asset.name = name;
                    asset.hideFlags = flags;
                    return asset;
                }
                else Debug.LogWarning("Failed to load scene asset at " + name);
            }

            return null;
        }
    }
}
#endif