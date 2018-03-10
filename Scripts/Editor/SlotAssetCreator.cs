using PowerGridInventory.Utility;
using Toolbox.Graphics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;


namespace PowerGridInventory.PGIEditor
{
    public class SlotAssetCreator
    {

        [UnityEditor.MenuItem("Assets/Create/PGI/Create Slot Prefab")]
        static void CreateSlotAsset()
        {
            var path = GenerateUniqueAssetPath("Slot Prefab.prefab");
            GameObject go = new GameObject("Plot Prefab");
            go.AddComponent<RectTransform>();
            go.AddComponent<CanvasRenderer>();
            var image = go.AddComponent<Image>();
            var slot = go.AddComponent<PGISlot>();

            GameObject hilight = new GameObject("Hilight");
            hilight.transform.SetParent(go.transform);
            hilight.AddComponent<RectTransform>();
            hilight.AddComponent<CanvasRenderer>();
            hilight.AddComponent<IgnoreUIRaycasts>();
            var hilightImage = hilight.AddComponent<Image>();
            image.raycastTarget = false;

            GameObject icon = new GameObject("Icon");
            icon.transform.SetParent(go.transform);
            icon.AddComponent<RectTransform>();
            icon.AddComponent<CanvasRenderer>();
            icon.AddComponent<IgnoreUIRaycasts>();
            var iconImage = icon.AddComponent<Image>();
            icon.AddComponent<UIRotate>();
            icon.AddComponent<UIRotate>();
            image.raycastTarget = false;

            GameObject icon3d = new GameObject("Icon3D");
            icon3d.transform.SetParent(go.transform);
            icon3d.AddComponent<IgnoreUIRaycasts>();
            var iconImage3d = icon3d.AddComponent<Image3D>();
            icon3d.AddComponent<UIRotate>();

            GameObject stack = new GameObject("Stack");
            stack.transform.SetParent(go.transform);
            stack.AddComponent<RectTransform>();
            stack.AddComponent<CanvasRenderer>();
            stack.AddComponent<IgnoreUIRaycasts>();
            var stackText = stack.AddComponent<Text>();
            stackText.raycastTarget = false;

            slot.Highlight = hilightImage;
            slot.IconImage = iconImage;
            slot.IconImage3D = iconImage3d;
            slot.StackSize = stackText;

            var prefab = PrefabUtility.CreatePrefab(path, go);
            AssetDatabase.SaveAssets();
            GameObject.DestroyImmediate(go);
            UnityEditor.ProjectWindowUtil.ShowCreatedAsset(prefab);
        }

        public static string GenerateUniqueAssetPath(string fileName)
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path)) path = "Assets";
            if (!string.IsNullOrEmpty(System.IO.Path.GetExtension(path)))
                path = path.Replace(System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), string.Empty);
            return AssetDatabase.GenerateUniqueAssetPath(path + "/" + fileName);
        }
    }
}
