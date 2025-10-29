Shader "Custom/URP/HolographicBuilding"
{
    Properties
    {
        [Header(Base Colors)]
        _BaseColor ("Base Color", Color) = (0, 1, 1, 0.3)
        _EdgeColor ("Edge/Wireframe Color", Color) = (1, 1, 1, 1)
        _FresnelColor ("Fresnel Rim Color", Color) = (0.5, 1, 1, 1)

        [Header(Holographic Effect)]
        _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 3
        _FresnelIntensity ("Fresnel Intensity", Range(0, 5)) = 1.5
        _Transparency ("Overall Transparency", Range(0, 1)) = 0.4

        [Header(Scanline Animation)]
        _ScanlineColor ("Scanline Color", Color) = (0, 1, 1, 1)
        _ScanlineSpeed ("Scanline Speed", Range(0, 5)) = 1
        _ScanlineWidth ("Scanline Width", Range(0.01, 0.5)) = 0.1
        _ScanlineIntensity ("Scanline Intensity", Range(0, 3)) = 2

        [Header(Dot Trail Effect)]
        _DotColor ("Dot/Trail Color", Color) = (1, 1, 1, 1)
        _DotSpeed ("Dot Speed", Range(0, 10)) = 2
        _DotSize ("Dot Size", Range(0.01, 0.2)) = 0.05
        _TrailLength ("Trail Length", Range(0.1, 1)) = 0.3
        _DotFrequency ("Dot Frequency", Range(1, 20)) = 5

        [Header(Glow Settings)]
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.5
        _GlowColor ("Glow Color", Color) = (0.5, 1, 1, 1)

        [Header(Wireframe)]
        _WireframeWidth ("Wireframe Width", Range(0, 0.1)) = 0.02
        _WireframeIntensity ("Wireframe Intensity", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "HolographicPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float2 uv : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half4 _FresnelColor;
                half4 _ScanlineColor;
                half4 _DotColor;
                half4 _GlowColor;

                half _FresnelPower;
                half _FresnelIntensity;
                half _Transparency;

                half _ScanlineSpeed;
                half _ScanlineWidth;
                half _ScanlineIntensity;

                half _DotSpeed;
                half _DotSize;
                half _TrailLength;
                half _DotFrequency;

                half _GlowIntensity;
                half _WireframeWidth;
                half _WireframeIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize vectors
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                // === FRESNEL EFFECT ===
                half fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                fresnel = pow(fresnel, _FresnelPower) * _FresnelIntensity;
                half3 fresnelColor = _FresnelColor.rgb * fresnel;

                // === SCANLINE EFFECT ===
                half scanlinePos = frac((input.positionWS.y + _Time.y * _ScanlineSpeed) * 2.0);
                half scanline = smoothstep(0.5 - _ScanlineWidth, 0.5, scanlinePos) *
                               smoothstep(0.5 + _ScanlineWidth, 0.5, scanlinePos);
                half3 scanlineEffect = _ScanlineColor.rgb * scanline * _ScanlineIntensity;

                // === TRAVELING DOTS WITH TRAILS ===
                // Create multiple dot streams based on UV and world position
                half dotPhase = frac((input.uv.x + input.uv.y) * _DotFrequency - _Time.y * _DotSpeed);

                // Dot with trail (exponential falloff for trail effect)
                half dotTrail = saturate(exp(-dotPhase / _TrailLength) - exp(-_DotSize / _TrailLength));
                half3 dotEffect = _DotColor.rgb * dotTrail * 2.0;

                // Add vertical dots
                half dotPhaseVertical = frac(input.positionWS.y * 0.5 - _Time.y * _DotSpeed * 0.7);
                half dotTrailVertical = saturate(exp(-dotPhaseVertical / _TrailLength) - exp(-_DotSize / _TrailLength));
                dotEffect += _DotColor.rgb * dotTrailVertical * 1.5;

                // === WIREFRAME EFFECT (using UV derivatives for edge detection) ===
                half2 uvDeriv = fwidth(input.uv) * 50.0;
                half wireframe = saturate((uvDeriv.x + uvDeriv.y) * _WireframeWidth) * _WireframeIntensity;
                half3 wireframeEffect = _EdgeColor.rgb * wireframe;

                // === COMBINE ALL EFFECTS ===
                half3 finalColor = _BaseColor.rgb;
                finalColor += fresnelColor;
                finalColor += scanlineEffect;
                finalColor += dotEffect;
                finalColor += wireframeEffect;

                // Add glow (HDR emission)
                finalColor += _GlowColor.rgb * _GlowIntensity * (fresnel + scanline + dotTrail);

                // Calculate final alpha
                half alpha = _Transparency + fresnel * 0.5 + scanline * 0.3 + dotTrail * 0.2;
                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
