Shader "Custom/URP/MeteorCore"
{
    Properties
    {
        [Header(Meteor Colors)]
        _CoreColor ("Core Color", Color) = (0.05, 0.15, 0.8, 1)
        _GlowColor ("Glow Color", Color) = (0.2, 0.5, 1.0, 1)

        [Header(Glow Settings)]
        [HDR] _EmissionColor ("Emission Color (HDR)", Color) = (0.5, 1.5, 3.0, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 20)) = 8
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.5
        _FresnelIntensity ("Fresnel Intensity", Range(0, 10)) = 5

        [Header(Animation)]
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.4
        _FlickerSpeed ("Flicker Speed", Range(0, 20)) = 8
        _FlickerIntensity ("Flicker Intensity", Range(0, 1)) = 0.15

        [Header(Surface Details)]
        _NoiseScale ("Noise Scale", Range(0, 20)) = 3
        _NoiseSpeed ("Noise Speed", Range(0, 5)) = 1.5
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.25

        [Header(Transparency)]
        _BaseTransparency ("Base Transparency", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "MeteorGlowPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

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
                float3 positionOS : TEXCOORD3;
                float2 uv : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _GlowColor;
                half4 _EmissionColor;

                half _GlowIntensity;
                half _FresnelPower;
                half _FresnelIntensity;

                half _PulseSpeed;
                half _PulseIntensity;
                half _FlickerSpeed;
                half _FlickerIntensity;

                half _NoiseScale;
                half _NoiseSpeed;
                half _NoiseStrength;

                half _BaseTransparency;
            CBUFFER_END

            // === PROCEDURAL NOISE FUNCTIONS ===
            // Hash function for noise generation
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // 3D noise function
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

            // Fractional Brownian Motion for more detailed noise
            float fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                for(int i = 0; i < 3; i++)
                {
                    value += amplitude * noise3D(p * frequency);
                    frequency *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
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
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize vectors
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                // === FRESNEL RIM GLOW ===
                half NdotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = 1.0 - NdotV;
                fresnel = pow(saturate(fresnel), _FresnelPower) * _FresnelIntensity;

                // === ANIMATED PULSE ===
                half pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                pulse = lerp(1.0, pulse, _PulseIntensity);

                // === FLICKER EFFECT ===
                float flickerNoise = noise3D(input.positionWS * 0.5 + _Time.y * _FlickerSpeed);
                half flicker = lerp(1.0, flickerNoise, _FlickerIntensity);

                // === SURFACE NOISE ===
                float3 noiseCoord = input.positionWS * _NoiseScale + _Time.y * _NoiseSpeed;
                float surfaceNoise = fbm(noiseCoord);
                surfaceNoise = surfaceNoise * 2.0 - 1.0; // Remap to -1 to 1

                // === COMBINE COLORS ===
                // Base core color with noise variation
                half3 coreColor = _CoreColor.rgb * (1.0 + surfaceNoise * _NoiseStrength);

                // Fresnel glow color
                half3 glowRim = _GlowColor.rgb * fresnel * _GlowIntensity * pulse * flicker;

                // HDR Emission (values > 1 create bloom)
                half3 emission = _EmissionColor.rgb * fresnel * pulse;

                // Combine all components
                half3 finalColor = coreColor + glowRim + emission;

                // === CALCULATE ALPHA ===
                half alpha = _BaseTransparency;

                // Add fresnel to alpha (edges more visible)
                alpha += fresnel * 0.6;

                // Modulate by pulse and flicker
                alpha *= pulse * flicker;

                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
