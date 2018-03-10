/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace Pantagruel.Serializer
{
    /// <summary>
    /// Exposes a few usful methods that we will want to access
    /// in a non-generic and non-type specific way.
    /// </summary>
    public interface IResourceMap : ISerializationCallbackReceiver
    {
        List<string> Values { get; }
        UnityEngine.Object GetObjectAtPath(string path);
    }

    /// <summary>
    /// Since Unity can't serialize Dictionaries or polymorphic data,
    /// this entire mechanism is desgined to work around that fact
    /// using a colossal monster of rubbish code and magic fairy dust.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class ResourceMap<T> : ISerializationCallbackReceiver, IResourceMap where T : class
    {
        [SerializeField]
        List<T> ResourceList;

        [SerializeField]
        List<string> PathsList;

        Dictionary<T, string> Map;

        public List<string> Values
        {
            get { return new List<string>(Map.Values); }
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ResourceMap()
        {
            Init();
        }

        /// <summary>
        /// Helper to initialize the lists and dictionary.
        /// </summary>
        void Init()
        {
            if (ResourceList == null) ResourceList = new List<T>();
            if (PathsList == null) PathsList = new List<string>();
            if (Map == null) Map = new Dictionary<T,string>();
        }

        
        #region Unity Events
        /// <summary>
        /// This method is used to convert two lists into the internal dictionary after
        /// deserialization has taken place. This method must be called manually
        /// by the owner of this object since Unity will not trigger it automatically.
        /// </summary>
        public void OnAfterDeserialize()
        {
            Map = new Dictionary<T, string>();
            for (int i = 0; i < Math.Min(PathsList.Count, ResourceList.Count); i++)
            {
                Map.Add(ResourceList[i], PathsList[i]);
            }
        }

        /// <summary>
        /// This method is used to convert the internal dictionary to a set of lists
        /// that can then be serialized by Unity. This method must be called manually
        /// by the owner of this object since Unity will not trigger it automatically.
        /// </summary>
        public void OnBeforeSerialize()
        {
            PathsList.Clear();
            ResourceList.Clear();
            foreach (var kv in Map)
            {
                PathsList.Add(kv.Value);
                ResourceList.Add(kv.Key);
            }
        }
        #endregion
        

        /// <summary>
        /// Get the Resource folder relative path, if one exists, to the given object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>The path associated with the object. This path is relative to a Resources folder and can be used to load the object at a later time using <see cref="Resources.Load(string)"/>.</returns>
        public string GetPath(UnityEngine.Object obj)
        {
            T key = obj as T;
            if (obj != null && Map.ContainsKey(key)) return Map[key];
            return null;
        }

        /// <summary>
        /// Adds the resource object to this manifest and associates it with
        /// given path relative to a Resources folder. The object's type must be
        /// the same or a subclass of this ResourceMap's type parameter.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="path"></param>
        /// <returns><c>true</c> if the object was successfully added, <c>false</c> otherwise.</returns>
        public bool Add(UnityEngine.Object obj, string path)
        {
            Type t = obj.GetType();
            if (t == typeof(T) || t.IsSubclassOf(typeof(T)))
            {
                Map[obj as T] = path;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the resource object, if any, associated with the
        /// Resources/ folder relative path in this manifest.
        /// </summary>
        /// <param name="path">A path, relative to a Resources folder.</param>
        /// <returns>The object in this manifest that is associated with the path if any.</returns>
        public UnityEngine.Object GetObjectAtPath(string path)
        {
            foreach(var kvp in Map)
            {
                if (kvp.Value == path) return kvp.Key as UnityEngine.Object;
            }

            return null;
        }
    }
}