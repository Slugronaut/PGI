/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
using UnityEngine;
using UnityEngine.Events;
using System;
using Toolbox.Common;
using System.Linq;
using Toolbox;
using PowerGridInventory.Utility;

namespace PowerGridInventory
{
    /// <summary>
    /// This component allows a <see cref="PGISlotItem"/> to become a valid
    /// drop target in the grid for other socketable items.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Power Grid Inventory/Socketed", 15)]
    [RequireComponent(typeof(PGISlotItem))]
    [Serializable]
    public sealed class Socketed : MonoBehaviour
    {
        #region Members
        [Serializable]public class SocketEvent : UnityEvent<Socketable, Socket> { };
        [Serializable] public class SocketFailableEvent : UnityEvent<UnityAction, Socketable, Socket> { };
        

        [HideInInspector]
        [SerializeField]
        int _SocketCount = 1;
        /// <summary>
        /// The number of sockets this socketable has. Changing this number at runtime is not advised as data may be lost if the number of sockets is reduced
        /// </summary>
        [Inspectable("The number of sockets this socketable has. Changing this number at runtime is not advised as data may be lost if the number of sockets is reduced.")]
        public int SocketCount
        {
            get
            {
                return _SocketCount;
            }
            set
            {
                if (value < 0) value = 0;
                var sockets = GetComponents<Socket>();
                int diff = value - sockets.Length;
                if (diff > 0)
                {
                    for (int i = 0; i < diff; i++)
                        gameObject.AddComponent<Socket>();
                }
                else if (diff < 0)
                {
                    diff = -diff;
                    for (int i = 0; i < diff; i++)
                    {
                        if (Application.isPlaying) Destroy(sockets[i]);
                        else DestroyImmediate(sockets[i]);
                    }
                }

                _SocketCount = value;
                Sockets = GetComponents<Socket>();
            }
        }

        /// <summary>
        /// Whether or not this item should propogate its PGI events (store, equip, drop, unequip) to the items that are socketed to it.
        /// If not set, socketed item will never receive any of these events for any inventory actions taken with this item.
        /// </summary>
        [Tooltip("Whether or not this item should propogate its PGI events (store, equip, drop, unequip) to the items that are socketed to it. If not set, socketed item will never receive any of these events for any inventory actions taken with this item.")]
        public bool PropogateEvents = false;

        public Socket[] Sockets { get; private set; }

        /// <summary>
        /// Invoked when a socketable <see cref="PGISlotItem"/> is about to be dropped into
        /// a socketed one. You can disallow this action by setting the the provided model's
        /// <see cref="PGIModel.CanPerformAction"/> to <c>false</c>.
        /// </summary>
        [SerializeField] [PGIFoldedEvent] public SocketFailableEvent OnCanSocket = new SocketFailableEvent();

        /// <summary>
        /// Invoked after a socketable <see cref="PGISlotItem"/> has been dropped and
        /// attached to a socketed one.
        /// </summary>
        [SerializeField] [PGIFoldedEvent] public SocketEvent OnSocketed = new SocketEvent();
        [SerializeField] [FoldFlag("Events")] public bool FoldedEvents = false; //used by the inspector

        /// <summary>
        /// Returns the number of un-used sockets.
        /// </summary>
        public int EmptySocketCount
        {
            get
            {
                if (Sockets == null || Sockets.Length < 1) return 0;

                int count = 0;
                foreach (var soc in Sockets)
                {
                    if (soc.Store == null) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Returns an array of empty sockets in this socketable.
        /// </summary>
        public Socket[] EmptySockets
        {
            get { return Sockets.Where((x) => x.Store == null).ToArray(); }
        }

        /// <summary>
        /// Returns an array of used sockets in this socketable.
        /// </summary>
        public Socket[] FilledSockets
        {
            get { return Sockets.Where((x) => x.Store != null).ToArray(); }
        }

        /// <summary>
        /// Returns an array of the items stored in this socketable.
        /// </summary>
        public Socketable[] SocketedItems
        {
            get { return Sockets.Where((x) => x.Store != null).Select((x) => x.Store).ToArray(); }
        }
        #endregion


        #region Methods
        /// <summary>
        /// 
        /// </summary>
        void Awake()
        {
            Sockets = GetComponents<Socket>();
        }

        /// <summary>
        /// Returns the index to the first socket that is not used or -1 if there are none.
        /// </summary>
        /// <returns></returns>
        public Socket GetFirstEmptySocket()
        {
            if (Sockets == null || Sockets.Length < 1)
                return null;

            for (int i = 0; i < Sockets.Length; i++)
            {
                if (Sockets[i].Store == null)
                    return Sockets[i];
            }

            return null;
        }

        /// <summary>
        /// Attempts to attach one socketable item to this socketed one.
        /// </summary>
        /// <param name="receiver">The socketed item that will receive the other.</param>
        /// <param name="thing">The socketable item that will be attached.</param>
        /// <returns>The index of the socket array the socketable was stored in, or -1 if it was not stored.</returns>
        public Socket AttachSocketable(Socketable thing)
        {
            if (thing == null)
                return null;

            //if (!thing.SocketId.Equals(SocketId)) return -1;
            var socket = GetFirstEmptySocket();
            if (socket != null)
            {
                socket.Store = thing;
                return socket;
            }
            return null;
        }

        /// <summary>
        /// Attempts tp remove one socketable item from this socketed one.
        /// </summary>
        /// <param name="thing">The socketable item being removed.</param>
        /// <returns><c>true</c> if the item was removed, <c>false</c> otherwise.</returns>
        public bool DetachSocketable(Socketable thing)
        {
            if (thing == null) return false;
            for (int i = 0; i < Sockets.Length; i++)
            {
                if (Sockets[i].Store == thing)
                {
                    Sockets[i].Store = null;
                    return true;
                }
            }
            return false;
        }
        #endregion

    }
}
