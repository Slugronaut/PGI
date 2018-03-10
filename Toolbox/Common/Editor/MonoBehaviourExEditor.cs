/**********************************************
* Ancient Craft Games
* Copyright 2014-2017 James Clark
**********************************************/

namespace Toolbox.ToolboxEditor
{
    /// <summary>
    /// Provides all the benifits of the AbstractSuper editor to any
    /// behaviour that derived from MonoBehaviourEx.
    /// </summary>
    [UnityEditor.CustomEditor(typeof(MonoBehaviourEx), true)]
    public class MonoBehaviourExEditor : AbstractSuperEditor { }
}