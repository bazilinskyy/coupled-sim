// The city maps' water (canals, rivers, ponds, harbours): a murky body colour, two layers of small wind ripples moving
// with the wind, and the scene's mirror image in it (PlanarWaterReflection renders it for each view; distorted by the
// ripples, stronger at grazing angles by Fresnel), else the sky's reflection probe; a glint of the sun where it is
// not in shadow; fog. URP, forward.
Shader "CoupledSim/Water"
{
    Properties
    {
        _BaseColor ("Water body colour", Color) = (0.055, 0.075, 0.06, 1)
        _BumpMap ("Ripples (normal map)", 2D) = "bump" {}
        _RippleScale ("Ripple repeats per metre", Float) = 0.4
        _RippleStrength ("Ripple strength", Range(0, 2)) = 0.55
        _RippleSpeed ("Ripple speed (m/s)", Vector) = (0.03, 0.018, -0.022, 0.026)
        _Distortion ("Reflection distortion", Range(0, 0.1)) = 0.025
        _ReflectionStrength ("Reflection strength", Range(0, 1)) = 0.9
        _Glint ("Sun glint", Range(0, 4)) = 1.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_WaterReflectionTex); SAMPLER(sampler_WaterReflectionTex);
            float _WaterReflectionOn;   // global: 1 while PlanarWaterReflection has rendered the mirror image for this view

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BumpMap_ST;
                float _RippleScale, _RippleStrength, _Distortion, _ReflectionStrength, _Glint;
                float4 _RippleSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float fog : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float3 Ripple(float2 uv)
            {
                float3 n = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv));
                return float3(n.x, n.y, n.z);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 w = i.positionWS.xz * _RippleScale;
                float3 a = Ripple(w + _Time.y * _RippleSpeed.xy * _RippleScale);
                float3 b = Ripple(w * 1.7 + float2(0.37, 0.71) + _Time.y * _RippleSpeed.zw * _RippleScale * 1.7);
                float2 slope = (a.xy + b.xy) * _RippleStrength;
                float3 N = normalize(float3(slope.x, 1.0, slope.y));
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float NdotV = saturate(dot(N, V));
                float fresnel = 0.02 + 0.98 * pow(1.0 - NdotV, 5.0);

                // the mirror image (horizontally flipped: the reflection camera's own image), else the sky probe
                float2 suv = i.screenPos.xy / i.screenPos.w;
                half3 refl;
                if (_WaterReflectionOn > 0.5)
                    refl = SAMPLE_TEXTURE2D(_WaterReflectionTex, sampler_WaterReflectionTex, float2(1.0 - suv.x, suv.y) + slope * _Distortion).rgb;
                else
                    refl = GlossyEnvironmentReflection(reflect(-V, N), 0.05, 1.0);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light sun = GetMainLight(shadowCoord);
                float lit = sun.shadowAttenuation * sun.distanceAttenuation;
                // the body: a little light scattered back from the murky water, more where the sun reaches it
                half3 ambient = SampleSH(float3(0, 1, 0));
                half3 body = _BaseColor.rgb * (ambient + sun.color * lit * saturate(sun.direction.y) * 0.6);
                half3 col = lerp(body, refl * _ReflectionStrength, saturate(fresnel * 1.6 + 0.12));
                // the sun's glint on the ripples
                float3 H = normalize(sun.direction + V);
                col += sun.color * lit * pow(saturate(dot(N, H)), 600.0) * _Glint;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; };
            float4 vert(A v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
