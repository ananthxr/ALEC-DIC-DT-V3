Shader "Custom/URP/HolographicNeonWireframe"
{
    Properties
    {
        [Header(Neon Colors)]
        _BaseWireColor ("Base Wire Color (Cyan)", Color) = (0, 1, 1, 1)
        _HighlightColor ("Highlight Color (Pink/Magenta)", Color) = (1, 0.3, 0.8, 1)
        _EdgeGlowColor ("Edge Glow Color", Color) = (0.5, 1, 1, 1)
        _GlassColor ("Glass Tint", Color) = (0.1, 0.3, 0.5, 0.1)

        [Header(Grid Wireframe)]
        _GridDensity ("Grid Density", Range(1, 100)) = 20
        _LineThickness ("Line Thickness", Range(0.5, 20)) = 5
        _WireGlow ("Wire Glow Intensity", Range(0, 10)) = 3

        [Header(Fresnel Rim)]
        _FresnelColor ("Fresnel Color", Color) = (0, 1, 1, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.5
        _FresnelIntensity ("Fresnel Intensity", Range(0, 5)) = 2

        [Header(Transparency)]
        _BaseTransparency ("Base Transparency", Range(0, 1)) = 0.15
        _WireAlpha ("Wire Alpha Boost", Range(0, 1)) = 0.6

        [Header(Animated Scanlines)]
        _ScanlineColor ("Scanline Color", Color) = (0, 1, 1, 1)
        _ScanlineSpeed ("Scanline Speed", Range(0, 10)) = 2
        _ScanlineWidth ("Scanline Width", Range(0.01, 0.5)) = 0.1
        _ScanlineIntensity ("Scanline Intensity", Range(0, 5)) = 3

        [Header(Animated Trails)]
        _TrailColor ("Trail Color", Color) = (1, 1, 1, 1)
        _TrailSpeed ("Trail Speed", Range(0, 10)) = 3
        _TrailSpacing ("Trail Spacing", Range(0.1, 5)) = 1
        _TrailIntensity ("Trail Intensity", Range(0, 5)) = 2

        [Header(Highlight Pulse)]
        _HighlightIntensity ("Highlight Pulse Intensity", Range(0, 5)) = 2
        _HighlightSpeed ("Highlight Pulse Speed", Range(0, 5)) = 1.5
        _HighlightFrequency ("Highlight Frequency", Range(0.1, 10)) = 3
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
            Name "NeonWireframePass"

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
                float3 positionOS : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseWireColor;
                half4 _HighlightColor;
                half4 _EdgeGlowColor;
                half4 _FresnelColor;
                half4 _GlassColor;
                half4 _ScanlineColor;
                half4 _TrailColor;

                half _GridDensity;
                half _LineThickness;
                half _WireGlow;

                half _FresnelPower;
                half _FresnelIntensity;

                half _BaseTransparency;
                half _WireAlpha;

                half _ScanlineSpeed;
                half _ScanlineWidth;
                half _ScanlineIntensity;

                half _TrailSpeed;
                half _TrailSpacing;
                half _TrailIntensity;

                half _HighlightIntensity;
                half _HighlightSpeed;
                half _HighlightFrequency;
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
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize vectors
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                // === WORLD-SPACE GRID WIREFRAME (works on all walls) ===
                // Use world position for consistent grid across all surfaces
                float3 worldPos = input.positionWS;

                // Create grid on all three axes
                float2 gridXY = frac(worldPos.xy * _GridDensity);
                float2 gridXZ = frac(worldPos.xz * _GridDensity);
                float2 gridYZ = frac(worldPos.yz * _GridDensity);

                // Distance to grid lines for each plane
                float2 distXY = min(gridXY, 1.0 - gridXY);
                float2 distXZ = min(gridXZ, 1.0 - gridXZ);
                float2 distYZ = min(gridYZ, 1.0 - gridYZ);

                // Get minimum distance to any grid line
                float gridLineXY = min(distXY.x, distXY.y);
                float gridLineXZ = min(distXZ.x, distXZ.y);
                float gridLineYZ = min(distYZ.x, distYZ.y);

                // Combine all grids (use the strongest signal)
                float gridLine = min(min(gridLineXY, gridLineXZ), gridLineYZ);

                // Create sharp wireframe lines
                float wireframe = 1.0 - smoothstep(0.0, _LineThickness / 100.0, gridLine);

                // === FRESNEL RIM GLOW ===
                half fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                fresnel = pow(fresnel, _FresnelPower) * _FresnelIntensity;
                half3 fresnelGlow = _FresnelColor.rgb * fresnel;

                // === VERTICAL SCANLINES ===
                half scanlinePos = frac((worldPos.y + _Time.y * _ScanlineSpeed) * 0.5);
                half scanline = smoothstep(0.5 - _ScanlineWidth, 0.5, scanlinePos) *
                               smoothstep(0.5 + _ScanlineWidth, 0.5, scanlinePos);
                half3 scanlineEffect = _ScanlineColor.rgb * scanline * _ScanlineIntensity;

                // === TRAVELING DOT TRAILS (on grid lines) ===
                // Horizontal trails
                half trailH = frac(worldPos.x * _TrailSpacing - _Time.y * _TrailSpeed);
                trailH = smoothstep(0.3, 0.0, trailH);

                // Vertical trails
                half trailV = frac(worldPos.y * _TrailSpacing - _Time.y * _TrailSpeed * 0.8);
                trailV = smoothstep(0.3, 0.0, trailV);

                // Combine trails (appear only on wireframe)
                half combinedTrail = max(trailH, trailV);
                half3 trailEffect = _TrailColor.rgb * combinedTrail * wireframe * _TrailIntensity;

                // === HIGHLIGHT PULSE ZONES ===
                // Create pulsing zones based on world position
                float highlightNoise = frac(sin(dot(worldPos.xz, float2(12.9898, 78.233))) * 43758.5453);
                half highlightPulse = sin(_Time.y * _HighlightSpeed + highlightNoise * _HighlightFrequency) * 0.5 + 0.5;
                half3 highlightEffect = _HighlightColor.rgb * highlightNoise * highlightPulse * _HighlightIntensity * wireframe;

                // === COMBINE ALL EFFECTS ===
                // Base glass color
                half3 finalColor = _GlassColor.rgb;

                // Add wireframe with glow
                half3 wireColor = lerp(_BaseWireColor.rgb, _EdgeGlowColor.rgb, fresnel * 0.5);
                finalColor += wireColor * wireframe * _WireGlow;

                // Add fresnel rim
                finalColor += fresnelGlow;

                // Add scanlines
                finalColor += scanlineEffect;

                // Add animated trails
                finalColor += trailEffect;

                // Add highlight pulses
                finalColor += highlightEffect;

                // === CALCULATE ALPHA ===
                half alpha = _BaseTransparency;
                alpha += wireframe * _WireAlpha;
                alpha += fresnel * 0.3;
                alpha += scanline * 0.2;
                alpha += combinedTrail * 0.25;
                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
