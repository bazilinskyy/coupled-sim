// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Varjo
{
    /// <summary>
	/// Camera of a single view.
	/// </summary>
	public class VarjoViewCamera : MonoBehaviour
    {
        /// <summary>
        /// Components that will be ignored when copying the components from the VarjoCamera while creating the view cameras.
        /// </summary>
        private static Type[] ignoredCameraCopyComponents = {
            typeof(Transform),
            typeof(Camera),
            typeof(FlareLayer),
            typeof(AudioListener),
#if !UNITY_2018_2_OR_NEWER
			typeof(GUILayer),
#endif
		};

        [Serializable]
        public enum CAMERA_ID
        {
            CONTEXT_LEFT = 0,
            CONTEXT_RIGHT = 1,
            FOCUS_LEFT = 2,
            FOCUS_RIGHT = 3
        };

        [SerializeField]
        private CAMERA_ID _cameraId;
        public CAMERA_ID CameraId
        {
            get { return _cameraId; }
            private set { _cameraId = value; }
        }

        [HideInInspector]
        public Camera cam;

        // The projection and view matrices we're supposed to submit to the compositor
        private double[] m_SubmissionProjMatrix = new double[16];
        private double[] m_SubmissionViewMatrix = new double[16];

        // The layer that owns this camera
        private VarjoLayer m_Owner;

        private Mesh m_OcclusionMesh = null;
        private float[] m_OcclusionMeshVerts = null;

        private bool VerifyCamera()
        {
            if (cam == null)
            {
                cam = GetComponent<Camera>();
            }
            if (cam == null)
            {
                cam = gameObject.AddComponent<Camera>();
            }
            if (m_Owner == null)
                m_Owner = GetComponentInParent<VarjoLayer>();

            if (cam && m_Owner && m_Owner.useOcclusionMesh && (m_OcclusionMeshVerts == null))
            {
                m_OcclusionMesh = null;
                m_OcclusionMeshVerts = VarjoPlugin.GetOcclusionMesh((int)CameraId);

                if (m_OcclusionMeshVerts.Length > 0)
                {
                    m_OcclusionMesh = new Mesh();
                    int vertCount = m_OcclusionMeshVerts.Length / 2;
                    var vertices = new Vector3[vertCount];
                    var indices = new int[vertices.Length];
                    for (int i = 0; i < vertices.Length; ++i)
                    {
                        vertices[i] = new Vector3(m_OcclusionMeshVerts[i * 2], m_OcclusionMeshVerts[i * 2 + 1], 0.0f);
                        indices[i] = i;
                    }

                    m_OcclusionMesh.vertices = vertices;
                    m_OcclusionMesh.triangles = indices;

                }
            }

            return cam != null;
        }

        public void SetupCamera(CAMERA_ID cameraId, Camera sourceCamera, bool copyCameraComponents, VarjoLayer owner)
        {
            VerifyCamera();

            CameraId = cameraId;
            m_Owner = owner;
            cam.CopyFrom(sourceCamera);

            if (copyCameraComponents)
            {
                CopyCameraComponents(sourceCamera);
            }
        }

        private void CopyCameraComponents(Camera sourceCamera)
        {
            // Copy all components from source camera, uses reflection
            foreach (var sourceComponent in sourceCamera.GetComponents<MonoBehaviour>())
            {
                Type componentType = sourceComponent.GetType();
                if (IsComponentValidToCopy(componentType))
                {
                    MonoBehaviour newComponent = (MonoBehaviour)gameObject.AddComponent(componentType);

                    // Does not copy private fields, as generally these are not serialized. Note that copying isn't fool proof, as the components
                    // can handle fields in various ways, and even though something is serialized, it necessarily shouldn't be copied.
                    BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Default | BindingFlags.DeclaredOnly;

                    PropertyInfo[] properties = componentType.GetProperties(flags);
                    foreach (PropertyInfo property in properties)
                    {
                        if (property.CanWrite)
                        {
                            try
                            {
                                property.SetValue(newComponent, property.GetValue(sourceComponent, null), null);
                            }
                            catch { }
                        }
                    }

                    FieldInfo[] fields = componentType.GetFields(flags);
                    foreach (FieldInfo field in fields)
                    {
                        field.SetValue(newComponent, field.GetValue(sourceComponent));
                    }

                    newComponent.enabled = sourceComponent.enabled;
                }
            }
        }

        private bool IsComponentValidToCopy(Type componentType)
        {
            for (int i = 0; i < ignoredCameraCopyComponents.Length; ++i)
            {
                if (ignoredCameraCopyComponents[i] == componentType)
                    return false;
            }
            return true;
        }

        public void UpdateFromLayer()
        {
            VerifyCamera();

            VarjoPlugin.PoseType pose = (CameraId == CAMERA_ID.CONTEXT_LEFT || CameraId == CAMERA_ID.FOCUS_LEFT) ? VarjoPlugin.PoseType.LEFT_EYE : VarjoPlugin.PoseType.RIGHT_EYE;

            VarjoPlugin.Matrix centerPoseVRJ, eyePoseVRJ;
            VarjoManager.Instance.GetPose(VarjoPlugin.PoseType.CENTER, out centerPoseVRJ);
            VarjoManager.Instance.GetPose(pose, out eyePoseVRJ);

            var centerPose = VarjoMatrixUtils.WorldMatrixToUnity(centerPoseVRJ.value);
            var eyePose = VarjoMatrixUtils.WorldMatrixToUnity(eyePoseVRJ.value);

            var centerPoseInv = centerPose.inverse;

            var worldMatrix = centerPoseInv * eyePose;

            // TODO: for the first few frames we can receive invalid data which causes annoying debug log output.
            // Let's just do a quick and dirty validation and skip the buffer info if the data doesn't seem to be correct.
            for (int i = 0; i < 16; i++)
            {
                if (float.IsNaN(worldMatrix[i]))
                {
                    Debug.Log("Received invalid data.");
                    return;
                }
            }

            transform.localPosition = worldMatrix.GetColumn(3);
            transform.localRotation = Quaternion.LookRotation(worldMatrix.GetColumn(2), worldMatrix.GetColumn(1));

            var viewInfo = VarjoManager.Instance.GetViewInfo((int)CameraId);

            viewInfo.projectionMatrix.CopyTo(m_SubmissionProjMatrix, 0);
            viewInfo.invViewMatrix.CopyTo(m_SubmissionViewMatrix, 0);

            // Plug the near and far clip values to the submission projection matrix as well, for depth reconstruction
            float near = cam.nearClipPlane;
            float far = cam.farClipPlane;

            m_SubmissionProjMatrix[2 * 4 + 2] = -far / (near - far) - 1;
            m_SubmissionProjMatrix[3 * 4 + 2] = -(far * near) / (near - far);
            cam.usePhysicalProperties = true;

            var projMat = VarjoMatrixUtils.ProjectionMatrixToUnity(viewInfo.projectionMatrix, near, far);

            float aspect = projMat.m11 / projMat.m00;
            cam.sensorSize = new Vector2(cam.sensorSize.x, cam.sensorSize.x / aspect);
            // Calculate FOV and shift
            float t = projMat.m11;
            const float Rad2Deg = 180.0f / (float)Math.PI;
            float fov = ((float)Math.Atan(1.0f / t)) * 2.0f * Rad2Deg;

            if (m_Owner.flipY)
                projMat.m12 *= -1.0f;

            float lensX = projMat.m02 / 2.0f;
            float lensY = projMat.m12 / 2.0f;

            cam.fieldOfView = fov;
            cam.lensShift = new Vector2(lensX, lensY);

#if UNITY_2018_3_OR_NEWER
            cam.gateFit = Camera.GateFitMode.None;
#else
            // Pre-2018.3, we couldn't get exact 1:1 match with the projection matrix by tweaking camera parameters, so override the matrix directly
            cam.projectionMatrix = projMat;
#endif

            if (m_Owner.useOcclusionMesh && (m_OcclusionMesh != null))
            {
                // Draw the occlusion mesh; move it to the camera near plane so that it won't get frustum culled.
                Matrix4x4 occMeshMtx = new Matrix4x4();
                occMeshMtx.SetTRS(new Vector3(0.0f, 0.0f, cam.nearClipPlane), Quaternion.identity, Vector3.one);
                occMeshMtx = transform.localToWorldMatrix * occMeshMtx;
                Graphics.DrawMesh(m_OcclusionMesh, occMeshMtx, m_Owner.occlusionMaterial, 0, cam);
            }

        }

        public double[] GetProjectionMatrixForSubmission() { return m_SubmissionProjMatrix; }
        public double[] GetViewMatrixForSubmission() { return m_SubmissionViewMatrix; }

        private void LateUpdate()
        {
            if (!VarjoPlugin.SessionValid)
                return;

            VerifyCamera();

            if (cam.targetTexture == null)
            {
                var rt = m_Owner.GetRenderTextureForCamera(CameraId);
                if (rt != null)
                {
                    rt.name = gameObject.name;
                    cam.targetTexture = rt;
                }
            }

            float factor = m_Owner.contextDisplayFactor;
            if (CameraId == CAMERA_ID.FOCUS_LEFT || CameraId == CAMERA_ID.FOCUS_RIGHT)
            {
                factor = m_Owner.focusDisplayFactor;
            }

            cam.rect = new Rect(0, 0, factor, factor);
        }

        public Camera GetCamera()
        {
            return cam;
        }
    }
}