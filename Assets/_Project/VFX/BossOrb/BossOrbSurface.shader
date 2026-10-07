// 보스 오브 구체 표면(10/7, 10 A43 연출). 조명 없는 URP 셰이더로 고정 광원 방향의 명암·가장자리 발광·흐르는 무늬를 그린다.
// 중심체는 불투명(깊이 기록)으로 그려 바깥 기운 입자가 구체 앞뒤를 지나가게 하고, 껍질은 같은 셰이더를 가산으로 써서 가장자리만 빛낸다.
// _FlowTime·_Reveal·_Glow는 BossOrbVisual/OrbBreakVfx가 게임 시간으로 넘긴다(정지·배속을 따름).
Shader "ScrollHunter/BossOrbSurface"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.03, 0.0, 0.06, 1)
        _LitColor ("Lit Color", Color) = (0.2, 0.06, 0.32, 1)
        _LightDir ("Fake Light Dir (world)", Vector) = (0.35, 0.75, -0.55, 0)
        _LightPower ("Light Falloff", Range(0.3, 4)) = 1.4
        _CenterColor ("Center Tint (a = amount)", Color) = (0, 0, 0, 0)
        _CenterPower ("Center Falloff", Range(0.5, 8)) = 3
        _RimColor ("Rim Color", Color) = (0.7, 0.3, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0, 4)) = 1.2
        _FlowTex ("Flow Noise (tileable)", 2D) = "gray" {}
        _FlowColor ("Flow Color", Color) = (0.55, 0.2, 0.9, 1)
        _FlowIntensity ("Flow Intensity", Range(0, 3)) = 0.8
        _FlowSharpness ("Flow Vein Sharpness", Range(1, 12)) = 4
        _FlowSpeed ("Flow Speed (uv1 xy, uv2 zw)", Vector) = (0.05, 0.015, -0.035, 0.02)
        _FlowTiling ("Flow Tiling", Vector) = (2, 1, 3.1, 1.6)
        _Glow ("Glow Multiplier", Range(0, 3)) = 1
        _Reveal ("Reveal (0 = energy blob, 1 = full surface)", Range(0, 1)) = 1
        _RevealColor ("Reveal Start Color", Color) = (0.5, 0.2, 0.8, 1)
        _Shell ("Shell Mode (rim only)", Range(0, 1)) = 0
        _Alpha ("Alpha", Range(0, 1)) = 1
        _FlowTime ("Flow Time (script)", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_FlowTex); SAMPLER(sampler_FlowTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _ShadowColor, _LitColor, _RimColor, _FlowColor, _RevealColor, _CenterColor;
            float4 _LightDir, _FlowSpeed, _FlowTiling, _FlowTex_ST;
            half _CenterPower, _LightPower, _RimPower, _RimIntensity, _FlowIntensity, _FlowSharpness, _Glow, _Reveal, _Shell, _Alpha;
            float _FlowTime;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "OrbForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 l = normalize(_LightDir.xyz);
                half ndl = pow(saturate(dot(n, l) * 0.5 + 0.5), _LightPower);
                half ndv = saturate(dot(n, viewDir));
                half fres = pow(1.0 - ndv, _RimPower);
                // 껍질은 중심체 실루엣 근처에서 가장 밝고 바깥 끝으로 갈수록 0이 되어, 떨어진 유리 테두리처럼 보이지 않게
                half shellFres = pow(saturate(ndv * 2.0), 1.5) * fres * 1.6;

                float t = _FlowTime;
                half n1 = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, i.uv * _FlowTiling.xy + _FlowSpeed.xy * t).r;
                half n2 = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, i.uv * _FlowTiling.zw + _FlowSpeed.zw * t).r;
                half veins = pow(saturate(1.0 - abs(n1 + n2 - 1.0) * 2.0), _FlowSharpness);   // 두 무늬가 엇갈리는 곳만 가는 줄기로 빛남
                half body = saturate(n1 * n2 * 2.0);                                            // 큰 명암 덩어리

                half3 surface = lerp(_ShadowColor.rgb, _LitColor.rgb, ndl) * (0.75 + 0.5 * body);
                surface = lerp(surface, _CenterColor.rgb, pow(ndv, _CenterPower) * _CenterColor.a);   // 파멸은 검은 중심, 순환은 청백색 중심
                surface += _FlowColor.rgb * veins * _FlowIntensity * (0.35 + 0.65 * (1.0 - fres)) * _Glow;
                surface += _RimColor.rgb * fres * _RimIntensity * _Glow;
                half3 blob = _RevealColor.rgb * (0.35 + 1.4 * fres) * _Glow;                    // 소환 초반: 무늬 없는 기운 덩어리
                half3 core = lerp(blob, surface, _Reveal);

                half3 shell = (_RimColor.rgb * shellFres * _RimIntensity + _FlowColor.rgb * veins * shellFres * _FlowIntensity * 0.5) * _Glow;
                half3 color = lerp(core, shell, _Shell) * _Alpha;
                return half4(color, _Alpha);
            }
            ENDHLSL
        }

        // 깊이 텍스처·깊이 선행 그리기용(입자 부드러운 경계와 가림이 구체를 인식하게).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V vertDepth(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half fragDepth(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
