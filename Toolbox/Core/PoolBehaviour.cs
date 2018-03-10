/**
 * Copyright 2016
 * James Clark
 **/

//#define TOOLBOX_POOL_DEBUG

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

namespace Toolbox
{
    /// <summary>
    /// Base class for components that provide object pools for 
    /// quick 'n' clean object instantiation at runtime.
    /// </summary>
    [Serializable]
    [DisallowMultipleComponent]
    public abstract class PoolBehaviour<T> : MonoBehaviour where T : Component
    {
		public T Prefab;

        /// <summary>
        /// The maximum amount of time in seconds per frame that can be spent pre-allocating a pool.
        /// </summary>
        [Tooltip("The maximum amount of time in seconds per frame that can be spent pre-allocating a pool.")]
        public float AllocTime = 0.005f;

        private bool Allocating = false;
        private int PrematureRequests = 0;

        #if UNITY_5_5_OR_NEWER
        [Tooltip("For Unity 5.5 or later only. If set, when a scene is unloaded all active objects from this pool will be relenquished so as not to loose pre-allocated objects when changing scenes.")]
        public bool RelenquishOnSceneUnload = true;
        #endif

        /// <summary>
        /// Stores all objects that have been allocated by this pool and are currently active.
        /// </summary>
        private List<GameObject> _ActiveList = new List<GameObject>();

        /// <summary>
        /// Returns a list of all objects that have been allocated by this pool and are currently active.
        /// Warning! Altering this list in any way could affect pool behaviour!
        /// </summary>
        public List<GameObject> ActiveList { get { return _ActiveList; } }

        /// <summary>
        /// Returns a list of all objects that have been allocated by this pool active or not.
        /// Warning! Altering this list in any way could affect pool behaviour!
        /// </summary>
        public List<GameObject> OwnedObjects
        {
            get
            {
                List<GameObject> all = new List<GameObject>(_ActiveList);
                int count = transform.childCount;
                for (int i = 0; i < count; i++)
                    all.Add(transform.GetChild(i).gameObject);
                return all;
            }
        }

        /// <summary>
        /// The maxium numebr of elements that can exist in this pool at a time. When this
        /// number is exceeded, the pool will start destroying objects that are relenquished
        /// to it rather than storing them.
        /// </summary>
        public int MaxPoolSize;

        /// <summary>
        /// The number of elements to allocate when the pool runs dry.
        /// </summary>
        public int ChunkSize;

        /// <summary>
        /// Returns true while the internal pool allocator is running.
        /// </summary>
        public bool IsAllocating
        {
            get { return Allocating; }
        }


#if UNITY_5_5_OR_NEWER
        protected virtual void Awake()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        protected virtual void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        /// <summary>
        /// We need to clean up all references to active objects that weren't within the pool when
        /// the scene reloaded. This will cause a leak!
        /// </summary>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                ReleaseActiveReferences();
        }

        /// <summary>
        /// Relenquishes all active objects back to this pool so that we don't loose them when restarting a scene.
        /// </summary>
        /// <param name="scene"></param>
        private void HandleSceneUnloaded(Scene scene)
        {
            //if (RelenquishOnSceneUnload)
            //    RecallAll();
        }

        #else

        protected virtual void Awake()  {}

        protected virtual void OnDestroy() {}

        /// <summary>
        /// We need to clean up all references to active objects that weren't within the pool when
        /// the scene reloaded. This will cause a leak!
        /// </summary>
        /// <param name="index"></param>
        void OnLevelWasLoaded(int index)
        {
            ReleaseActiveReferences();
        }
        #endif
        
        /// <summary>
        /// Releases all references this pool has to any objects active in the scene.
        /// This method is typcially called when loading new scene in order to clean up
        /// after active objects were removed.
        /// 
        /// Don't call this unless you know exactly what your are doing.
        /// </summary>
        public void ReleaseActiveReferences()
        {
            if (_ActiveList == null) return;

            for (int i = 0; i < _ActiveList.Count; i++)
                Destroy(_ActiveList[i]);
            _ActiveList.Clear();
        }

