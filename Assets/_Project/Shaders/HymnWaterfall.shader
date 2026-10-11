// Project Hymn: falling water (the island's rim, the platforms' spouts, the fountain's sheets). Streaks scroll down
// a mesh whose UV y runs top (0) to bottom (1); columns of water break up into separate falls; a foam glow gathers
// toward the bottom. Transparent, two-sided, no depth write.
Shader "Project Hymn/Waterfall"
{
    Properties
    {
        [HDR] _Color ("Water", Color) = (0.55, 0.85, 1.2, 1)
        [HDR] _Highlight ("Streaks", Color) = (1.4, 1.5, 1.8, 1)
        _Alpha ("Base opacity", Range(0, 1)) = 0.18
        _StreakAlpha ("Streak opacity", Range(0, 1)) = 0.55
        _Columns ("Columns across", Float) = 40
        _Gaps ("Gaps between falls", Range(0, 1)) = 0.35
        _Speed ("Fall speed", Float) = 1.6
        _Stretch ("Streak length", Float) = 3
        _FadeTop ("Fade in at the top", Range(0.001, 0.5)) = 0.05
        _FadeBottom ("Fade out from", Range(0, 1)) = 0.7
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Waterfall"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HymnCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _Highlight;
                float _Alpha, _StreakAlpha, _Columns, _Gaps, _Speed, _Stretch, _FadeTop, _FadeBottom;
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

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;
                float c = uv.x * _Columns;
                float fall = uv.y * _Stretch - t * _Speed;
                float streak = HymnNoise(float2(c, fall)) * 0.65 + HymnNoise(float2(c * 2.3 + 5.0, fall * 1.7)) * 0.35;
                streak = smoothstep(0.35, 0.95, streak);
                float sheet = smoothstep(_Gaps, _Gaps + 0.25, HymnNoise(float2(c * 0.18, 3.7)));
                float fadeIn = smoothstep(0.0, _FadeTop, uv.y);
                float fadeOut = 1.0 - smoothstep(_FadeBottom, 1.0, uv.y);
                float foam = smoothstep(_FadeBottom - 0.15, 1.0, uv.y) * HymnFbm(float2(c * 0.5, uv.y * 6.0 - t * 2.0)) * 0.6;
                float a = ((_Alpha + streak * _StreakAlpha) * sheet + foam * sheet) * fadeIn * fadeOut;
                float3 col = lerp(_Color.rgb, _Highlight.rgb, saturate(streak + foam));
                return half4(col, saturate(a));
            }
            ENDHLSL
        }
    }
}
