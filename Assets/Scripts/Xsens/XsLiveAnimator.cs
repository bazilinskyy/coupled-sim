///<summary>
/// XsLiveAnimator is a component to bind Xsens MVN Studio stream to Unity3D Game Engine.
/// MVN Studio capable to stream 4 actors at the same time and this component makes the 
/// connections between those actors and the characters in Unity3D.
/// 
/// Using the same settings on different characters will result of multiple characters are playing the same animation.
/// 
/// Relocation of the animation based on the start position of the character.
/// 
/// This component uses Macanim own animator to bind the bones with MVN avatar and the real model in Unity3D.
/// 
/// The animation applied on the pelvis as global position and rotation, while only the local rotation applied on each segments.
///</summary>
/// <version>
/// 1.0, 2013.04.11 by Attila Odry
/// </version>
///<remarks>
/// Copyright (c) 2013, Xsens Technologies B.V.
/// All rights reserved.
/// 
/// Redistribution and use in source and binary forms, with or without modification,
/// are permitted provided that the following conditions are met:
/// 
/// 	- Redistributions of source code must retain the above copyright notice, 
///		  this list of conditions and the following disclaimer.
/// 	- Redistributions in binary form must reproduce the above copyright notice, 
/// 	  this list of conditions and the following disclaimer in the documentation 
/// 	  and/or other materials provided with the distribution.
/// 
/// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" 
/// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
/// AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS
/// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES 
/// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, 
/// OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
/// STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, 
/// EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
///</remarks>