        /// <summary>
        /// Coroutine used to allocate pools over time to avoid
        /// choppy frames when instantiating large pools. This
        /// version tracks internal state to ensure chunk-size is
        /// maintained.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="alreadyExist"></param>
        /// <returns></returns>
        IEnumerator Allocator(T prefab, int alreadyExist, int chunkSize, float delayTime)
        {
            Allocating = true;
            T thing;
            float startTime = Time.realtimeSinceStartup;
            var wait = new WaitForEndOfFrame();
            WaitForSeconds delay = null;
            if(delayTime > 0) delay = new WaitForSeconds(delayTime);

            //we need to take into account the chunk size, the number of elements
            //already existing, and the number of requested allocations that have
            //been performed since we started this coroutine
            int count = 0;
            for (int i = 0; i < chunkSize - alreadyExist - PrematureRequests;)
            {
                if (Time.realtimeSinceStartup - startTime > AllocTime)
                {
                    if (delayTime > 0) yield return delay;
                    else yield return wait;
                    startTime = Time.realtimeSinceStartup;
                }
                else
                {
                    if(PrematureRequests > 0) PrematureRequests--;
                    count++;
                    thing = GameObject.Instantiate(Prefab);
                    thing.transform.SetParent(null);
                    thing.transform.SetParent(this.transform);
                    thing.gameObject.SetActive(false);
                    i++;
                }
            }

            PrematureRequests = 0;
            Allocating = false;
            yield return null;
        }

        /// <summary>
        /// Coroutine used to allocate pools over time to avoid
        /// choppy frames when instantiating large pools. This version
        /// allows specifying an exact number of elements to instantiate.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="alreadyExist"></param>
        /// <returns></returns>
        IEnumerator DirectAllocator(T prefab, int desired, float delayTime)
        {
            Allocating = true;
            T thing;
            float startTime = Time.realtimeSinceStartup;
            var wait = new WaitForEndOfFrame();
            WaitForSeconds delay = null;
            if (delayTime > 0) delay = new WaitForSeconds(delayTime);

            //we need to take into account the chunk size, the number of elements
            //already existing, and the number of requested allocations that have
            //been performed since we started this coroutine
            int count = 0;
            for (int i = 0; i < desired; )
            {
                if (Time.realtimeSinceStartup - startTime > AllocTime)
                {
                    if (delayTime > 0) yield return delay;
                    else yield return wait;
                    startTime = Time.realtimeSinceStartup;
                }
                else
                {
                    if (PrematureRequests > 0) PrematureRequests--;
                    count++;
                    thing = GameObject.Instantiate(Prefab);
                    thing.transform.SetParent(null);
                    thing.transform.SetParent(this.transform);
                    thing.gameObject.SetActive(false);
                    i++;
                    //just in case someone calls an obtain method that triggers
                    //the normal pool instantiator - we might want to keep track of this
                    if (Allocating) PrematureRequests++;
                }
            }

            PrematureRequests = 0;
            Allocating = false;
            yield return null;
        }

        /// <summary>
        /// Helper used to preallocate pooled objects. Invokes a coroutine to perform the preallocation over time.
        /// Susequent calls to this method while the background allocator is running will cause it to block in order
        /// to properly process the next request.
        /// </summary>
        protected virtual void InstantiatePool(float delayTime = 0)
        {
            #if UNITY_EDITOR
            //if this is used in-editor while not playing, just return
            if (!Application.isPlaying) return;
            #endif

            if (Prefab == null) throw new ArgumentNullException("prefab");

            //first, lets add any children of this object to the pool if they are of the correct type
            int count = 0;
            Transform child;
            T thing;
            for (int i = 0; i < transform.childCount; i++)
            {
                child = transform.GetChild(i);
                thing = child.GetComponent(typeof(T)) as T;
                if (thing != null)
                {
                    count++;
                    thing.gameObject.SetActive(false);
                }
            }

            //BUG ALERT: potential issue if this couroutine takes more than one frame.
            //It is possible for us to request more elements while we are still creating some,
            //which in turn will start a new coroutine and now we have a far bigger pool than intended.
            if (!Allocating)
                StartCoroutine(Allocator(Prefab, count, ChunkSize, delayTime));
            
        }

