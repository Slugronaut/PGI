/**********************************************
* Ancient Craft Games
* Copyright 2014-2017 James Clark
**********************************************/
using System;
using UnityEngine;
using System.Collections;
using System.Text;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

namespace Toolbox.ToolboxEditor
{
    /// <summary>
    /// Editor utility class for all edit-time Pantagruel systems.
    /// </summary>
    public static class ToolboxEditorUtility
    {
        /// <summary>
        /// Ensures the database asset Directory exists and creates it if it doesn't.
        /// </summary>
        /// <param name="directories">A full directory path that is split into a list of strings where each string represents a the next folder in the path.</param>
        public static void ConfirmAssetDirectory(params string[] directories)
        {
            if (directories == null || directories.Length < 1) throw new NullReferenceException();
            StringBuilder sb = new StringBuilder("Assets");
            StringBuilder sb2 = new StringBuilder();
            foreach (string dir in directories)
            {
                sb.Append("/");
                sb.Append(dir);
                sb2.Append("/");
                sb2.Append(dir);

                if (!Directory.Exists(Application.dataPath + sb2.ToString()))
                {
                    string d = sb.ToString().Remove(sb.ToString().LastIndexOf('/'));
                    AssetDatabase.CreateFolder(d, dir);

                }

            }

        }

        /// <summary>
        /// Ensures the database asset Directory exists and creates it if it doesn't.
        /// </summary>
        /// <param name="directories">A full directory path that is split into a list of strings where each string represents a the next folder in the path.</param>
        public static void ConfirmAssetDirectory(string path, bool stripAssetsDir)
        {
            path = path.TrimStart('/');
            if(stripAssetsDir)
            {
                path = path.Remove(0, "Assets/".Length);
            }
            List<string> dirs = new List<string>();

            foreach(var s in path.Split('/'))
            {
                if (!string.IsNullOrEmpty(s)) dirs.Add(s);
            }

            ConfirmAssetDirectory(dirs.ToArray());
        }

        /// <summary>
        /// Returns true if a file exists at the given resource directory.
        /// </summary>
        /// <param name="path">A path to a file that is relative to the project's Assets directory.</param>
        public static bool ConfirmResourceExists(string path)
        {
            string fullPath = Application.dataPath + "/" + path;
            if (System.IO.File.Exists(fullPath))
                return true;

            return false;
        }

        /// <summary>
        /// Returns all assets of the given type from the provided file directory.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="paths"></param>
        /// <returns></returns>
        public static T[] GetAssetsAtPath<T>(string paths)
        {
            ArrayList al = new ArrayList();
            string fullPath = Application.dataPath;
            fullPath = fullPath.Remove(fullPath.Length - "/Assets".Length, "/Assets".Length);
            fullPath += "/" + paths;
            if (fullPath.EndsWith("/")) fullPath.Remove(fullPath.Length - 2, 1);


            string[] fileEntries = Directory.GetFiles(fullPath);
            foreach (string fileName in fileEntries)
            {
                int assetPathIndex = fileName.IndexOf("Assets");
                string localPath = fileName.Substring(assetPathIndex);

                UnityEngine.Object t = AssetDatabase.LoadAssetAtPath(localPath, typeof(T));

                if (t != null)
                    al.Add(t);
            }
            T[] result = new T[al.Count];
            for (int i = 0; i < al.Count; i++)
                result[i] = (T)al[i];

            return result;
        }

        /// <summary>
        /// Helper method used to get the actual datatype being represented by a SerializedProperty.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static T GetBackingField<T>(SerializedProperty prop)
        {
            string[] paths = prop.propertyPath.Split('.');
            object target = prop.serializedObject.targetObject as object;
            foreach (var path in paths)
            {
                var info = target.GetType().GetField(path);
                target = info.GetValue(target);
            }

            return (T)target;
        }

        /// <summary>
        /// Creates a new asset file and saves an objects data within it.
        /// If an asset file already exists, the data it contains is updated
        /// instead so as to preserve any reference links.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="asset"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static T CreateOrReplaceAsset<T>(T asset, string path) where T : UnityEngine.Object
        {
            T existingAsset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existingAsset == null)
                AssetDatabase.CreateAsset(asset, path);
            else EditorUtility.CopySerialized(asset, existingAsset);
            return existingAsset;
        }