//todo turn on this feature once we send T-Pose in the first frame
//#define TPOSE_FIRST

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace xsens
{

    /// <summary>
    /// Xsens Live Animator.
    /// 
    /// This class provide the logic to read an actor pose from MVN Stream and 
    /// retarget the animation to a character.
    /// </summary>
    /// <remarks>
    /// Attach this component to the character and bind the MvnActors with this object.
    /// </remarks>
    public class XsLiveAnimator : MonoBehaviour
    {
        public XsStreamReader mvnActors;			//network streamer, which contains all 4 actors' poses
        public int actorID = 1;                     //current actor ID, where 1 is the first streamed character from MVN

        public bool applyRootMotion = true;         //if true, position will be applied to the root (pelvis)

        public XsProp prop1;
        public XsProp prop2;
        public XsProp prop3;
        public XsProp prop4;
        private XsProp[] props;
        private XsPropManager[] targetProps;
        private GameObject[] currentProps;

        private Transform mvnActor; 				//reference for MVN Actor. This has the same layout as the streamed pose.
        private Transform target;                   //Reference to the character in Unity3D.
        private Transform origPos;					//original position of the animation, this is the zero
        private Transform[] targetModel;			//Model holds each segments
        private Transform[] currentPose;			//animation applyed on skeleton. global position and rotation, used to represent a pose	
        private Quaternion[] modelRotTP;			//T-Pose reference rotation on each segment
        private Vector3[] modelPosTP;				//T-Pose reference position on each segment
        private GameObject missingSegments;			//Empty object for not used segments
        private Animator animator;					//Animator object to get the Humanoid character mapping correct.
        private Dictionary<XsBodyAnimationSegment, HumanBodyBones> bodyMecanimBones;
        private Dictionary<XsFingerAnimationSegment, HumanBodyBones> fingerMechanimBones;
        private bool isInited;						//flag to check if the plugin was correctly intialized.
        private int segmentCount = 0;               //used to figure out the total segment count provided by the data
        private bool fingerTrackingEnabled;         //toggles setting up the finger transforms 
        private int propDataSize = 0;                  //used to offset the index of incoming data since props sit between body segments and finger segments

#if TPOSE_FIRST		
        private bool isFirstPose;					//check if the first pose is passed
#endif		
        private bool isDebugFrame = false;			//debug animation skeleton

        /// <summary>
        /// Contains the segment numbers for the body animation
        /// </summary>
        public enum XsBodyAnimationSegment
        {
            Pelvis = 0,

            L5 = 1,//not used
            L3 = 2,//spine
            T12 = 3,//not used
            T8 = 4,//chest

            Neck = 5,
            Head = 6,

            RightShoulder = 7,
            RightUpperArm = 8,
            RightLowerArm = 9,
            RightHand = 10,

            LeftShoulder = 11,
            LeftUpperArm = 12,
            LeftLowerArm = 13,
            LeftHand = 14,

            RightUpperLeg = 15,
            RightLowerLeg = 16,
            RightFoot = 17,
            RightToe = 18,

            LeftUpperLeg = 19,
            LeftLowerLeg = 20,
            LeftFoot = 21,
            LeftToe = 22
        }

        /// <summary>
        /// Contains the segment numbers for the finger animation
        /// </summary>
        public enum XsFingerAnimationSegment
        {
            LeftCarpus = 0,

            LeftFirstMetacarpal = 1,
            LeftFirstProximalPhalange = 2,
            LeftFirstDistalPhalange = 3,

            LeftSecondMetacarpal = 4, //not used
            LeftSecondProximalPhalange = 5,
            LeftSecondMiddlePhalange = 6,
            LeftSecondDistalPhalange = 7,

            LeftThirdMetacarpal = 8, //not used
            LeftThirdProximalPhalange = 9,
            LeftThirdMiddlePhalange = 10,
            LeftThirdDistalPhalange = 11,

            LeftFourthMetacarpal = 12, //not used
            LeftFourthProximalPhalange = 13,
            LeftFourthMiddlePhalange = 14,
            LeftFourthDistalPhalange = 15,

            LeftFifthMetacarpal = 16, //not used
            LeftFifthProximalPhalange = 17,
            LeftFifthMiddlePhalange = 18,
            LeftFifthDistalPhalange = 19,

            RightCarpus = 20,

            RightFirstMetacarpal = 21,
            RightFirstProximalPhalange = 22,
            RightFirstDistalPhalange = 23,

            RightSecondMetacarpal = 24, //not used
            RightSecondProximalPhalange = 25,
            RightSecondMiddlePhalange = 26,
            RightSecondDistalPhalange = 27,

            RightThirdMetacarpal = 28, //not used
            RightThirdProximalPhalange = 29,
            RightThirdMiddlePhalange = 30,
            RightThirdDistalPhalange = 31,

            RightFourthMetacarpal = 32, //not used
            RightFourthProximalPhalange = 33,
            RightFourthMiddlePhalange = 34,
            RightFourthDistalPhalange = 35,

            RightFifthMetacarpal = 36, //not used
            RightFifthProximalPhalange = 37,
            RightFifthMiddlePhalange = 38,
            RightFifthDistalPhalange = 39

        }

        /// <summary>
        /// The body segment order.
        /// </summary>
        int[] bodySegmentOrder =
        {
                    (int)XsBodyAnimationSegment.Pelvis,

                    (int)XsBodyAnimationSegment.L5,
                    (int)XsBodyAnimationSegment.L3,
                    (int)XsBodyAnimationSegment.T12,
                    (int)XsBodyAnimationSegment.T8,

                    (int)XsBodyAnimationSegment.Neck,
                    (int)XsBodyAnimationSegment.Head,

                    (int)XsBodyAnimationSegment.RightShoulder,
                    (int)XsBodyAnimationSegment.RightUpperArm,
                    (int)XsBodyAnimationSegment.RightLowerArm,
                    (int)XsBodyAnimationSegment.RightHand,

                    (int)XsBodyAnimationSegment.LeftShoulder,
                    (int)XsBodyAnimationSegment.LeftUpperArm,
                    (int)XsBodyAnimationSegment.LeftLowerArm,
                    (int)XsBodyAnimationSegment.LeftHand,

                    (int)XsBodyAnimationSegment.RightUpperLeg,
                    (int)XsBodyAnimationSegment.RightLowerLeg,
                    (int)XsBodyAnimationSegment.RightFoot,
                    (int)XsBodyAnimationSegment.RightToe,

                    (int)XsBodyAnimationSegment.LeftUpperLeg,
                    (int)XsBodyAnimationSegment.LeftLowerLeg,
                    (int)XsBodyAnimationSegment.LeftFoot,
                    (int)XsBodyAnimationSegment.LeftToe
        };

        /// <summary>
        /// The finger segment order.
        /// </summary>
        int[] fingerSegmentOrder =
        {
            (int)XsFingerAnimationSegment.LeftCarpus,

            (int)XsFingerAnimationSegment.LeftFirstMetacarpal,
            (int)XsFingerAnimationSegment.LeftFirstProximalPhalange,
            (int)XsFingerAnimationSegment.LeftFirstDistalPhalange,

            (int)XsFingerAnimationSegment.LeftSecondMetacarpal, //not used
            (int)XsFingerAnimationSegment.LeftSecondProximalPhalange,
            (int)XsFingerAnimationSegment.LeftSecondMiddlePhalange,
            (int)XsFingerAnimationSegment.LeftSecondDistalPhalange,

            (int)XsFingerAnimationSegment.LeftThirdMetacarpal, //not used
            (int)XsFingerAnimationSegment.LeftThirdProximalPhalange,
            (int)XsFingerAnimationSegment.LeftThirdMiddlePhalange,
            (int)XsFingerAnimationSegment.LeftThirdDistalPhalange,

            (int)XsFingerAnimationSegment.LeftFourthMetacarpal, //not used
            (int)XsFingerAnimationSegment.LeftFourthProximalPhalange,
            (int)XsFingerAnimationSegment.LeftFourthMiddlePhalange,
            (int)XsFingerAnimationSegment.LeftFourthDistalPhalange,

            (int)XsFingerAnimationSegment.LeftFifthMetacarpal, //not used
            (int)XsFingerAnimationSegment.LeftFifthProximalPhalange,
            (int)XsFingerAnimationSegment.LeftFifthMiddlePhalange,
            (int)XsFingerAnimationSegment.LeftFifthDistalPhalange,

            (int)XsFingerAnimationSegment.RightCarpus,

            (int)XsFingerAnimationSegment.RightFirstMetacarpal,
            (int)XsFingerAnimationSegment.RightFirstProximalPhalange,
            (int)XsFingerAnimationSegment.RightFirstDistalPhalange,

            (int)XsFingerAnimationSegment.RightSecondMetacarpal, //not used
            (int)XsFingerAnimationSegment.RightSecondProximalPhalange,
            (int)XsFingerAnimationSegment.RightSecondMiddlePhalange,
            (int)XsFingerAnimationSegment.RightSecondDistalPhalange,

            (int)XsFingerAnimationSegment.RightThirdMetacarpal, //not used
            (int)XsFingerAnimationSegment.RightThirdProximalPhalange,
            (int)XsFingerAnimationSegment.RightThirdMiddlePhalange,
            (int)XsFingerAnimationSegment.RightThirdDistalPhalange,

            (int)XsFingerAnimationSegment.RightFourthMetacarpal, //not used
            (int)XsFingerAnimationSegment.RightFourthProximalPhalange,
            (int)XsFingerAnimationSegment.RightFourthMiddlePhalange,
            (int)XsFingerAnimationSegment.RightFourthDistalPhalange,

            (int)XsFingerAnimationSegment.RightFifthMetacarpal, //not used
            (int)XsFingerAnimationSegment.RightFifthProximalPhalange,
            (int)XsFingerAnimationSegment.RightFifthMiddlePhalange,
            (int)XsFingerAnimationSegment.RightFifthDistalPhalange
        };

        /// <summary>
        /// Maps the mecanim body bones.
        /// </summary>
        protected void mapMecanimBones()
        {
            bodyMecanimBones = new Dictionary<XsBodyAnimationSegment, HumanBodyBones>();

            bodyMecanimBones.Add(XsBodyAnimationSegment.Pelvis, HumanBodyBones.Hips);
            bodyMecanimBones.Add(XsBodyAnimationSegment.LeftUpperLeg, HumanBodyBones.LeftUpperLeg);
            bodyMecanimBones.Add(XsBodyAnimationSegment.LeftLowerLeg, HumanBodyBones.LeftLowerLeg);
            bodyMecanimBones.Add(XsBodyAnimationSegment.LeftFoot, HumanBodyBones.LeftFoot);
            bodyMecanimBones.Add(XsBodyAnimationSegment.LeftToe, HumanBodyBones.LeftToes);
            bodyMecanimBones.Add(XsBodyAnimationSegment.RightUpperLeg, HumanBodyBones.RightUpperLeg);
            bodyMecanimBones.Add(XsBodyAnimationSegment.RightLowerLeg, HumanBodyBones.RightLowerLeg);
            bodyMecanimBones.Add(XsBodyAnimationSegment.RightFoot, HumanBodyBones.RightFoot);
            bodyMecanimBones.Add(XsBodyAnimationSegment.RightToe, HumanBodyBones.RightToes);
            bodyMecanimBones.Add(XsBodyAnimationSegment.L5, HumanBodyBones.LastBone);	//not used
            bodyMecanimBones.Add(XsBodyAnimationSegment.L3, HumanBodyBones.Spine);
            bodyMecanimBones.Add(XsBodyAnimationSegment.T12, HumanBodyBones.LastBone);	//not used
            bodyMecanimBones.Add(XsBodyAnimationSegment.T8, HumanBodyBones.Chest);
            bodyMecanimBones.Add(XsBodyAnimationSegment.LeftShoulder, HumanBodyBones.LeftShoulder);
            bodyMecanimBones.Add(XsBodyAnimationSegment.LeftUpperArm, HumanBodyBones.LeftUpperArm);
            bodyMecanimBones.Add(XsBodyAnimationSegment.LeftLowerArm, HumanBodyBones.LeftLowerArm);
            bodyMecanimBones.Add(XsBodyAnimationSegment.LeftHand, HumanBodyBones.LeftHand);
            bodyMecanimBones.Add(XsBodyAnimationSegment.RightShoulder, HumanBodyBones.RightShoulder);
            bodyMecanimBones.Add(XsBodyAnimationSegment.RightUpperArm, HumanBodyBones.RightUpperArm);
            bodyMecanimBones.Add(XsBodyAnimationSegment.RightLowerArm, HumanBodyBones.RightLowerArm);
            bodyMecanimBones.Add(XsBodyAnimationSegment.RightHand, HumanBodyBones.RightHand);
            bodyMecanimBones.Add(XsBodyAnimationSegment.Neck, HumanBodyBones.Neck);
            bodyMecanimBones.Add(XsBodyAnimationSegment.Head, HumanBodyBones.Head);
        }

        /// <summary>
        /// Maps the mecanim finger bones.
        /// </summary>
        protected void mapFingerMecanimBones()
        {
            fingerMechanimBones = new Dictionary<XsFingerAnimationSegment, HumanBodyBones>();

            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftCarpus, HumanBodyBones.LeftHand);

            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftFirstMetacarpal, HumanBodyBones.LeftThumbProximal);
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftFirstProximalPhalange, HumanBodyBones.LeftThumbIntermediate);
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftFirstDistalPhalange, HumanBodyBones.LeftThumbDistal);

            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftSecondMetacarpal, HumanBodyBones.LastBone); //not used
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftSecondProximalPhalange, HumanBodyBones.LeftIndexProximal);
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftSecondMiddlePhalange, HumanBodyBones.LeftIndexIntermediate);
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftSecondDistalPhalange, HumanBodyBones.LeftIndexDistal);

            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftThirdMetacarpal, HumanBodyBones.LastBone); //not used
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftThirdProximalPhalange, HumanBodyBones.LeftMiddleProximal);
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftThirdMiddlePhalange, HumanBodyBones.LeftMiddleIntermediate);
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftThirdDistalPhalange, HumanBodyBones.LeftMiddleDistal);

            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftFourthMetacarpal, HumanBodyBones.LastBone); //not used
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftFourthProximalPhalange, HumanBodyBones.LeftRingProximal);
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftFourthMiddlePhalange, HumanBodyBones.LeftRingIntermediate);
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftFourthDistalPhalange, HumanBodyBones.LeftRingDistal);

            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftFifthMetacarpal, HumanBodyBones.LastBone); //not used
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftFifthProximalPhalange, HumanBodyBones.LeftLittleProximal);
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftFifthMiddlePhalange, HumanBodyBones.LeftLittleIntermediate);
            fingerMechanimBones.Add(XsFingerAnimationSegment.LeftFifthDistalPhalange, HumanBodyBones.LeftLittleDistal);

            fingerMechanimBones.Add(XsFingerAnimationSegment.RightCarpus, HumanBodyBones.RightHand);

            fingerMechanimBones.Add(XsFingerAnimationSegment.RightFirstMetacarpal, HumanBodyBones.RightThumbProximal);
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightFirstProximalPhalange, HumanBodyBones.RightThumbIntermediate);
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightFirstDistalPhalange, HumanBodyBones.RightThumbDistal);

            fingerMechanimBones.Add(XsFingerAnimationSegment.RightSecondMetacarpal, HumanBodyBones.LastBone); //not used
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightSecondProximalPhalange, HumanBodyBones.RightIndexProximal);
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightSecondMiddlePhalange, HumanBodyBones.RightIndexIntermediate);
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightSecondDistalPhalange, HumanBodyBones.RightIndexDistal);

            fingerMechanimBones.Add(XsFingerAnimationSegment.RightThirdMetacarpal, HumanBodyBones.LastBone); //not used
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightThirdProximalPhalange, HumanBodyBones.RightMiddleProximal);
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightThirdMiddlePhalange, HumanBodyBones.RightMiddleIntermediate);
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightThirdDistalPhalange, HumanBodyBones.RightMiddleDistal);

            fingerMechanimBones.Add(XsFingerAnimationSegment.RightFourthMetacarpal, HumanBodyBones.LastBone); //not used
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightFourthProximalPhalange, HumanBodyBones.RightRingProximal);
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightFourthMiddlePhalange, HumanBodyBones.RightRingIntermediate);
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightFourthDistalPhalange, HumanBodyBones.RightRingDistal);

            fingerMechanimBones.Add(XsFingerAnimationSegment.RightFifthMetacarpal, HumanBodyBones.LastBone); //not used
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightFifthProximalPhalange, HumanBodyBones.RightLittleProximal);
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightFifthMiddlePhalange, HumanBodyBones.RightLittleIntermediate);
            fingerMechanimBones.Add(XsFingerAnimationSegment.RightFifthDistalPhalange, HumanBodyBones.RightLittleDistal);
        }

        /// <summary>
        /// Wake this instance and initialize the live objects.
        /// </summary>
        IEnumerator Start()
        {
            isInited = false;
#if TPOSE_FIRST
            isFirstPose = true;
#endif
            //save start positions
            target = gameObject.transform;
            origPos = target;

            //create an MvnActor 
            GameObject obj = (GameObject)Instantiate(Resources.Load("MvnActor"));
            obj.transform.parent = gameObject.transform;
            mvnActor = obj.transform;
            if (mvnActor == null)
            {
                Debug.LogError("[xsens] No AnimationSkeleton found!");
                yield return null;
            }

            // Search for the network stream, so we can communicate with it.
            if (mvnActors == null)
            {
                Debug.LogError("[xsens] No MvnActor found! You must assign an MvnActor to this component.");
                yield return null;
            }

            //Wait for data to come in so that we can figure out incomming segment counts before setup 
            while (!mvnActors.poseEstablished(actorID, out segmentCount))
            {
                yield return null;
            }


            CheckFingers(segmentCount);

            beginSetup();

        }

        /// <summary>
        /// This was moved out of Start since Start is now an IEnumerator to establish segment counts before setup
        /// </summary>
        private void beginSetup()
        {
            //Work out way through the segment count cominations to figure out what we need to setup

            
            try
            {
                //map each bone with xsens bipad model and mecanim bones
                mapMecanimBones();

                mapFingerMecanimBones();
                fingerTrackingEnabled = targetHasFingers();

                //setup arrays for pose's
                targetModel = new Transform[XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnFingerSegmentCount + XsMvnPose.MvnPropSegmentCount];
                modelRotTP = new Quaternion[XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnFingerSegmentCount + XsMvnPose.MvnPropSegmentCount];
                modelPosTP = new Vector3[XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnFingerSegmentCount + XsMvnPose.MvnPropSegmentCount];
                currentPose = new Transform[XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnFingerSegmentCount + XsMvnPose.MvnPropSegmentCount];

                //add an empty object, which we can use for missing segments
                missingSegments = new GameObject("MissingSegments");
                missingSegments.transform.parent = gameObject.transform;


                //setup the animation and the model as well
                if (!setupMvnActor())
                {
                    Debug.Log("[xsens] failed to init MvnActor");
                    return;
                }

                if (!setupModel(target, targetModel))
                {
                    return;
                }

                //face model to the right direction	
                target.transform.rotation = transform.rotation;

                isInited = true;
            }
            catch (Exception e)
            {
                print("[xsens] Something went wrong setting up.");
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Sets up the mvn actor, with binding the currentPose to the actor bones.
        /// </summary>
        /// <returns>
        /// true on success
        /// </returns>
        public bool setupMvnActor()
        {
            mvnActor.rotation = transform.rotation;
            mvnActor.position = transform.position;

            currentPose[(int)XsBodyAnimationSegment.Pelvis] = mvnActor.Find("Pelvis");
            currentPose[(int)XsBodyAnimationSegment.L5] = mvnActor.Find("Pelvis/L5");

            currentPose[(int)XsBodyAnimationSegment.L3] = mvnActor.Find("Pelvis/L5/L3");
            currentPose[(int)XsBodyAnimationSegment.T12] = mvnActor.Find("Pelvis/L5/L3/T12");
            currentPose[(int)XsBodyAnimationSegment.T8] = mvnActor.Find("Pelvis/L5/L3/T12/T8");
            currentPose[(int)XsBodyAnimationSegment.LeftShoulder] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder");
            currentPose[(int)XsBodyAnimationSegment.LeftUpperArm] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm");
            currentPose[(int)XsBodyAnimationSegment.LeftLowerArm] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm");
            currentPose[(int)XsBodyAnimationSegment.LeftHand] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand");

            currentPose[(int)XsBodyAnimationSegment.Neck] = mvnActor.Find("Pelvis/L5/L3/T12/T8/Neck");
            currentPose[(int)XsBodyAnimationSegment.Head] = mvnActor.Find("Pelvis/L5/L3/T12/T8/Neck/Head");

            currentPose[(int)XsBodyAnimationSegment.RightShoulder] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder");
            currentPose[(int)XsBodyAnimationSegment.RightUpperArm] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm");
            currentPose[(int)XsBodyAnimationSegment.RightLowerArm] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm");
            currentPose[(int)XsBodyAnimationSegment.RightHand] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand");

            currentPose[(int)XsBodyAnimationSegment.LeftUpperLeg] = mvnActor.Find("Pelvis/LeftUpperLeg");
            currentPose[(int)XsBodyAnimationSegment.LeftLowerLeg] = mvnActor.Find("Pelvis/LeftUpperLeg/LeftLowerLeg");
            currentPose[(int)XsBodyAnimationSegment.LeftFoot] = mvnActor.Find("Pelvis/LeftUpperLeg/LeftLowerLeg/LeftFoot");
            currentPose[(int)XsBodyAnimationSegment.LeftToe] = mvnActor.Find("Pelvis/LeftUpperLeg/LeftLowerLeg/LeftFoot/LeftToe");
            currentPose[(int)XsBodyAnimationSegment.RightUpperLeg] = mvnActor.Find("Pelvis/RightUpperLeg");
            currentPose[(int)XsBodyAnimationSegment.RightLowerLeg] = mvnActor.Find("Pelvis/RightUpperLeg/RightLowerLeg");
            currentPose[(int)XsBodyAnimationSegment.RightFoot] = mvnActor.Find("Pelvis/RightUpperLeg/RightLowerLeg/RightFoot");
            currentPose[(int)XsBodyAnimationSegment.RightToe] = mvnActor.Find("Pelvis/RightUpperLeg/RightLowerLeg/RightFoot/RightToe");

            setupPropsWithActor();
            setupFingerTrackingWithActor();
            return true;
        }

        /// <summary>
        /// Sets up the mvn actors props
        /// </summary>
        private void setupPropsWithActor()
        {
            props = new XsProp[4] { prop1, prop2, prop3, prop4 };
            targetProps = new XsPropManager[XsMvnPose.MvnPropSegmentCount];
            currentProps = new GameObject[XsMvnPose.MvnPropSegmentCount];
            int startingPoiont = XsMvnPose.MvnBodySegmentCount;
            for (int i = 0; i < XsMvnPose.MvnPropSegmentCount; i++)
            {
                GameObject prop = new GameObject("prop " + (i + 1));
                currentProps[i] = prop;
                prop.transform.parent = currentPose[(int)props[i].segment];
                currentPose[startingPoiont + i] = prop.transform;
            }
        }

        /// <summary>
        /// Sets up the mvn actor, with binding the currentPose to the actor bones.
        /// </summary>
        /// <returns>
        /// true on success
        /// </returns>
        private bool setupFingerTrackingWithActor()
        {
            int nonFingerCount = XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnPropSegmentCount;
            currentPose[(int)XsFingerAnimationSegment.LeftCarpus + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand");

            currentPose[(int)XsFingerAnimationSegment.LeftFirstDistalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftFirstMC/LeftFirstPP/LeftFirstDP");
            currentPose[(int)XsFingerAnimationSegment.LeftFirstMetacarpal + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftFirstMC");
            currentPose[(int)XsFingerAnimationSegment.LeftFirstProximalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftFirstMC/LeftFirstPP");

            currentPose[(int)XsFingerAnimationSegment.LeftSecondDistalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftSecondMC/LeftSecondPP/LeftSecondMP/LeftSecondDP");
            currentPose[(int)XsFingerAnimationSegment.LeftSecondMetacarpal + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftSecondMC");
            currentPose[(int)XsFingerAnimationSegment.LeftSecondProximalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftSecondMC/LeftSecondPP");
            currentPose[(int)XsFingerAnimationSegment.LeftSecondMiddlePhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftSecondMC/LeftSecondPP/LeftSecondMP");

            currentPose[(int)XsFingerAnimationSegment.LeftThirdDistalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftThirdMC/LeftThirdPP/LeftThirdMP/LeftThirdDP");
            currentPose[(int)XsFingerAnimationSegment.LeftThirdMetacarpal + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftThirdMC");
            currentPose[(int)XsFingerAnimationSegment.LeftThirdProximalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftThirdMC/LeftThirdPP");
            currentPose[(int)XsFingerAnimationSegment.LeftThirdMiddlePhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftThirdMC/LeftThirdPP/LeftThirdMP");

            currentPose[(int)XsFingerAnimationSegment.LeftFourthDistalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftFourthMC/LeftFourthPP/LeftFourthMP/LeftFourthDP");
            currentPose[(int)XsFingerAnimationSegment.LeftFourthMetacarpal + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftFourthMC");
            currentPose[(int)XsFingerAnimationSegment.LeftFourthProximalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftFourthMC/LeftFourthPP");
            currentPose[(int)XsFingerAnimationSegment.LeftFourthMiddlePhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftFourthMC/LeftFourthPP/LeftFourthMP");

            currentPose[(int)XsFingerAnimationSegment.LeftFifthDistalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftFifthMC/LeftFifthPP/LeftFifthMP/LeftFifthDP");
            currentPose[(int)XsFingerAnimationSegment.LeftFifthMetacarpal + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftFifthMC");
            currentPose[(int)XsFingerAnimationSegment.LeftFifthProximalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftFifthMC/LeftFifthPP");
            currentPose[(int)XsFingerAnimationSegment.LeftFifthMiddlePhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/LeftShoulder/LeftUpperArm/LeftLowerArm/LeftHand/LeftFifthMC/LeftFifthPP/LeftFifthMP");

            currentPose[(int)XsFingerAnimationSegment.RightCarpus + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand");

            currentPose[(int)XsFingerAnimationSegment.RightFirstDistalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightFirstMC/RightFirstPP/RightFirstDP");
            currentPose[(int)XsFingerAnimationSegment.RightFirstMetacarpal + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightFirstMC");
            currentPose[(int)XsFingerAnimationSegment.RightFirstProximalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightFirstMC/RightFirstPP");

            currentPose[(int)XsFingerAnimationSegment.RightSecondDistalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightSecondMC/RightSecondPP/RightSecondMP/RightSecondDP");
            currentPose[(int)XsFingerAnimationSegment.RightSecondMetacarpal + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightSecondMC");
            currentPose[(int)XsFingerAnimationSegment.RightSecondProximalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightSecondMC/RightSecondPP");
            currentPose[(int)XsFingerAnimationSegment.RightSecondMiddlePhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightSecondMC/RightSecondPP/RightSecondMP");

            currentPose[(int)XsFingerAnimationSegment.RightThirdDistalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightThirdMC/RightThirdPP/RightThirdMP/RightThirdDP");
            currentPose[(int)XsFingerAnimationSegment.RightThirdMetacarpal + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightThirdMC");
            currentPose[(int)XsFingerAnimationSegment.RightThirdProximalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightThirdMC/RightThirdPP");
            currentPose[(int)XsFingerAnimationSegment.RightThirdMiddlePhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightThirdMC/RightThirdPP/RightThirdMP");

            currentPose[(int)XsFingerAnimationSegment.RightFourthDistalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightFourthMC/RightFourthPP/RightFourthMP/RightFourthDP");
            currentPose[(int)XsFingerAnimationSegment.RightFourthMetacarpal + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightFourthMC");
            currentPose[(int)XsFingerAnimationSegment.RightFourthProximalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightFourthMC/RightFourthPP");
            currentPose[(int)XsFingerAnimationSegment.RightFourthMiddlePhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightFourthMC/RightFourthPP/RightFourthMP");

            currentPose[(int)XsFingerAnimationSegment.RightFifthDistalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightFifthMC/RightFifthPP/RightFifthMP/RightFifthDP");
            currentPose[(int)XsFingerAnimationSegment.RightFifthMetacarpal + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightFifthMC");
            currentPose[(int)XsFingerAnimationSegment.RightFifthProximalPhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightFifthMC/RightFifthPP");
            currentPose[(int)XsFingerAnimationSegment.RightFifthMiddlePhalange + nonFingerCount] = mvnActor.Find("Pelvis/L5/L3/T12/T8/RightShoulder/RightUpperArm/RightLowerArm/RightHand/RightFifthMC/RightFifthPP/RightFifthMP");

            return true;
        }

        /// <summary>
        /// We check to see if the target Rig has the correct rigging and avatar setup to accept finger data
        /// </summary>
        /// <returns>Finger Tracking Enabled</returns>
        private bool targetHasFingers()
        {
            animator = target.GetComponent<Animator>();
            if (segmentCount < XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnFingerSegmentCount) return false;

            for (int i = 0; i < XsMvnPose.MvnFingerSegmentCount; i++)
            {
                if (fingerMechanimBones[(XsFingerAnimationSegment)fingerSegmentOrder[i]] != HumanBodyBones.LastBone)    //Used to fix error with Unity 2018+ (GetBoneTransform must be BETWEEN 0 and LastBone)
                {
                    if (animator.GetBoneTransform(fingerMechanimBones[(XsFingerAnimationSegment)fingerSegmentOrder[i]]) == null)
                    {
                        Debug.Log(i);
                        segmentCount -= XsMvnPose.MvnFingerSegmentCount;
                        Debug.Log("[xsens] actorID: " + actorID + "'s data from MVN contains finger tracking data but the avatar does not contain the bones needed to accept the data.");
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Sets up the model.
        /// Bind the model with the animation.
        /// This funciton will use Macanim animator to find the right bones, 
        /// then it will store it in an array by animation segment id
        /// </summary>
        /// <param name='model'>
        /// Model.
        /// </param>
        /// <param name='modelRef'>
        /// Model reference.
        /// </param>
        /// <returns>
        /// true on success
        /// </returns>
        public bool setupModel(Transform model, Transform[] modelRef)
        {
            animator = model.GetComponent<Animator>();
            if (!animator)
            {
                return false;
            }

            //face the input model same as our animation
            model.rotation = transform.rotation;
            model.position = transform.position;

            //go through the model's body segments and store values
            for (int i = 0; i < XsMvnPose.MvnBodySegmentCount; i++)
            {

                XsBodyAnimationSegment segID = (XsBodyAnimationSegment)bodySegmentOrder[i];
                HumanBodyBones boneID = bodyMecanimBones[(XsBodyAnimationSegment)bodySegmentOrder[i]];
                try
                {

                    if (boneID == HumanBodyBones.LastBone)
                    {
                        //not used bones
                        modelRef[(int)segID] = null;
                        modelPosTP[(int)segID] = Vector3.zero;
                        modelRotTP[(int)segID] = Quaternion.Euler(Vector3.zero);

                    }
                    else
                    {
                        //used bones
                        Transform tmpTransf = animator.GetBoneTransform(boneID);
                        Vector3 tempPos = transform.position;
                        Quaternion tempRot = transform.rotation;

                        transform.position = Vector3.zero;
                        transform.rotation = Quaternion.identity;

                        modelRef[(int)segID] = tmpTransf;
                        modelPosTP[(int)segID] = modelRef[(int)segID].position;
                        modelRotTP[(int)segID] = modelRef[(int)segID].rotation;

                        transform.position = tempPos;
                        transform.rotation = tempRot;
                    }
                }
                catch (Exception e)
                {
                    Debug.Log("[xsens] Can't find " + boneID + " in the model! " + e);
                    modelRef[(int)segID] = null;
                    modelPosTP[(int)segID] = Vector3.zero;
                    modelRotTP[(int)segID] = Quaternion.Euler(Vector3.zero);

                    return false;
                }

            }

            //set our starting index
            int startingPoiont = XsMvnPose.MvnBodySegmentCount;
            //iterate through the prop segment count and setup each prop
            for (int i = 0; i < XsMvnPose.MvnPropSegmentCount; i++)
            {
                try
                {
                    if (props[i] == null)
                    {
                        modelRef[startingPoiont + i] = null;
                        modelPosTP[startingPoiont + i] = Vector3.zero;
                        modelRotTP[startingPoiont + i] = Quaternion.Euler(Vector3.zero);
                    }
                    else
                    {
                        GameObject prop = Instantiate(props[i].SpawnProp());
                        targetProps[i] = prop.GetComponent<XsPropManager>();
                        prop.transform.parent = targetModel[(int)props[i].segment];

                        modelRef[startingPoiont + i] = prop.transform;
                        modelPosTP[startingPoiont + i] = modelRef[startingPoiont + i].position;
                        modelRotTP[startingPoiont + i] = modelRef[startingPoiont + i].rotation;
                    }
                }
                catch (Exception e)
                {
                    Debug.Log("[xsens] Can't find prop " + i + ". Pleast make sure you add it to the XsLiveAnimator " + e);
                    modelRef[startingPoiont + i] = null;
                    modelPosTP[startingPoiont + i] = Vector3.zero;
                    modelRotTP[startingPoiont + i] = Quaternion.Euler(Vector3.zero);

                    return false;
                }
            }

            //go through the model's finger segments and store values
            for (int i = 0; i < XsMvnPose.MvnFingerSegmentCount; i++)
            {
                XsFingerAnimationSegment segID = (XsFingerAnimationSegment)fingerSegmentOrder[i] + XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnPropSegmentCount;
                HumanBodyBones boneID = fingerMechanimBones[(XsFingerAnimationSegment)fingerSegmentOrder[i]];

                try
                {
                    if (boneID == HumanBodyBones.LastBone)
                    {
                        //not used bones
                        modelRef[(int)segID] = null;
                        modelPosTP[(int)segID] = Vector3.zero;
                        modelRotTP[(int)segID] = Quaternion.Euler(Vector3.zero);

                    }
                    else
                    {
                        //used bones
                        Transform tmpTransf = animator.GetBoneTransform(boneID);

                        Vector3 tempPos = transform.position;
                        Quaternion tempRot = transform.rotation;

                        transform.position = Vector3.zero;
                        transform.rotation = Quaternion.identity;

                        modelRef[(int)segID] = tmpTransf;
                        modelPosTP[(int)segID] = modelRef[(int)segID].position;
                        modelRotTP[(int)segID] = modelRef[(int)segID].rotation;
                        transform.position = tempPos;
                        transform.rotation = tempRot;
                    }
                }
                catch (Exception e)
                {
                    Debug.Log("[xsens] Can't find " + boneID + " in the model! " + e);
                    modelRef[(int)segID] = null;
                    modelPosTP[(int)segID] = Vector3.zero;
                    modelRotTP[(int)segID] = Quaternion.Euler(Vector3.zero);

                    return false;
                }
            }
            return true;
        }

        //Sets up data needed for fingers
        void CheckFingers(int segmentSize)
        {
            segmentCount = segmentSize;
            fingerTrackingEnabled = false;

            if (segmentCount >= XsMvnPose.MvnFingerSegmentCount + XsMvnPose.MvnBodySegmentCount)
            {
                fingerTrackingEnabled = true;
                propDataSize = segmentCount - (XsMvnPose.MvnFingerSegmentCount + XsMvnPose.MvnBodySegmentCount);
            }
            else if (segmentCount > XsMvnPose.MvnBodySegmentCount)
            {
                propDataSize = segmentCount - XsMvnPose.MvnBodySegmentCount;
            }
            else
            {
                propDataSize = 0;
            }
        }

        
        /// <summary>
        /// Reset the model to Tpose, used when we change actor during runtime
        /// </summary>
        void TposeModel()
        {
            for(int i = 0; i < modelRotTP.Length; i++)
            {
                if(targetModel[i] != null)
                {
                    targetModel[i].position = modelPosTP[i];
                    targetModel[i].rotation = modelRotTP[i];
                }
            }
        }

        /// <summary>
        /// Checks the props and updates models/segments in runtime
        /// </summary>
        void CheckProps()
        {
            for(int i = 0; i < props.Length; i++)
            {
                if (i < propDataSize)
                {
                    targetProps[i].gameObject.SetActive(true);

                    if (props[i].type != targetProps[i].currentType)
                    {
                        targetProps[i].SwapPropType(props[i].type);
                    }

                    if (props[i].segment != targetProps[i].currentSegment)
                    {
                        currentProps[i].transform.parent = currentPose[(int)props[i].segment];
                        targetProps[i].gameObject.transform.parent = targetModel[(int)props[i].segment];
                        targetProps[i].currentSegment = props[i].segment;
                    }
                }
                else
                {
                    targetProps[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        ///	Update the body segments in every frame.
        ///
        /// The mvn actors segment orientations and positions are read from the network,
        /// using the MvnLiveActor component.
        /// This component provides all data for the current pose for each actors.
        /// </summary>
        void Update()
        {

            //only do magic if we have everything ready
            if (!isInited)
                return;

            CheckProps();

            //Store the parent transform so that we can apply data from MVN assuming our character is in the center of the world facing the forward vector
            Vector3 storedPos = transform.position;
            Vector3 storedScale = transform.localScale;
            Quaternion storedRot = transform.rotation;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            Vector3[] latestPositions;
            Quaternion[] latestOrientations;
            // Get the pose data in one call, else the orientation might come from a different pose
            // than the position
            if (mvnActors.getLatestPose(actorID - 1, out latestPositions, out latestOrientations))
            {

                if (latestPositions.Length != segmentCount)
                {
                    TposeModel();
                    CheckFingers(latestPositions.Length);
                    return;
                }

#if TPOSE_FIRST
                if (isFirstPose)
                {
                    //init the animation skeleton with the first pose
                    initSkeleton(currentPose, latestPositions, latestOrientations);
                }
                else
#endif
                {
                    //update pose		
                    updateMvnActor(currentPose, latestPositions, latestOrientations);
                    updateModel(currentPose, targetModel);
                }
            }

            //Reset the parent object to its position in the scene
            transform.position = storedPos;
            transform.localScale = storedScale;
            transform.rotation = storedRot;

        }

        /// <summary>
        /// Updates the actor skeleton local positions, based on the first valid pose
        /// </summary>
        /// <param name='model'>
        /// Model.
        /// </param>
        /// <param name='positions'>
        /// Positions.
        /// </param>
        protected void initSkeleton(Transform[] model, Vector3[] positions, Quaternion[] orientations)
        {

            //wait for real data
            if (positions[(int)XsBodyAnimationSegment.Pelvis] == Vector3.zero)
            {
                return;
            }

            //reposition the segments based on the data
#if TPOSE_FIRST			
            isFirstPose = false;
#endif

            for (int i = 0; i < bodySegmentOrder.Length; i++)
            {
                if (XsBodyAnimationSegment.Pelvis == (XsBodyAnimationSegment)bodySegmentOrder[i])
                {
                    //global for pelvis
                    model[bodySegmentOrder[i]].transform.position = positions[bodySegmentOrder[i]];
                    model[bodySegmentOrder[i]].transform.rotation = orientations[bodySegmentOrder[i]];

                }
                else
                {
                    //local for segments
                    model[bodySegmentOrder[i]].transform.localPosition = positions[bodySegmentOrder[i]];
                    model[bodySegmentOrder[i]].transform.localRotation = orientations[bodySegmentOrder[i]];
                }
            }

            //reinit the actor
            setupMvnActor();
        }


        /// <summary>
        /// Updates the mvn actor segment orientations and positions.
        /// </summary>
        /// <param name='model'>
        /// Model to update.
        /// </param>
        /// <param name='positions'>
        /// Positions in array
        /// </param>
        /// <param name='orientations'>
        /// Orientations in array
        /// </param>
        private void updateMvnActor(Transform[] model, Vector3[] positions, Quaternion[] orientations)
        {
            try
            {
                for (int i = 0; i < bodySegmentOrder.Length; i++)	//front
                {
                    if (XsBodyAnimationSegment.Pelvis == (XsBodyAnimationSegment)bodySegmentOrder[i])
                    {
                        //we apply global position and orientaion to the pelvis
                        if (applyRootMotion)
                        {
                            model[bodySegmentOrder[i]].transform.position = (positions[bodySegmentOrder[i]] + origPos.position) * transform.localScale.x;
                        }
                        else
                        {
                            model[bodySegmentOrder[i]].transform.localPosition =
                                new Vector3(model[bodySegmentOrder[i]].transform.position.x,
                                positions[bodySegmentOrder[i]].y + origPos.position.y,
                                model[bodySegmentOrder[i]].transform.position.z);
                        }
                        Quaternion orientation =
                            Quaternion.Inverse(model[i].transform.parent.rotation)
                             * orientations[bodySegmentOrder[i]]
                            * modelRotTP[i];

                        model[bodySegmentOrder[i]].transform.localRotation = orientation;

                    }
                    else
                    {
                        if (model[bodySegmentOrder[i]] == null)
                        {
                            Debug.LogError("[xsens] XsLiveAnimator: Missing bone from mvn actor! Did you change MvnLive plugin? Please check if you are using the right actor!");
                            break;
                        }
                        Quaternion orientation =
                            Quaternion.Inverse(model[i].transform.parent.rotation)
                             * orientations[bodySegmentOrder[i]]
                            * modelRotTP[i];

                        model[bodySegmentOrder[i]].transform.localRotation = orientation;

                        //draw wireframe for original animation
                        if (isDebugFrame)
                        {

                            Color color = new Color(1.0f, 0.0f, 0.0f, 1.0f);
                            int id = bodySegmentOrder[i];
                            if (model[id - 1] != null)
                            {
                                if (((id - 1) != (int)XsBodyAnimationSegment.LeftHand)
                                && ((id - 1) != (int)XsBodyAnimationSegment.RightHand)
                                && ((id - 1) != (int)XsBodyAnimationSegment.Head)
                                && ((id - 1) != (int)XsBodyAnimationSegment.LeftToe)
                                && ((id - 1) != (int)XsBodyAnimationSegment.RightToe))
                                {
                                    Debug.DrawLine(model[id].position, model[id - 1].position, color);
                                }
                            }
                        }//isDebugFrame
                    }

                }//for i

                //if we have props to animate
                if (propDataSize > 0 && props.Length != 0)
                {
                    int startingIndex = XsMvnPose.MvnBodySegmentCount;
                    for (int i = 0; i < propDataSize; i++)
                    {
                        if (model[startingIndex + i] == null)
                        {
                            Debug.LogError("[xsens] XsLiveAnimator: Missing prop from mvn actor! Did you change MvnLive plugin? Please check if you are using the right actor!");
                            break;
                        }
                        else
                        {
                            model[startingIndex + i].transform.position = model[startingIndex + i].transform.parent.position + (positions[startingIndex + i] - positions[(int)props[i].segment]) * transform.localScale.x;

                            Quaternion orientation =
                                Quaternion.Inverse(model[startingIndex + i].transform.parent.rotation)
                                 * orientations[startingIndex + i]
                                * modelRotTP[startingIndex + i];
                            model[startingIndex + i].transform.localRotation = orientation;
                        }
                    }//for i
                }//if props

                //if we have finger data
                if (fingerTrackingEnabled)
                {
                    for (int i = 0; i < fingerSegmentOrder.Length; i++)
                    {
                        if (model[i + XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnPropSegmentCount] == null)
                        {
                            Debug.Log(i);
                            Debug.LogError("[xsens] XsLiveAnimator: Missing finger bone from mvn actor! Did you change MvnLive plugin? Please check if you are using the right actor!");
                            break;
                        }
                        else
                        {
                            Quaternion orientation =
                                Quaternion.Inverse(model[i + XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnPropSegmentCount].transform.parent.rotation)
                                 * orientations[orientations.Length - XsMvnPose.MvnFingerSegmentCount + i]
                                * modelRotTP[i + XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnPropSegmentCount];
                            model[i + XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnPropSegmentCount].transform.localRotation = orientation;

                        }
                    } // for i
                }// if fingers
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Updates the model.
        /// Every pose contains the transform objects for each segment within the model.
        /// </summary>
        /// <param name='pose'>
        /// Pose holds the positions and orientations of the actor.
        /// </param>
        /// <param name='model'>
        /// Model to update with the pose.
        /// </param>
        private void updateModel(Transform[] pose, Transform[] model)
        {
            //reset the target, then set it based on segments
            Vector3 pelvisPos = new Vector3();
            Vector3 lastPos = target.position;

            // if(applyRootMotion) target.position = Vector3.zero;
            for (int i = 0; i < XsMvnPose.MvnBodySegmentCount; i++)
            {
                switch (i)
                {
                    //no update required
                    case (int)XsBodyAnimationSegment.L5:
                    case (int)XsBodyAnimationSegment.T12:
                        break;

                    case (int)XsBodyAnimationSegment.Pelvis:
                        //position only on the y axis, leave the x,z to the body
                        pelvisPos = (pose[i].localPosition);
                        model[i].localPosition = new Vector3(0, pelvisPos.y, 0);

                        model[i].rotation = pose[i].rotation;
                        break;

                    default:
                        //only update rotation for rest of the segments
                        if (model[i] != null)
                        {
                            model[i].rotation = pose[i].rotation;
                        }
                        break;
                }
            }

            if (propDataSize > 0 && props.Length != 0)
            {
                int startingIndex = XsMvnPose.MvnBodySegmentCount;
                for (int i = 0; i < propDataSize; i++)
                {
                    Vector3 propPos = (pose[i + startingIndex].localPosition);
                    model[i + startingIndex].localPosition = propPos;
                    if (model[i + startingIndex] != null && pose[i + startingIndex] != null)
                    {
                        model[i + startingIndex].rotation = pose[i + startingIndex].rotation;
                    }
                }//for i
            }//if props


            if (fingerTrackingEnabled)
            {
                for (int i = 0; i < XsMvnPose.MvnFingerSegmentCount; i++)
                {
                    switch (i)
                    {
                        default:
                            //only update rotation for rest of the segments
                            if (model[i + XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnPropSegmentCount] != null && pose[i + XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnPropSegmentCount] != null)
                            {
                                model[i + XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnPropSegmentCount].rotation = pose[i + XsMvnPose.MvnBodySegmentCount + XsMvnPose.MvnPropSegmentCount].rotation;
                            }
                            break;
                    }
                }//for i
            }//if fingers

            //apply root motion if flag enabled only
            if (applyRootMotion)
            {
                model[0].transform.parent.transform.localPosition = new Vector3(pelvisPos.x, 0, pelvisPos.z);
            }
            else
            {
                model[0].transform.parent.transform.localPosition = Vector3.zero;
            }
        }
    }//class XsLiveAnimator
}//namespace Xsens