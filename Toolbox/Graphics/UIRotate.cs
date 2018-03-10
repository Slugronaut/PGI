/**********************************************
* Ancient Craft Games
* Copyright 2015-2016 James Clark
**********************************************/
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace Toolbox.Graphics
{
    /// <summary>
    /// Provides an extension to UI.Graphic elements that allows them to
    /// be rotated at the vertex level.
    /// 
    /// TODO: Enable scaling
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UIRotate : BaseMeshEffect
    {
        /// <summary>
        /// The angle at which the UI.Graphic will be displayed.
        /// </summary>
        [Tooltip("The angle in degrees at which the UI.Graphic will be displayed.")]
        public Vector3 EulerAngles;

        /// <summary>
        /// The scale to apply to the UI.Graphic's verticies.
        /// </summary>
        [Tooltip("The scale to apply to the UI.Graphics's verticies.")]
        public Vector3 Scale = Vector3.one;

        /// <summary>
        /// If true, the verticies of the image will be re-scaled to fit within the
        /// bounds of the UI.Image's RectTransform. Otherwise they will remain the same size.
        /// </summary>
        [Tooltip("If true, the verticies of the image will be re-scaled to fit within the bounds of the UI.Image's RectTransform. Otherwise they will remain the same size.")]
        public bool ScaleToFit = true;


        Graphic UIGraphic;
        Image UIImage;
        Image3D UIImage3D;

        
        /// <summary>
        /// Unity constructor. Caches some commonly used values.
        /// This *must* be called if overridden.
        /// </summary>
        protected override void Start()
        {
            base.Start();
            UIGraphic = GetComponent<Graphic>();
            UIImage = GetComponent<Image>();
            UIImage3D = GetComponent<Image3D>();
        }

        /// <summary>
        /// Helper method to rotate the verts of a raw mesh.
        /// </summary>
        /// <param name="verts"></param>
        /// <param name="size"></param>
        public static void RotateVerts(List<UIVertex> verts, Vector3 angles)
        {
            Quaternion rotation = Quaternion.Euler(angles);

            for (int i = 0; i < verts.Count; i++)
            {
                var uiVertex = verts[i];
                uiVertex.position = rotation * verts[i].position;
                verts[i] = uiVertex;
            }

        }

        /// <summary>
        /// Helper method to rotate the verts of a raw mesh. The rotated verts,
        /// normal, and the new resulting AABB size will be returned.
        /// </summary>
        /// <param name="verts">The raw vertices to be rotated. These will be mutated by this method.</param>
        /// <param name="angles">The angle in degrees to rotate the vertices.</param>
        /// <param name="aabb">The resulting axis-aligned bounding box of the vertices after rotation.</param>
        public static void RotateVerts(List<UIVertex> verts, Vector3 angles, out Bounds aabb)
        {
            Quaternion rotation = Quaternion.Euler(angles);
            float minX = 0.0f, maxX = 0.0f, minY = 0.0f, maxY = 0.0f, minZ = 0.0f, maxZ = 0.0f;

            for (int i = 0; i < verts.Count; i++)
            {
                //transform vert
                var uiVertex = verts[i];
                uiVertex.position = rotation * verts[i].position;
                verts[i] = uiVertex;

                //determine bounds
                if (uiVertex.position.x < minX) minX = uiVertex.position.x;
                if (uiVertex.position.y < minY) minY = uiVertex.position.y;
                if (uiVertex.position.x < minZ) minZ = uiVertex.position.z;

                if (uiVertex.position.x > maxX) maxX = uiVertex.position.x;
                if (uiVertex.position.y > maxY) maxY = uiVertex.position.y;
                if (uiVertex.position.y > maxZ) maxZ = uiVertex.position.z;
            }

            aabb = new Bounds();
            aabb.SetMinMax(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
        }

        /// <summary>
        /// Modifies the verticies of the UI element by scaling them.
        /// </summary>
        /// <param name="verts">The raw verticies to be scaled. These will be mutated by this method.</param>
        /// <param name="scale">The amout to scale the verticies in all three dimensions.</param>
        public static void ScaleVerts(List<UIVertex> verts, Vector3 scale)
        {
            for (int i = 0; i < verts.Count; i++)
            {
                var uiVertex = verts[i];
                uiVertex.position.Scale(scale);
                verts[i] = uiVertex;
            }
        }

        /// <summary>
        /// Modifies the vertices of the UI element by applying a rotation and scaling.
        /// </summary>
        /// <param name="vh"></param>
        public override void ModifyMesh(VertexHelper vh)
        {
            List<UIVertex> verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);

            //Graphic gr = GetComponent<Graphic>();
            Bounds rotMeshBounds;
            RotateVerts(verts, EulerAngles, out rotMeshBounds);

            //Make sure verts fit within bounds
            Vector3 scale = Vector3.one;
            if (UIGraphic != null)
            {
                float scaleX = Scale.x * (UIGraphic.rectTransform.rect.size.x / rotMeshBounds.size.x);
                float scaleY = Scale.y * (UIGraphic.rectTransform.rect.size.y / rotMeshBounds.size.y);
                float scaleZ = Scale.z * ((rotMeshBounds.size.z <= 0.0f) ? 1.0f : rotMeshBounds.size.z);
                //preserve aspect ratio if flag is set on any graphic that supports it
                if (UIImage != null)
                {
                    if (UIImage.preserveAspect) scale *= Mathf.Min(scaleX, scaleY);
                    else scale = new Vector3(scaleX, scaleY, scaleZ);
                }
                else if (UIImage3D != null)
                {
                    if (UIImage3D.PreserveAspect) scale *= Mathf.Min(scaleX, scaleY);
                    else scale = new Vector3(scaleX, scaleY, scaleZ);
                }
                else
                {
                    scale = new Vector3(scaleX, scaleY, scaleZ);
                }
            }

            ScaleVerts(verts, scale);

            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }
    }
}