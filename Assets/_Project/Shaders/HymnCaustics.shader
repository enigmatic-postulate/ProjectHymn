// Project Hymn: caustics, the net of light that ripples across the floor under moving water (the Drowned Nave). Drawn
// on flat quads just above a floor: two drifting layers of a cellular pattern whose cell edges glow, so bright lines
// knot and unknot. Additive, fading at the quad's edges so it lies on the floor like light, not a decal.
Shader "Project Hymn/Caustics"
{
    Properties
    {
        [HDR] _Color ("Light", Color) = (0.55, 1.0, 1.05, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.8
        _Scale ("Cells per metre", Float) = 0.9
        _Speed ("Drift speed", Float) = 0.35
        _Sharpness ("Line sharpness", Range(1, 12)) = 5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Caustics"
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
                float _Intensity, _Scale, _Speed, _Sharpness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float fog : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = v.uv;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Distance to the nearest cell edge of a moving cellular pattern (small = on a bright line).
            float Edge(float2 p, float t)
            {
                float2 id = floor(p);
                float2 f = frac(p);
                float d1 = 8.0, d2 = 8.0;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 h = float2(HymnHash21(id + g), HymnHash21(id + g + 31.7));
                    float2 c = g + 0.5 + 0.4 * sin(t + 6.2831 * h) - f;
                    float d = dot(c, c);
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) { d2 = d; }
                }
                return sqrt(d2) - sqrt(d1);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 p = i.positionWS.xz * _Scale;
                float2 warp = float2(HymnNoise(p * 0.7 + t * 0.3), HymnNoise(p * 0.7 - t * 0.27 + 9.1)) * 0.6;
                float a = pow(saturate(1.0 - Edge(p + warp, t * 2.0) * 1.6), _Sharpness);
                float b = pow(saturate(1.0 - Edge(p * 1.37 - warp + 5.3, -t * 1.6) * 1.6), _Sharpness);
                float c = a * 0.7 + b * 0.5 + a * b * 0.8;
                float edge = smoothstep(0.0, 0.12, i.uv.x) * smoothstep(1.0, 0.88, i.uv.x) * smoothstep(0.0, 0.2, i.uv.y) * smoothstep(1.0, 0.8, i.uv.y);
                float3 col = _Color.rgb * c * edge * _Intensity * (1.0 + 0.2 * _HymnBeat);
                col = MixFogColor(col, float3(0, 0, 0), i.fog);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
