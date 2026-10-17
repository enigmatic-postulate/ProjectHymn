// Project Hymn: light shafts falling through deep water (the Drowned Nave). Drawn on tall quads whose UV x runs across
// the shaft and y from the top (0) down (1): soft bands of light that drift and flicker slowly, bright at the top and
// fading into the gloom. Additive, two-sided, no depth write.
Shader "Project Hymn/Shafts"
{
    Properties
    {
        [HDR] _Color ("Light", Color) = (0.6, 0.95, 1.1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.6
        _Bands ("Bands across", Float) = 5
        _Speed ("Drift speed", Float) = 0.15
        _Fade ("Fade down to", Range(0.05, 1)) = 0.9
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Shafts"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HymnCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity, _Bands, _Speed, _Fade, _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                // Bands across the shaft, slanted a little and drifting; two layers of noise so they never line up.
                float x = i.uv.x * _Bands + i.uv.y * 0.6;
                float bands = HymnNoise(float2(x + t + _Seed, _Seed * 3.1)) * 0.65 + HymnNoise(float2(x * 2.3 - t * 1.7, _Seed + 7.0)) * 0.45;
                bands = smoothstep(0.35, 1.0, bands);
                float edge = smoothstep(0.0, 0.25, i.uv.x) * smoothstep(1.0, 0.75, i.uv.x);
                float down = saturate(1.0 - i.uv.y / _Fade);
                down *= down;
                float top = smoothstep(0.0, 0.06, i.uv.y);
                float flicker = 0.85 + 0.15 * sin(_Time.y * 0.7 + _Seed * 5.0);
                float a = bands * edge * down * top * flicker * _Intensity * (1.0 + 0.15 * _HymnBeat);
                float3 col = _Color.rgb * a;
                col = MixFogColor(col, float3(0, 0, 0), i.fog);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
