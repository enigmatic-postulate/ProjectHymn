// Project Hymn toon shader (URP, Unity 6).
// - Main light in hard bands (default 3), shadow side tinted rather than darkened, receives main-light shadows.
// - Thin rim light, flat ambient from the scene, per-object tint / glow / flash (set by ToonTint and BeatLightPulse).
// - Ink outline via an inverted hull pass. Generated meshes store smoothed normals in UV3 so the outline stays
//   closed around flat-shaded, low-poly faces; meshes without UV3 fall back to their regular normals.
// Shadow casting and depth come from URP's Lit shader passes.
Shader "ProjectHymn/Toon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Tint", Color) = (0.62, 0.5, 0.82, 1)
        _Bands ("Light Bands", Range(2, 5)) = 3
        _BandSoftness ("Band Softness", Range(0.001, 0.2)) = 0.02
        _RimColor ("Rim Color", Color) = (1, 0.95, 0.85, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.35
        _RimWidth ("Rim Width", Range(0.05, 0.9)) = 0.3
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.35
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _OutlineColor ("Outline Color", Color) = (0.08, 0.05, 0.14, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.02)) = 0.004
        [HideInInspector] _Tint ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector][HDR] _GlowColor ("Glow", Color) = (0, 0, 0, 0)
        [HideInInspector] _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _FlashAmount ("Flash Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadowColor;
            half _Bands;
            half _BandSoftness;
            half4 _RimColor;
            half _RimStrength;
            half _RimWidth;
            half _AmbientStrength;
            half4 _EmissionColor;
            half4 _OutlineColor;
            float _OutlineWidth;
            half4 _Tint;
            half4 _GlowColor;
            half4 _FlashColor;
            half _FlashAmount;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Quantise 0..1 lighting into hard bands with a tiny soft edge (no shimmering stairs).
            half Band(half x)
            {
                half b = max(_Bands, 2.0h);
                half s = saturate(x) * (b - 0.001h);
                half level = floor(s);
                half edge = smoothstep(1.0h - _BandSoftness, 1.0h, frac(s));
                return saturate((level + edge) / (b - 1.0h));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 albedo = _BaseColor.rgb * _Tint.rgb;
                float3 n = normalize(i.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(n, mainLight.direction));
                half lit = ndl * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half band = Band(lit);

                half3 shadowSide = albedo * _ShadowColor.rgb;
                half3 color = lerp(shadowSide, albedo, band) * mainLight.color;
                color += albedo * SampleSH(n) * _AmbientStrength;

                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = 1.0h - saturate(dot(n, viewDir));
                rim = smoothstep(1.0h - _RimWidth, 1.0h - _RimWidth + 0.02h, rim) * (0.35h + 0.65h * band);
                color += _RimColor.rgb * rim * _RimStrength;

                color += _EmissionColor.rgb + _GlowColor.rgb;
                color = lerp(color, _FlashColor.rgb, _FlashAmount);
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 smoothNormal : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 nOS = dot(v.smoothNormal.xyz, v.smoothNormal.xyz) > 0.01 ? normalize(v.smoothNormal.xyz) : v.normalOS;
                float4 positionCS = TransformObjectToHClip(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(nOS);
                float3 normalCS = TransformWorldToHClipDir(normalWS);
                float2 offset = normalize(normalCS.xy + 1e-5);
                // Constant screen-space width: scale by w, correct for aspect.
                offset.x *= _ScreenParams.y / _ScreenParams.x;
                positionCS.xy += offset * _OutlineWidth * positionCS.w * 2.0;
                o.positionCS = positionCS;
                o.fogFactor = ComputeFogFactor(positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 color = lerp(_OutlineColor.rgb, _GlowColor.rgb * 0.6h, saturate(max(_GlowColor.r, max(_GlowColor.g, _GlowColor.b))));
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Universal Render Pipeline/Simple Lit"
}
