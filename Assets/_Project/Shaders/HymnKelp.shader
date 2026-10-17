// Project Hymn: kelp and weed swaying in the current (the Drowned Nave). The mesh grows up from y = 0; vertices sway
// sideways and fore-and-aft more the higher they are, each frond on its own phase (from its world position), with a
// slow surge on the beat. Coloured by height from dark roots to bright tips, lit softly by the main light, two-sided.
Shader "Project Hymn/Kelp"
{
    Properties
    {
        [HDR] _Root ("Root colour", Color) = (0.03, 0.12, 0.09, 1)
        [HDR] _Tip ("Tip colour", Color) = (0.35, 0.7, 0.35, 1)
        _Height ("Frond height (object Y)", Float) = 3
        _Sway ("Sway (metres at the tip)", Float) = 0.45
        _Speed ("Sway speed", Float) = 0.8
        [HDR] _Rim ("Rim", Color) = (0.3, 0.9, 0.8, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Kelp"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "HymnCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Root, _Tip, _Rim;
                float _Height, _Sway, _Speed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float h : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float h = saturate(v.positionOS.y / max(0.01, _Height));
                float3 root = TransformObjectToWorld(float3(0, 0, 0));
                float phase = root.x * 0.7 + root.z * 1.3;
                float t = _Time.y * _Speed + phase;
                float bend = h * h * _Sway * (1.0 + 0.25 * _HymnBeat);
                float3 p = v.positionOS.xyz;
                p.x += sin(t + h * 2.5) * bend;
                p.z += cos(t * 0.8 + h * 2.0) * bend * 0.6;
                VertexPositionInputs pi = GetVertexPositionInputs(p);
                o.positionCS = pi.positionCS;
                o.positionWS = pi.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.h = h;
                o.fog = ComputeFogFactor(pi.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light sun = GetMainLight();
                float ndl = abs(dot(n, sun.direction));
                float3 col = lerp(_Root.rgb, _Tip.rgb, i.h);
                col *= 0.45 + 0.75 * ndl * sun.color;
                col += _Rim.rgb * pow(1.0 - abs(dot(n, v)), 3.0) * i.h;
                col = MixFog(col, i.fog);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
