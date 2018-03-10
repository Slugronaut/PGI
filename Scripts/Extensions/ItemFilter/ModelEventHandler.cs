using UnityEngine;


namespace PowerGridInventory
{
    /// <summary>
    /// Concrete componene that can be attached to a <see cref="PGIModel"/>
    /// an used to connect event handlers for a specific <see cref="CellModel"/>
    /// using the inspector.
    /// </summary>
    [RequireComponent(typeof(PGIModel))]
    public sealed class ModelEventHandler : AbstractModelEventHandler, IModelEventHandler
    {
        //Don't need to do anything. It's already done! Wheeee!
    }
}