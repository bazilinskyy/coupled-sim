#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace xsens
{
    [RequireComponent(typeof(XsLiveAnimator))]
    public class XsAnimationRecorder : MonoBehaviour
    {
        [Tooltip("This is the root of your rig, should not be the same Transform that this component is attached to based on the way XsLiveAnimator is meant to function.")]
        public Transform root;                  //the root of the rig
        private string filePath = "Assets/";     //filepath in the unity project to save the animationClip to
        private string fileName = "Animation";      //filename for the animation once it is saved to disk

        public bool recordFromUnityPlay = false;

        private Animator animatorComponent;
        private HumanPoseHandler humanPoseHandler;
        private HumanPose humanPose = new HumanPose();

        private EditorCurveBinding[] bindableCurves;
        private Dictionary<EditorCurveBinding, int> curveToMuscle = new Dictionary<EditorCurveBinding, int>();
        private Dictionary<int, EditorCurveBinding> indexToCurve = new Dictionary<int, EditorCurveBinding>();
        private AnimationCurve[] curves;

        private bool recording = false;
        private bool paused = false;
        private bool saveProptOpen = false;

        private XsLiveAnimator xsensAnimator;   //allows us to get the actorId so that we can arrange the GUI buttons for all 4 actors if needed

        //Used for the GUI buttons
        private int buttonWidth = 150;
        private int buttonHeight = 25;

        //used to keep track of how many curves we are currently recording
        private int recordedCurvesCount = 0;

        /// <summary>
        /// Initialize our objects and setup Dictionaries to hold the conneciton between the EditorCurveBinding and the corresponding float values
        /// </summary>
        void Start()
        {
            xsensAnimator = GetComponent<XsLiveAnimator>();
            animatorComponent = GetComponent<Animator>();
            humanPoseHandler = new HumanPoseHandler(animatorComponent.avatar, root);
            bindableCurves = AnimationUtility.GetAnimatableBindings(gameObject, gameObject);

            fileName = "Actor_" + xsensAnimator.actorID + "_" + fileName;

            SetupBodyCurves();
            SetupFingerCurves();
            SetupRootCurves();

            curves = new AnimationCurve[recordedCurvesCount];

            for (int i = 0; i < curves.Length; i++)
            {
                curves[i] = new AnimationCurve();
            }

            //Allows a user to start recording as soon as unity starts playing
            if (recordFromUnityPlay)
            {
                recording = true;
                StartCoroutine(Record());
            }
        }

        /// <summary>
        /// Add the root transform data to the indexToCurve Dictionary and setup the indecies in the binableCurves array since this data isn't found in HumanPose.muscles.
        /// These 7 properties are added from HumanPose.bodyPosition and HumanPose.bodyRotation
        /// </summary>
        private void SetupRootCurves()
        {
            //Iterate through all possible EditorCurveBindings so that we can match them up with the correct index in the indexToCurve Dictionary
            for (int i = 0; i < bindableCurves.Length; i++)
            {
                if (
                      bindableCurves[i].propertyName == "RootT.x" ||
                      bindableCurves[i].propertyName == "RootT.y" ||
                      bindableCurves[i].propertyName == "RootT.z" ||
                      bindableCurves[i].propertyName == "RootQ.x" ||
                      bindableCurves[i].propertyName == "RootQ.y" ||
                      bindableCurves[i].propertyName == "RootQ.z" ||
                      bindableCurves[i].propertyName == "RootQ.w"
                      )
                {
                    indexToCurve[recordedCurvesCount] = bindableCurves[i];
                    recordedCurvesCount++;
                }
            }
        }

        /// <summary>
        /// Add the finger data to the indexToCurve Dictionary and setup the indecies in the binableCurves array since the indecies that match HumanPose.muscles isn't found in HumanTrait.MuscleName for some reason. 
        /// The Data for the curves can still be found in the last 40 indicies of HumanPose.Muscles
        /// </summary>
        private void SetupFingerCurves()
        {
            //Iterate through all possible EditorCurveBindings so that we can match them up with the correct index in the indexToCurve Dictionary
            for (int i = 0; i < bindableCurves.Length; i++)
            {
                if (
                      bindableCurves[i].propertyName == "LeftHand.Thumb.1 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Thumb.Spread" ||
                      bindableCurves[i].propertyName == "LeftHand.Thumb.2 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Thumb.3 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Index.1 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Index.Spread" ||
                      bindableCurves[i].propertyName == "LeftHand.Index.2 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Index.3 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Middle.1 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Middle.Spread" ||
                      bindableCurves[i].propertyName == "LeftHand.Middle.2 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Middle.3 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Ring.1 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Ring.Spread" ||
                      bindableCurves[i].propertyName == "LeftHand.Ring.2 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Ring.3 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Little.1 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Little.Spread" ||
                      bindableCurves[i].propertyName == "LeftHand.Little.2 Stretched" ||
                      bindableCurves[i].propertyName == "LeftHand.Little.3 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Thumb.1 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Thumb.Spread" ||
                      bindableCurves[i].propertyName == "RightHand.Thumb.2 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Thumb.3 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Index.1 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Index.Spread" ||
                      bindableCurves[i].propertyName == "RightHand.Index.2 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Index.3 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Middle.1 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Middle.Spread" ||
                      bindableCurves[i].propertyName == "RightHand.Middle.2 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Middle.3 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Ring.1 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Ring.Spread" ||
                      bindableCurves[i].propertyName == "RightHand.Ring.2 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Ring.3 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Little.1 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Little.Spread" ||
                      bindableCurves[i].propertyName == "RightHand.Little.2 Stretched" ||
                      bindableCurves[i].propertyName == "RightHand.Little.3 Stretched"
                    )
                {
                    indexToCurve[recordedCurvesCount] = bindableCurves[i];
                    recordedCurvesCount++;
                }
            }
        }

        /// <summary>
        /// Add the body data to the CurveToMuscle and indexToCurve Dictionaries and setup the indecies in the binableCurves array.
        /// The Data for the curves can still be found in the first 55 indicies of HumanPose.Muscles
        /// </summary>
        private void SetupBodyCurves()
        {
            //Iterate through all possible EditorCurveBindings then compare them to the muscles found in the human trait system.
            for (int i = 0; i < bindableCurves.Length; i++)
            {
                for (int j = 0; j < HumanTrait.MuscleName.Length; j++)
                {
                    if (bindableCurves[i].propertyName == HumanTrait.MuscleName[j])
                    {
                        curveToMuscle[bindableCurves[i]] = j;
                        indexToCurve[recordedCurvesCount] = bindableCurves[i];
                        recordedCurvesCount++;
                    }
                }
            }
        }

        /// <summary>
        /// GUI to help users create anim clips in the Game View
        /// </summary>
        private void OnGUI()
        {

            if(saveProptOpen)
            {
                Rect windowRect = new Rect(Screen.width/2 - 150, Screen.height/2 - 150, 300, 150);
                windowRect = GUI.Window(0, windowRect, SavePrompt, "Stop Recording");

                return;
            }

            if (recording == false)
            {
                if (GUI.Button(new Rect(0, 0 + (buttonHeight * xsensAnimator.actorID - 1), buttonWidth, buttonHeight), "Start Rec Actor " + xsensAnimator.actorID))
                {
                    recording = true;
                    StartCoroutine(Record());
                }
            }
            else
            {
                if (GUI.Button(new Rect(0, 0 + (buttonHeight * xsensAnimator.actorID - 1), buttonWidth, buttonHeight), "Stop Rec Actor " + xsensAnimator.actorID))
                {
                    recording = false;
                    saveProptOpen = true;
                }
            }
        }

        void SavePrompt(int windowID)
        {
            GUI.Label(new Rect(0, 20, 100, 20), "File Path:");
            GUI.Label(new Rect(0, 40, 100, 20), "File Name:");
            filePath = GUI.TextField(new Rect(100, 20, 200, 20), filePath);
            fileName = GUI.TextField(new Rect(100, 40, 200, 20), fileName);

            if (GUI.Button(new Rect(0, 60, 150, 20), "Save Recording"))
            {
                recording = false;
                saveProptOpen = false;
                SaveRecording();
            }
            if (GUI.Button(new Rect(150, 60, 150, 20), "Delete Recording"))
            {
                recording = false;
                saveProptOpen = false;
                for (int i = 0; i < curves.Length; i++)
                {
                    curves[i] = new AnimationCurve();
                }
            }
        }

        /// <summary>
        /// This runs as a corrutine the entire time of recording and adds keyframes to all curves each frame that can later be connected to an animationClip.
        /// </summary>
        IEnumerator Record()
        {
            //Used for setting up the keyframes
            float time = 0;
            while (recording)
            {
                //Wait until we are done with the frame before we get the humanPose data from the avatar. This will insure that all mocap data has already been applied to the rig.
                yield return new WaitForEndOfFrame();

                //Get this frame's humanPose
                humanPoseHandler.GetHumanPose(ref humanPose);

                //Iterate though the body muscles.
                for (int i = 0; i < recordedCurvesCount - 47; i++)
                {
                    curves[i].AddKey(time, humanPose.muscles[curveToMuscle[indexToCurve[i]]]);
                }

                //Iterate through the finger muscles
                int count = 0;
                for (int i = recordedCurvesCount - 48; i < recordedCurvesCount - 7; i++)
                {
                    curves[i].AddKey(time, humanPose.muscles[humanPose.muscles.Length - 41 + count]);
                    count++;
                }

                //Finally add the root transform data
                curves[recordedCurvesCount - 7].AddKey(time, humanPose.bodyPosition.x);
                curves[recordedCurvesCount - 6].AddKey(time, humanPose.bodyPosition.y);
                curves[recordedCurvesCount - 5].AddKey(time, humanPose.bodyPosition.z);
                curves[recordedCurvesCount - 4].AddKey(time, humanPose.bodyRotation.x);
                curves[recordedCurvesCount - 3].AddKey(time, humanPose.bodyRotation.y);
                curves[recordedCurvesCount - 2].AddKey(time, humanPose.bodyRotation.z);
                curves[recordedCurvesCount - 1].AddKey(time, humanPose.bodyRotation.w);

                //Increment time so we know where the keyframes should be added to the curves.
                time += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Use the AnimationUtility to save our AnimationCurves to the correct EditorCurveBindings and then save our new clip to disk
        /// </summary>
        void SaveRecording()
        {
            //Creates a new clip so we can save all of our curves.
            AnimationClip clip = new AnimationClip();

            //Iterate through all curves and add the data to the currect EditorCurveBindings
            for (int i = 0; i < recordedCurvesCount; i++)
            {
                AnimationUtility.SetEditorCurve(clip, indexToCurve[i], curves[i]);
            }

            //Save the clip to disk
            AssetDatabase.CreateAsset(clip, filePath + fileName + ".anim");
            AssetDatabase.SaveAssets();

            Debug.Log("[xens] Animation Clip <b>\"" + fileName + ".anim\"</b> saved successfuly in <b>" + filePath + "</b>");
        }

    }
}
#endif