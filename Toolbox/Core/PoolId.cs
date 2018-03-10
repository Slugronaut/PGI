using UnityEngine;


namespace Toolbox
{
    /// <summary>
    /// A special component that is automatically and invisibly added to any object
    /// within the context of the Lazurus for GameObjects. This should never
    /// be manually placed on an object by the user.
    /// 
    /// WARNING: This component should never be created or destroyed manually! Doing so 
    /// could cause inconsitant states and memory leaks with the <see cref="AutoPool"/>  class 
    /// that uses it.
    /// 
    /// NOTE: There is a slight bug in Lazarus where a pre-allocated object will initially
    ///       not have its PoolId set to 
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public sealed class PoolId : MonoBehaviour
    {
        public int Id;
        /// <summary>
        /// You should not manually set the value unless you have a really good reason to.
        /// It is set by Lazarus as a means of determining if this entity is in a pool or currently
        /// in-use but simply deactivated. Note that this value initially starts as true under the
        /// assumption that this component will only be added when an object is allocated by Lazarus.
        /// </summary>
        public bool InPool = true;

        /// <summary>
        /// 
        /// </summary>
        void Awake()
        {
            if (Application.isEditor && !Application.isPlaying)
            {
                Debug.Log("<color=red>PoolId is not meant to be manually placed in a scene. One will be generated automatically at runtime. Deleting component now.</color>");
                if(!Application.isPlaying) DestroyImmediate(this);
                else Destroy(this);
                return;
            }

            hideFlags = HideFlags.NotEditable;
        }

    }
}
