// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Varjo;
using UnityEngine.VR;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

namespace Varjo
{
    [CustomEditor(typeof(VarjoLayer), true)]
    public class VarjoLayerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            VarjoLayer varjoLayer = (VarjoLayer)target;

            if (!PlayerSettings.virtualRealitySupported && (target is VarjoManager) && !(((VarjoManager)target).forceVRSupport))
            {
                EditorGUILayout.HelpBox("ERROR: Unity VR support is not enabled.\nPlease select 'Force VR Support' or enable VR support in:\nProject Settings | Player ", MessageType.Error);
            }

            if (QualitySettings.vSyncCount > 0 && (target is VarjoManager) && !(((VarjoManager)target).disableVSync))
            {
                EditorGUILayout.HelpBox("Warning: VSync is enabled.\nPlease select 'Disable V Sync' or disable VSync in:\nProject Settings | Quality", MessageType.Warning);
            }

            if (varjoLayer.varjoCamera == null)
            {
                EditorGUILayout.HelpBox("No main camera assigned. Varjo manager will try to use camera tagged as Main Camera.", MessageType.Info);
            }

            if(varjoLayer.submitDepth)
            {
                if (QualitySettings.antiAliasing > 1 && varjoLayer.varjoCamera.allowMSAA)
                {
                    EditorGUILayout.HelpBox("Warning: Depth submission is not supported when anti-aliasing is used.", MessageType.Warning);
                }

                if(GraphicsSettings.renderPipelineAsset != null)
                {
                    EditorGUILayout.HelpBox("Warning: Depth submission does not support scriptable render pipelines.", MessageType.Warning);
                }
            }

            DrawDefaultInspector();

			EditorGUILayout.Separator();



            if (!Application.isPlaying)
            {
				EditorGUI.indentLevel++;
                if (varjoLayer.viewportCameras != null && varjoLayer.viewportCameras.Count > 0)
                {
                    if (Button("Hide viewport cameras"))
                    {
                        DeleteCameras(varjoLayer);
                        EditorUtility.SetDirty(varjoLayer);
                        EditorUtility.SetDirty(varjoLayer.gameObject);
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    }

                    if (Button("Regenerate viewport cameras"))
                    {
                        DeleteCameras(varjoLayer);
                        EditorUtility.SetDirty(varjoLayer);
                        EditorUtility.SetDirty(varjoLayer.gameObject);
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                        varjoLayer.CreateCameras();
                        EditorUtility.SetDirty(varjoLayer);
                        EditorUtility.SetDirty(varjoLayer.gameObject);
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    }
                }
                else
                {
                    if (Button("Generate viewport cameras"))
                    {
                        if (varjoLayer.varjoCamera == null)
                        {
                            Debug.LogError("Can't enable Advanced Mode when Varjo Layer Main Camera is not set!");
                        }
                        else
                        {
                            varjoLayer.CreateCameras();
                            EditorUtility.SetDirty(varjoLayer);
                            EditorUtility.SetDirty(varjoLayer.gameObject);
                            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                        }
                    }
                }
				EditorGUI.indentLevel--;

			}

			EditorGUILayout.Space();
            //varjoManager.debug = EditorGUILayout.Toggle("Debug", varjoManager.debug);
        }

		bool Button(string txt)
		{
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			bool res = GUILayout.Button(txt, GUILayout.Width(256));
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			return res;
		}

        void DeleteCameras(VarjoLayer varjoLayer)
        {
            for (int i = 0; i < varjoLayer.viewportCameras.Count; ++i)
            {
                if (varjoLayer.viewportCameras[i] != null)
                {
                    DestroyImmediate(varjoLayer.viewportCameras[i].gameObject);
                }
            }
            varjoLayer.viewportCameras.Clear();
        }
    }
}
