///<summary>
/// Xsens Stream Reader Thread read from the stream and store the latest pose from 1 actor.
/// 
///</summary>
///<version>
/// 0.1, 2013.03.12 by Peter Heinen
/// 1.0, 2013.05.14 by Attila Odry, Daniël van Os
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
using UnityEngine;
using System.Threading;

namespace xsens
{
    /// <summary>
    /// Xsens Stream Reader Thread.
    /// Every actor from MVN Stream has its own reader trhead.
    /// </summary>
    class XsStreamReaderThread
    {
        private Thread thread;
        private byte[] lastPackets;
        //private bool newData = false;
        private bool dataUpdated = true;

        private Vector3[] lastPosePositions;
        private Quaternion[] lastPoseOrientations;

        /// <summary>
        /// Initializes a new instance of the <see cref="xsens.XsStreamReaderThread"/> class.
        /// </summary>
        public XsStreamReaderThread()
        {
            //make sure we always have some date, even when no streaming
            lastPosePositions = new Vector3[XsMvnPose.MvnDefaultSegmentCount];
            lastPoseOrientations = new Quaternion[XsMvnPose.MvnDefaultSegmentCount];
            //start a new thread		
            thread = new Thread(new ThreadStart(start));
            thread.Start();
        }

        /// <summary>
        /// Start this instance.
        /// The datapacket will be set to one of the supported mode, based on its type.
        /// </summary>
        public void start()
        {
            while (true)
            {
                  Thread.Sleep(1);
            }
        }

        /// <summary>
        /// Check if there is data available.
        /// </summary>
        /// <returns>
        /// true if data is available
        /// </returns>
        public bool dataAvailable()
        {
            return dataUpdated;
        }

        /// <summary>
        /// Get the latest pose info that is available
        /// </summary>
        /// <param name="positions">This will return the positions</param>
        /// <param name="orientations">This will return the orientations</param>
        /// <returns>True if a proper pose was available, false otherwise</returns>
        public bool getLatestPose(out Vector3[] positions, out Quaternion[] orientations)
        {
            positions = lastPosePositions;
            orientations = lastPoseOrientations;
            return true;
        }

        /// <summary>
        /// Kills the thread.
        /// </summary>
        public void killThread()
        {
            thread.Abort();
        }

        /// <summary>
        /// Sets the packet.
        /// </summary>
        /// <param name='incomingData'>
        /// _incoming data in array
        /// </param>
        public void setPacket(byte[] incomingData)
        {
            XsDataPacket dataPacket = new XsQuaternionPacket(incomingData);
            XsMvnPose pose = dataPacket.getPose();
            if (pose != null)
            {
                lastPosePositions = pose.positions;
                lastPoseOrientations = pose.orientations;
                dataUpdated = true;
            }
        }

    }//class XsStreamReaderThread
}//namespace xsens