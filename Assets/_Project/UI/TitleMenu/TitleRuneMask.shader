Shader "Hidden/ScrollHunter/TitleRuneMask"
{
    Properties { _MainTex ("Source", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MaskThreshold;
            float4 frag(v2f_img i):SV_Target
            {
                float3 c=tex2D(_MainTex,i.uv).rgb;
                float mask=smoothstep(_MaskThreshold.x,_MaskThreshold.y,c.b-c.r);
                return float4(mask,mask,mask,1);
            }
            ENDCG
        }
    }
}
