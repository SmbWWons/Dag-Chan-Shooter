Shader "Smb/Revolver Muzzle Flash"
{
    Properties { _Intensity("Intensity",Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float4 positionOS:POSITION; float4 color:COLOR; };
            struct Varying { float4 positionCS:SV_POSITION; float4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
            float _Intensity;
            CBUFFER_END
            // Переводит вершину вспышки из локального пространства в координаты экрана.
            // Передаёт цвет вершины фрагментной функции, сохраняя градиент яркости от центра к краям.
            Varying Vert(Input v) { Varying o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.color=v.color; return o; }
            // Умножает цвет и прозрачность вершины на _Intensity.
            // Аддитивное смешивание прохода делает результат светящейся вспышкой, которая гаснет по команде RevolverMuzzleFlash.
            half4 Frag(Varying v):SV_Target { return half4(v.color.rgb*_Intensity,v.color.a*_Intensity); }
            ENDHLSL
        }
    }
}


