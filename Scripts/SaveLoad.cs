/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
//#define PGI_LITE
using UnityEngine;
using System.Xml;
using System.IO;

namespace PowerGridInventory
{
    /// <summary>
    /// Helper component that can be easily attached to unity events in the inspector and used
    /// to trigger saving and loading of PGIModels.
    /// </summary>
    public class SaveLoad : MonoBehaviour
    {
        public PGIModel Model;

#if !PGI_LITE
        /// <summary>
        /// Saves this component's PGIModel to the given file path.
        /// </summary>
        /// <param name="path"></param>
        public void Save(string path)
        {
            if (Model != null && !string.IsNullOrEmpty(path))
                Model.SaveModel(1).Save(path);
        }

        /// <summary>
        /// Saves this component's PGIModel to the give path relative to the persistent data path.
        /// </summary>
        /// <param name="subPath"></param>
        public void SaveToPersistentPath(string subPath)
        {
            if (Model != null && !string.IsNullOrEmpty(Application.persistentDataPath + "/" + subPath))
                Model.SaveModel(1).Save(Application.persistentDataPath+"/"+subPath);
        }

        /// <summary>
        /// Loads a PGIModel from a given file path and replaces this component's model with it.
        /// </summary>
        /// <param name="path"></param>
        public void Load(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                XmlDocument doc = new XmlDocument();
                doc.InnerXml = File.ReadAllText(path);
                PGIModel.LoadModel(doc.InnerXml, 1, ref Model);
            }
        }

        /// <summary>
        /// Loads a PGIModel from a given file path relative to the persistent data path 
        /// and replaces this component's model with it.
        /// </summary>
        /// <param name="path"></param>
        public void LoadFromPersistentPath(string path)
        {
            if (!string.IsNullOrEmpty(Application.persistentDataPath+"/"+path))
            {
                XmlDocument doc = new XmlDocument();
                doc.InnerXml = File.ReadAllText(Application.persistentDataPath+"/"+path);
                PGIModel.LoadModel(doc.InnerXml, 1, ref Model);
            }
        }
#endif
    }
}

