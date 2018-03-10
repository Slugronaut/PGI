using UnityEditor;
using UnityEngine;

namespace PowerGridInventory.PGIEditor
{
    /// <summary>
    /// 
    /// </summary>
    [CustomEditor(typeof(AbstractBaseModelEventHandler), true)]
    public class ModelEventHandlerEditor : UnityEditor.Editor
    {
        AbstractBaseModelEventHandler Handler;
        PGIModel Model;

        public void OnEnable()
        {
            Handler = target as AbstractBaseModelEventHandler;
            Model = Handler.GetComponent<PGIModel>();
        }

        public override void OnInspectorGUI()
        {
            var type = Handler.GetType();
            //techinally, since the class is defined as the second class within a monobehaviour script file, we shouldn't
            //have to worry about users accidentally attaching like this, but just in case... poop a warning at them.
            if(type == typeof(AbstractBaseModelEventHandler))// && !type.IsSubclassOf(typeof(AbstractModelEventHandler)))
            {
                EditorGUILayout.HelpBox("AbstractModelEventHandler is not used like this. Instead, derive your custom handler from it. It is meant to be treated as an abstract class but cannot actually be defined as such due to limitation in Unity's inspector.", MessageType.Error);
                return;
            }

            GUILayout.Space(10);

            if (Handler == null) return;

            Handler.Type = (AbstractBaseModelEventHandler.CellType)EditorGUILayout.EnumPopup(new GUIContent("Cell Type", "The type of cell being handled."), Handler.Type);
            if (Handler.Type == AbstractBaseModelEventHandler.CellType.Equipment)
            {
                if (Model.EquipmentCellsCount > 0)
                    Handler.EquipmentIndex = EditorGUILayout.IntSlider("Equipment Index", Handler.EquipmentIndex, 0, Model.EquipmentCellsCount-1);
                else
                {
                    Handler.EquipmentIndex = -1;
                    EditorGUILayout.HelpBox("This model has no equipment cells.", MessageType.Warning);
                }
            }
            else
            {
                if(Model.GridCellsX > 0 && Model.GridCellsY > 0)
                {
                    Handler.GridX = EditorGUILayout.IntSlider("Grid X", Handler.GridX, 0, Model.GridCellsX-1);
                    Handler.GridY = EditorGUILayout.IntSlider("Grid Y", Handler.GridY, 0, Model.GridCellsY-1);
                }
                else
                {
                    Handler.GridX = -1;
                    Handler.GridY = -1;
                    EditorGUILayout.HelpBox("This model has no grid cells.", MessageType.Warning);
                }
            }
            GUILayout.Space(10);
            this.DrawDefaultInspector();
        }
    }
}
