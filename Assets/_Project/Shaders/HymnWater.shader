// Project Hymn: the spring's pool. Wind ripples (summed sines in world XZ) bend a fake reflection of the night sky
// (horizon violet to deep navy, a band of aurora green), rings spread from the fountain, and star glints wink on the
// surface. Opaque and unlit.
Shader "Project Hymn/Water"
{
    Properties
    {
        _DeepColor ("Deep", Color) = (0.03, 0.06, 0.16, 1)
        _ShallowColor ("Shallow", Color) = (0.12, 0.28, 0.42, 1)
        [HDR] _SkyColor ("Reflected sky", Color) = (0.32, 0.22, 0.62, 1)
        [HDR] _AuroraColor ("Reflected aurora", Color) = (0.25, 1.1, 0.75, 1)
        _Reflect ("Reflection", Range(0, 1.5)) = 0.9
        _Ripple ("Ripple strength", Range(0, 1)) = 0.25
        _RingCenter ("Ring centre (world XZ)", Vector) = (0, 5, 0, 0)
        _Glints ("Glints", Range(0, 3)) = 1.2
        _RingStrength ("Fountain rings", Range(0, 1)) = 1
        _WaveScale ("Wave scale (bigger = broader waves)", Float) = 1
        _WaveSpeed ("Wave speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HymnCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor, _ShallowColor, _SkyColor, _AuroraColor, _RingCenter;
                float _Reflect, _Ripple, _Glints, _RingStrength, _WaveScale, _WaveSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float fog : TEXCOORD2;
            };

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

            // Height slope of the ripples at p (d/dx, d/dz).
            float2 Slope(float2 p, float t)
            {
                float2 s = float2(0.0, 0.0);
                float2 d0 = float2(0.8, 0.6), d1 = float2(-0.5, 0.86), d2 = float2(0.2, -0.98);
                s += d0 * cos(dot(p, d0) * 2.3 + t * 1.3) * 0.5;
                s += d1 * cos(dot(p, d1) * 3.7 - t * 1.9) * 0.3;
                s += d2 * cos(dot(p, d2) * 6.1 + t * 2.6) * 0.15;
                float2 r = p - _RingCenter.xy;
                float dist = length(r) + 0.0001;
                s += (r / dist) * cos(dist * 5.0 - t * 2.4) * 0.6 * exp(-dist * 0.35) * _RingStrength;
                return s;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _WaveSpeed;
                float2 p = i.positionWS.xz / max(0.05, _WaveScale);
                float2 slope = Slope(p, t) * _Ripple;
                float3 n = normalize(float3(-slope.x, 1.0, -slope.y));
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float fres = 0.04 + 0.96 * pow(1.0 - saturate(dot(n, v)), 4.0);

                float3 r = reflect(-v, n);
                float up = saturate(r.y);
                float3 sky = lerp(_SkyColor.rgb, _SkyColor.rgb * 0.25, up);
                float auroraBand = exp(-pow((up - 0.35 - 0.08 * sin(r.x * 4.0 + t * 0.1)) / 0.07, 2.0));
                sky += _AuroraColor.rgb * auroraBand * (0.5 + 0.5 * sin(r.x * 60.0 + t * 0.4));

                float depthLook = saturate(dot(n, v));
                float3 body = lerp(_DeepColor.rgb, _ShallowColor.rgb, depthLook * 0.6 + 0.2 * HymnNoise(p * 0.8 + t * 0.05));
                float3 col = lerp(body, sky, saturate(fres * _Reflect + 0.15));

                float glint = HymnStars(p * 0.9 + slope * 1.5, 7.0, 0.12, t * 2.5);
                col += glint * _Glints * float3(0.9, 0.85, 1.0) * (0.3 + fres);

                col = MixFog(col, i.fog);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