        /// <summary>
        /// Used to manually invoke a number of elements to be pre-loaded into the pool.
        /// This will ignore the current pool size, the max pool size, and the allocation
        /// chunk size and simpy cause the given number of elements to be
        /// instantiated asynconously using a co-routine.
        /// </summary>
        /// <param name="count"></param>
        public virtual void PreallocateAsync(int count, float delayTime = 0)
        {
            #if UNITY_EDITOR
            //if this is used in-editor while not playing, just return
            if (!Application.isPlaying) return;
            #endif

            if (Prefab == null) throw new ArgumentNullException("prefab");

            //BUG ALERT: starting this co-routine multiples times in parrallel might cause a pool to have more
            //elements than it should.
            StartCoroutine(DirectAllocator(Prefab, count, delayTime));
        }

        /// <summary>
        /// Obtains an object stored within the pool and returns the component that is expected.
        /// It is guaranteed that an object will be returned, even if the pool runs dry.
        /// </summary>
        /// <param name="newOwner"></param>
        /// <returns>The desired component attached to the pooled object found.</returns>
        public virtual T ObtainObject(Transform newOwner, float poolDelayTime = 0, bool activate = true)
        {
            T thing = null;
            
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                thing = GameObject.Instantiate<T>(Prefab);
                thing.gameObject.transform.SetParent(null); //must do this due to a bug in unity involving prefabs
                thing.gameObject.transform.SetParent(newOwner);
                thing.gameObject.transform.localPosition = Vector3.zero;
                if(activate) thing.gameObject.SetActive(true);
                _ActiveList.Add(thing.gameObject);
                return thing;
            }
            #endif

            //get first available object from the pool -  but only if valid
            for(int i = 0; i < transform.childCount; i++)
            {
                thing = transform.GetChild(i).GetComponent(typeof(T)) as T;
                if(thing != null)
                {
                    //GlobalMessagePump.ForwardDispatch<PoolRelenquishedRequest>(thing, new PoolRelenquishedRequest(this));
                    //GlobalMessagePump.ForwardDispatch<PoolObtainedRequest>(thing, new PoolObtainedRequest(this, null));
                    //TODO: handle response here - if any
                    thing.gameObject.transform.SetParent(null); //must do this due to a bug in unity involving prefabs
                    thing.gameObject.transform.SetParent(newOwner, false);
                    if(activate) thing.gameObject.SetActive(true);
                    _ActiveList.Add(thing.gameObject);
                    
                    return thing;
                }
            }

            //we don't have any valid children, make a new pool and then search again
            InstantiatePool(poolDelayTime);

            //try again, get first available
            for(int i = 0; i < transform.childCount; i++)
            {
                thing = transform.GetChild(i).GetComponent(typeof(T)) as T;
                if(thing != null)
                {
                    thing.gameObject.transform.SetParent(null); //must do this due to a bug in unity involving prefabs
                    thing.gameObject.transform.SetParent(newOwner);
                    thing.gameObject.transform.localPosition = Vector3.zero;
                    if(activate) thing.gameObject.SetActive(true);
                    _ActiveList.Add(thing.gameObject);
                    return thing;
                }
            }

            //no matter what, we want our object. We'll need to track this so that if we are currently
            //performing allocations we can make sure we don't get too many in the pool.
            if(Allocating) PrematureRequests++;

