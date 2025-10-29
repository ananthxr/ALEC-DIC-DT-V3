Shader "Custom/URP/AlarmIndicator"
{
    Properties
    {
        [Header(Alarm Colors)]
        _AlarmColor ("Alarm Color", Color) = (1, 0.2, 0.1, 1)
        _HotspotColor ("Hotspot Color", Color) = (1, 0.5, 0.1, 1)
        [HDR] _EmissionColor ("Emission Color (HDR)", Color) = (3, 0.5, 0.2, 1)

        [Header(Pulse Effect)]
        _PulseSpeed ("Pulse Speed (BPM)", Range(20, 240)) = 120
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.8
        _PulseSharpness ("Pulse Sharpness", Range(1, 10)) = 3

        [Header(Warning Patterns)]
        [Toggle] _EnableStripes ("Enable Warning Stripes", Float) = 1
        _StripeScale ("Stripe Scale", Range(1, 50)) = 10
        _StripeSpeed ("Stripe Scroll Speed", Range(-5, 5)) = 1
        _StripeAngle ("Stripe Angle", Range(-180, 180)) = -45

        [Header(Glow Settings)]
        _GlowIntensity ("Glow Intensity", Range(0, 20)) = 8
        _FresnelPower ("Edge Glow Power", Range(0.5, 8)) = 2.0
        _CoreBrightness ("Core Brightness", Range(0, 5)) = 2

        [Header(Flicker Effect)]
        _FlickerSpeed ("Flicker Speed", Range(0, 20)) = 10
        _FlickerIntensity ("Flicker Intensity", Range(0, 0.5)) = 0.15

        [Header(Transparency)]
        _BaseTransparency ("Base Transparency", Range(0, 1)) = 0.4
        _AlarmAlpha ("Alarm Alpha Boost", Range(0, 1)) = 0.8

        [Header(Advanced)]
        [Toggle] _UseScreenPulse ("Screen-Space Flash", Float) = 0
        _ScreenPulseIntensity ("Screen Flash Intensity", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 100
        Blend SrcAlpha One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "AlarmIndicatorPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma shader_feature _ENABLESTRIPES_ON
            #pragma shader_feature _USESCREENPULSE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _AlarmColor;
                half4 _HotspotColor;
                half4 _EmissionColor;

                half _PulseSpeed;
                half _PulseIntensity;
                half _PulseSharpness;

                half _StripeScale;
                half _StripeSpeed;
                half _StripeAngle;

                half _GlowIntensity;
                half _FresnelPower;
                half _CoreBrightness;

                half _FlickerSpeed;
                half _FlickerIntensity;

                half _BaseTransparency;
                half _AlarmAlpha;

                half _ScreenPulseIntensity;
            CBUFFER_END

            // === NOISE FUNCTION ===
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise3D(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                         lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                         lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y),
                    f.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize vectors
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                // === HEARTBEAT PULSE (EKG-style) ===
                // Convert BPM to frequency (beats per second)
                float bps = _PulseSpeed / 60.0;
                float pulseTime = _Time.y * bps;

                // Create sharp pulse (like heartbeat)
                float pulse = frac(pulseTime);
                pulse = pow(pulse, _PulseSharpness); // Sharp attack
                pulse = 1.0 - pulse; // Invert for decay
                pulse = pulse * pulse; // Square for sharper peak

                // Double-beat (lub-dub) effect
                float subPulse = frac(pulseTime * 2.0 + 0.15);
                subPulse = pow(subPulse, _PulseSharpness * 1.5);
                subPulse = (1.0 - subPulse) * 0.4; // Weaker second beat

                pulse = max(pulse, subPulse);

                // Modulate pulse intensity
                pulse = lerp(1.0, pulse, _PulseIntensity);

                // === FLICKER EFFECT (Critical failure randomness) ===
                float flicker = noise3D(input.positionWS * 0.5 + _Time.y * _FlickerSpeed);
                flicker = lerp(1.0, flicker, _FlickerIntensity);

                // === FRESNEL EDGE GLOW ===
                half NdotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = 1.0 - NdotV;
                fresnel = pow(fresnel, _FresnelPower);

                // === WARNING STRIPES ===
                half stripePattern = 0.0;
                #ifdef _ENABLESTRIPES_ON
                    // Rotate UV coordinates for angled stripes
                    float angleRad = radians(_StripeAngle);
                    float2 rotatedUV = float2(
                        input.uv.x * cos(angleRad) - input.uv.y * sin(angleRad),
                        input.uv.x * sin(angleRad) + input.uv.y * cos(angleRad)
                    );

                    // Scrolling diagonal stripes
                    float stripeCoord = rotatedUV.x * _StripeScale + _Time.y * _StripeSpeed;
                    stripePattern = frac(stripeCoord);
                    stripePattern = step(0.5, stripePattern); // Hard edge stripes
                    stripePattern = stripePattern * 0.5 + 0.5; // Remap to 0.5-1.0
                #else
                    stripePattern = 1.0;
                #endif

                // === HOTSPOT VARIATION (Procedural danger zones) ===
                float hotspotNoise = noise3D(input.positionWS * 2.0 + _Time.y * 0.3);
                hotspotNoise = pow(hotspotNoise, 3.0); // Concentrated hotspots

                // === COLOR COMBINATION ===
                // Base alarm color
                half3 alarmBase = _AlarmColor.rgb * _CoreBrightness;

                // Hotspot mixing
                half3 colorMix = lerp(alarmBase, _HotspotColor.rgb, hotspotNoise * 0.7);

                // Apply stripe pattern
                colorMix *= stripePattern;

                // Fresnel edge highlighting
                half3 edgeGlow = _EmissionColor.rgb * fresnel * _GlowIntensity;

                // Combine with pulse
                half3 pulsingColor = colorMix * pulse * flicker;

                // HDR Emission
                half3 emission = _EmissionColor.rgb * pulse * flicker * 2.0;

                // Final color
                half3 finalColor = pulsingColor + edgeGlow + emission;

                // === SCREEN-SPACE PULSE (Optional flash effect) ===
                #ifdef _USESCREENPULSE_ON
                    float2 screenUV = input.screenPos.xy / input.screenPos.w;
                    float distFromCenter = length(screenUV - 0.5) * 2.0;
                    float screenFlash = (1.0 - distFromCenter) * pulse * _ScreenPulseIntensity;
                    finalColor += screenFlash * _AlarmColor.rgb;
                #endif

                // === CALCULATE ALPHA ===
                half alpha = _BaseTransparency;

                // Pulse affects alpha (throbbing visibility)
                alpha += pulse * _AlarmAlpha;

                // Fresnel boosts edge visibility
                alpha += fresnel * 0.5;

                // Flicker modulation
                alpha *= flicker;

                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