        /// <summary>
        /// Looks for an asset at path. If one is found, it is returned.
        /// Otherwise the source object is copied to the specified location
        /// and then returned.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static T GetCopyOfAsset<T>(T source, string path) where T : UnityEngine.Object
        {
            if (source == null) return null;
            T existingAsset = AssetDatabase.LoadAssetAtPath<T>(path);
            if(existingAsset == null)
            {
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path)) return null;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                existingAsset = AssetDatabase.LoadAssetAtPath<T>(path);
            }

            if (existingAsset == null) return null;
            var s = path.Split('/');
            var fileName = s[s.Length - 1].Split('.');
            existingAsset.name = fileName[0];
            return existingAsset;
        }

        /// <summary>
        /// Performs a normal assetdatabase-copy of the original asset to a special shadow file.
        /// Then the shadow asset is serialized-copied to the final desired asset. 
        /// 
        /// This seemingly odd way of copying the asset is done for three reasons. 
        /// 1) Unity's AnimatorController has a truly fucked internal data format
        /// that is impervious to any kind of duplication (even through serialization!)
        /// 2) Unity's CopySerialized method does not perform a deep copy which means all 
        /// AnimatorControllers created by it would share the same StateMachine internally, 
        /// and 3) AssetDatabase copying requires destroying the original asset which would break 
        /// all currently existing links to that animator controller, making life harder in general.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="newPath"></param>
        /// <returns></returns>
        public static T ShadowCopyAsset<T>(T source, string path, string fileName) where T : UnityEngine.Object
        {
            if (source == null) return null;
            string destPath = path + fileName;
            string sourcePath = AssetDatabase.GetAssetPath(source);
            T existingAsset = AssetDatabase.LoadAssetAtPath<T>(destPath);
            if (existingAsset == null)
            {
                if (!AssetDatabase.CopyAsset(sourcePath, destPath)) return null;
            }
            else
            {
                //CopySerialized() won't work very well with assets that use references (like AnimationController's stateMachines)
                //since it will not perform a deep copy.
                string shadowPath = path + "shadow.controller";
                AssetDatabase.DeleteAsset(shadowPath);
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), shadowPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                var shadow = AssetDatabase.LoadAssetAtPath<T>(shadowPath);
                if(shadow == null)
                {
                    Debug.LogError("There was a error when copying the resource in-place.");
                    return null;
                }
                EditorUtility.CopySerialized(shadow, existingAsset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<T>(destPath);
            
        }

    }


    /// <summary>
    /// Helper methods for editor GUIs.
    /// </summary>
    public static class UI
    {
        /// <summary>
        /// Displays a complex integer form that allows a min/max value and
        /// supplies both a slider and textboxes for data entry.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="startFrame"></param>
        /// <param name="endFrame"></param>
        public static void GUISlider(string title, ref int startFrame, ref int endFrame, int max = 20, int min = 0)
        {
            float s = startFrame;
            float e = endFrame;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(title);
            s = EditorGUILayout.IntField((int)s, GUILayout.Width(50));
            EditorGUILayout.MinMaxSlider(ref s, ref e, min, max, GUILayout.Width(150));
            e = EditorGUILayout.IntField((int)e, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            startFrame = (int)s;
            endFrame = (int)e;
            if (endFrame < startFrame) endFrame = startFrame;
            if (startFrame < min) startFrame = min;
            else if (startFrame > max) startFrame = max;
            if (endFrame > max) endFrame = max;
            else if (endFrame < min) endFrame = min;
        }

        static string LastPath;
        /// <summary>
        /// Helper for displaying an OpenFolder dialog and returning a path string to the location chosen.
        /// </summary>
        /// <param name="displayPath"></param>
        /// <param name="realPath"></param>
        /// <returns><c>true</c> if the directory selected has changed, <c>false</c> otherwise."/></returns>
        public static bool DisplayOpenFolder(EditorWindow user, ref string displayPath, ref string realPath, ref string assetPath, ref string directory)
        {
            bool changed = false;
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.PrefixLabel("Working Directory");
                EditorGUILayout.SelectableLabel("Assets/" + displayPath, EditorStyles.textField, GUILayout.Height(22));
                if (GUILayout.Button("Select Dir", GUILayout.Width(100)))
                {
                    if (LastPath != null) realPath = LastPath;
                    realPath = EditorUtility.OpenFolderPanel("Where to chief?", realPath, "");
                    LastPath = realPath;
                    string temp = realPath.Remove(0, Application.dataPath.Length + 1);
                    if (string.IsNullOrEmpty(temp))
                    {
                        user.ShowNotification(new GUIContent("The selected directory was invalid."));
                        changed = true;
                        displayPath = null;
                        realPath = "";
                        assetPath = "";
                        directory = "";
                    }
                    else
                    {
                        if (displayPath != temp) changed = true;
                        displayPath = temp;
                        assetPath = "Assets/" + temp;

                        //just the final directory of the whole path
                        var s = realPath.Split('/');
                        if (s != null && s.Length > 1) directory = s[s.Length - 1];
                        else directory = realPath;
                    }
                }
            } EditorGUILayout.EndHorizontal();

            return changed;
        }
    }


    /// <summary>
    /// Helper class for previewing audio clips in an editor window/inspector
    /// </summary>
    public static class PublicAudioUtil
    {
        /// <summary>
        /// Plays the audio clip in-editor.
        /// 
        /// NOTE: This is using reflection to gain access to internal, undocumented
        /// classes that allow us to play sounds in-editor and as a result is 
        /// likely to break in future updates.
        /// </summary>
        /// <param name="clip"></param>
        public static void PlayClip(AudioClip clip)
        {
            Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
            Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            MethodInfo method = audioUtilClass.GetMethod(
                "PlayClip",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new System.Type[] {
                    typeof(AudioClip)
                },
                null
            );
            method.Invoke(
                null,
                new object[] {
                    clip
                }
            );
        }
    }


    /// <summary>
    /// Helper class from drawing shapes in the editor.
    /// </summary>
    public class GUIDraw
    {
        private static Texture2D _staticRectTexture;
        private static GUIStyle _staticRectStyle;

        /// <summary>
        /// Draws a filled rectangular shape. Only meant for use in OnGUI methods.
        /// </summary>
        /// <param name="position">Defines the size, shape, and position to draw the shape.</param>
        /// <param name="color">The color to fill the shape with.</param>
        public static void Rect(Rect position, Color fillColor)
        {
            if (_staticRectTexture == null)
            {
                _staticRectTexture = new Texture2D(1, 1);
            }

            if (_staticRectStyle == null)
            {
                _staticRectStyle = new GUIStyle();
            }

            if (fillColor != Color.clear)
            {
                _staticRectTexture.SetPixel(0, 0, fillColor);
                _staticRectTexture.Apply();
                _staticRectStyle.normal.background = _staticRectTexture;
            }

            UnityEngine.GUI.Box(position, GUIContent.none, _staticRectStyle);
        }
    }

}
