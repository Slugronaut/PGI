/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections.Generic;
using Toolbox.Common;
using UnityEngine.Events;
using System;

namespace PowerGridInventory.Extensions.ItemFilter
{
    /// <summary>
    /// Component used for handling item type filtering in a
    /// a socketed item.
    /// </summary>
    [AddComponentMenu("Power Grid Inventory/Extensions/Item Filter/Socket Type Filter")]
    [RequireComponent(typeof(Socketed))]
    public sealed class SocketedTypeFilter : MonoBehaviour
    {
        /// <summary>
        /// The socket this filter applies to.
        /// </summary>
        [Tooltip("The socket this filter applies to.")]
        public Socket Source;

        /// <summary>
        /// A list of ids that an item must belong to int order to be a socketable item that can be place din this socket.
        /// </summary>
        public HashedString[] AllowedIds;
        


        void Awake()
        {
            GetComponent<Socketed>().OnCanSocket.AddListener(CanStore);
        }

        public void CanStore(UnityAction onFailed, Socketable socketable, Socket socket)
        {
            //filter out what can and can't be socketed
            if (AllowedIds != null && AllowedIds.Length > 0)
            {
                var type = socketable.GetComponent<SocketableType>();
                if (type == null || HashedString.DoNotContain(AllowedIds, type.TypeName.Hash))
                {
                    //let the inventory know that things are not well
                    onFailed();
                    return;
                }
            }
        }

    }
}
