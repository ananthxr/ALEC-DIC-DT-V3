Shader "Custom/URP/AmbientBuildingSimple"
{
    Properties
    {
        [Header(Base Appearance)]
        _BaseColor ("Base Color", Color) = (0.15, 0.18, 0.22, 1)
        _Desaturation ("Desaturation", Range(0, 1)) = 0.7
        _Brightness ("Brightness", Range(0, 2)) = 0.4

        [Header(Transparency)]
        _GlobalAlpha ("Overall Transparency", Range(0, 1)) = 0.5
        _DistanceFadeStart ("Distance Fade Start", Range(0, 200)) = 50
        _DistanceFadeEnd ("Distance Fade End", Range(0, 200)) = 100

        [Header(Lighting)]
        _DiffuseStrength ("Light Response", Range(0, 1)) = 0.2
        _AmbientInfluence ("Ambient Light Influence", Range(0, 1)) = 0.5
        _ShadowDarkness ("Shadow Darkness", Range(0, 1)) = 0.3

        [Header(Edge Definition)]
        _FresnelPower ("Edge Sharpness", Range(0.5, 8)) = 4.0
        _FresnelIntensity ("Edge Brightness", Range(0, 2)) = 0.3

        [Header(Fog Integration)]
        _FogTint ("Fog Tint", Color) = (0.5, 0.6, 0.7, 1)
        _FogDensity ("Fog Density", Range(0, 1)) = 0.3
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
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

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
                float4 shadowCoord : TEXCOORD3;
                float distanceToCamera : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Desaturation;
                float _Brightness;
                float _GlobalAlpha;
                float _DistanceFadeStart;
                float _DistanceFadeEnd;
                float _DiffuseStrength;
                float _AmbientInfluence;
                float _ShadowDarkness;
                float _FresnelPower;
                float _FresnelIntensity;
                float4 _FogTint;
                float _FogDensity;
            CBUFFER_END

            float3 DesaturateColor(float3 color, float amount)
            {
                float luminance = dot(color, float3(0.299, 0.587, 0.114));
                return lerp(color, float3(luminance, luminance, luminance), amount);
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
                output.shadowCoord = TransformWorldToShadowCoord(vertexInput.positionWS);
                output.distanceToCamera = length(_WorldSpaceCameraPos - vertexInput.positionWS);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                float3 baseColor = DesaturateColor(_BaseColor.rgb, _Desaturation);
                baseColor = baseColor * _Brightness;

                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = 1.0 - NdotV;
                fresnel = pow(saturate(fresnel), _FresnelPower) * _FresnelIntensity;

                Light mainLight = GetMainLight(input.shadowCoord);

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 diffuse = NdotL * _DiffuseStrength;

                float shadow = mainLight.shadowAttenuation;
                float shadowFactor = lerp(1.0 - _ShadowDarkness, 1.0, shadow);

                float3 ambient = float3(0.2, 0.2, 0.25) * _AmbientInfluence;

                float3 finalColor = baseColor * shadowFactor;
                finalColor = finalColor + diffuse * 0.1;
                finalColor = finalColor + ambient;
                finalColor = finalColor + fresnel * 0.2;

                finalColor = lerp(finalColor, _FogTint.rgb * _Brightness, _FogDensity * 0.5);

                float distanceFade = 1.0 - saturate((input.distanceToCamera - _DistanceFadeStart) / (_DistanceFadeEnd - _DistanceFadeStart));

                float alpha = _GlobalAlpha;
                alpha = alpha + fresnel * 0.15;
                alpha = alpha * distanceFade;
                alpha = saturate(alpha);

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
