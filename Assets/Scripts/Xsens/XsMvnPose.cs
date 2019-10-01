///<summary>
/// Xsens Mvn Pose represents all segment data to create a pose.
///</summary>
///<version>
/// 0.1, 2013.03.12, Peter Heinen
/// 1.0, 2013.04.11, Attila Odry
///</version>
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace xsens
{
    /// <summary>
    /// This class converts all the data from the packet into something Unity3D can easily read.
    /// This also contains the orientations and position fixes needed because of the different coordinate system.
    /// </summary>
    class XsMvnPose
    {
        //Stored segment counts for iterating through
        public static int MvnDefaultSegmentCount = 67;
        public int MvnCurrentSegmentCount = 0;
        public int MvnCurrentPropCount = 0;
        public static int MvnBodySegmentCount = 23;
        public static int MvnFingerSegmentCount = 40;
        public static int MvnPropSegmentCount = 4;

        public Vector3[] positions;
        public Quaternion[] orientations;

        //For use with MVN 2018-
        public XsMvnPose(int segmentCount)
        {
            SetupSegmentAmounts(segmentCount);

            positions = new Vector3[MvnCurrentSegmentCount];
            orientations = new Quaternion[MvnCurrentSegmentCount];
        }

        //For use with MVN 2019+
        public XsMvnPose(int bodySegments, int fingerSegments, int propCount)
        {
            SetupSegmentAmounts(bodySegments,fingerSegments,propCount);

            positions = new Vector3[MvnCurrentSegmentCount];
            orientations = new Quaternion[MvnCurrentSegmentCount];
        }

        //For use with MVN 2018-
        private void SetupSegmentAmounts(int segmentCount)
        {
            MvnCurrentSegmentCount = segmentCount;
            if(segmentCount > MvnBodySegmentCount + MvnFingerSegmentCount)
            {
                MvnCurrentPropCount = segmentCount - (MvnBodySegmentCount + MvnFingerSegmentCount);
            }
        }

        //For use with MVN 2019+
        private void SetupSegmentAmounts(int bodySegments, int fingerSegments, int propCount)
        {
            MvnCurrentSegmentCount = bodySegments + fingerSegments + propCount;
            if (MvnCurrentSegmentCount > MvnBodySegmentCount + MvnFingerSegmentCount)
            {
                MvnCurrentPropCount = MvnCurrentSegmentCount - (MvnBodySegmentCount + MvnFingerSegmentCount);
            }
        }

        /// <summary>
        /// Creates the vector3 positions and the Quaternion rotations for unity, based on the current data packet.
        /// Recursive so it does every segment
        /// </summary>
        /// <param name='startPosition'>
        /// Start position.
        /// </param>
        /// <param name='segmentCounter'>
        /// Segment counter.
        /// </param>
        public void createPose(double[] payloadData)
        {
            int segmentCounter = 0;
            int startPosition = 0;

            while (segmentCounter < payloadData.Length / 8)
            {
                Quaternion rotation = new Quaternion();
                Vector3 position = new Vector3();

                position.x = Convert.ToSingle(payloadData[startPosition + 1]);  //X=1
                position.y = Convert.ToSingle(payloadData[startPosition + 2]);  //Y=2
                position.z = Convert.ToSingle(payloadData[startPosition + 3]);  //Z=3

                rotation.w = Convert.ToSingle(payloadData[startPosition + 4]);  //W=4
                rotation.x = Convert.ToSingle(payloadData[startPosition + 5]);  //x=5 
                rotation.y = Convert.ToSingle(payloadData[startPosition + 6]);  //y=6
                rotation.z = Convert.ToSingle(payloadData[startPosition + 7]); //Z=7

                positions[segmentCounter] = ConvertToUnity(position);
                orientations[segmentCounter] = ConvertToUnity(rotation);

                segmentCounter++;
                startPosition += 8;
            }
        }


        /// <summary>
        /// Converts a position from MVN Coordinate Space to Unity Coordinate Space
        /// </summary>
        /// <param name="originalVector"></param>
        /// <returns></returns>
        Vector3 ConvertToUnity(Vector3 originalVector)
        {
            return new Vector3(
                -originalVector.y,
                originalVector.z,
                originalVector.x);
        }

        /// <summary>
        /// Converts a orientation from MVN Coordinate Space to Unity Coordinate Space
        /// </summary>
        /// <param name="originalOrientation"></param>
        /// <returns></returns>
        Quaternion ConvertToUnity(Quaternion originalOrientation)
        {
            return new Quaternion(
                originalOrientation.y,
                -originalOrientation.z,
                -originalOrientation.x,
                originalOrientation.w);
        }


    }//class XsMvnPose	
}//namespace xsens