/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pantagruel.Collections
{
    /// <summary>
    /// This is a wrapper for a Dictionary and two lists that allows this object to be serialized by Unity.
    /// The type parameters must also be serializable by unity for this to work.
    /// </summary>
    /// <remarks>
    /// The user of this class is required to manually use the ISerialializationCallbackReceiver methods
    /// to transform the data internally to serializable formats and back. Because this is not
    /// a scriptable object or component, Unity will not call these methods itself.</remarks>
    [Serializable]
    public class SerializableDictionary<K, V> : ISerializationCallbackReceiver
    {
        [SerializeField]
        List<K> KeyList;

        [SerializeField]
        List<V> ValueList;

        Dictionary<K, V> Map;


        /// <summary>
        /// Default constructor.
        /// </summary>
        public SerializableDictionary()
        {
            Init();
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="capacity"></param>
        public SerializableDictionary(int capacity)
        {
            Init(capacity);
        }

        /// <summary>
        /// 
        /// </summary>
        void Init()
        {
            if (KeyList == null) KeyList = new List<K>();
            if (ValueList == null) ValueList = new List<V>();
            if (Map == null) Map = new Dictionary<K, V>();
        }

        /// <summary>
        /// 
        /// </summary>
        void Init(int capacity)
        {
            if (KeyList == null) KeyList = new List<K>(capacity);
            if (ValueList == null) ValueList = new List<V>(capacity);
            if (Map == null) Map = new Dictionary<K, V>(capacity);
        }

        /// <summary>
        /// Converts internal data from lists to a dictionary.
        /// </summary>
        public void OnAfterDeserialize()
        {
            Map = new Dictionary<K, V>();
            for (int i = 0; i < Math.Min(KeyList.Count, ValueList.Count); i++)
            {
                Map.Add(KeyList[i], ValueList[i]);
            }
        }

        /// <summary>
        /// Converts internal data from a dictionary to lists.
        /// </summary>
        public void OnBeforeSerialize()
        {
            KeyList.Clear();
            ValueList.Clear();
            foreach (var kv in Map)
            {
                KeyList.Add(kv.Key);
                ValueList.Add(kv.Value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public void Add(K key, V value)
        {
            Map.Add(key, value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue(K key, out V value)
        {
            return Map.TryGetValue(key, out value);
        }
    }
}