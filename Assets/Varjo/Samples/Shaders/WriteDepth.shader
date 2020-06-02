Shader "Custom/WriteDepth"{

		SubShader{
		Tags{ "RenderType" = "Opaque" "Queue" = "Geometry-1"}

		Pass{
		ZWrite On
		ZTest LEqual
		Offset -1, -1

		CGPROGRAM
		#include "UnityCG.cginc"

		#pragma vertex vert
		#pragma fragment frag

		struct appdata {
			float4 vertex : POSITION;
		};

		struct v2f {
			float4 position : SV_POSITION;
		};

		v2f vert(appdata v) {
			v2f o;
			o.position = UnityObjectToClipPos(v.vertex);
			return o;
		}

		fixed4 frag(v2f i) : SV_TARGET{
			return 0;
		}

		ENDCG
	}
	}
}