            if(Prefab == null) throw new UnityException("The prefab source for the PoolBehaviour attached to " + name + " was null.");
            thing = GameObject.Instantiate<T>(Prefab);
            thing.gameObject.transform.SetParent(null); //must do this due to a bug in unity involving prefabs
            thing.gameObject.transform.SetParent(newOwner);
            thing.gameObject.transform.localPosition = Vector3.zero;
            if(activate) thing.gameObject.SetActive(true);
            _ActiveList.Add(thing.gameObject);
            return thing;
        }

        /// <summary>
        /// Obtains an object stored within the pool and returns the component that is expected.
        /// It is guaranteed that an object will be returned, even if the pool runs dry. This version
        /// simply returns a game object and does not validate that a proper component is attached.
        /// </summary>
        /// <param name="newOwner"></param>
        /// <returns>The desired component attached to the pooled object found.</returns>
        public virtual GameObject ObtainObjectNonValidate(Transform newOwner, float poolDelayTime = 0, bool activate = true)
        {
            GameObject thing = null;

            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                thing = GameObject.Instantiate<T>(Prefab).gameObject;
                thing.transform.SetParent(null); //must do this due to a bug in unity involving prefabs
                thing.transform.SetParent(newOwner);
                thing.transform.localPosition = Vector3.zero;
                if(activate) thing.SetActive(true);
                _ActiveList.Add(thing);
                return thing;
            }
            #endif

            //get first available object from the pool
            if(transform.childCount > 0)
            {
                thing = transform.GetChild(0).gameObject;
                if (!TypeHelper.IsReferenceNull(thing)) //can sometimes be a missing reference after scene re-load
                {
                    thing.transform.SetParent(null); //must do this due to a bug in unity involving prefabs
                    thing.transform.SetParent(newOwner, false);
                    if(activate) thing.SetActive(true);
                    _ActiveList.Add(thing);
                    #if TOOLBOX_POOL_DEBUG
                    print("Pool Id: " + name + "  Size: " + transform.childCount + "  Allocating from pool: " + thing.name);
                    #endif
                    return thing;
                }
                else
                {
                    Debug.LogWarning("Reference went missing! Re-creating now.");
                    Destroy(thing);
                }
            }

            //we don't have any children, make a new pool and then try again
            InstantiatePool(poolDelayTime);

            //try again
            if (transform.childCount > 0)
            {
                thing = transform.GetChild(0).gameObject;
                if (!TypeHelper.IsReferenceNull(thing)) //can sometimes be a missing reference after scene re-load
                {
                    thing.transform.SetParent(null); //must do this due to a bug in unity involving prefabs
                    thing.transform.SetParent(newOwner, false);
                    if (activate) thing.SetActive(true);
                    _ActiveList.Add(thing);
                    #if TOOLBOX_POOL_DEBUG
                    print("Pool Id: " + name + "  Size: " + transform.childCount + "  Pool had to be instantiated to obtain: " + thing.name);
                    #endif
                    return thing;
                }
                else
                {
                    Debug.LogWarning("Reference went missing! Re-creating now.");
                    Destroy(thing);
                }
            }

            //No matter what, we want our object. We'll need to track this so that if we are currently
            //performing allocations we can make sure we don't get too many in the pool.
            PrematureRequests++;
            if (Prefab == null) throw new UnityException("The prefab source for the PoolBehaviour attached to " + name + " was null.");
            thing = GameObject.Instantiate<T>(Prefab).gameObject;
            thing.transform.SetParent(null); //must do this due to a bug in unity involving prefabs
            thing.transform.SetParent(newOwner);
            thing.transform.localPosition = Vector3.zero;
            if (activate) thing.SetActive(true);
            _ActiveList.Add(thing);
            #if TOOLBOX_POOL_DEBUG
            print("Pool Id: " + name + "  Size: " + transform.childCount + "  Pool not available for: " + thing.name);
            #endif
            return thing;
        }

        /// <summary>
        /// Returns the object to this pool. If the pool is at
        /// capcity, the object will be destroyed instead.
        /// Objects relenquished this way should be considered
        /// destroyed for all intents and purposes and must never
        /// be accessed again without using the pool's 'obtain' method.
        /// </summary>
        /// <param name="thing"></param>
        public virtual void RelenquishObject(T thing, PoolId poolId = null)
        {
            _ActiveList.Remove(thing.gameObject);

            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                //TODO: check if slot or this pool is a prefab before destroying
                //NOTE: There is no built-in surefire way to determine prefab status
                //but rumor has it that the hideflags can be used to imply such a status
                GameObject.DestroyImmediate(thing.gameObject);
                return;
            }
            #endif


            #if UNITY_EDITOR
            if(TypeHelper.IsReferenceNull(transform) || transform.childCount >= MaxPoolSize)
            #else
            if(transform.childCount >= MaxPoolSize)
            #endif
            {
                //if we have enough in our pool, just destroy
                #if UNITY_EDITOR
                if (Application.isPlaying) GameObject.Destroy(thing.gameObject);
                else GameObject.DestroyImmediate(thing.gameObject);
                #else
                GameObject.Destroy(thing.gameObject);
                #endif
            }
            else
            {
                //send back to pool
                thing.gameObject.SetActive(false);
                //TODO: Confirm if this is still a bug in 5.6+
                //thing.transform.SetParent(null); //must do this due to bug in Unity involving prefabs
                thing.transform.SetParent(this.transform);

                //NOTE: not an entirely pure system anymore... but whatever
                if (poolId != null) poolId.InPool = true;
                else
                {
                    poolId = thing.GetComponent<PoolId>();
                    if (poolId != null) poolId.InPool = true;
                }
            }
        }

        /// <summary>
        /// Returns the object to this pool. If the pool is at
        /// capcity, the object will be destroyed instead.
        /// Objects relenquished this way should be considered
        /// destroyed for all intents and purposes and must never
        /// be accessed again without using the pool's obtain method.
        /// </summary>
        /// <param name="thing"></param>
        public virtual void RelenquishObject(GameObject thing, PoolId poolId = null)
        {
            _ActiveList.Remove(thing);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                //TODO: check if slot or this pool is a prefab before destroying
                //NOTE: There is no built-in surefire way to determine prefab status
                //but rumor has it that the hideflags can be used to imply such a status
                GameObject.DestroyImmediate(thing);
                return;
            }
