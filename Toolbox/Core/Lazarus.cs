/*
 * Ancient Craft Games
 * Copyright 2016-2017 James Clark
 */
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System;
using Toolbox.Common;

namespace Toolbox
{
    /// <summary>
    /// This is a general-purpose utility for instantiating 
    /// GameObjects at runtime using a pooling mechanism. This
    /// allows for very fast instantiations at the cost of
    /// using more memory. All allocation and pool management
    /// is handled automatically behind the scenes. Simply use
    /// 'Lazurus.Summon(somePrefab)' to get an object and
    /// 'Lazurus.RelenquishToPool(someObject)' to return it.
    /// 
    /// 
    /// BUGS: 
    ///     -There is a potential issue in GetAssociatedPool. See the comment in that function for more details.
    ///     -If an Alloc Chunk size that is not a multiple of the Max Pool size is specified, the pool might over-allocate objects at times due to async operations.
    ///      Note really a big issue but it is an imperfection.
    /// </summary>
    [DisallowMultipleComponent]
    public class Lazarus : GlobalSingletonMonoBehaviour<Lazarus>
    {
        #region Static Members

        /// <summary>
        /// This tracks the current pools for individual types of GameObjects instantiated.
        /// Any time a gameObject is used as the blueprint for an instantiation, that object
        /// automatically has an invisible 'PoolId' component attached in order to ensure
        /// all future instantiations are made using the correct pool. As well, all objects
        /// that are created from this will also have the same PoolId as their blueprint.
        /// </summary>
        static Dictionary<int, TransformPool> Pools = new Dictionary<int, TransformPool>();

        /// <summary>
        /// The maximum number of objects that can exist in a single pool before
        /// it simply starts destroying objects that are relenquished to it.
        /// </summary>
        public static int MaxPoolSize { get { return Instance._MaxPoolSize; } set { Instance._MaxPoolSize = value; } }
        [Tooltip("The maximum number of objects that can exist in a single pool before "+
                 "it simply starts destroying objects that are relenquished to it.")]
        public int _MaxPoolSize = 10000;
        
        /// <summary>
        /// The number of elements that are allocated at a time when a request is made
        /// from a pool that has run dry.
        /// </summary>
        public static int AllocChunk { get { return Instance._AllocChunk; } set { Instance._AllocChunk = value; } }
        [Tooltip("The number of elements that are allocated at a time when a request is made from a pool that has run dry.")]
        public int _AllocChunk = 50;
        
        /// <summary>
        /// Set to true before allocating anything from the pool and all pools will become persistent.
        /// </summary>
        public static bool PersistAllPools { get { return Instance._PersistAllPools; } set { Instance._PersistAllPools = value; } }
        [Tooltip("Set to true before allocating anything from the pool and all pools will become persistent.")]
        public bool _PersistAllPools = true;
        
        /// <summary>
        /// Used to hide/show the pool GameObjects in the scene hierarchy window.
        /// </summary>
        public static bool PoolsAreVisible { get { return Instance._PoolsAreVisible; } set { Instance._PoolsAreVisible = value; } }
        [Tooltip("Used to hide/show the pool GameObjects in the scene hierarchy window.")]
        public bool _PoolsAreVisible = true;
        
        /// <summary>
        /// The maximum amount of time in seconds per frame that can be spent pre-allocating a pool.
        /// </summary>
        public static float AllocTime { get { return Instance._AllocTime; } set { Instance._AllocTime = value; } }
        [Tooltip("The maximum amount of time in seconds per frame that can be spent pre-allocating a pool.")]
        public float _AllocTime = 0.005f;
        
        /// <summary>
        /// Time to delay between allocations. Used to spread out choppy frames when pre-loading during gameplay.
        /// </summary>
        public static float AllocDelay { get { return Instance._AllocDelay; } set { Instance._AllocDelay = value; } }
        [Tooltip("Time to delay between allocations. Used to spread out choppy frames when pre-loading during gameplay.")]
        public float _AllocDelay = 0.002f;
        
        /// <summary>
        /// Returns true while the pool allocation coroutine is running.
        /// </summary>
        public static bool IsAllocating
        {
            get
            {
                var list = SubPools;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].IsAllocating) return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Current total number of elements in the pooling system.
        /// This is a rough count and may not always be completely accurate.
        /// </summary>
        public static int CurrentPoolSize
        {
            get
            {
                int count = 0;
                var list = SubPools;
                for (int i = 0; i < list.Count; i++)
                    count += list[i].transform.childCount;
                return count;
            }
        }

