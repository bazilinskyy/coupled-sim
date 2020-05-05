Shader "Hidden/FXAA3"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}

	CGINCLUDE
		#include "UnityCG.cginc"

		uniform sampler2D _MainTex;
		uniform float4 _MainTex_TexelSize;
		uniform float4 _rcpFrame;
		uniform float4 _rcpFrameOpt;

		struct v2f
		{
			float4 pos : POSITION;
			float2 uv : TEXCOORD0;
			float4 uvAux : TEXCOORD1;
		};

		v2f vert( appdata_img v )
		{
			v2f o;
			o.pos = UnityObjectToClipPos (v.vertex);
			o.uv = v.texcoord.xy;
			o.uvAux.xy = v.texcoord.xy + float2( -_MainTex_TexelSize.x, +_MainTex_TexelSize.y ) * 0.5f;
			o.uvAux.zw = v.texcoord.xy + float2( +_MainTex_TexelSize.x, -_MainTex_TexelSize.y ) * 0.5f;
			return o;
		}
	ENDCG

	// SM 3.0
	SubShader
	{
		Pass
		{
			ZTest Always Cull Off ZWrite Off
			Fog { Mode off }

			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma target 3.0

				#define FXAA_PC
				#define FXAA_HLSL_3
				#define FXAA_EARLY_EXIT 0
				#include "Fxaa3_9.cginc"

				half4 frag( v2f i ) : COLOR
				{
					return FxaaPixelShader_Quality( i.uv, i.uvAux, _MainTex, _rcpFrame.xy, _rcpFrameOpt );
				}
			ENDCG
		}
	}

	Fallback off
}
