Shader "Custom/URP/GlowingRippleRing_Debug"
{
    Properties
    {
        [Header(DEBUG MODE - See What Is Rendering)]
        [KeywordEnum(Normal, UVs, Distance, RingMask, Glow, Alpha)] _DebugMode ("Debug View", Float) = 0

        [Header(Ring Colors)]
        _RingColor ("Ring Color", Color) = (0, 1, 1, 1)
        [HDR] _GlowColor ("Glow Color (HDR)", Color) = (0, 3, 3, 1)

        [Header(Ring Shape)]
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.35
        _OuterRadius ("Outer Radius", Range(0, 1)) = 0.45
        _Softness ("Edge Softness", Range(0, 0.5)) = 0.08
        _RingThickness ("Ring Thickness Boost", Range(1, 3)) = 1.5

        [Header(Glow Settings)]
        _GlowIntensity ("Glow Intensity", Range(0, 20)) = 10
        _GlowSpread ("Glow Spread", Range(0, 1)) = 0.4
        _InnerGlow ("Inner Glow", Range(0, 2)) = 0.5
        _OuterGlow ("Outer Glow", Range(0, 2)) = 0.8

        [Header(Gradient)]
        _CenterBrightness ("Center Brightness", Range(0, 2)) = 1.2
        _EdgeBrightness ("Edge Brightness", Range(0, 2)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "RippleRingGlowDebug"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float _DebugMode;
                half4 _RingColor;
                half4 _GlowColor;
                half _InnerRadius;
                half _OuterRadius;
                half _Softness;
                half _RingThickness;
                half _GlowIntensity;
                half _GlowSpread;
                half _InnerGlow;
                half _OuterGlow;
                half _CenterBrightness;
                half _EdgeBrightness;
            CBUFFER_END

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // Center UVs to [-0.5, 0.5] range
                float2 centeredUV = i.uv - 0.5;

                // Calculate distance from center
                float dist = length(centeredUV) * 2.0; // Scale to 0-1 range

                // === RING CALCULATION ===
                float ringCenter = (_InnerRadius + _OuterRadius) * 0.5;
                float ringWidth = (_OuterRadius - _InnerRadius) * 0.5 * _RingThickness;
                float distFromRing = abs(dist - ringCenter);
                float ringMask = 1.0 - smoothstep(ringWidth - _Softness, ringWidth + _Softness, distFromRing);

                // === GLOW CALCULATION ===
                float innerGlowDist = max(0, ringCenter - dist);
                float innerGlowMask = exp(-innerGlowDist / _GlowSpread) * _InnerGlow;
                float outerGlowDist = max(0, dist - ringCenter);
                float outerGlowMask = exp(-outerGlowDist / _GlowSpread) * _OuterGlow;
                float glowMask = (innerGlowMask + outerGlowMask) * _GlowIntensity;

                // === RADIAL GRADIENT ===
                float radialGradient = 1.0 - smoothstep(0, 0.7, dist);
                radialGradient = lerp(_EdgeBrightness, _CenterBrightness, radialGradient);

                // === DEBUG MODES ===
                if (_DebugMode < 0.5) // Normal
                {
                    half3 ringColorFinal = _RingColor.rgb * ringMask * radialGradient;
                    half3 glowFinal = _GlowColor.rgb * glowMask;
                    half3 finalColor = ringColorFinal + glowFinal;

                    half alpha = ringMask * _RingColor.a;
                    alpha += glowMask * 0.5;
                    alpha *= smoothstep(1.0, 0.7, dist);
                    alpha *= i.color.a;
                    alpha = saturate(alpha);

                    finalColor *= i.color.rgb;
                    return half4(finalColor, alpha);
                }
                else if (_DebugMode < 1.5) // UVs
                {
                    return half4(i.uv.x, i.uv.y, 0, 1);
                }
                else if (_DebugMode < 2.5) // Distance
                {
                    return half4(dist, dist, dist, 1);
                }
                else if (_DebugMode < 3.5) // Ring Mask
                {
                    return half4(ringMask, ringMask, ringMask, 1);
                }
                else if (_DebugMode < 4.5) // Glow
                {
                    float normalizedGlow = saturate(glowMask / 10.0);
                    return half4(normalizedGlow, normalizedGlow, normalizedGlow, 1);
                }
                else // Alpha
                {
                    half alpha = ringMask * _RingColor.a;
                    alpha += glowMask * 0.5;
                    alpha *= smoothstep(1.0, 0.7, dist);
                    alpha *= i.color.a;
                    alpha = saturate(alpha);
                    return half4(alpha, alpha, alpha, 1);
                }
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
