///<summary>
/// This class will read the data from the stream and convert it to valid Quaternions.
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
using System.IO;

namespace xsens
{
    /// <summary>
    /// Parse the data from the stream as quaternions.
    /// </summary>
    class XsQuaternionPacket : XsDataPacket
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="xsens.XsQuaternionPacket"/> class.
        /// </summary>
        /// <param name='readData'>
        /// Create the packet from this data.
        /// </param>
        public XsQuaternionPacket(byte[] readData)
            : base(readData)
        {

        }

        protected override double[] parsePayload(BinaryReader br, int segmentCount)
        {
            //  double[] payloadData = new double[XsMvnPose.MvnSegmentCount * 8];
            double[] payloadData = new double[segmentCount * 8];
            int startPoint = 0;
            int segmentCounter = 0;



           // while (segmentCounter != XsMvnPose.MvnSegmentCount)
                while (segmentCounter != segmentCount)
                {
                var newBytes = br.ReadBytes(4);
                payloadData[startPoint + 0] = convert32BitInt(newBytes);     // Segment ID

                payloadData[startPoint + 1] = convert32BitFloat(br.ReadBytes(4));   // X position
                payloadData[startPoint + 2] = convert32BitFloat(br.ReadBytes(4));   // Y Position
                payloadData[startPoint + 3] = convert32BitFloat(br.ReadBytes(4));   // Z Position

                payloadData[startPoint + 4] = convert32BitFloat(br.ReadBytes(4));   // Quaternion W
                payloadData[startPoint + 5] = convert32BitFloat(br.ReadBytes(4));   // Quaternion X
                payloadData[startPoint + 6] = convert32BitFloat(br.ReadBytes(4));   // Quaternion Y 
                payloadData[startPoint + 7] = convert32BitFloat(br.ReadBytes(4));   // Quaternion Z	
          

                startPoint += 8;
                segmentCounter++;
            }

            return payloadData;
        }


    }//class XsQuaternionPacket
}//namespace xsens