#endif

            if(transform.childCount >= MaxPoolSize)
            {
                //if we have enough in our pool, just destroy
#if UNITY_EDITOR
                if (Application.isPlaying) GameObject.Destroy(thing);
                else GameObject.DestroyImmediate(thing);
#else
                GameObject.Destroy(thing);
#endif
            }
            else
            {
                //send back to pool
                thing.SetActive(false);
                thing.transform.SetParent(null); //must do this due to bug in Unity involving prefabs
                thing.transform.SetParent(transform);

                //NOTE: not an entirely pure system anymore... but whatever
                if (poolId != null) poolId.InPool = true;
                else
                {
                    poolId = thing.GetComponent<PoolId>();
                    if (poolId != null) poolId.InPool = true;
                }
            }
        }

        /// <summary>
        /// Forces all currently active objects that were obtained from this pool
        /// to immediately relenquish themselves. This may destroy some objects
        /// if the pool exceeds capacity.
        /// </summary>
        public void RecallAll()
        {
            while(_ActiveList.Count > 0)
            {
                if (_ActiveList[0] != null) RelenquishObject(_ActiveList[0]); //this will remove it from activte list and interate this loop
            }

        }

        /// <summary>
        /// Destroys pooled objects until it reaches the desired count.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="verifyChildType">If set, will verify that the given child type</param>
        public void DrainPool(int level = 0, bool verifyChildType = true)
        {
            if (level < 0) level = 0;
            if (level > transform.childCount) return;

            //remember: we actually have to iterate our child index
            //in this loop since the destroyed objects will likely
            //persist until the end of the frame.
            if(verifyChildType)
            {
                for (int i = transform.childCount - 1; i >= level; i--)
                {
                    var child = transform.GetChild(i).GetComponent(typeof(T));
                    if (child != null) Destroy(child.gameObject);
                    else level++; //need to increment to account for the fact that our actual level hasn't lowered
                }
            }
            else
            {
                for (int i = transform.childCount - 1; i >= level; i--)
                    Destroy(transform.GetChild(i).gameObject);
            }
            

        }
    }

#if ACG_FULL_TOOLBOX
    /// <summary>
    /// Sent to a GameObject just before relenquishing it to a pool.
    /// This message can be used to manually provide special-case handling.
    /// If the handler responds with the PoolRelenquishedResponse message
    /// then the pool will forgo any internal actions normally taken. In
    /// such a case it is expected that the handler performed all of the
    /// necessary actions to properly pool the object.
    /// </summary>
    public class PoolRelenquishedRequest<T> : IMessageRequest where T : Component
    {
        public PoolBehaviour<T> LocalPool { get; protected set; }

        public PoolRelenquishedRequest(PoolBehaviour<T> localPool)
        {
            LocalPool = localPool;
        }
    }


    /// <summary>
    /// Sent to a GameObject just after obtaining it from a pool but before
    /// handling any state changes (such as active state or parent transform).
    /// This message can be used to manually provide special-case handling.
    /// If the handler responds with the PoolObtaineddResponse message
    /// then the pool will forgo any internal actions normally taken. In
    /// such a case it is expected that the handler performed all of the
    /// necessary actions to properly instantiate the object.
    /// </summary>
    public class PoolObtainedRequest<T> : IMessageRequest where T : Component
    {
        public PoolBehaviour<T> LocalPool { get; protected set; }
        public Transform NewParent { get; protected set; }
        public Vector3 Position { get; protected set; }

        public PoolObtainedRequest(PoolBehaviour<T> localPool, Transform newParent, Vector3 position)
        {
            LocalPool = localPool;
            NewParent = newParent;
            Position = position;
        }
    }
#endif
}
