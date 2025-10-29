Shader "Custom/URP/TransparentGrey"
{
    Properties
    {
        [Header(Base Appearance)]
        _BaseColor ("Base Color", Color) = (0.5, 0.5, 0.5, 0.3)
        _Transparency ("Transparency", Range(0, 1)) = 0.3

        [Header(Edge Definition)]
        _RimColor ("Rim Color", Color) = (0.7, 0.7, 0.7, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 0.5

        [Header(Surface Details)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _FresnelStrength ("Fresnel Strength", Range(0, 1)) = 0.5
        _SpecularPower ("Specular Power", Range(1, 128)) = 32
        _SpecularIntensity ("Specular Intensity", Range(0, 5)) = 2.0
        _Glossiness ("Glossiness", Range(0, 1)) = 0.8

        [Header(Depth Fade)]
        _DepthFade ("Depth Fade Distance", Range(0, 50)) = 10
        _DepthFadeStrength ("Depth Fade Strength", Range(0, 1)) = 0.5
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
            Name "TransparentGreyPass"

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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _Transparency;
                half _RimPower;
                half _RimIntensity;
                half _Smoothness;
                half _FresnelStrength;
                half _SpecularPower;
                half _SpecularIntensity;
                half _Glossiness;
                half _DepthFade;
                half _DepthFadeStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize vectors
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                // === FRESNEL/RIM EFFECT ===
                // Makes edges more visible than center
                half NdotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = 1.0 - NdotV;
                half rim = pow(fresnel, _RimPower) * _RimIntensity;

                // === DEPTH FADE ===
                // Objects further from camera fade more
                float3 cameraPos = GetCameraPositionWS();
                float distToCamera = distance(input.positionWS, cameraPos);
                float depthFade = saturate(distToCamera / _DepthFade);
                depthFade = lerp(1.0, 0.5, depthFade * _DepthFadeStrength);

                // === LIGHTING ===
                // Get main light direction
                Light mainLight = GetMainLight();
                half3 lightDir = mainLight.direction;
                half NdotL = saturate(dot(normalWS, lightDir)) * 0.5 + 0.5; // Wrap lighting

                // === SPECULAR/GLOSSY HIGHLIGHTS ===
                // Blinn-Phong specular for glossy appearance
                half3 halfVector = normalize(lightDir + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfVector));
                half specular = pow(NdotH, _SpecularPower * _Glossiness) * _SpecularIntensity;

                // Add environment reflection simulation
                half3 reflectionDir = reflect(-viewDirWS, normalWS);
                half envReflection = saturate(reflectionDir.y * 0.5 + 0.5); // Simple sky reflection
                half glossyReflection = pow(envReflection, 3.0) * _Glossiness * 0.3;

                // === COMBINE EFFECTS ===
                half3 baseColor = _BaseColor.rgb;

                // Add subtle lighting
                half3 finalColor = baseColor * NdotL;

                // Add glossy specular highlights
                finalColor += specular * mainLight.color;

                // Add environment reflection for glass-like look
                finalColor += glossyReflection * half3(1, 1, 1);

                // Add rim highlight for edge definition
                finalColor += _RimColor.rgb * rim;

                // Apply fresnel for glass-like appearance (enhanced)
                half fresnelEffect = pow(fresnel, 2.0) * _FresnelStrength * (1.0 + _Glossiness * 0.5);
                finalColor += fresnelEffect;

                // === CALCULATE ALPHA ===
                half alpha = _Transparency;
                alpha += rim * 0.3; // Edges slightly more opaque
                alpha *= depthFade; // Fade with distance
                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
