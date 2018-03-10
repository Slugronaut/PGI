/**********************************************
* Pantagruel
* Copyright 2015-2017 James Clark
**********************************************/
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Pantagruel.Serializer
{
    /// <summary>
    /// A utility component that must be used at edit-time to build
    /// a manifest of all resources used by any components attached
    /// to the same GameObject. This is the only way to translate
    /// an in-game resource's path so that it can be dynamically
    /// loaded using Resouce.Load().
    /// </summary>
    /// <remarks>
    /// Any time you need to add support for a new resource type,
    /// search for the text 'APPEND NEW TYPES HERE' within this file
    /// to see where you will need to add code to make that type fully functional.
    /// Sadly, this can be automatd in code because Unity simply doesn't allow it
    /// (no support for serializing polymorphic data or generics).
    /// You will also have to add this type to the 'Pantagruel.Serializer.Constants' class.
    /// 
    /// ------------------------ IMPORTANT ---------------------------------------------------
    /// The 'AllMaps' method is pretty brittle and might choke. Make sure you understand
    /// that method before adding any new non-public fields that expose the IResourceMap interface.
    /// --------------------------------------------------------------------------------------
    /// </remarks>
    [Serializable]
    public class ResourceManifest : ScriptableObject, ISerializationCallbackReceiver
    {
        //APPEND NEW TYPES HERE

        //no support for serialization of polymorphism or generics means
        //we need to do silly stuff like this. We need to define a non generic class
        //derived from ResourceMap<> for each resource type we want to support.
        //APPEND NEW TYPES HERE
        [Serializable]
        public class AnimationClipMap : ResourceMap<AnimationClip> { }
        [Serializable]
        public class AnimatorControllerMap : ResourceMap<RuntimeAnimatorController>{ }
        [Serializable]
        public class AudioClipMap : ResourceMap<AudioClip> { }
        [Serializable]
        public class FontMap : ResourceMap<Font> { }
        [Serializable]
        public class GameObjectMap : ResourceMap<GameObject> { }
        [Serializable]
        public class MaterialMap : ResourceMap<Material> { }
        [Serializable]
        public class MeshMap : ResourceMap<Mesh> { }
        [Serializable]
        public class PhysicMaterialMap : ResourceMap<PhysicMaterial> { }
        [Serializable]
        public class PhysicsMaterial2DMap : ResourceMap<PhysicsMaterial2D> { }
        [Serializable]
        public class ShaderMap : ResourceMap<Shader> { }
        [Serializable]
        public class SpriteMap : ResourceMap<Sprite> { }
        [Serializable]
        public class Texture2DMap : ResourceMap<Texture2D> { }
        [Serializable]
        public class Texture3DMap : ResourceMap<Texture3D> { }


        //We need to define a variable for each type of resource map
        //we want to use. We can't simply use a list of UnityEngine.Objects
        //because unity can't serialize polymorphic data without loosing
        //the original data type.
        //APPEND NEW TYPES HERE
        [SerializeField]
        AnimationClipMap AnimationClips;
        [SerializeField]
        AnimatorControllerMap AnimatorControllers;
        [SerializeField]
        AudioClipMap AudioClips;
        [SerializeField]
        FontMap Fonts;
        [SerializeField]
        GameObjectMap GameObjects;
        [SerializeField]
        MaterialMap Materials;
        [SerializeField]
        MeshMap Meshes;
        [SerializeField]
        PhysicMaterialMap PhysicMaterials;
        [SerializeField]
        PhysicsMaterial2DMap PhysicsMaterial2Ds;
        [SerializeField]
        ShaderMap Shaders;
        [SerializeField]
        SpriteMap Sprites;
        [SerializeField]
        Texture2DMap Texture2Ds;
        [SerializeField]
        Texture3DMap Texture3Ds;

        /// <summary>
        /// Returns the path to the given resource, if there is such a path.
        /// The path leads to the unity-compiled 'Resources' directory.
        /// </summary>
        /// <param name="resource"></param>
        /// <returns></returns>
        public string GetPathToResource(UnityEngine.Object resource)
        {
            Type resType = resource.GetType();
            if (resType == null) return null;

            //UnityEditor.Animations.AnimatorController
            //APPEND NEW TYPES HERE
            if (resType == typeof(AnimationClip)) return AnimationClips.GetPath(resource);
            else if (resType == typeof(RuntimeAnimatorController)) return AnimatorControllers.GetPath(resource);
            //*sigh* we need a special case here since the animator controllers we get
            //from AssetDatabase at edit-time aren't of the correct type.
            else if (resType.IsSubclassOf(typeof(RuntimeAnimatorController))) return AnimatorControllers.GetPath(resource);
            else if (resType == typeof(AudioClip)) return AudioClips.GetPath(resource);
            else if (resType == typeof(Material)) return Materials.GetPath(resource);
            else if (resType == typeof(Mesh)) return Meshes.GetPath(resource);
            else if (resType == typeof(Font)) return Fonts.GetPath(resource);
            else if (resType == typeof(GameObject)) return GameObjects.GetPath(resource);
            else if (resType == typeof(PhysicMaterial)) return PhysicMaterials.GetPath(resource);
            else if (resType == typeof(PhysicsMaterial2D)) return PhysicsMaterial2Ds.GetPath(resource);
            else if (resType == typeof(Shader)) return Shaders.GetPath(resource);
            else if (resType == typeof(Sprite)) return Sprites.GetPath(resource);
            else if (resType == typeof(Texture2D)) return Texture2Ds.GetPath(resource);
            else if (resType == typeof(Texture3D)) return Texture3Ds.GetPath(resource);
            return null;
        }

        /// <summary>
        /// Adds a new resource and its location to the manifest.
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="path"></param>
        public bool AddResource(UnityEngine.Object resource, string path)
        {
            if (Application.isPlaying) throw new UnityException("ResourceManifest should only have new resourceses added to it during edit-time.");

            if (resource == null || path == null) return false;
            Type resType = resource.GetType();
            if (resType == null) return false;

            InitMaps();
            bool flag = false;

            //APPEND NEW TYPES HERE
            if (resType == typeof(AnimationClip)) flag = AnimationClips.Add(resource, path);
            else if (resType == typeof(AudioClip)) flag = AudioClips.Add(resource, path);
            else if (resType == typeof(RuntimeAnimatorController)) flag = AnimatorControllers.Add(resource, path);
            else if (resType.IsSubclassOf(typeof(RuntimeAnimatorController))) flag = AnimatorControllers.Add(resource, path);
            else if (resType == typeof(Font)) flag = Fonts.Add(resource, path);
            else if (resType == typeof(GameObject)) flag = GameObjects.Add(resource, path);
            else if (resType == typeof(Material)) flag = Materials.Add(resource, path);
            else if (resType == typeof(Mesh)) flag = Meshes.Add(resource, path);
            else if (resType == typeof(PhysicMaterial)) flag = PhysicMaterials.Add(resource, path);
            else if (resType == typeof(PhysicsMaterial2D)) flag = PhysicsMaterial2Ds.Add(resource, path);
            else if (resType == typeof(Shader)) flag = Shaders.Add(resource, path);
            else if (resType == typeof(Sprite)) flag = Sprites.Add(resource, path);
            else if (resType == typeof(Texture2D)) flag = Texture2Ds.Add(resource, path);
            else if (resType == typeof(Texture3D)) flag = Texture3Ds.Add(resource, path);
            else return false;

            return flag;
        }

        /// <summary>
        /// Returns the object associated with the given path relative
        /// to a Resources/ folder and of the given type.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public UnityEngine.Object GetResourceFromPath(string path, Type type)
        {
            var maps = AllMaps();

            for (int i = 0; i < maps.Count; i++)
            {
                if(maps[i].Values.Contains(path))
                {
                    var obj = maps[i].GetObjectAtPath(path);
                    if (obj.GetType() == type) return obj;
                }
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        void OnEnable()
        {
            InitMaps();
        }
        
        /// <summary>
        /// 
        /// </summary>
        void InitMaps()
        {
            //APPEND NEW TYPES HERE
            if (AnimationClips == null) AnimationClips = new AnimationClipMap();
            if (AnimatorControllers == null) AnimatorControllers = new AnimatorControllerMap();
            if (AudioClips == null) AudioClips = new AudioClipMap();
            if (Fonts == null) Fonts = new FontMap();
            if (GameObjects == null) GameObjects = new GameObjectMap();
            if (Materials == null) Materials = new MaterialMap();
            if (Meshes == null) Meshes = new MeshMap();
            if (PhysicMaterials == null) PhysicMaterials = new PhysicMaterialMap();
            if (PhysicsMaterial2Ds == null) PhysicsMaterial2Ds = new PhysicsMaterial2DMap();
            if (Shaders == null) Shaders = new ShaderMap();
            if (Sprites == null) Sprites = new SpriteMap();
            if (Texture2Ds == null) Texture2Ds = new Texture2DMap();
            if (Texture3Ds == null) Texture3Ds = new Texture3DMap();
        }

        public void OnBeforeSerialize()
        {
            foreach(IResourceMap map in AllMaps())
            {
                map.OnBeforeSerialize();
            }
        }

        public void OnAfterDeserialize()
        {
            foreach (IResourceMap map in AllMaps())
            {
                map.OnAfterDeserialize();
            }
        }

        /// <summary>
        /// Use a little reflection magic to help us avoid
        /// hand-writing more code than we really need to.
        /// This will return a list all non-public fields that
        /// expose the IResourceMap interface. Mostly this is used
        /// as a helper by the 'AllPaths()' method.
        /// </summary>
        /// <returns></returns>
        List<IResourceMap> AllMaps()
        {
            List<IResourceMap> map = new List<IResourceMap>();
            Type type = this.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            for(int i = 0; i < fields.Length; i++)
            {
                IResourceMap m = fields[i].GetValue(this) as IResourceMap;
                if (m != null) map.Add(m);
            }
            return map;
        }

        /// <summary>
        /// Returns the 'Resources' relative paths for all objects stored in this manifest.
        /// </summary>
        /// <returns></returns>
        public List<string> AllPaths()
        {
            var maps = AllMaps();
            List<string> paths = new List<string>(5);

            for(int i = 0; i < maps.Count; i++)
            {
                paths.AddRange(maps[i].Values);
            }
            return paths;
        }

        /// <summary>
        /// Some resource objects (most notably, Shaders) may put additional
        /// directories in the name that do not correspond to a real directory
        /// within a Resources folder. This method will strip that information out
        /// along with the file exention. Useful for finding a manifest match using
        /// an object's name at runtime.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string CleanResourceObjectName(string name)
        {
            //remove the extension
            var split = name.Split('.');
            if (split != null && split.Length > 0) name = split[0];

            //now we want to be sure we strip off any directories that might be
            //a part of the resource name itself (shaders do this frequently).
            int i = name.LastIndexOf("/");
            if (i >= 0) name = name.Remove(0, i + 1);

            return name;
        }

        /// <summary>
        /// Given the name of an object, this will return the appropriate manifest resource name it
        /// should be associated with. This only returns the appropriate name and does not actually
        /// confirm that any such manifest file exists. It also does not provide any appropriate
        /// subdirectories, only the filename of the asset itself.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetManifestName(string name)
        {
            string manifestName = CleanResourceObjectName(name);
            manifestName = manifestName.Substring(0, Mathf.Min(Constants.ManifestNameLength, manifestName.Length)).ToUpper();

            //for some reason, there are certain asset names that are illegal in Unity
            if (Constants.IllegalAssetNames.Contains(manifestName))
                manifestName += "_legal";

            return manifestName;
        }
    }
}
