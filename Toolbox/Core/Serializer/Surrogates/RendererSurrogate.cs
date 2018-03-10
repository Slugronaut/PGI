/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using System.Runtime.Serialization;
using System.Reflection;

namespace Pantagruel.Serializer.Surrogate
{
    /// <summary>
    /// Handles serialization of components derived from Renderer. The following
    /// are currently support by this:
    /// BillboardRenderer, LineRenderer, MeshRenderer, ParticleRenderer, ParticleSystemRenderer,
    /// SkinnedMeshRenderer, SpriteRenderer, Trailrenderer, and Renderer.
    /// CanvaseRenderer is not derived from Renderer so it is not supported by this surrogate.
    /// </summary>
    public class RendererSurrogate : UnityEngineObjectSurrogate
    {
        public override void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            //we cannot access 'material' or 'materials' - ever. Even reading
            //from them will cause them in instantiate a new copy and we will 
            //lose our shared reference. Luckily, 'sharedMaterial' and 'sharedmaterials'
            //will work just fine for our purposes.
            GatherFieldsAndProps(obj, info, context, "material", "materials");
        }

        public override object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            /*
            //TODO: We need a way to codify a Material resource to it's Resource path
            //so that it can be dynamically loaded at runtime!
            string matPath = (string)info.GetValue("name", typeof(string));
            int index = matPath.LastIndexOf(" (Instance)");
            if(index >= 0) matPath = matPath.Remove(index);

            //WARNING: This will only work if the material is at the root of a reasources folder
            Material mat = Resources.Load(matPath, typeof(Material)) as Material;
            if(SerializerBase.IsReferenceNull(mat))
            {
                Debug.LogError("The material '" + matPath + "' could not be located at runtime deserialization. Please be sure it exists and has been added to the Resources folder of your project workspace.");
            }

            //we already set the name when we loaded the resource. Now we
            //need to filter out setting the field for it or bad things will occur.
            ReplaceState(mat, info, "name");
            return mat;
            */
            ReplaceState(obj, info);
            return obj;
        }
    }
}
