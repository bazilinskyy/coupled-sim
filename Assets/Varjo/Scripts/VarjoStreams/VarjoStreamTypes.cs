// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using System;
using System.Runtime.InteropServices;

namespace Varjo
{
    internal delegate void VarjoStreamCallback(VarjoStreamFrame frame, IntPtr userdata);

    internal enum VarjoStreamType : long
    {
        DistortedColor = 1,        //!< Distorted (i.e. uncorrected) color data stream from visible light RGB camera.
        EnvironmentCubemap = 2,    //!< Lighting estimate stream as a cubemap.
    }

    internal enum VarjoCalibrationModel : long
    {
        Omnidir = 1,        //!< Omnidir calibration model.
    }

    internal enum VarjoTextureFormat : long
    {
        R8G8B8A8_SRGB = 1,
        B8G8R8A8_SRGB = 2,
        D32_FLOAT = 3,
        A8_UNORM = 4,
        YUV422 = 5,
        RGBA16_FLOAT = 6,
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct VarjoStreamFrame
    {
        [FieldOffset(0)]
        internal VarjoStreamType type; //!< Type of the stream.
        [FieldOffset(8)]
        internal long id;              //!< Id of the stream.
        [FieldOffset(16)]
        internal long frameNumber;     //!< Monotonically increasing frame number.
        [FieldOffset(24)]
        internal long channelFlags;    //!< Channels that this frame contains.
        [FieldOffset(32)]
        internal long dataFlags;       //!< Data that this frame contains.
        [FieldOffset(40)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        internal double[] hmdPose;     //!< Pose at the time when the frame was produced.
        //!< Frame data. Use 'type' to determine which element to access. 
        [FieldOffset(168)]
        internal VarjoDistortedColorData distortedColorData;
        [FieldOffset(168)]
        internal VarjoEnvironmentCubemapData environmentCubemapData;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VarjoDistortedColorData
    {
        internal long timestamp;                 //!< Timestamp at end of exposure.
        internal double ev;                      //!< EV (exposure value) at ISO100.
        internal double exposureTime;            //!< Exposure time in seconds.
        internal double whiteBalanceTemperature; //!< White balance temperature in Kelvin degrees.
        internal double whiteBalanceColorGainR;  //!< White balance R color gain.
        internal double whiteBalanceColorGainG;  //!< White balance G color gain.
        internal double whiteBalanceColorGainB;  //!< White balance B color gain.
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VarjoEnvironmentCubemapData
    {
        internal readonly long timestamp;     //!< Timestamp when the cubemap was last updated.
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VarjoBufferMetadata
    {
        internal VarjoTextureFormat textureFormat;  //!< Texture format.
        internal long bufferType;                   //!< CPU or GPU.
        internal int byteSize;                      //!< Buffer size in bytes.
        internal int rowStride;                     //!< Buffer row stride in bytes.
        internal int width;                         //!< Image width.
        internal int height;                        //!< Image height.
    }

    [StructLayout(LayoutKind.Sequential)]
    struct VarjoCameraIntrinsics
    {
        internal VarjoCalibrationModel model;       //!< Intrisics calibration model.
        internal double principalPointX;            //!< Camera principal point X.
        internal double principalPointY;            //!< Camera principal point Y.
        internal double focalLengthX;               //!< Camera focal length X.
        internal double focalLengthY;               //!< Camera focal length Y.
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        internal double[] distortionCoefficients;   //!< Intrinsics model coefficients. For omnidir: 2 radial, skew, xi, 2 tangential.
    };

    [StructLayout(LayoutKind.Sequential)]
    internal struct VarjoTexture
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        internal long[] reserved;
    }
}
