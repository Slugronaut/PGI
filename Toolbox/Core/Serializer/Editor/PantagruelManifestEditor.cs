/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using Toolbox;
using Pantagruel.Serializer.Surrogate;

namespace Pantagruel.Serializer.Editor
{
    
    /// <summary>
    /// Editor for generating and manipulating the resource manifest prefab
    /// that Pantagruel requires for serializing resource references correctly.
    /// </summary>
    public class PantagruelManifestEditor : EditorWindow
    {
        double LastBuildTime = 0.0;

        #region Unity Events
        [MenuItem("Window/Pantagruel/Resource Manifest")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<PantagruelManifestEditor>("Resource Manifest");
        }

        void OnEnable()
        {
            hideFlags = HideFlags.HideAndDontSave;
        }

        void OnGUI()
        {
            GUILayout.Space(15);
            //The debug logs are actually useless since it will be delayed until
            //the entire OnGUI event is done. We should probably move the Building
            //to another thread.
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                this.ShowNotification(new GUIContent("Please Wait..."));
                return;
            }

            /*
            if (GUILayout.Button("Build Resource Manifest Library\n(Fast)", GUILayout.Height(45)))
            {
                Debug.Log("<color=blue>Building manifest library. Please wait...</color>");
                double start = EditorApplication.timeSinceStartup;
                //TODO: Delete all old manifest files
                Toolbox.Editor.Utility.ConfirmAssetDirectory(Constants.ManifestPath);
                BuildManifestLibrary(Constants.ManifestPath, false, Constants.ResourceTypes);
                LastBuildTime = EditorApplication.timeSinceStartup - start;

                Debug.Log("<color=blue>Manifest library complete. All manifest assets can be found in</color> Assets/" + Constants.ManifestPath + "");

            }
            GUILayout.Space(25);
            */
            if(GUILayout.Button("Build ResourceManifest Library", GUILayout.Height(45)))
            {
                Debug.Log("<color=blue>Building manifest library. Please wait...</color>");

                double start = EditorApplication.timeSinceStartup;
                //TODO: Delete all old manifest files
                Toolbox.ToolboxEditor.ToolboxEditorUtility.ConfirmAssetDirectory(Constants.ManifestPath);
                BuildManifestLibrary(Constants.ManifestPath, true, Constants.ResourceTypes);
                LastBuildTime = EditorApplication.timeSinceStartup - start;

                Debug.Log("<color=blue>Manifest library complete. All manifest assets can be found in</color> Assets/" + Constants.ManifestPath + "");

            }

            
            GUILayout.Space(25);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Time:");
            GUILayout.Label(LastBuildTime.ToString());
            GUILayout.EndHorizontal();
        }
        #endregion


