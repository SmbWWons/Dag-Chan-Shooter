Shader "Smb/Gelatin Bear URP"
{
    Properties
    {
        _GelColor ("Gel color", Color) = (0.25, 0.90, 0.05, 1)
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal strength", Range(0, 2)) = 1
        [Toggle] _UseRoughnessMap ("Use Roughness Map", Float) = 0
        _RoughnessMap ("Roughness Map (grayscale, linear)", 2D) = "white" {}
        _Roughness ("Roughness without map", Range(0.03, 1)) = 0.18
        _IOR ("Index of refraction", Range(1, 1.65)) = 1.38
        _RefractionStrength ("Refraction strength", Range(0, 0.15)) = 0.04
        _Density ("Color absorption density", Range(0, 8)) = 2.2
        _Thickness ("Approximate center thickness", Range(0, 4)) = 1.2
        _EdgeThickness ("Approximate edge thickness", Range(0, 1)) = 0.08
        _BodyLight ("Body lighting", Range(0, 0.5)) = 0.06
        _BackLight ("Backlight scatter approximation", Range(0, 1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "GelatinForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite Off
            Blend One Zero

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _GelColor;
                float4 _NormalMap_ST;
                float4 _RoughnessMap_ST;
                float _NormalStrength, _UseRoughnessMap, _Roughness;
                float _IOR, _RefractionStrength, _Density;
                float _Thickness, _EdgeThickness, _BodyLight, _BackLight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w * GetOddNegativeScale());
                o.uv = v.uv;
                return o;
            }

            float3 GelLight(float3 N, float3 V, float3 L, float3 lightColor, float attenuation,
                            float3 tint, float roughness, float f0)
            {
                float nl = saturate(dot(N, L));
                float nv = max(saturate(dot(N, V)), 0.001);
                float3 H = normalize(L + V + 0.00001);
                float nh = saturate(dot(N, H));
                float vh = saturate(dot(V, H));
                float a = max(roughness * roughness, 0.002);
                float a2 = a * a;
                float d = nh * nh * (a2 - 1.0) + 1.0;
                float D = a2 / max(PI * d * d, 0.000001);
                float k = (roughness + 1.0) * (roughness + 1.0) / 8.0;
                float G = (nv / (nv * (1.0 - k) + k)) * (nl / max(nl * (1.0 - k) + k, 0.001));
                float F = f0 + (1.0 - f0) * pow(1.0 - vh, 5.0);
                float spec = D * G * F / max(4.0 * nv * nl, 0.0001);
                return (tint * _BodyLight * (1.0 - F) / PI + spec) * lightColor * nl * attenuation;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 Ng = normalize(i.normalWS);
                float3 T = normalize(i.tangentWS.xyz);
                float3 B = normalize(cross(Ng, T)) * i.tangentWS.w;
                float2 normalUV = i.uv * _NormalMap_ST.xy + _NormalMap_ST.zw;
                float3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV), _NormalStrength);
                float3 N = normalize(T * nt.x + B * nt.y + Ng * nt.z);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float2 roughUV = i.uv * _RoughnessMap_ST.xy + _RoughnessMap_ST.zw;
                float mapR = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, roughUV).r;
                float roughness = clamp(lerp(_Roughness, mapR, saturate(_UseRoughnessMap)), 0.03, 1.0);
                float f0 = (_IOR - 1.0) / (_IOR + 1.0);
                f0 *= f0;
                float fresnel = f0 + (1.0 - f0) * pow(1.0 - saturate(dot(N, V)), 5.0);

                // A controllable approximation, not a measured mesh thickness.
                float thickness = lerp(_EdgeThickness, _Thickness, saturate(dot(Ng, V)));
                float3 tint = saturate(_GelColor.rgb);
                float3 transmission = exp(-(1.0 - tint) * _Density * max(thickness, 0.0));
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float3 viewN = TransformWorldToViewDir(N, true);
                float2 offset = viewN.xy * _RefractionStrength * (_IOR - 1.0);
                offset.x *= _ScreenParams.y / max(_ScreenParams.x, 1.0);
                float2 refractUV = clamp(screenUV + offset, 0.001, 0.999);
                float3 color = SampleSceneColor(refractUV) * transmission * (1.0 - fresnel);

                // One reflection probe; avoids expensive environment convolution in this example.
                float3 R = reflect(-V, N);
                half4 encoded = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, R, roughness * 6.0);
                color += DecodeHDREnvironment(encoded, unity_SpecCube0_HDR) * fresnel;
                color += SampleSH(N) * tint * _BodyLight;
                Light main = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                color += GelLight(N, V, main.direction, main.color,
                                  main.distanceAttenuation * main.shadowAttenuation, tint, roughness, f0);
                color += tint * main.color * _BackLight * pow(saturate(dot(-main.direction, V)), 3.0);

                // Additional pixel lights for URP Forward. Use Forward for predictable results.
                #if defined(_ADDITIONAL_LIGHTS)
                    uint count = GetAdditionalLightsCount();
                    for (uint j = 0u; j < count; ++j)
                    {
                        Light extra = GetAdditionalLight(j, i.positionWS);
                        color += GelLight(N, V, extra.direction, extra.color,
                                          extra.distanceAttenuation * extra.shadowAttenuation,
                                          tint, roughness, f0);
                    }
                #endif
                // The captured background is already fogged; avoid applying fog to it twice.
                return half4(max(color, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
