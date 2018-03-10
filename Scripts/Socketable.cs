/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
using UnityEngine;
using System;
using PowerGridInventory.Utility;
using Toolbox.Common;

namespace PowerGridInventory
{
    //TODO: When an item is attached to another, it must be removed from the inventory
    //and stored within a local system of that item (we must also change the transforms if allowed).
    //Hoever, if we want the events to persist for the attached items we will need to hook all of their
    //events to the corresponding ones of the socketed item and trigger them accordingly.

    /// <summary>
    /// This component allows a <see cref="PGISlotItem"/> to be 
    /// dropped in the grid space of another socketed item
    /// and thus become merged with it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Power Grid Inventory/Socketable", 16)]
    [RequireComponent(typeof(PGISlotItem))]
    [Serializable]
    public class Socketable : MonoBehaviour
    {
        #region Members
        /// <summary>
        /// Invoked when a socketable <see cref="PGISlotItem"/> is about to be dropped into
        /// a socketed one. You can disallow this action by setting the the provided model's
        /// <see cref="PGIModel.CanPerformAction"/> to <c>false</c>.
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public Socketed.SocketFailableEvent OnCanSocket = new Socketed.SocketFailableEvent();

        /// <summary>
        /// Invoked after a socketable <see cref="PGISlotItem"/> has been dropped and
        /// attached to a socketed one.
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public Socketed.SocketEvent OnSocketed = new Socketed.SocketEvent();
        [SerializeField]
        [FoldFlag("Events")]
        public bool FoldedEvents = false; //used by the inspector

       
        #endregion


        
    }
}
