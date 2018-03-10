/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PowerGridInventory.Extensions.Tooltip
{
    /// <summary>
    /// Used to display a socketed item's sockets and any socketables
    /// placed in them. Typically, one will Hook this object's display methods
    /// to the hover events of an inventory view.
    /// 
    /// 
    ///  ***********************************************************************************************************************
    /// PLEASE NOTE: This is current meant only as an example right now.
    /// It is not very robust and will almost certainly fail in any production
    /// environment. Use this as a basis for your own methods until I can get a
    /// better working system in place.
    /// 
    /// Currently this system assumes there is a max of four sockets in any socketed item.
    /// And has a lot of hardcoded functionality towards that end. It also doesn't consider
    /// the possibility of socketed socketable items. (They work just fine, but only the top-most
    /// socketable will be displayed by this component). There is also the issue of scaling the socketable
    /// items when rendering various sized objects. This example uses vertical and horizontal layout
    /// elements to scale the sockets to the size of the item when viewed in the inventory. However,
    /// it has a nasty habit of making the socketable icons too small or too big and displaying jagged
    /// layouts at times.
    /// 
    /// Also, I'm using GetComponent() a lot and a some extra image layers so it could be more efficient too.
    /// 
    /// ************************************************************************************************************************
    /// 
    /// </summary>
    [AddComponentMenu("Power Grid Inventory/Extensions/Tooltip/Socketed Tooltip")]
    public class SocketedTooltip : MonoBehaviour
    {
        /// <summary>
        /// A GameObject representing our socket overlay.
        /// </summary>
        [Tooltip("A game object representing out socket overlay.")]
        public GameObject Overlay;

        [Tooltip("The image displaying the socket.")]
        public Image[] QuadSocketImage;
        
        public Sprite DefaultSocketIcon;

        private RectTransform OverlayTrans;
        private bool Inited = false;

        /// <summary>
        /// Gets references to images used by the overlay's prefab.
        /// </summary>
        void Init()
        {
            if (Inited) return;
            Inited = true;
            if(Overlay != null)
            {
                OverlayTrans = Overlay.GetComponent<RectTransform>();
            }
            
        }

        /// <summary>
        /// Hook this to a view's OnHover event to display a socket overlay for applicable items.
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="slot"></param>
        public void DisplaySockets(PointerEventData eventData, PGISlot slot)
        {
            //Yuck. Only way I can get access to elements and still have
            //this GameObject start as inactive.
            Init();

            if (Overlay == null) return;
            if (slot == null || slot.Item == null) return;
            
            Socketed socketed = slot.Item.GetComponent<Socketed>();
            if (socketed == null) return;

            
            RectTransform slotTrans = slot.GetComponent<RectTransform>();
            if(slotTrans != null && OverlayTrans != null)// && SocketImage != null)
            {
                //activate the overlay and size it to our inventory slot
                Overlay.SetActive(true);
                OverlayTrans.position = slotTrans.position;
                OverlayTrans.sizeDelta = slotTrans.sizeDelta;
               
                
                //HACK ALERT: This is a really crappy way of doing this and is hard-coded
                //to work with items that have four or less sockets.

                //Now we need to determine how many slots our item has and display the correct
                //overlay sub-object. 
                int socs = Mathf.Min(socketed.Sockets.Length, 4);
                if (socs == 0) return;

                for (int child = 0; child < transform.childCount; child++ )
                {
                    transform.GetChild(child).gameObject.SetActive(false);
                }
                Transform socketSet = transform.GetChild(socs - 1);
                socketSet.gameObject.SetActive(true);

                for (int i = 0; i < socs; i++)
                {
                    if (socketed.Sockets[i] != null)
                    {
                        //we have a socket with something in it, display that thing if we can
                        var thing = socketed.Sockets[i].GetComponent<PGISlotItem>();
                        if (thing != null)
                        {
                            //The prefabs for four sockets uses two levels of parents
                            //in order to display a grid-like appearance. So we'll just use
                            //our previously defined reference array to access the images.
                            if(socs < 4)
                                socketSet.GetChild(i).GetComponent<Image>().sprite = thing.Icon;
                            else
                            {
                                QuadSocketImage[i].sprite = thing.Icon;
                            }
                        }
                       
                    }
                    else
                    {
                        //Gotta reset sockets that aren't in use by this item
                        //(since we may not have cleaned up the view since the last item to display socketables).
                        if (socs < 4)
                            socketSet.GetChild(i).GetComponent<Image>().sprite = DefaultSocketIcon;
                        else
                        {
                            QuadSocketImage[i].sprite = DefaultSocketIcon;
                        }
                    }
                }
               
            }
        }

        /// <summary>
        /// Hook this to a view's OnEndHover event to hide the socket overlay.
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="slot"></param>
        public void HideSockets(PointerEventData eventData, PGISlot slot)
        {
            if (Overlay == null) return;
            Overlay.SetActive(false);

            //TODO: Reset all socket images to default when ending hover
            /*
            if (Inited && SocketImage != null)
                SocketImage.sprite = DefaultSocketIcon;
             * */
        }
    }
}