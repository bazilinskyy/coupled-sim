// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Varjo
{
    class VarjoMatrixUtils
    {
        private static Matrix4x4 unityToNDC = new Matrix4x4(new Vector4(2, 0, 0, 0), new Vector4(0, 2, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(-1, -1, 0, 1));
        private static Matrix4x4 ndcToUnity = new Matrix4x4(new Vector4(0.5f, 0, 0, 0), new Vector4(0, 0.5f, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(0.5f, 0.5f, 0, 1));
        private static Matrix4x4 flipZ = new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, 1, 0, 0), new Vector4(0, 0, -1, 0), new Vector4(0, 0, 0, 1));

        public static Matrix4x4 WorldMatrixToUnity(double[] mat)
        {
            Debug.Assert(mat.Length == 16);
            return (flipZ * ConvertDoubleToFloatMatrix(mat) * flipZ);
        }

        public static Matrix4x4 ViewMatrixToUnity(double[] mat)
        {
            Debug.Assert(mat.Length == 16);
            return (flipZ * ConvertDoubleToFloatMatrix(mat) * flipZ).inverse;
        }

        public static Matrix4x4 ProjectionMatrixToUnity(double[] mat, float near, float far)
        {
            var umat = ConvertDoubleToFloatMatrix(mat);
            umat[2 * 4 + 2] = -(far + near) / (far - near);
            umat[3 * 4 + 2] = -(2.0f * far * near) / (far - near);

            var proj = ndcToUnity * umat;
            return (unityToNDC * proj);
        }

        public static Matrix4x4 ConvertDoubleToFloatMatrix(double[] mat)
        {
            Debug.Assert(mat.Length == 16);
            Matrix4x4 m = new Matrix4x4();
            for (int i = 0; i < 16; i++) m[i] = (float)mat[i];
            return m;
        }
    }
}
