// Project Hymn: the sky behind every stage (Starlit Spring first, then the whole run in VS-4). A three-colour
// gradient, three layers of twinkling stars, a slow nebula, an aurora curtain, drifting cumulus clouds lit from a
// sun, and the sun's glow. Each stage turns layers on or off with their strengths (day: clouds and sun, no stars;
// night: stars and aurora). Drawn on a big quad behind the stage; unlit, no depth write.
Shader "Project Hymn/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.004, 0.006, 0.025, 1)
        _MidColor ("Middle", Color) = (0.018, 0.016, 0.06, 1)
        _HorizonColor ("Horizon", Color) = (0.11, 0.04, 0.17, 1)
        [HDR] _AuroraLow ("Aurora, lower edge", Color) = (0.25, 1.6, 0.9, 1)
        [HDR] _AuroraHigh ("Aurora, upper", Color) = (1.2, 0.35, 1.1, 1)
        _AuroraStrength ("Aurora strength", Range(0, 2)) = 1.1
        _AuroraHeight ("Aurora height (0-1)", Range(0, 1)) = 0.66
        [HDR] _NebulaColor ("Nebula", Color) = (0.55, 0.18, 0.75, 1)
        _NebulaStrength ("Nebula strength", Range(0, 1)) = 0.35
        _StarStrength ("Stars", Range(0, 3)) = 1.4
        _Aspect ("Quad width / height", Float) = 2.2
        [HDR] _CloudColor ("Cloud, lit", Color) = (1, 0.92, 0.85, 1)
        _CloudShade ("Cloud, shade", Color) = (0.55, 0.6, 0.8, 1)
        _CloudStrength ("Clouds", Range(0, 1)) = 0
        _CloudCover ("Cloud cover", Range(0, 1)) = 0.45
        _CloudHeight ("Cloud band centre (0-1)", Range(0, 1)) = 0.45
        _CloudSpeed ("Cloud drift", Float) = 0.004
        _SunPos ("Sun position (uv)", Vector) = (0.8, 0.35, 0, 0)
        [HDR] _SunColor ("Sun", Color) = (2.5, 1.8, 1.1, 1)
        _SunSize ("Sun disc size", Range(0, 0.2)) = 0.03
        _SunGlow ("Sun glow", Range(0, 2)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" }
        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HymnCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor, _MidColor, _HorizonColor;
                float4 _AuroraLow, _AuroraHigh, _NebulaColor;
                float _AuroraStrength, _AuroraHeight, _NebulaStrength, _StarStrength, _Aspect;
                float4 _CloudColor, _CloudShade, _SunPos, _SunColor;
                float _CloudStrength, _CloudCover, _CloudHeight, _CloudSpeed, _SunSize, _SunGlow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float Aurora(float2 uv, float t, out float upper)
            {
                float x = uv.x;
                float edge = _AuroraHeight + 0.09 * sin(x * 3.1 + t * 0.07) + 0.045 * sin(x * 7.3 - t * 0.11)
                           + 0.03 * (HymnFbm(float2(x * 4.0, t * 0.04)) - 0.5);
                float d = uv.y - edge;
                float lower = exp(-(d * d) / 0.0012);                       // the bright hem
                upper = d > 0.0 ? exp(-d * 8.0) : exp(d * 60.0);             // the curtain rising from it
                float rays = 0.5 + 0.5 * sin(x * 95.0 + 14.0 * HymnFbm(float2(x * 4.0, t * 0.06)) + t * 0.5);
                rays = lerp(0.55, 1.0, rays * rays);
                float fold = 0.6 + 0.4 * HymnNoise(float2(x * 9.0 + t * 0.1, 0.5));
                float gaps = smoothstep(0.25, 0.55, HymnFbm(float2(x * 2.2 + t * 0.02, 1.3)));   // it comes and goes along the sky
                return (lower * 0.7 + upper * 0.6) * rays * fold * gaps;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;
                float2 suv = float2(uv.x * _Aspect, uv.y);

                // Gradient: horizon glow at the bottom, navy in the middle, near black at the top.
                float3 col = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.45, uv.y));
                col = lerp(col, _TopColor.rgb, smoothstep(0.45, 1.0, uv.y));

                // Nebula: a soft diagonal band of violet cloud.
                float band = exp(-pow((uv.y - 0.35 - uv.x * 0.25) / 0.22, 2.0));
                float neb = HymnFbm(suv * 3.0 + float2(t * 0.004, 0.0));
                col += _NebulaColor.rgb * _NebulaStrength * band * smoothstep(0.35, 0.85, neb);

                // Stars, three sizes, fewer near the horizon glow.
                float s = HymnStars(suv, 90.0, 0.10, t) + HymnStars(suv + 3.3, 45.0, 0.07, t * 0.8) * 1.3
                        + HymnStars(suv + 7.7, 18.0, 0.05, t * 0.6) * 1.8;
                col += s * _StarStrength * smoothstep(0.05, 0.3, uv.y) * float3(0.95, 0.95, 1.0);

                // Aurora.
                float upper;
                float a = Aurora(uv, t, upper) * _AuroraStrength;
                float3 aCol = lerp(_AuroraLow.rgb, _AuroraHigh.rgb, saturate(upper * 0.2 + (uv.y - _AuroraHeight) * 3.0));
                col += aCol * a;

                // Sun: a disc with a wide halo (day and dusk).
                float2 sd = (uv - _SunPos.xy) * float2(_Aspect, 1.0);
                float sdist = length(sd);
                col += _SunColor.rgb * _SunGlow * (smoothstep(_SunSize, _SunSize * 0.7, sdist) * 2.0 + exp(-sdist * 6.0) * 0.6 + exp(-sdist * 1.6) * 0.25);

                // Clouds: a band of fbm cumulus, lit on the side toward the sun, drifting slowly.
                if (_CloudStrength > 0.001)
                {
                    float2 cuv = float2(suv.x * 1.6 + t * _CloudSpeed * 10.0, uv.y * 3.2);
                    float band2 = exp(-pow((uv.y - _CloudHeight) / 0.22, 2.0));
                    float d1 = HymnFbm(cuv * 1.3);
                    float d2 = HymnFbm(cuv * 1.3 + normalize(_SunPos.xy - uv + 0.0001) * 0.05);
                    float dens = smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.12, d1) * band2;
                    float lit = saturate((d1 - d2) * 9.0 + 0.5);
                    float3 cc = lerp(_CloudShade.rgb, _CloudColor.rgb, lit);
                    cc += _SunColor.rgb * 0.15 * exp(-sdist * 3.0);                 // silver lining near the sun
                    col = lerp(col, cc, dens * _CloudStrength);
                }

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
