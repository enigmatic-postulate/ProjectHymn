// Project Hymn character and world shader (URP, Unity 6). Two looks, switched for every material at once by the
// global keyword HYMN_LOOK_CEL (set from Settings → Look by HymnLook.Apply):
//
// Valor look (default, GDD M6) — Dragon Valor-style:
// - Painted texture (_BaseMap, sharp pixels) times the colour; world-space UVs for the building blocks.
// - Lighting per vertex as on PS1 (half-Lambert main light + ambient), softened by blending the flat face normal
//   toward the smoothed normal stored in UV3. Main-light shadows are still received per pixel. No outline.
//
// Cel look (the M5 look, kept as an option):
// - Main light in hard bands, shadow side tinted, rim light, ink outline via an inverted hull pass (smoothed
//   normals in UV3 keep it closed around flat faces). The painted texture shows here too.
//
// Both: per-object tint / glow / flash (ToonTint, BeatLightPulse), emission for glowing parts, fog.
// Shadow casting and depth come from URP's Lit shader passes.
Shader "ProjectHymn/Toon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Painted Texture", 2D) = "white" {}
        [Toggle] _WorldUV ("World-space UVs (blocks)", Float) = 0
        _WorldUVScale ("World UV Scale (tiles per metre)", Float) = 0.5
        _Gouraud ("Valor: Smooth Lighting", Range(0, 1)) = 0.6
        _ValorAmbient ("Valor: Ambient", Range(0, 1.5)) = 0.75
        _ValorShadow ("Valor: Shadow Strength", Range(0, 1)) = 0.6
        _ShadowColor ("Cel: Shadow Tint", Color) = (0.62, 0.5, 0.82, 1)
        _Bands ("Cel: Light Bands", Range(2, 5)) = 3
        _BandSoftness ("Cel: Band Softness", Range(0.001, 0.2)) = 0.02
        _RimColor ("Cel: Rim Color", Color) = (1, 0.95, 0.85, 1)
        _RimStrength ("Cel: Rim Strength", Range(0, 1)) = 0.35
        _RimWidth ("Cel: Rim Width", Range(0.05, 0.9)) = 0.3
        _AmbientStrength ("Cel: Ambient Strength", Range(0, 1)) = 0.35
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _OutlineColor ("Cel: Outline Color", Color) = (0.08, 0.05, 0.14, 1)
        _OutlineWidth ("Cel: Outline Width", Range(0, 0.02)) = 0.004
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
            float4 _BaseMap_ST;
            half _WorldUV;
            float _WorldUVScale;
            half _Gouraud;
            half _ValorAmbient;
            half _ValorShadow;
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

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
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
            #pragma multi_compile _ HYMN_LOOK_CEL
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 smoothNormal : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half3 vertexLight : TEXCOORD3;   // Valor: main light, per vertex
                half3 vertexAmbient : TEXCOORD4; // Valor: ambient, per vertex
                half fogFactor : TEXCOORD5;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                float3 flatN = TransformObjectToWorldNormal(v.normalOS);
                o.normalWS = flatN;

                // Painted texture: model UVs, or world-space projection along the face's main axis (blocks).
                float2 uv = TRANSFORM_TEX(v.uv, _BaseMap);
                if (_WorldUV > 0.5)
                {
                    float3 an = abs(flatN);
                    float3 w = p.positionWS * _WorldUVScale;
                    uv = an.y >= max(an.x, an.z) ? w.xz : (an.x >= an.z ? w.zy : w.xy);
                }
                o.uv = uv;

            #if !defined(HYMN_LOOK_CEL)
                float3 smoothN = dot(v.smoothNormal.xyz, v.smoothNormal.xyz) > 0.01
                    ? TransformObjectToWorldNormal(normalize(v.smoothNormal.xyz)) : flatN;
                float3 n = normalize(lerp(flatN, smoothN, _Gouraud));
                Light l = GetMainLight();
                half wrap = saturate(dot(n, l.direction) * 0.5h + 0.5h);
                o.vertexLight = l.color * (wrap * wrap);
                o.vertexAmbient = SampleSH(n);
            #endif

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
                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half3 albedo = tex * _BaseColor.rgb * _Tint.rgb;
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 color;

            #if defined(HYMN_LOOK_CEL)
                float3 n = normalize(i.normalWS);
                half ndl = saturate(dot(n, mainLight.direction));
                half lit = ndl * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half band = Band(lit);
                half3 shadowSide = albedo * _ShadowColor.rgb;
                color = lerp(shadowSide, albedo, band) * mainLight.color;
                color += albedo * SampleSH(n) * _AmbientStrength;

                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = 1.0h - saturate(dot(n, viewDir));
                rim = smoothstep(1.0h - _RimWidth, 1.0h - _RimWidth + 0.02h, rim) * (0.35h + 0.65h * band);
                color += _RimColor.rgb * rim * _RimStrength;
            #else
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ValorShadow);
                color = albedo * (i.vertexLight * shadow + i.vertexAmbient * _ValorAmbient);
            #endif

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
            #pragma multi_compile _ HYMN_LOOK_CEL
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
                Varyings o = (Varyings)0;
            #if defined(HYMN_LOOK_CEL)
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
            #else
                // Valor look: no outline. Put the vertex outside the clip volume so nothing is drawn.
                o.positionCS = float4(0.0, 0.0, -2.0, 1.0);
            #endif
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