        /// <summary>
        /// The total possible number of objects that can exist in all pools combined.
        /// </summary>
        public static int MaxPossibleObjects
        {
            get
            {
                int count = 0;
                var list = SubPools;
                for (int i = 0; i < list.Count; i++)
                    count += list[i].MaxPoolSize;
                return count;
            }
        }

        /// <summary>
        /// Combine total of all sub-pool chunk sizes.
        /// </summary>
        public static int MaxChunkSize
        {
            get
            {
                int count = 0;
                var list = SubPools;
                for (int i = 0; i < list.Count; i++)
                    count += list[i].ChunkSize;
                return count;
            }
        }

        /// <summary>
        /// Helper for getting all sub-pool behaviours.
        /// </summary>
        private static List<TransformPool> SubPools
        {
            get
            {
                List<TransformPool> tp = new List<TransformPool>(); //for my bung-hole
                for (int i = 0; i < Instance.transform.childCount; i++)
                {
                    var comp = Instance.transform.GetChild(i).GetComponent<TransformPool>();
                    if (comp != null) tp.Add(comp);
                }

                return tp;
            }
        }

        /// <summary>
        /// Returns all objects created by the AutoPooler.
        /// </summary>
        public static List<GameObject> AllPooledObjects
        {
            get
            {
                List<GameObject> all = new List<GameObject>(100);
                if (Instance != null)
                {
                    var list = SubPools;
                    for (int i = 0; i < list.Count; i++)
                        all.AddRange(list[i].OwnedObjects);
                }
                return all;
            }
        }
        #endregion


        #region Instance Members
        /// <summary>
        /// Invoked to force all objects allocated from this master pool system to return to it.
        /// All pooled objects register with it upon creation.
        /// </summary>
        /// <returns></returns>
        public EventAdaptor OnForceRelenquish = new EventAdaptor();
        #endregion


        protected override void SingletonAwake() { }

        public override void AutoSingletonInit() { }


        #region Static Methods
        /// <summary>
        /// Instantiates a copied instance of the given GameObject. The
        /// returned object will be tracked internally by the pool and may
        /// be relenquished by calling this object's static 'Relenquish' method.
        /// </summary>
        /// <param name="blueprint"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException" />
        [Obsolete("Use Summon instead.")]
        public static GameObject InstantiateFromPool(GameObject blueprint, bool activate = true)
        {
            return Summon(blueprint, blueprint.transform.position, activate);
        }

        /// <summary>
        /// Instantiates a copied instance of the given GameObject. The
        /// returned object will be tracked internally by the pool and can
        /// be relenquished by calling this object's static 'Relenquish' method.
        /// </summary>
        /// <param name="blueprint"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException" />
        [Obsolete("Use Summon instead.")]
        public static GameObject InstantiateFromPool(GameObject blueprint, Vector3 position, bool activate = true)
        {
            return Summon(blueprint, position, activate);
        }

        /// <summary>
        /// Instantiates an copied instance of the given GameObject. The
        /// returned object will be tracked internally by the pool and may
        /// be relenquished by calling this object's static 'Relenquish' method.
        /// </summary>
        /// <param name="blueprint"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException" />
        public static GameObject Summon(GameObject blueprint, bool activate = true)
        {
            return Summon(blueprint, blueprint.transform.position, activate);
        }

        /// <summary>
        /// Instantiates a copied instance of the given GameObject. The
        /// returned object will be tracked internally by the pool and can
        /// be relenquished by calling this object's static 'Relenquish' method.
        /// </summary>
        /// <param name="blueprint"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException" />
        public static GameObject Summon(GameObject blueprint, Vector3 position, bool activate = true)
        {
            if (blueprint == null) throw new ArgumentNullException("blueprint");

            //don't use pools during editmode, it fucks everything
            #if UNITY_EDITOR
            if (!Application.isPlaying) return GameObject.Instantiate<GameObject>(blueprint);
            #endif

            //MINOR BUG ALERT: We don't really want to check the max size here
            //since each pool can have its own max size. This is effectively useless.
            GameObject obj = null;
            if (MaxPoolSize < 1)
                obj = GameObject.Instantiate<GameObject>(blueprint);
            else
            {
                var pool = GetAssociatedPool(blueprint);
                obj = pool.ObtainObjectNonValidate(null, Lazarus.AllocDelay, activate);

                var id = obj.GetComponent<PoolId>();
                if (id == null) id = obj.AddComponent<PoolId>();
                id.Id = pool.PoolId;
                id.InPool = false;
                obj.transform.position = position;
                var agent = obj.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.Warp(position);
            }
            obj.name = blueprint.name + "(" + obj.GetInstanceID() + ")";
            return obj;
        }

