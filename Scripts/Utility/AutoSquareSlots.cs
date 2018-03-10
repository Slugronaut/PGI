/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
using UnityEngine;
using Toolbox.Common;
using UnityEngine.UI;
using System.Xml.Serialization;
using Toolbox;

namespace PowerGridInventory
{
    /// <summary>
    /// Attach this component to the parent of a <see cref="GridView"/>
    /// to provide automatic resize of the view in order to maintain square slots.
    /// </summary>
    [ExecuteInEditMode]
    [AddComponentMenu("PGI/Utility/Auto-Square Slots", 21)]
    [RequireComponent(typeof(RectTransform))]
    public class AutoSquareSlots : MonoBehaviourEx
    {
        [Inspectable]
        [XmlIgnore]
        public PGIView View
        {
            get { return _View; }
            set
            {
                _View = value;
                if (value != null && _View.Model != null)
                {
                    Fitter = _View.GetComponent<AspectRatioFitter>();
                    if (Fitter == null) Fitter = _View.gameObject.AddComponent<AspectRatioFitter>();
                }
            }
        }
        [SerializeField]
        [HideInInspector]
        PGIView _View;

        int CachedGridX;
        int CachedGridY;
        RectTransform RectTrans;
        AspectRatioFitter Fitter;

        void OnEnable()
        {
            RectTrans = transform as RectTransform;
            UpdateView();
            View = _View;
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// Used to render frequent changes in-editor.
        /// </summary>
        protected void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                UpdateView();
        }
        #endif
        
        /// <summary>
        /// Used to render frequent changes in playmode.
        /// </summary>
        void Update()
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying) return;
            #endif

            UpdateView();
        }

        /// <summary>
        /// Recalculates the size of the PGIView's RectTransform so that it maitains square slots
        /// while fitting as much of the space of this object's RectTransform as possible.
        /// </summary>
        public void UpdateView()
        {
            if (View == null || View.Model == null) return;
            if(RectTrans.hasChanged || CachedGridX != View.Model.GridCellsX || CachedGridY != View.Model.GridCellsY)
            {
                CachedGridX = View.Model.GridCellsX;
                CachedGridY = View.Model.GridCellsY;

                if (Fitter == null) Fitter = _View.gameObject.GetComponent<AspectRatioFitter>();
                if (Fitter == null) Fitter = _View.gameObject.AddComponent<AspectRatioFitter>();

                RectTransform viewTrans = View.transform as RectTransform;
                viewTrans.anchoredPosition = Vector2.zero;
                Fitter.aspectRatio = ((float)View.Model.GridCellsX / (float)View.Model.GridCellsY);
                Fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

                //we need to force the view to update completely...
                View.RequireFullUpdate = true;

                //...and immediately when in edit-mode
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    View.UpdateView();
#endif
            }
            
        }

        void HandleModelResize()
        {
            UpdateView();
        }
    }
}
