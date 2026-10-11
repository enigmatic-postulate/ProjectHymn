// Project Hymn: luminous things (crystals, leaves, lanterns, spire bands, the fountain star). A two-colour gradient
// along the object's height, a little of the moonlight for shape, a fresnel rim and an optional pulse on the beat.
// Colours are HDR so bloom picks them up.
Shader "Project Hymn/Glow"
{
    Properties
    {
        [HDR] _ColorA ("Bottom colour", Color) = (0.45, 0.3, 1.0, 1)
        [HDR] _ColorB ("Top colour", Color) = (1.0, 0.6, 0.95, 1)
        _GradientMin ("Gradient from (object Y)", Float) = 0
        _GradientMax ("Gradient to (object Y)", Float) = 1
        [HDR] _RimColor ("Rim colour", Color) = (1, 1, 1, 1)
        _RimPower ("Rim power", Range(0.5, 8)) = 3
        _Lit ("Moonlight share", Range(0, 1)) = 0.35
        _BeatPulse ("Pulse on the beat", Range(0, 2)) = 0.25
        _Sparkle ("Sparkle", Range(0, 2)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "HymnCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA, _ColorB, _RimColor;
                float _GradientMin, _GradientMax, _RimPower, _Lit, _BeatPulse, _Sparkle, _Cull;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float objY : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.objY = v.positionOS.y;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float g = saturate((i.objY - _GradientMin) / max(0.0001, _GradientMax - _GradientMin));
                float3 col = lerp(_ColorA.rgb, _ColorB.rgb, g);

                Light moon = GetMainLight();
                float ndl = abs(dot(n, moon.direction)) * 0.5 + saturate(dot(n, moon.direction)) * 0.5;
                col *= lerp(1.0, 0.55 + 0.65 * ndl, _Lit);

                float rim = pow(1.0 - abs(dot(n, v)), _RimPower);         // abs: petals are seen from both sides
                col += _RimColor.rgb * rim;

                float sp = HymnStars(i.positionWS.xy * 0.7 + i.positionWS.zz * 0.3, 14.0, 0.12, _Time.y * 2.0);
                col += _Sparkle * sp * _ColorB.rgb;

                col *= 1.0 + _HymnBeat * _BeatPulse;
                col = MixFog(col, i.fog);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
