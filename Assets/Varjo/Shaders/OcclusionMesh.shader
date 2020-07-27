Shader "Hidden/OcclusionMesh"
{
	Properties
	{
		_StencilMask("Stencil mask", Int) = 127
	}
	SubShader
	{
		Cull Off ZWrite On ZTest Always

		Tags {
			"RenderType" = "Opaque"
			"Queue" = "Geometry-100"
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = float4(v.vertex.xy, 1.0f, 1.0f);
				return o;
			}
			

			fixed4 frag (v2f i) : SV_Target
			{
				return fixed4(0, 0, 0, 1);
			}
			ENDCG
		}
	}
}
