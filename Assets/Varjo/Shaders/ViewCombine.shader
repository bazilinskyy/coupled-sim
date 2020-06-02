// Copyright 2019 Varjo Technologies Oy. All rights reserved.

Shader "Hidden/Varjo/ViewCombine"
{
	Properties
	{
		_Tex0 ("Tex0", 2D) = "white" {}
		_Tex1 ("Tex1", 2D) = "white" {}
		_Tex2 ("Tex2", 2D) = "white" {}
		_Tex3 ("Tex3", 2D) = "white" {}
	}
	
	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float2 uv0 : TEXCOORD1;
				float2 uv1 : TEXCOORD2;
				float2 uv2 : TEXCOORD3;
				float2 uv3 : TEXCOORD4;
			};

			sampler2D _Tex0;
			sampler2D _Tex1;
			sampler2D _Tex2;
			sampler2D _Tex3;

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);

				#if UNITY_UV_STARTS_AT_TOP
					v.uv.y = 1-v.uv.y;
				#endif				
				o.uv = v.uv;
				
                float ySplitInv = 25.0 / 16.0;
                float ySplitCompInv = 25.0 / 9.0;
                float ySplitRatio = 16.0 / 9.0;

				float2 uvc = float2(2.0 * v.uv.x, ySplitInv * v.uv.y);
                float2 uvf = float2(2.0 * v.uv.x, ySplitCompInv * v.uv.y);
				o.uv0 = float2(-0.0, -0.0) + uvc;
				o.uv1 = float2(-1.0, -0.0) + uvc;
                o.uv2 = float2(-0.0, -ySplitRatio) + uvf;
                o.uv3 = float2(-1.0, -ySplitRatio) + uvf;
				return o;
			}
			
			float4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv;
                float xSplit = 0.5;
                float ySplit = 16.0 / 25.0;
                if (uv.x < xSplit && uv.y < ySplit) return tex2D(_Tex0, i.uv0);
                else if (uv.y < ySplit) return tex2D(_Tex1, i.uv1);
                else if (uv.x < xSplit) return tex2D(_Tex2, i.uv2);
				else return tex2D(_Tex3, i.uv3);

				return 0..xxxx;
			}
			ENDCG
		}
	}
}
