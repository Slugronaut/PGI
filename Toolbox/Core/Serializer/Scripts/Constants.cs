/**********************************************
* Pantagruel
* Copyright 2015-2017 James Clark
**********************************************/
using UnityEngine;
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pantagruel.Serializer
{
    /// <summary>
    /// Public shared constants of Pantagruel.
    /// </summary>
    public static class Constants
    {
#if UNITY_EDITOR
#pragma warning disable CS0169 // The field 'Constants.CachedRootPath' is never used
        /// <summary>
        /// This is the root path used by all edit-time asset build tools - specifically the manifest generator.
        /// </summary>
        private static string CachedRootPath;
#pragma warning restore CS0169 // The field 'Constants.CachedRootPath' is never used

        public static string RootPath
        {
            get
            {
                //need to do a little detective work to figure out
                //where the root of Pantagruel is actually located.
                var root = AssetDatabase.FindAssets("\"Pantagruel Serializer Root\"");
                if (root == null || root.Length < 1) throw new UnityException("Cannot locate the root folder for Panatgruel's serializer!");

                string path = AssetDatabase.GUIDToAssetPath(root[0]);
                path = path.Remove(0, "Assets/".Length);
                int i = path.LastIndexOf("/");
                if(i < path.Length-1) path = path.Remove(i + 1);
                //Debug.Log("<color=green>" + path + "</color>");
                return path;
            }
        }

        /// <summary>
        /// Path to store the resource manifests when the library is built at edit time.
        /// </summary>
        public static readonly string ManifestPath = RootPath + "Resources/Manifests/";

        
#endif


        /// <summary>
        /// This controls the length of the names generated for manifest files. In the event that different objects
        /// would be mapped to the same manifest, they must be resolved by searching through all objects within the
        /// manifest associated until the correct object is found.
        /// 
        /// The default value (int.MaxValue) ensures that each uniquely named object will have its own manifest file.
        /// For very large projects that have many assets stored in Resources folders, it can become quite cumbersome
        /// and bloated to have so many manifests. If this value is reduced to something small, say 1 to 3, there
        /// will be significantly fewer manifest files generated which can save many dozens to hundreds of megabytes
        /// in the final build size. However, having fewer manifests will cause the serializer to take longer due to
        /// the increased time spent searching for the correct asset path within the appropriate manifest.
        /// 
        /// At approximately 6 kilobytes per manifest, it is usually better to leave this value high unless you have more
        /// than, say, ten-thousand assets inside Resources folders. At that point it is advisable to reduce this value
        /// to something between 1 and 3. Never set it to zero or lower!
        /// 
        /// IMPORTANT: Any time you change this value you must run the manifest build tool again.
        /// </summary>
        public static readonly int ManifestNameLength = 1;// int.MaxValue;

        /// <summary>
        /// There are some asset names that are not allowed in Unity, This list is used
        /// to ensure manfiests that are generagted don't use these names. Note that they are
        /// all in caps, but all cases are illegal.
        /// </summary>
        public static readonly List<string> IllegalAssetNames = new List<string>(new string[]
        {
            "NUL",
            "CON"
        });

        /// <summary>
        /// This is a list of UnityEngine.Object sub-types that should be treated
        /// as 'Resource reference' types. That is, the system will try to serialize
        /// a string that can be used by Resources.Load() at runtime to deserialize
        /// the reference. This is also the list of types that are supported by the
        /// Resource Manifest Library builder.
        /// </summary>
        public static readonly Type[] ResourceTypes = new Type[] 
        {
            typeof(AnimationClip),
            typeof(RuntimeAnimatorController),
            typeof(AudioClip),
            typeof(Font),
            typeof(GameObject),
            typeof(Material),
            typeof(Mesh),
            typeof(PhysicMaterial),
            typeof(PhysicsMaterial2D),
            typeof(Shader),
            typeof(Sprite),
            typeof(Texture2D),
            typeof(Texture3D),
        };
        
        
    }
}
