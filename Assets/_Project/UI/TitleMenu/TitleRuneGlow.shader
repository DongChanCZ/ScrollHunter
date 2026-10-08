Shader "ScrollHunter/UI/TitleRuneGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Background", 2D) = "white" {}
        _RuneLight ("Rune light mask", 2D) = "black" {}
        _SpillStrength ("Surface illumination", Range(0,16)) = 8
        _SpillColor ("Reflected blue light", Color) = (0.3,0.65,1,1)
        _Color ("Tint", Color) = (1,1,1,1)
        _RuneColor ("Rune light", Color) = (0.12,0.62,1,1)
        _Pulse ("Glow", Range(0,1)) = 0
        _GlowStrength ("Glow strength", Range(0,4)) = 1.6
        _GlowRadius ("Halo pixels", Range(0,12)) = 3
        _MaskThreshold ("Blue ink threshold", Vector) = (0.002,0.012,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            sampler2D _MainTex, _RuneLight;
            float4 _MainTex_TexelSize, _Color, _RuneColor, _MaskThreshold, _SpillColor;
            float _Pulse, _GlowStrength, _GlowRadius, _SpillStrength;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o; }
            float ink(float2 uv) { float3 c=tex2D(_MainTex,uv).rgb;return smoothstep(_MaskThreshold.x,_MaskThreshold.y,c.b-c.r); }
            float4 frag(v2f i):SV_Target
            {
                float4 c=tex2D(_MainTex,i.uv);
                float mask=ink(i.uv);
                float2 d=_MainTex_TexelSize.xy*_GlowRadius;
                float halo=(ink(i.uv+float2(d.x,0))+ink(i.uv-float2(d.x,0))+ink(i.uv+float2(0,d.y))+ink(i.uv-float2(0,d.y))
                    +ink(i.uv+d)+ink(i.uv-d)+ink(i.uv+float2(d.x,-d.y))+ink(i.uv+float2(-d.x,d.y)))/8;
                float localLight=tex2Dlod(_RuneLight,float4(i.uv,0,4.5)).r;
                float broadLight=tex2Dlod(_RuneLight,float4(i.uv,0,6.5)).r;
                float spill=(localLight*.35+broadLight*.65)*_SpillStrength*_Pulse;
                c.rgb+=_SpillColor.rgb*spill*(.14+c.rgb*1.5);
                c.rgb+=_RuneColor.rgb*_Pulse*_GlowStrength*(mask+halo*.4);
                return c*i.color;
            }
            ENDCG
        }
    }
}
