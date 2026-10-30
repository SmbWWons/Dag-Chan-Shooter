Shader "DagChan/Monitor Pause Composite"
{
    Properties
    {
        _MainTex("Frozen game",2D)="black"{}
        _MenuTex("Monitor artwork",2D)="black"{}
        _Progress("Zoom out",Range(0,1))=0
        _DecodeCapture("Decode captured frame",Float)=0
    }
    SubShader
    {
        Tags {"Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Cull Off ZWrite Off ZTest Always Blend One Zero
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            sampler2D _MainTex,_MenuTex;float _Progress,_DecodeCapture;
            // Переводит вершины UI-геометрии в экранные координаты и передаёт UV.
            // Подготавливает данные для сборки изображения меню во фрагментной функции.
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            // Читает цвет замершего игрового кадра по UV.
            // При включённом _DecodeCapture переводит цвет из гамма-пространства в линейное, чтобы снимок не выглядел слишком светлым.
            float3 ReadGame(float2 uv)
            {
                float3 c=tex2D(_MainTex,uv).rgb;
                return lerp(c,GammaToLinearSpace(c),_DecodeCapture);
            }
            // По _Progress рассчитывает отдаление изображения с учётом пропорций экрана.
            // Заменяет зелёные области мониторов тёмными экранами, помещает снимок игры в измеренные границы центрального монитора и плавно смешивает результат с полноэкранной игрой.
            fixed4 frag(v2f i):SV_Target
            {
                float p=saturate(_Progress);
                float aspect=_ScreenParams.x/_ScreenParams.y;
                float2 fit=float2(max(aspect/(16.0/9.0),1),max((16.0/9.0)/aspect,1));
                float2 center=lerp(float2(.50234,.73611),float2(.5,.5),p);
                float zoom=lerp(3.55,1,p);
                float2 uv=(i.uv-.5)*fit/zoom+center;
                float inside=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
                float3 art=tex2D(_MenuTex,saturate(uv)).rgb;
                // Remove the green screens while keeping the original frames and desk.
                float green=art.g-max(art.r,art.b);
                float key=smoothstep(.055,.14,green)*step(.55,uv.y)*step(uv.y,.9);
                float3 darkScreen=lerp(float3(.009,.015,.021),float3(.025,.038,.05),saturate((uv.y-.57)/.32));
                art=lerp(art,darkScreen,key);
                // Measured corners of the central green screen in the 1920x1080 artwork.
                float y=(uv.y-(1-441.0/1080.0))/((441.0-129.0)/1080.0);
                float left=lerp(697.0/1920.0,693.0/1920.0,y);
                float right=lerp(1232.0/1920.0,1236.0/1920.0,y);
                float x=(uv.x-left)/(right-left);
                float screen=step(0,x)*step(x,1)*step(0,y)*step(y,1);
                float3 game=ReadGame(saturate(float2(x,y)));
                art=lerp(art,game,screen)*inside;
                // The first and last frame exactly match the full-screen game.
                return fixed4(lerp(ReadGame(i.uv),art,smoothstep(0,.12,p)),1);
            }
            ENDCG
        }
    }
}