        #region Static Methods
        /// <summary>
        /// This forces the asset database to save any asset files associated with
        /// //these manifest using the latest state. Effectively it is 'saving the files'.
        /// </summary>
        /// <param name="manifests"></param>
        static void PushObjectsToAssets(List<ResourceManifest> manifests)
        {
            if (manifests == null || manifests.Count < 1) return;

            //TODO: ensure manifest files exist

            foreach (var manifest in manifests)
            {
                if(! TypeHelper.IsReferenceNull(manifest))EditorUtility.SetDirty(manifest);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        static ResourceManifest CreateManifestAsset(string path)
        {
            ResourceManifest manifest = ScriptableObject.CreateInstance<ResourceManifest>();
            AssetDatabase.CreateAsset(manifest, "Assets/" + path);
            return manifest;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        static List<ResourceManifest> BuildManifestLibrary(string manifestPath, bool safetyChecks, params Type[] supportedTypes)
        { 
            List<ResourceManifest> list = new List<ResourceManifest>(10);
            Dictionary<string, ResourceManifest> map = new Dictionary<string, ResourceManifest>(10);

            EditorUtility.ClearProgressBar();
            //-remove all old manifests
            //-get all resources
            //-group by name
            //-assign each group to a resource manifest that is newly created
            int typeCount = 0;
            foreach (var type in supportedTypes)
            {
                string[] guids = AssetDatabase.FindAssets("t:"+type.Name);
                for(int gi = 0; gi < guids.Length; gi++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Building Resource Manifest", "Processing " + type.Name + " " + gi + " of " + guids.Length + "...", ((float)typeCount / (float)supportedTypes.Length)))
                        break;

                    //TODO: we really can only use the manifest for things located in a resouce folder
                    //we need to filter that here so that we aren't processing a bunch of usless resources
                    //that will never be properly accessable at runtime.

                    string guid = guids[gi];
                    string guidPath = AssetDatabase.GUIDToAssetPath(guid);
                    //don't bother processing if it's not in a resource folder
                    //TODO: we need to also check for built-in resource here - they are never in a Resources folder!
                    if (!guidPath.StartsWith("Assets/") || !guidPath.Contains("/Resources/"))
                        continue;
                    
                    var obj = AssetDatabase.LoadAssetAtPath(guidPath, type);
                    string assetPath = AssetDatabase.GetAssetPath(obj);
                    


                    if (type == typeof(GameObject))
                    {
                        //only support GameObject prefabs for now
                        //TODO: Add support for model prefabs
                        if (PrefabUtility.GetPrefabType(obj) != PrefabType.Prefab)
                            continue;
                    }

                    string path = AssetPathToResourcePath(assetPath);

                    //due to nested assets (sliced sprites) we might hit the same resource multiple times.
                    //Add them to a list of things already checked.
                    //if (alreadyProcessed.Contains(assetPath)) continue; //this could really slow things down in large projects!
                    //alreadyProcessed.Add(assetPath);


                    //HACK ALERT: special handler for sprites since they can contain sub-sprites when slicing
                    //yet are very frequently referred as individual resources.
                    //NOTE: This needs amore general-purpose handling for all types of assets!
                    if (type == typeof(Sprite))
                    {
                        var file = AssetPathToResourceName(assetPath);
                        var reps = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                        //we are skipping the first asset since it is actually a Texture2D that will be encoded separately anyway.
                        for(int i = 1; i < reps.Length; i++)
                        {
                            //HACK ALERT: Encoding our sub-resources directly into the path name!!
                            //This is the easiest way to keep the format the same for all resources yet
                            //be able to tell in the deserializer if we have a sub-asset.
                            string subFile = file + SurrogateBase.SubResourceDelimeter + (i-1);
                            RecordAsset(manifestPath, path, subFile, type, reps[i], map, list, safetyChecks);
                        }
                    }
                    else
                    {
                        var file = AssetPathToResourceName(assetPath);
                        RecordAsset(manifestPath, path, file, type, obj, map, list, safetyChecks);
                    }
                }
                typeCount++;
            }

            EditorUtility.DisplayProgressBar("Building Resource Manifest", "Pushing updates to assets...", 0.95f);
            PushObjectsToAssets(list);
            EditorUtility.ClearProgressBar();
            return list;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="manifestPath"></param>
        /// <param name="assetPath"></param>
        /// <param name="safetyChecks"></param>
        static void RecordAsset(string manifestPath, string path, string file, Type type, UnityEngine.Object obj, Dictionary<string, ResourceManifest> map, List<ResourceManifest> list, bool safetyChecks)
        {
            //We use this for the manifest rather than 'file' because
            //we can't determine the other one at runtime due to the use
            //of AssetDatabase. This only requires the object's name.
            string manifestName = ResourceManifest.GetManifestName(obj.name);


            //obtain the manifest that stores all resources with this name
            ResourceManifest manifest = null;
            map.TryGetValue(manifestName, out manifest);
            if (manifest == null)
            {
                try { manifest = CreateManifestAsset(manifestPath + manifestName + ".asset"); }
#pragma warning disable 0168
                catch (Exception e)
                {
                    Debug.Log("<color=red>Failed to create asset: " + manifestName + "</color>");
                    return;
                }
#pragma warning restore 0168
                if (manifest == null)
                {
                    Debug.Log("<color=red>Failed to create asset: " + manifestName + "</color>");
                    return;
                }
                map.Add(manifestName, manifest);
                list.Add(manifest);
            }

            //are we going to look for repeat paths to
            //different resources of the same type? (slow but safe)
            string fullPath = path + file;
            
            if (safetyChecks)
            {
                if (SafetyCheck(manifest, fullPath, type))
                    manifest.AddResource(obj, fullPath);
                else Debug.Log("<color=red>WARNING:</color>There are multiple resources of the type '" + type.Name + "' that are being compiled to the path 'Resources/" + fullPath + "'.\nThe manifest cannot determine which object this path should point to so only the first occurance has been stored.\nPlease ensure all resources of the same type and at the same directory level relative to 'Resources/' have unique names.");
            }
            else
            {
                manifest.AddResource(obj, fullPath);
            }
        }

        /// <summary>
        /// Helper method to perform a safety check that ensures us we won't accidentally
        /// create two resources of the same type with the same name that result in the same
        /// path when the Resources folders are compiled for a runtime player.
        /// </summary>
        /// <param name="manifest"></param>
        /// <param name="path"></param>
        /// <returns><c>false if the 'Resources' relative path already exists within the manifest</c></returns>
        static bool SafetyCheck(ResourceManifest manifest, string path, Type type)
        {
            foreach(string val in manifest.AllPaths())
            {
                if (path == val)
                {
                    //TODO: We need a faster way of doing this. Large projects
                    //will suffer a lot in this section, I suspect.
                    //THIS CAN BE SUPER SLOW
                    if(manifest.GetResourceFromPath(path, type) != null)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Helper method that takes a full asset path to a
        /// resource (relative to the project folder) and strips
        /// away everything but subdirectories within 'Resources' folders.
        /// In this way we can convert the edit-time asset path to a path that
        /// is usable by Resources.Load() at runtime.
        /// </summary>
        /// <remarks>
        /// This will strip away leading paths up-to-and-including 
        /// 'Resources/' well as the filename, leaving either a
        /// subdirectory of 'Resources/'or an empty string.
        /// </remarks>
        /// <param name="assetPath"></param>
        /// <returns></returns>
        static string AssetPathToResourcePath(string assetPath)
        {
            int i = -1;
            if (assetPath.StartsWith("Assets/"))
            {
                //this will strip away the filename
                i = assetPath.LastIndexOf("/");
                assetPath = assetPath.Remove(i) + "/";
            }

            //This will remove all directories up-to-and-including 'Resources',
            //leaving only subdirectories of 'Resources' or an empty string.
            i = -1;
            i = assetPath.IndexOf("Resources/");
            if (i >= 0)
            {
                assetPath = assetPath.Substring(i + 10); //offset by 10 to account for 'Resources/' string
            }
            
            return assetPath;
        }

        /// <summary>
        /// Converts a full path to an edit-time asset to a filename (with no path)
        /// useable by Resources.Load().
        /// </summary>
        /// <remarks>
        /// Essentially, all this does is strip away the path (and possibly any extension)
        /// //and leave only the filename.
        /// </remarks>
        /// <param name="path"></param>
        /// <returns></returns>
        static string AssetPathToResourceName(string path)
        {
            //remove directories
            int i = path.LastIndexOf("/");
            if (i >= 0) path = path.Remove(0, i + 1);

            //remove extensions
            var split = path.Split('.');
            if (split != null && split.Length > 0) path = split[0];

            return path;
        }

        /// <summary>
        /// 
        /// </summary>
        static void DisplayManifest(List<ResourceManifest> manifests)
        {
            GUILayout.Label("TODO: Display manifest contents here.");
        }
#endregion
    }


}