Shader "Custom/URP/GhostWall"
{
    Properties
    {
        [Header(Base Appearance)]
        _BaseColor ("Base Color", Color) = (0.3, 0.5, 0.8, 1)
        _Transparency ("Transparency", Range(0, 1)) = 0.3

        [Header(Edge Definition)]
        _FresnelPower ("Edge Sharpness", Range(0.5, 8)) = 2.5
        _FresnelIntensity ("Edge Brightness", Range(0, 5)) = 1.5
        _EdgeColor ("Edge Tint", Color) = (0.4, 0.7, 1.0, 1)

        [Header(Lighting Response)]
        _DiffuseStrength ("Light Response", Range(0, 1)) = 0.4
        _AmbientOcclusion ("Shadow Depth", Range(0, 1)) = 0.3
        _LightColorInfluence ("Light Color Influence", Range(0, 1)) = 0.5

        [Header(Surface Detail)]
        _NoiseScale ("Detail Scale", Range(0, 10)) = 2
        _NoiseStrength ("Detail Strength", Range(0, 0.5)) = 0.1

        [Header(Fade Control)]
        _FadeAmount ("Fade Amount", Range(0, 1)) = 1
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
            Name "GhostWallPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            // URP lighting support
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT

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
                float4 shadowCoord : TEXCOORD4;
                float depth : TEXCOORD5;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Transparency;

                half _FresnelPower;
                half _FresnelIntensity;
                half4 _EdgeColor;

                half _DiffuseStrength;
                half _AmbientOcclusion;
                half _LightColorInfluence;

                half _NoiseScale;
                half _NoiseStrength;

                half _FadeAmount;
            CBUFFER_END

            // === NOISE FUNCTIONS ===
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

                // Shadow coordinates for receiving shadows
                output.shadowCoord = TransformWorldToShadowCoord(vertexInput.positionWS);

                // Depth for depth fade effect
                output.depth = vertexInput.positionCS.w;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize vectors
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                // === FRESNEL RIM LIGHTING (Edge Definition) ===
                half NdotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = 1.0 - NdotV;
                fresnel = pow(saturate(fresnel), _FresnelPower) * _FresnelIntensity;

                // === LIGHTING CALCULATION ===
                Light mainLight = GetMainLight(input.shadowCoord);

                // Diffuse lighting (Lambert)
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = NdotL * mainLight.color * _DiffuseStrength;

                // Shadow attenuation
                half shadow = mainLight.shadowAttenuation;
                half shadowFactor = lerp(1.0 - _AmbientOcclusion, 1.0, shadow);

                // === SURFACE DETAIL (Subtle Noise) ===
                float surfaceNoise = noise3D(input.positionWS * _NoiseScale);
                surfaceNoise = surfaceNoise * 2.0 - 1.0; // Remap to -1 to 1

                // === COMBINE COLORS ===
                // Base wall color with subtle noise variation
                half3 baseColor = _BaseColor.rgb * (1.0 + surfaceNoise * _NoiseStrength);

                // Apply lighting (mix between base color and lit color)
                half3 litColor = baseColor * shadowFactor;
                litColor += diffuse * lerp(half3(1,1,1), mainLight.color, _LightColorInfluence);

                // Add edge highlight (fresnel)
                half3 edgeHighlight = _EdgeColor.rgb * fresnel;

                // Final color combination
                half3 finalColor = litColor + edgeHighlight;

                // === CALCULATE ALPHA ===
                half alpha = _Transparency;

                // Edges more visible (prevents monotonic appearance)
                alpha += fresnel * 0.3;

                // Apply fade amount (simple multiplier slider)
                alpha *= _FadeAmount;

                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }

        // Shadow caster pass (so walls can cast shadows on other objects)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
