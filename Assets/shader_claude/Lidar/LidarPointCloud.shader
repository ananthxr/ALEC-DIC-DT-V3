Shader "Custom/URP/LidarPointCloud"
{
    Properties
    {
        [Header(Point Cloud Colors)]
        _PointColor ("Point Color", Color) = (0, 1, 1, 1)
        _ScanColor ("Scan Line Color", Color) = (0, 1, 0.5, 1)
        _DepthColor ("Depth Color", Color) = (0, 0.5, 1, 1)

        [Header(Point Cloud Settings)]
        _PointDensity ("Point Density", Range(10, 500)) = 100
        _PointSize ("Point Size", Range(0.5, 20)) = 3
        _PointIntensity ("Point Intensity", Range(0, 5)) = 2
        _PointFalloff ("Point Falloff", Range(0.1, 5)) = 1

        [Header(Gaussian Splat)]
        _SplatSize ("Splat Size", Range(0.01, 0.5)) = 0.1
        _SplatSoftness ("Splat Softness", Range(0.1, 5)) = 1.5
        _SplatDensity ("Splat Density", Range(1, 100)) = 30

        [Header(LIDAR Scan Effect)]
        _ScanSpeed ("Scan Speed", Range(0, 10)) = 2
        _ScanWidth ("Scan Width", Range(0.05, 1)) = 0.2
        _ScanIntensity ("Scan Intensity", Range(0, 10)) = 4
        _ScanDirection ("Scan Direction", Vector) = (0, 1, 0, 0)

        [Header(Data Noise)]
        _NoiseScale ("Noise Scale", Range(0.1, 50)) = 10
        _NoiseIntensity ("Noise Intensity", Range(0, 1)) = 0.3
        _GlitchAmount ("Glitch Amount", Range(0, 0.5)) = 0.1

        [Header(Depth Gradient)]
        _DepthGradientStrength ("Depth Gradient", Range(0, 2)) = 0.5
        _NearColor ("Near Point Color", Color) = (0, 1, 1, 1)
        _FarColor ("Far Point Color", Color) = (0, 0.3, 1, 1)

        [Header(Transparency)]
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.1
        _PointAlpha ("Point Alpha", Range(0, 1)) = 0.9
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
            Name "LidarPass"

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
                float depth : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _PointColor;
                half4 _ScanColor;
                half4 _DepthColor;
                half4 _NearColor;
                half4 _FarColor;
                half4 _ScanDirection;

                half _PointDensity;
                half _PointSize;
                half _PointIntensity;
                half _PointFalloff;

                half _SplatSize;
                half _SplatSoftness;
                half _SplatDensity;

                half _ScanSpeed;
                half _ScanWidth;
                half _ScanIntensity;

                half _NoiseScale;
                half _NoiseIntensity;
                half _GlitchAmount;

                half _DepthGradientStrength;

                half _BaseAlpha;
                half _PointAlpha;
            CBUFFER_END

            // Hash function for randomness
            float hash(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 758.5453))) * 43758.5453);
            }

            // 3D noise
            float noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n = i.x + i.y * 157.0 + i.z * 113.0;
                return lerp(
                    lerp(lerp(hash(float3(n + 0.0, 0, 0)), hash(float3(n + 1.0, 0, 0)), f.x),
                         lerp(hash(float3(n + 157.0, 0, 0)), hash(float3(n + 158.0, 0, 0)), f.x), f.y),
                    lerp(lerp(hash(float3(n + 113.0, 0, 0)), hash(float3(n + 114.0, 0, 0)), f.x),
                         lerp(hash(float3(n + 270.0, 0, 0)), hash(float3(n + 271.0, 0, 0)), f.x), f.y),
                    f.z
                );
            }

            // Gaussian function for splatting
            float gaussian(float dist, float sigma)
            {
                return exp(-(dist * dist) / (2.0 * sigma * sigma));
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

                // Calculate depth from camera
                float3 viewPos = GetCameraPositionWS();
                output.depth = distance(output.positionWS, viewPos);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 worldPos = input.positionWS;
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                // === POINT CLOUD GENERATION ===
                // Create regular grid of points
                float3 pointGrid = worldPos * _PointDensity;
                float3 pointCell = floor(pointGrid);
                float3 pointLocal = frac(pointGrid);

                // Add noise displacement to points
                float3 noiseOffset = float3(
                    noise3D(pointCell * 0.1),
                    noise3D(pointCell * 0.1 + 100.0),
                    noise3D(pointCell * 0.1 + 200.0)
                ) * _NoiseIntensity;

                pointLocal += noiseOffset;

                // Distance to nearest point
                float3 pointDist = pointLocal - 0.5;
                float distToPoint = length(pointDist) * (100.0 / _PointDensity);

                // Create point with gaussian falloff
                float pointValue = gaussian(distToPoint, _PointSize * 0.1) * _PointIntensity;

                // === GAUSSIAN SPLAT EFFECT ===
                // Create larger splats with softer edges
                float3 splatGrid = worldPos * _SplatDensity;
                float3 splatCell = floor(splatGrid);
                float3 splatLocal = frac(splatGrid);

                float3 splatCenter = float3(
                    hash(splatCell),
                    hash(splatCell + 100.0),
                    hash(splatCell + 200.0)
                );

                float distToSplat = length(splatLocal - splatCenter);
                float splat = gaussian(distToSplat, _SplatSize) * _SplatSoftness;

                // === LIDAR SCAN LINE ===
                float scanPos = frac(dot(worldPos, _ScanDirection.xyz) * 0.1 + _Time.y * _ScanSpeed);
                float scanLine = smoothstep(_ScanWidth, 0.0, abs(scanPos - 0.5));
                half3 scanEffect = _ScanColor.rgb * scanLine * _ScanIntensity;

                // === DEPTH GRADIENT ===
                // Color variation based on depth
                float depthNorm = saturate(input.depth * 0.05);
                half3 depthColor = lerp(_NearColor.rgb, _FarColor.rgb, depthNorm);

                // === DATA GLITCH EFFECT ===
                float glitchNoise = noise3D(worldPos * 20.0 + _Time.y * 10.0);
                float glitch = step(1.0 - _GlitchAmount, glitchNoise);

                // === COMBINE EFFECTS ===
                half3 finalColor = _PointColor.rgb * 0.1; // Base ambient

                // Add point cloud
                finalColor += depthColor * pointValue;

                // Add gaussian splats
                finalColor += depthColor * splat * 0.5;

                // Add scan line
                finalColor += scanEffect;

                // Add depth gradient tint
                finalColor = lerp(finalColor, _DepthColor.rgb, _DepthGradientStrength * depthNorm * 0.3);

                // Add glitch (random bright spots)
                finalColor += glitch * _PointColor.rgb * 2.0;

                // === CALCULATE ALPHA ===
                half alpha = _BaseAlpha;
                alpha += pointValue * _PointAlpha;
                alpha += splat * _PointAlpha * 0.5;
                alpha += scanLine * 0.5;
                alpha += glitch * 0.3;
                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