        /// <summary>
        /// Instantiates a copied instance of the given GameObject. The
        /// returned object will be tracked internally by the pool and can
        /// be relenquished by calling this object's static 'Relenquish' method.
        /// If the pool is dry a currently active object will be recycled and returned instead.
        /// </summary>
        /// <param name="blueprint"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException" />
        public static GameObject RecycleFromPool(GameObject blueprint, Vector3 position, bool activate = true)
        {
            if (blueprint == null) throw new ArgumentNullException("blueprint");

            //don't use pools during editmode, it fucks everything
            #if UNITY_EDITOR
            if (!Application.isPlaying) return GameObject.Instantiate<GameObject>(blueprint);
            #endif


            bool error = false;
            GameObject obj = null;
            if (MaxPoolSize < 1)
                obj = Instantiate(blueprint);
            else
            {
                var pool = GetAssociatedPool(blueprint);
                //if we don't have any children left in the pool, it is time to start reusing them
                if (pool.ActiveList.Count < pool.MaxPoolSize || pool.ActiveList.Count < 1)
                    obj = pool.ObtainObjectNonValidate(null, Lazarus.AllocDelay, activate);
                else
                {
                    while (TypeHelper.IsReferenceNull(obj))
                    {
                        obj = pool.ActiveList[0];
                        if (!TypeHelper.IsReferenceNull(obj))
                        {
                            //need to move the object to the back of the list
                            pool.ActiveList.Remove(obj);
                            pool.ActiveList.Add(obj);
                        }
                        else
                        {
                            Debug.LogWarning("Pooled object reference went missing! Re-creating from the blueprint " + blueprint.name + " now.");
                            pool.ActiveList.Remove(obj);
                            Destroy(obj);
                            error = true;
                        }
                    }
                    
                    
                    //NOTE: we still want to call the proper events for relenquishing and instantiating
                    //the object - that way we will hopefully reset and maintain proper state
                    //GlobalMessagePump.ForwardDispatch<PoolRelenquishedRequest>(obj, new PoolRelenquishedRequest(pool, _Instance));
                    //GlobalMessagePump.ForwardDispatch<PoolObtainedRequest>(obj, new PoolObtainedRequest(pool, _Instance, null, position));
                }

                var id = obj.GetComponent<PoolId>();
                if (id == null) id = obj.AddComponent<PoolId>();
                id.Id = pool.PoolId;
                id.InPool = false;

                obj.transform.position = position;
                var agent = obj.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.Warp(position);
            }

            
            obj.name = blueprint.name + "(" + obj.GetInstanceID() + ")";
            if (error) Debug.LogWarning("Re-created failed object '" + obj.name + "'"); 
            return obj;
        }

        /// <summary>
        /// Pre-allocates a chunk-sized number of objects in a pool.
        /// This method will return immediately while a co-routine
        /// performs the allocation in the background.
        /// </summary>
        /// <param name="blueprint"></param>
        /// <returns></returns>
        public static void PreallocateAsync(GameObject blueprint, int preallocCount, int chunkSize, int maxPoolSize, float delayTime = 0)
        {
            if (blueprint == null) throw new ArgumentNullException("blueprint");

            //don't use pools during editmode, it fucks everything
            #if UNITY_EDITOR
            if (!Application.isPlaying) return;
            #endif

            if (MaxPoolSize < 1) return;

            var pool = GetAssociatedPool(blueprint, chunkSize, maxPoolSize);
            pool.PreallocateAsync(preallocCount, delayTime);
        }

        /// <summary>
        /// Pre-allocates a chunk-sized number of objects in a pool.
        /// This method will return immediately while a co-routine
        /// performs the allocation in the background.
        /// </summary>
        /// <param name="blueprint"></param>
        /// <returns></returns>
        public static void PreallocateAsync(GameObject blueprint, float delayTime = 0)
        {
            PreallocateAsync(blueprint, AllocChunk, AllocChunk, MaxPoolSize, delayTime);
        }

