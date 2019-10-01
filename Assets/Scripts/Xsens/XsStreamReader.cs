///<summary>
/// Xsens Stream Reader is a component which will read directly from the network.
/// It will spawn 4 threads, 1 for each actor. (MVN Studio can stream up to 4 actors)
/// 
///</summary>
///<version>
/// 0.1, 2013.03.12, Peter Heinen
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

using UnityEngine;
using System.Net.Sockets;
using System;
using System.Net;
using System.Threading;
using System.IO;

namespace xsens
{
    /// <summary>
    /// This class is responsible for setting up the connection with MVN studio.
    /// MVN Studio can stream up to 4 actors, therefore we have 1 thread for each (4 total).
    /// It also reads which datapacket it should create, and is responsible for always having the last correct pose ready to read for Unity3D
    /// </summary>
    public class XsStreamReader : MonoBehaviour
    {
        public int listenPort = 9763;		//default port in MVN Studio
        private const int NUM_STREAMS = 4;      //number of streams from MVN

        private UdpClient udpClient;
        private Thread connectionThread;
        private Vector3[] emptyPositions;
        private Quaternion[] emptyOrientations;
        private XsStreamReaderThread[] poseActors;
        private bool[] availableStreams;
        private int counter;
        private bool stopListening;
        private int[] segmentCounts;

        public enum StreamingProtocol
        {
            SPPoseEuler = 1,
            SPPoseQuaternion = 2,
            SPPosePositions = 3,
            SPTagPositionsLegacy = 4,
            SPPoseUnity3D = 5,
            SPMetaScalingLegacy = 10,
            SPMetaPropInfoLegacy = 11,
            SPMetaMoreMeta = 12,
            SPMetaScaling = 13
        };

        /// <summary>
        /// Wake this instance.
        /// </summary>
        void Awake()
        {
            //create empty list for reasons when no data arrives
            emptyPositions = new Vector3[XsMvnPose.MvnDefaultSegmentCount];
            emptyOrientations = new Quaternion[XsMvnPose.MvnDefaultSegmentCount];
            for (int i = 0; i < XsMvnPose.MvnDefaultSegmentCount; ++i)
            {
                emptyPositions[i] = Vector3.zero;
                emptyOrientations[i] = Quaternion.identity;
            }

            segmentCounts = new int[NUM_STREAMS];
            availableStreams = new bool[NUM_STREAMS];
            poseActors = new XsStreamReaderThread[NUM_STREAMS];
            for (int i = 0; i < NUM_STREAMS; ++i)
            {
                poseActors[i] = new XsStreamReaderThread();
                availableStreams[i] = false;    // this is set to true in the read thread when data is received for a stream
            }

            // Make a thread to read from the connection with MVN studio
            connectionThread = new Thread(new ThreadStart(read));
            connectionThread.Start();
        }


        /// <summary>
        /// Raises the application quit event.
        /// </summary>
        void OnApplicationQuit()
        {
            try
            {
                // shutdown 'the friendly' way
                stopListening = true;
                Debug.LogError("stopping");
                if (!connectionThread.Join(2000))
                {
                    // shutdown Schwarzenegger style
                    connectionThread.Abort();
                    if (udpClient != null)
                    {
                        udpClient.Close();
                        udpClient = null;
                    }
                }

                for (int i = 0; i < NUM_STREAMS; ++i)
                {
                    poseActors[i].killThread();
                }
            }
            catch
            {
                Debug.Log("[xsens] XsStreamReader: Something went wrong when trying to close down everything. This is not a critical error.");
            }
        }

        /// <summary>
        /// Read UDP network data.
        /// </summary>
        private void read()
        {
            stopListening = false;

            udpClient = new UdpClient(listenPort);
            IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, listenPort);

            try
            {
                while (!stopListening)
                {
                    byte[] receiveBytes = udpClient.Receive(ref groupEP);

                    string result = "";
                    result += (char)(receiveBytes[4]);
                    result += (char)(receiveBytes[5]);
                    StreamingProtocol packId = (StreamingProtocol)Int32.Parse(result);

                    if (packId == StreamingProtocol.SPPoseQuaternion)
                    {
                        if (receiveBytes.Length > 15)
                        {
                            int streamID = receiveBytes[16];
                            if (streamID >= 0 && streamID < NUM_STREAMS)
                            {
                                if (segmentCounts[streamID] == 0)
                                    segmentCounts[streamID] = receiveBytes.Length / 32;
                                poseActors[streamID].setPacket(receiveBytes);
                                availableStreams[streamID] = true;
                            }
                        }
                    }
                    else if (packId == StreamingProtocol.SPPoseUnity3D)
                    {
                        Debug.LogError("[xsens] Unity3D protocol no longer supported. Please uncheck this datapacket in the Network Streamer preferences in MVN Studio");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
                Debug.Log("[xsens] XsStreamReader terminated");
                Console.WriteLine("[xsens] XsStreamReader terminated.");
            }
            finally
            {
                udpClient.Close();
                udpClient = null;
            }
        }


        /// <summary>
        /// Get the latest pose by the actorId
        /// </summary>
        /// <param name="actorId"> id of actor to associated with the data</param>
        /// <param name="positions">segment positions of the actor</param>
        /// <param name="orientations">segment orientations of the actor</param>
        /// <returns>true if actor has data or false </returns>
        public bool getLatestPose(int actorId, out Vector3[] positions, out Quaternion[] orientations)
        {
            if ((actorId >= 0 && actorId < NUM_STREAMS)
             && (availableStreams[actorId] == true)
             && (poseActors[actorId].dataAvailable()))
            {
                return poseActors[actorId].getLatestPose(out positions, out orientations);
            }
            else
            {
                positions = emptyPositions;
                orientations = emptyOrientations;
                return true;
            }
        }

        /// <summary>
        /// Allows the XsLiveAnimator to setup properly based on the incoming data
        /// </summary>
        /// <param name="actorId"></param>
        /// <param name="segmentCount"></param>
        /// <returns></returns>
        public bool poseEstablished(int actorId, out int segmentCount)
        {
            segmentCount = segmentCounts[actorId-1];
            if(segmentCount != 0)
            {
                return true;
            }
            return false;
        }

    }//class XsStreamReader
}//namespace xsens
