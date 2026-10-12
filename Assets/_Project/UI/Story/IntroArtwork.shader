Shader "ScrollHunter/UI/IntroArtwork"
{
    Properties
    {
        [PerRendererData] _MainTex ("Artwork", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Style ("Scene style", Float) = 0
        _Pulse ("Light pulse", Range(0,1)) = 0
        _GlowStrength ("Glow strength", Range(0,1)) = .45
        _Shade ("Edge shade", Range(0,1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float4 world:TEXCOORD1; };
            sampler2D _MainTex;
            float4 _Color, _ClipRect;
            float _Style, _Pulse, _GlowStrength, _Shade;
            v2f vert(appdata v) { v2f o; o.world=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            float4 frag(v2f i):SV_Target
            {
                float4 c=tex2D(_MainTex,i.uv);
                if (_Style > .5 && _Style < 1.5)
                {
                    float fire=smoothstep(.10,.32,c.r-c.b)*smoothstep(.32,.8,c.r);
                    c.rgb+=float3(1,.34,.05)*fire*_GlowStrength*(.10+.20*_Pulse);
                }
                if (_Style > 1.5 && _Style < 2.5)
                {
                    float2 d=(i.uv-float2(.48,.52))/float2(.20,.24);
                    float area=1-smoothstep(.7,1.1,length(d));
                    float ink=smoothstep(.035,.20,c.b-c.r);
                    c.rgb+=float3(.12,.58,1)*area*ink*_GlowStrength*(.25+.75*_Pulse);
                }
                float2 edge=(i.uv-.5)*2;
                c.rgb*=1-_Shade*smoothstep(.35,1.1,length(edge));
                c*=i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
