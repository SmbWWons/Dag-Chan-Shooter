Shader "DagChan/WIP World Aligned"
{
    Properties
    {
        [MainTexture] _BaseMap("WIP texture", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _TileSize("Texture tile size (world meters)", Float) = 4
        [HideInInspector] _Cutoff("Alpha cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _TileSize;
                half _Cutoff;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half fog:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            // Вычисляет положение вершины и нормаль в мировом пространстве и положение на экране.
            // Передаёт данные GPU instancing, стереорендера и коэффициент тумана фрагментной функции.
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            // По основной оси нормали выбирает плоскость мировой проекции текстуры и делит координаты на размер плитки.
            // Это сохраняет размер надписи WIP при масштабировании стен.
            // Затем учитывает цвет, основной свет, тени, окружающее освещение и туман.
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normal=normalize(input.normalWS);
                half3 axis=abs(normal);
                float2 uv;
                // Project in world meters. Opposite sides keep the lettering readable.
                if(axis.y>axis.x && axis.y>axis.z) uv=float2(input.positionWS.x,input.positionWS.z*sign(normal.y));
                else if(axis.x>axis.z) uv=float2(input.positionWS.z*sign(normal.x),input.positionWS.y);
                else uv=float2(-input.positionWS.x*sign(normal.z),input.positionWS.y);
                uv/=max(_TileSize,.01);
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb*_BaseColor.rgb;
                Light mainLight=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse=saturate(dot(normal,mainLight.direction));
                half3 illumination=max(SampleSH(normal),half3(.18,.18,.18))+mainLight.color*(.2+.8*diffuse)*mainLight.shadowAttenuation;
                half3 color=albedo*illumination;
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
