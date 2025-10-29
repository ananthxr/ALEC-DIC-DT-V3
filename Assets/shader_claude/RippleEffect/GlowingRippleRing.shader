Shader "Custom/URP/GlowingRippleRing"
{
    Properties
    {
        [Header(Ring Colors)]
        _RingColor ("Ring Color", Color) = (0, 1, 1, 1)
        [HDR] _GlowColor ("Glow Color (HDR)", Color) = (0, 3, 3, 1)

        [Header(Ring Shape)]
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.35
        _OuterRadius ("Outer Radius", Range(0, 1)) = 0.45
        _Softness ("Edge Softness", Range(0, 0.5)) = 0.05
        _RingThickness ("Ring Thickness Boost", Range(1, 3)) = 1.5

        [Header(Glow Settings)]
        _GlowIntensity ("Glow Intensity", Range(0, 20)) = 8
        _GlowSpread ("Glow Spread", Range(0, 1)) = 0.3
        _InnerGlow ("Inner Glow", Range(0, 2)) = 0.5
        _OuterGlow ("Outer Glow", Range(0, 2)) = 0.8

        [Header(Gradient)]
        _CenterBrightness ("Center Brightness", Range(0, 2)) = 1.2
        _EdgeBrightness ("Edge Brightness", Range(0, 2)) = 0.8

        [Header(Animation)]
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 0
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0

        [Header(Advanced)]
        _DistortionAmount ("Distortion Amount", Range(0, 0.1)) = 0.01
        _DistortionSpeed ("Distortion Speed", Range(0, 5)) = 1
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
            Name "RippleRingGlow"

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

                half _PulseSpeed;
                half _PulseIntensity;

                half _DistortionAmount;
                half _DistortionSpeed;
            CBUFFER_END

            // Simple noise for subtle distortion
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float noise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

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

                // Add subtle animated distortion
                float noiseVal = noise(i.uv * 10.0 + _Time.y * _DistortionSpeed);
                float2 distortion = (noiseVal - 0.5) * _DistortionAmount;
                centeredUV += distortion;

                // Calculate distance from center
                float dist = length(centeredUV) * 2.0; // Scale to 0-1 range

                // Animated pulse
                float pulse = 1.0;
                if (_PulseSpeed > 0.001)
                {
                    pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                    pulse = lerp(1.0, pulse, _PulseIntensity);
                }

                // Animate ring radius with pulse
                float animatedInner = _InnerRadius * pulse;
                float animatedOuter = _OuterRadius * pulse;

                // === CORE RING CALCULATION ===
                // Distance from ideal ring position (center between inner and outer)
                float ringCenter = (animatedInner + animatedOuter) * 0.5;
                float ringWidth = (animatedOuter - animatedInner) * 0.5 * _RingThickness;

                // Distance from ring centerline
                float distFromRing = abs(dist - ringCenter);

                // Sharp ring mask with soft edges
                float ringMask = 1.0 - smoothstep(ringWidth - _Softness, ringWidth + _Softness, distFromRing);

                // === GLOW LAYERS ===
                // Inner glow (inside the ring)
                float innerGlowDist = max(0, ringCenter - dist);
                float innerGlowMask = exp(-innerGlowDist / _GlowSpread) * _InnerGlow;

                // Outer glow (outside the ring)
                float outerGlowDist = max(0, dist - ringCenter);
                float outerGlowMask = exp(-outerGlowDist / _GlowSpread) * _OuterGlow;

                // Combined glow
                float glowMask = (innerGlowMask + outerGlowMask) * _GlowIntensity;

                // === RADIAL GRADIENT ===
                // Brighter at center, dimmer at edges of screen
                float radialGradient = 1.0 - smoothstep(0, 0.7, dist);
                radialGradient = lerp(_EdgeBrightness, _CenterBrightness, radialGradient);

                // === COMBINE EFFECTS ===
                // Base ring color
                half3 ringColorFinal = _RingColor.rgb * ringMask * radialGradient;

                // HDR glow (creates bloom in post-processing)
                half3 glowFinal = _GlowColor.rgb * glowMask * pulse;

                // Combine additively for glow effect
                half3 finalColor = ringColorFinal + glowFinal;

                // === ALPHA CALCULATION ===
                // Ring contributes to alpha
                half alpha = ringMask * _RingColor.a;

                // Glow contributes to alpha (makes glow visible)
                alpha += glowMask * 0.5;

                // Fade out at extreme edges
                alpha *= smoothstep(1.0, 0.7, dist);

                // Multiply by vertex color alpha (for scripted fading)
                alpha *= i.color.a;

                // Clamp alpha
                alpha = saturate(alpha);

                // Apply vertex color tint
                finalColor *= i.color.rgb;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
