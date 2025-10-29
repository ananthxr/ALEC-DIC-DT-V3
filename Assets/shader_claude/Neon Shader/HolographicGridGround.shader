Shader "Custom/URP/HolographicGridGround"
{
    Properties
    {
        [Header(Grid Colors)]
        _GridColor ("Grid Line Color", Color) = (0, 1, 1, 1)
        _BackgroundColor ("Background Color", Color) = (0, 0.1, 0.2, 0.5)
        _EmissiveColor ("Emissive Glow Color", Color) = (0, 1, 1, 1)

        [Header(Grid Settings)]
        _GridScale ("Grid Scale", Range(0.1, 50)) = 10
        _LineWidth ("Line Width", Range(0.001, 0.1)) = 0.02
        _LineIntensity ("Line Intensity", Range(0, 10)) = 2
        _EmissiveIntensity ("Emissive Intensity", Range(0, 5)) = 1.5

        [Header(Animation)]
        _ScanlineSpeed ("Scanline Speed", Range(0, 5)) = 1
        _ScanlineWidth ("Scanline Width", Range(0.05, 1)) = 0.3
        _ScanlineColor ("Scanline Color", Color) = (0, 1, 1, 1)
        _PulseSpeed ("Grid Pulse Speed", Range(0, 5)) = 0.5

        [Header(Reflection)]
        _ReflectionIntensity ("Reflection Intensity", Range(0, 1)) = 0.3

        [Header(Transparency)]
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.3
        _FadeIntensity ("Fade Intensity", Range(0, 1)) = 1.0
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
        Cull Off

        Pass
        {
            Name "GridGroundPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _GridColor;
                half4 _BackgroundColor;
                half4 _EmissiveColor;
                half4 _ScanlineColor;

                half _GridScale;
                half _LineWidth;
                half _LineIntensity;
                half _EmissiveIntensity;

                half _ScanlineSpeed;
                half _ScanlineWidth;
                half _PulseSpeed;

                half _ReflectionIntensity;
                half _BaseAlpha;
                half _FadeIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // === GRID GENERATION ===
                float2 gridUV = input.positionWS.xz * _GridScale;
                float2 gridFrac = frac(gridUV);

                // Distance to grid lines
                float2 gridDist = min(gridFrac, 1.0 - gridFrac);
                float grid = min(gridDist.x, gridDist.y);

                // Create sharp lines
                float gridLines = smoothstep(_LineWidth, 0.0, grid);

                // === SCANLINE ANIMATION ===
                float scanlinePos = frac((input.positionWS.z + _Time.y * _ScanlineSpeed) * 0.5);
                float scanline = smoothstep(0.5 - _ScanlineWidth, 0.5, scanlinePos) *
                                smoothstep(0.5 + _ScanlineWidth, 0.5, scanlinePos);

                // === PULSE EFFECT ===
                float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                float gridPulse = gridLines * pulse * 0.3;

                // === COMBINE EFFECTS ===
                half3 finalColor = _BackgroundColor.rgb;

                // Add grid lines
                finalColor += _GridColor.rgb * gridLines * _LineIntensity;

                // Add scanline
                finalColor += _ScanlineColor.rgb * scanline * 2.0;

                // Add emissive glow on grid lines
                half3 emissive = _EmissiveColor.rgb * (gridLines + scanline) * _EmissiveIntensity;
                finalColor += emissive;

                // Add pulse
                finalColor += _GridColor.rgb * gridPulse;

                // Apply manual fade intensity
                finalColor *= _FadeIntensity;

                // === CALCULATE ALPHA ===
                half alpha = _BaseAlpha;
                alpha += gridLines * 0.5;
                alpha += scanline * 0.3;
                alpha *= _FadeIntensity;
                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
