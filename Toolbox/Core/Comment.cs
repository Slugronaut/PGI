/**********************************************
* Ancient Craft Games
* Copyright 2014-2017 James Clark
**********************************************/
using UnityEngine;
using System.Collections;

namespace Toolbox
{
    /// <summary>
    /// Handy component for adding 'comments' to a scene hierarchy.
    /// </summary>
    [ExecuteInEditMode]
    public class Comment : MonoBehaviour
    {
        [Tooltip("Should this comment self-destruct when loaded in-game?")]
        public bool DestroyOnStart = true;

        [Space(5)]
        [TextArea(15,25)]
        public string Text;


        //TODO: have an option to display an in-editor popup when this Comment is enabled.


        void Awake()
        {
            if(Application.isPlaying) Destroy(this);
        }
        
    }
}
