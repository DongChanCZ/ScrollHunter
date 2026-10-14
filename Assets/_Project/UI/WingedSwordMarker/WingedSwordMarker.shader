Shader "ScrollHunter/Winged Sword Marker"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite On ZTest LEqual
        Pass
        {
            ZWrite Off // Outline must not occlude the metal face.

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4x4 _MarkerMVP, _MarkerRotation;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            float4 vert(appdata v):SV_POSITION
            {
                float4 p = mul(_MarkerMVP,v.vertex);
                float2 n = mul((float3x3)_MarkerRotation, v.normal).xy;
                p.xy += n * .036 * p.w;
                return p;
            }
            fixed4 frag():SV_Target { return fixed4(.055,.031,.012,1); }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4x4 _MarkerMVP, _MarkerRotation;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 color:COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos=mul(_MarkerMVP,v.vertex);
                o.normal=mul((float3x3)_MarkerRotation,v.normal);
                o.color=v.color.rgb;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal);
                if (n.z < 0) n = -n; // Both metal faces stay readable during the shallow tilt.
                float3 light=normalize(float3(-.5,.8,1));
                float diffuse=saturate(dot(n,light));
                float shine=pow(saturate(dot(n,normalize(light+float3(0,0,1)))),28);
                float rim=pow(1-abs(n.z),3);
                float3 c=i.color*(.58+.50*diffuse) + float3(1,.9,.66)*shine*.42 + i.color*rim*.14;
                return fixed4(c,1);
            }
            ENDCG
        }
    }
}