        /// <summary>
        /// Relenquishes an object to the internal pooling mechanism. For all intents
        /// and purposes, this should be treated the same as calling Destroy on the GameObject
        /// and it should no longer be accessed. If the internal pool is at maximum capacity 
        /// the GameObject will be destroyed.
        /// 
        /// Note that even if an object was not instantiated using a pool, it can still be
        /// relenquished to one, however, the pool it is placed in will not be the same one
        /// as any other copies that were instantiated using Summon().
        /// </summary>
        /// <param name="gameObject"></param>
        public static void RelenquishToPool(GameObject gameObject)
        {
            if (gameObject == null) return;

            //don't use pools during editmode, it fucks everything
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var prefabType = UnityEditor.PrefabUtility.GetPrefabType(gameObject);
                if(prefabType == UnityEditor.PrefabType.None ||
                   (int)prefabType >= (int)UnityEditor.PrefabType.PrefabInstance)
                    DestroyImmediate(gameObject);
                else Debug.LogWarning("Could not destroy prefab: " + gameObject.name);
                return;
            }
            #endif

            if (MaxPoolSize < 1)
            {
                Destroy(gameObject);
                return;
            }

            //If we don't have a pool for this object's id
            //we know it didn't come from one. We just want
            //to destroy it like normal.
            PoolId p = null;
            int id = PoolIdValue(gameObject, out p);

            TransformPool pool = null;
            if (Pools.TryGetValue(id, out pool))
                pool.RelenquishObject(gameObject.transform, p);
            else Destroy(gameObject);
        }

        /// <summary>
        /// Relenquishes an object to the internal pooling mechanism. For all intents
        /// and purposes, this should be treated the same as calling Destroy on the GameObject
        /// and it should no longer be accessed. If the internal pool is at maximum capacity 
        /// the GameObject will be destroyed.
        /// 
        /// Note that even if an object was not instantiated using a pool, it can still be
        /// relenquished to one, however, the pool it is placed in will not be the same one
        /// as any other copies that were instantiated using Summon().
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="time"></param>
        public static void RelenquishToPool(GameObject gameObject, float time)
        {
            Instance.StartCoroutine(Instance.DelayedRelenquish(gameObject, time));
        }

        /// <summary>
        /// Helper for creating a deleayed relenquish function.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="time"></param>
        IEnumerator DelayedRelenquish(GameObject gameObject, float time)
        {
            yield return new WaitForSeconds(time);
            RelenquishToPool(gameObject);
        }

        /// <summary>
        /// Helper for retreiving the number used to identify
        /// to what pool an object should belong.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public static int PoolIdValue(GameObject gameObject, out PoolId pool)
        {
            int id;
            var poolId = gameObject.GetComponent<PoolId>();
            if (poolId != null) id = poolId.Id;
            else id = gameObject.GetInstanceID();
            pool = poolId;
            return id;
        }

        /// <summary>
        /// Helper for retreiving the number used to identify
        /// to what pool an object should belong.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public static int PoolIdValue(GameObject gameObject)
        {
            int id;
            var poolId = gameObject.GetComponent<PoolId>();
            if (poolId != null) id = poolId.Id;
            else id = gameObject.GetInstanceID();
            return id;
        }

        /// <summary>
        /// Creates a pool for the gameObject if one doesn't exist already and then forces
        /// its pool chunk and max sizes to the given values. If the current pool size is
        /// already over the new max, it will remain as so until drained or the excess gameObjects
        /// are summoned.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="chunkSize"></param>
        /// <param name="maxPoolsSize"></param>
        public static void ForcePoolSize(GameObject gameObject, int chunkSize, int maxPoolSize)
        {
            GetAssociatedPool(gameObject, chunkSize, maxPoolSize);
        }

        /// <summary>
        /// Returns a pool that is appropriate for storing identical copies of an object.
        /// </summary>
        static TransformPool GetAssociatedPool(GameObject gameObject, int chunkSize, int maxPoolSize)
        {
            //first, we need to know the id we will be using for this object's pool
            //if there is no PoolId attached we know this wasn't created by this pooler.
            //In that case, treat is as a prefab. WARNING: If it isn't actually a prefab and we
            //destroy it later, this could cause some serious issues!
            PoolId p = null;
            int id = PoolIdValue(gameObject, out p);

            //now we need to see if we have a pool for it yet
            TransformPool pool = null;
            if (!Pools.TryGetValue(id, out pool))
            {
                //looks like we need to create a new pool for this type of object
                GameObject go = new GameObject("Pool: " + id + " (" + gameObject.name + ")");
                go.transform.SetParent(Instance.transform, false);
                pool = go.AddComponent<TransformPool>();
                pool.PoolId = id;
                Pools[id] = pool;
                pool.ChunkSize = chunkSize;
                pool.MaxPoolSize = maxPoolSize;
                pool.AllocTime = AllocTime;

                //WARNING: This is a potential bug if we actually passed a live object and not a prefab!
                //If we destroy the live object, we can't properly use the pool associated with it!
                pool.Prefab = gameObject.transform;
            }

            return pool;
        }

