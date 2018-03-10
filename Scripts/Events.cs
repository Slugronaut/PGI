using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace PowerGridInventory
{
    /// <summary>
    /// Used internall by some classes by helper functions to
    /// determine the type of event to trigger.
    /// </summary>
    public enum EventClass
    {
        CanRemove,
        CanStore,
        Remove,
        Store,
        Failed,
    }

    [Serializable]
    public class ViewEvent : UnityEvent<PGIView> { }

    [Serializable]
    public class ModelEvent : UnityEvent<PGISlotItem, PGIModel> { }

    [Serializable]
    public class ModelUpdateEvent : UnityEvent<PGISlotItem, CellModel> { }

    [Serializable]
    public class ViewUpdateEvent : UnityEvent<PGISlotItem, PGISlot> { }

    [Serializable]
    public class ViewTransferEvent : UnityEvent<PGISlotItem, PGISlot, PGISlot> { }

    [Serializable]
    public class InvalidDropEvent : UnityEvent<UnityAction, PointerEventData, PGISlotItem, GameObject> { }

    [Serializable]
    public class ModelFailableEvent : UnityEvent<UnityAction, PGISlotItem, CellModel> { }

    [Serializable]
    public class ViewFailableEvent : UnityEvent<UnityAction, PGISlotItem, PGISlot> { }
}
