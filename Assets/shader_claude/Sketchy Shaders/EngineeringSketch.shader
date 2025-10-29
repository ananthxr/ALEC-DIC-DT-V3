Shader "Custom/URP/EngineeringSketch"
{
    Properties
    {
        [Header(Sketch Line Colors)]
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
        _BackgroundColor ("Background Tint", Color) = (0, 0, 0, 0)

        [Header(Edge Detection)]
        _EdgeThickness ("Edge Thickness", Range(0.0, 5.0)) = 1.0
        _EdgeIntensity ("Edge Intensity", Range(0, 10)) = 3.0
        _EdgeThreshold ("Edge Threshold", Range(0, 1)) = 0.1

        [Header(Sketch Details)]
        _DetailLineThickness ("Detail Line Thickness", Range(0, 3)) = 0.8
        _DetailDensity ("Detail Density", Range(1, 50)) = 20
        _SketchNoise ("Sketch Noise Amount", Range(0, 1)) = 0.15

        [Header(Hatching Lines)]
        _HatchingDensity ("Hatching Density", Range(0, 100)) = 30
        _HatchingIntensity ("Hatching Intensity", Range(0, 1)) = 0.3
        _HatchingAngle ("Hatching Angle", Range(0, 180)) = 45

        [Header(Depth and Occlusion)]
        _DepthEdgeStrength ("Depth Edge Strength", Range(0, 5)) = 1.5
        _OcclusionLinesIntensity ("Occlusion Lines", Range(0, 1)) = 0.2

        [Header(Animation)]
        _LineFlickerSpeed ("Line Flicker Speed", Range(0, 10)) = 0
        _LineFlickerAmount ("Line Flicker Amount", Range(0, 0.5)) = 0.1

        [Header(Transparency)]
        _FaceAlpha ("Face Alpha (0 = hidden)", Range(0, 1)) = 0.05
        _LineAlpha ("Line Alpha", Range(0, 1)) = 1.0
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
            Name "SketchPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

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
                float4 screenPos : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _LineColor;
                half4 _BackgroundColor;

                half _EdgeThickness;
                half _EdgeIntensity;
                half _EdgeThreshold;

                half _DetailLineThickness;
                half _DetailDensity;
                half _SketchNoise;

                half _HatchingDensity;
                half _HatchingIntensity;
                half _HatchingAngle;

                half _DepthEdgeStrength;
                half _OcclusionLinesIntensity;

                half _LineFlickerSpeed;
                half _LineFlickerAmount;

                half _FaceAlpha;
                half _LineAlpha;
            CBUFFER_END

            // Noise function for sketch variation
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

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
                output.screenPos = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize vectors
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                // Screen space UV for edge detection
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // === EDGE DETECTION (Contour Lines) ===
                // Normal-based edge detection (silhouette)
                half NdotV = abs(dot(normalWS, viewDirWS));
                half silhouette = 1.0 - NdotV;
                silhouette = smoothstep(_EdgeThreshold, _EdgeThreshold + 0.1, silhouette);
                silhouette = pow(silhouette, 0.5) * _EdgeIntensity;

                // === DETAIL LINES (Surface Grid) ===
                // World-space grid for technical drawing lines
                float3 worldPos = input.positionWS;

                // Create grid lines on all axes
                float2 gridXY = frac(worldPos.xy * _DetailDensity);
                float2 gridXZ = frac(worldPos.xz * _DetailDensity);
                float2 gridYZ = frac(worldPos.yz * _DetailDensity);

                float lineWidth = _DetailLineThickness / 100.0;

                float gridLineXY = step(1.0 - lineWidth, gridXY.x) + step(1.0 - lineWidth, gridXY.y);
                float gridLineXZ = step(1.0 - lineWidth, gridXZ.x) + step(1.0 - lineWidth, gridXZ.y);
                float gridLineYZ = step(1.0 - lineWidth, gridYZ.x) + step(1.0 - lineWidth, gridYZ.y);

                float detailLines = max(max(gridLineXY, gridLineXZ), gridLineYZ);
                detailLines = saturate(detailLines);

                // Fade detail lines on edges
                detailLines *= (1.0 - silhouette * 0.5);

                // === HATCHING LINES (Shadow/Depth Indication) ===
                float hatchAngle = radians(_HatchingAngle);
                float2 hatchDir = float2(cos(hatchAngle), sin(hatchAngle));
                float hatchPattern = frac(dot(worldPos.xy, hatchDir) * _HatchingDensity);
                float hatching = step(0.5, hatchPattern) * _HatchingIntensity;

                // Apply hatching based on surface angle (darker areas get more hatching)
                hatching *= (1.0 - NdotV * 0.7);

                // === SKETCH NOISE (Hand-drawn effect) ===
                float sketchNoise = noise(worldPos.xy * 50.0 + _Time.y * 0.1) * _SketchNoise;

                // === LINE FLICKER (Pencil variation) ===
                float flicker = 1.0;
                if (_LineFlickerSpeed > 0.01)
                {
                    float flickerNoise = noise(worldPos.xz * 10.0 + _Time.y * _LineFlickerSpeed);
                    flicker = 1.0 - (flickerNoise * _LineFlickerAmount);
                }

                // === COMBINE ALL LINE EFFECTS ===
                float totalLines = 0.0;

                // Strong contour/silhouette edges
                totalLines += silhouette;

                // Detail grid lines (lighter)
                totalLines += detailLines * 0.4;

                // Hatching for depth
                totalLines += hatching;

                // Add sketch noise variation
                totalLines += sketchNoise;

                // Apply flicker
                totalLines *= flicker;

                // Clamp to valid range
                totalLines = saturate(totalLines);

                // === FINAL COLOR COMPOSITION ===
                // Base: nearly transparent or slight background tint
                half3 finalColor = _BackgroundColor.rgb;

                // Add white lines where detected
                finalColor = lerp(finalColor, _LineColor.rgb, totalLines);

                // === CALCULATE ALPHA ===
                half alpha = _FaceAlpha; // Face is mostly transparent
                alpha += totalLines * _LineAlpha; // Lines are visible
                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