        /// <summary>
        /// Returns a pool that is appropriate for storing identical copies of an object.
        /// </summary>
        public static TransformPool GetAssociatedPool(GameObject gameObject)
        {
            return GetAssociatedPool(gameObject, AllocChunk, MaxPoolSize);
        }

        /// <summary>
        /// Clears the internal cache of pooled objects, freeing the memory used by them.
        /// This will not affect objects currently in use, only the ones that have been
        /// relenquished back to their source pools. 
        /// </summary>
        /// <param name="level">The number of elements that should be left in the pools after draining.</param>
        public static void DrainPools(int level = 0)
        {
            if (level < 0) level = 0;

            //there are no pooled resources when in edit mode
            #if UNITY_EDITOR
            if(!Application.isPlaying) return;
            #endif

            //if there is no instance, there can't possibly be any pools
            if(Instance == null) return;

            if (level == 0)
            {
                //simply destroying all children of this instance should be enough to clear things up
                //REMEMBER: Destroy takes a frame to work so we do actually need to interate through
                //all of the child indices.
                for (int i = 0; i < Instance.transform.childCount; i++)
                    Destroy(Instance.transform.GetChild(i).gameObject);
                Pools = new Dictionary<int, TransformPool>();
            }
            else
            {
                foreach (var pool in Pools)
                    pool.Value.DrainPool(level);
            }

        }

        /// <summary>
        /// This forces all entities that came from any pool to immediately
        /// relenquish back to it. Note that this may cause some entities
        /// to become destroyed if its pool is at full capcity.
        /// </summary>
        public static void RelenquishAll()
        {
            //there are no pooled resources when in edit mode
            #if UNITY_EDITOR
            if (!Application.isPlaying) return;
            #endif

            //if there is no instance, there can't possibly be any pools
            if (Instance == null) return;

            var list = SubPools;
            for(int i = 0; i < list.Count; i++)
                list[i].RecallAll(); //BUG ALERT: not setting PoolId.InPool!

        }

        /// <summary>
        /// This forces all entities that came from the specified pool to immediately
        /// relenquish back to it. Note that this may cause some entities
        /// to become destroyed if the pool is at full capcity.
        /// </summary>
        /// <param name="poolId"></param>
        public static void RelenquishPool(TransformPool pool)
        {
            //there are no pooled resources when in edit mode
            #if UNITY_EDITOR
            if (!Application.isPlaying) return;
            #endif

            //if there is no instance, there can't possibly be any pools
            if (pool != null && Instance != null) pool.RecallAll(); //BUG ALERT: not setting PoolId.InPool!
        }

        /// <summary>
        /// This forces all entities that came from any of the specified pools to immediately
        /// relenquish back to them. Note that this may cause some entities
        /// to become destroyed if their pool is at full capcity.
        /// </summary>
        /// <param name="pools"></param>
        public static void RelenquishPools(List<TransformPool> pools)
        {
            throw new UnityException("Not yet implemented.");
        }

        /// <summary>
        /// This forces all entities that came from any pool except the ones
        /// specified to immediately relenquish back to it. Note that this may
        /// cause some entities to become destroyed if its pool is at full capcity.
        /// </summary>
        /// <param name="excludedPools"></param>
        public static void RelenquishAllBut(List<TransformPool> excludedPools)
        {
            throw new UnityException("Not yet implemented.");
        }
        #endregion
    }
}


namespace UnityEngine
{
    /// <summary>
    /// Extension methods for GameObject that allows easy instantiation of pooled instances.
    /// </summary>
    public static partial class GameObjectExtensions
    {
        /// <summary>
        /// Instantiates a copy of the supplied GameObject and registers it with an interal object pool.
        /// The returned instance can then be returned to the pool at any time using RelenquishToPool().
        /// </summary>
        /// <param name="go"></param>
        /// <returns></returns>
        public static GameObject InstantiateFromPool(this GameObject go, GameObject blueprint)
        {
            return Toolbox.Lazarus.Summon(go);
        }

        /// <summary>
        /// Returns a GameObject to an internally managed pool. For all intents and purposes
        /// you must treat objects that are relenquished in this way as though you called Destroy()
        /// on them. If the intertnal pool is full, the object will in fact be destroyed.
        /// </summary>
        /// <param name="go"></param>
        public static void RelenquishToPool(this GameObject go)
        {
            Toolbox.Lazarus.RelenquishToPool(go);
        }
    }

    
}

