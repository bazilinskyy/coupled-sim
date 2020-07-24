using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PolyIk))]
public class PolyIkEditor : Editor
{
    static bool showRotationOffsets, showLeftFootIk, ShowRightFootIk, ShowLeftHandIk, ShowRightHandIk, ShowLookAtIk = false;
    public override void OnInspectorGUI()
    {
        PolyIk myPolyIk = (PolyIk)target;
        serializedObject.Update();

        SerializedProperty rotationOffsets = serializedObject.FindProperty("rotationOffsets");

        //Targets
        SerializedProperty leftFootTarget = serializedObject.FindProperty("leftFootTarget");
        SerializedProperty rightFootTarget = serializedObject.FindProperty("rightFootTarget");
        SerializedProperty LeftHandTarget = serializedObject.FindProperty("LeftHandTarget");
        SerializedProperty rightHandTarget = serializedObject.FindProperty("rightHandTarget");
        SerializedProperty lookAtTarget = serializedObject.FindProperty("lookAtTarget");

        //Pole Targets
        SerializedProperty leftFootPole = serializedObject.FindProperty("leftFootPole");
        SerializedProperty rightFootPole = serializedObject.FindProperty("rightFootPole");
        SerializedProperty LeftHandPole = serializedObject.FindProperty("LeftHandPole");
        SerializedProperty rightHandPole = serializedObject.FindProperty("rightHandPole");

        //Weights
        SerializedProperty rightHandWeight = serializedObject.FindProperty("rightHandWeight");
        SerializedProperty LookAtEyesWeight = serializedObject.FindProperty("LookAtEyesWeight");
        SerializedProperty lookAtHeadWeight = serializedObject.FindProperty("lookAtHeadWeight");
        SerializedProperty lookAtWeight = serializedObject.FindProperty("lookAtWeight");
        SerializedProperty leftHandWeight = serializedObject.FindProperty("leftHandWeight");
        SerializedProperty rightFootWeight = serializedObject.FindProperty("rightFootWeight");
        SerializedProperty leftFootWeight = serializedObject.FindProperty("leftFootWeight");

        //Rotation Weights
        SerializedProperty rotationWeightRightHand = serializedObject.FindProperty("rotationWeightRightHand");
        SerializedProperty rotationWeightLeftHand = serializedObject.FindProperty("rotationWeightLeftHand");
        SerializedProperty rotationWeightRightFoot = serializedObject.FindProperty("rotationWeightRightFoot");
        SerializedProperty rotationWeightLeftFoot = serializedObject.FindProperty("rotationWeightLeftFoot");

        //Bool
        SerializedProperty leftFootIk = serializedObject.FindProperty("leftFootIk");
        SerializedProperty lookAtIk = serializedObject.FindProperty("lookAtIk");
        SerializedProperty rightHandIk = serializedObject.FindProperty("rightHandIk");
        SerializedProperty leftHandIk = serializedObject.FindProperty("leftHandIk");
        SerializedProperty rightFootIk = serializedObject.FindProperty("rightFootIk");


        var mainTexture = Resources.Load<Texture2D>("IKLogo");

        //Main Image    	
        GUILayout.BeginHorizontal();
        GUILayout.Label(mainTexture);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Show Rotation Offsets"))
        {
            showRotationOffsets = !showRotationOffsets;
        }

        if (showRotationOffsets)
        {
            EditorGUILayout.PropertyField(rotationOffsets, true);
        }

        if (GUILayout.Button("Show Left Hand Ik"))
        {
            ShowLeftHandIk = !ShowLeftHandIk;
        }

        if (ShowLeftHandIk)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(LeftHandTarget);

            if (myPolyIk.LeftHandTarget == null)
            {
                if (GUILayout.Button("Create Target"))
                {
                    myPolyIk.LeftHandTarget = MakeTargetTransform(HumanBodyBones.LeftHand, myPolyIk.animator, null);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(LeftHandPole);

            if (myPolyIk.LeftHandPole == null)
            {
                if (GUILayout.Button("Create Target"))
                {
                    myPolyIk.LeftHandPole = MakeTargetTransform(HumanBodyBones.LeftLowerArm, myPolyIk.animator, myPolyIk.LeftHandTarget);
                }
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(leftHandIk);
            EditorGUILayout.PropertyField(leftHandWeight);
            EditorGUILayout.PropertyField(rotationWeightLeftHand);

        }

        if (GUILayout.Button("Show Right Hand Ik"))
        {
            ShowRightHandIk = !ShowRightHandIk;
        }

        if (ShowRightHandIk)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(rightHandTarget);

            if (myPolyIk.rightHandTarget == null)
            {
                if (GUILayout.Button("Create Target"))
                {
                    myPolyIk.rightHandTarget = MakeTargetTransform(HumanBodyBones.RightHand, myPolyIk.animator, null);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(rightHandPole);

            if (myPolyIk.rightHandPole == null)
            {
                if (GUILayout.Button("Create Target"))
                {
                    myPolyIk.rightHandPole = MakeTargetTransform(HumanBodyBones.RightLowerArm, myPolyIk.animator, myPolyIk.rightHandTarget);
                }
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(rightHandIk);
            EditorGUILayout.PropertyField(rightHandWeight);
            EditorGUILayout.PropertyField(rotationWeightRightHand);

        }

        if (GUILayout.Button("Show Right Foot Ik"))
        {
            ShowRightFootIk = !ShowRightFootIk;
        }

        if (ShowRightFootIk)
        {

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(rightFootTarget);

            if (myPolyIk.rightFootTarget == null)
            {
                if (GUILayout.Button("Create Target"))
                {
                    myPolyIk.rightFootTarget = MakeTargetTransform(HumanBodyBones.RightFoot, myPolyIk.animator, null);
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(rightFootPole);

            if (myPolyIk.rightFootPole == null)
            {
                if (GUILayout.Button("Create Target"))
                {
                    myPolyIk.rightFootPole = MakeTargetTransform(HumanBodyBones.RightLowerLeg, myPolyIk.animator, myPolyIk.rightFootTarget);
                }
            }

            GUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(rightFootIk);
            EditorGUILayout.PropertyField(rightFootWeight);
            EditorGUILayout.PropertyField(rotationWeightRightFoot);

        }

        if (GUILayout.Button("Show Left Foot Ik"))
        {
            showLeftFootIk = !showLeftFootIk;
        }

        if (showLeftFootIk)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(leftFootTarget);

            if (myPolyIk.leftFootTarget == null)
            {
                if (GUILayout.Button("Create Target"))
                {
                    myPolyIk.leftFootTarget = MakeTargetTransform(HumanBodyBones.LeftFoot, myPolyIk.animator, null);
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(leftFootPole);

            if (myPolyIk.leftFootPole == null)
            {
                if (GUILayout.Button("Create Target"))
                {
                    myPolyIk.leftFootPole = MakeTargetTransform(HumanBodyBones.LeftLowerLeg, myPolyIk.animator, myPolyIk.leftFootTarget);
                }
            }

            GUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(leftFootIk);
            EditorGUILayout.PropertyField(leftFootWeight);
            EditorGUILayout.PropertyField(rotationWeightLeftFoot);
        }

        if (GUILayout.Button("Show Look At Ik"))
        {
            ShowLookAtIk = !ShowLookAtIk;
        }

        if (ShowLookAtIk)
        {

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(lookAtTarget);

            if (myPolyIk.lookAtTarget == null)
            {
                if (GUILayout.Button("Create Target"))
                {
                    myPolyIk.lookAtTarget = MakeTargetTransform(HumanBodyBones.Head, myPolyIk.animator, null);
                }
            }

            GUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(lookAtIk);
            EditorGUILayout.PropertyField(lookAtWeight);
            EditorGUILayout.PropertyField(lookAtHeadWeight);
            EditorGUILayout.PropertyField(LookAtEyesWeight);
        }

        serializedObject.ApplyModifiedProperties();


    }

    Transform MakeTargetTransform(HumanBodyBones bone, Animator anim, Transform parent)
    {
        var target = new GameObject(bone.ToString());
        target.transform.position = anim.GetBoneTransform(bone).position;
        target.transform.rotation = anim.GetBoneTransform(bone).rotation;

        if (parent != null)
        {
            target.transform.parent = parent;
        }

        else
        {
            target.transform.parent = anim.transform;
        }

        return target.transform;
    }
}


