/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using System;
using UnityEngine;

namespace PowerGridInventory
{
    /// <summary>
    /// A single socket on a socketed item.
    /// </summary>
    [RequireComponent(typeof(Socketed))]
    [Serializable]
    public sealed class Socket : MonoBehaviour
    {
        public Socketed Root { get; private set; }
        public Socketable Store;

        void Awake()
        {
            Root = GetComponent<Socketed>();
        }
    }
}
