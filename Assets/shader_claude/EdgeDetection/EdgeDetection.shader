Shader "Custom/EdgeDetection"
{
    Properties
    {
        _EdgeColor ("Edge Color", Color) = (1,1,1,1)
        _BackgroundColor ("Background Color", Color) = (0,0,0,1)
        _EdgeThickness ("Edge Thickness", Range(0, 5)) = 1.0
        _DepthSensitivity ("Depth Sensitivity", Range(0, 100)) = 10.0
        _NormalSensitivity ("Normal Sensitivity", Range(0, 10)) = 1.0
        _EdgeGlow ("Edge Glow", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "EdgeDetection"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

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
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float depth : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _EdgeColor;
                float4 _BackgroundColor;
                float _EdgeThickness;
                float _DepthSensitivity;
                float _NormalSensitivity;
                float _EdgeGlow;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.depth = vertexInput.positionCS.z;
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            // Sobel-like edge detection based on view angle
            float DetectEdge(float3 normal, float3 viewDir)
            {
                // Fresnel-like edge detection
                float NdotV = abs(dot(normalize(normal), normalize(viewDir)));

                // Sharp edge threshold
                float edgeThreshold = 0.3 * _NormalSensitivity;
                float edge = 1.0 - smoothstep(0.0, edgeThreshold, NdotV);

                return edge;
            }

            // Additional edge detection based on depth discontinuities
            float DetectDepthEdge(float depth)
            {
                // Create edge based on depth changes
                float depthEdge = frac(depth * _DepthSensitivity) * _EdgeThickness;
                depthEdge = step(0.95, depthEdge);
                return depthEdge;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Normalize vectors
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Edge detection
                float edge = DetectEdge(normalWS, viewDirWS);

                // Depth-based edge (creates grid-like lines)
                float depthEdge = DetectDepthEdge(input.depth);

                // Combine edge detections
                float finalEdge = saturate(edge + depthEdge);

                // Apply edge glow
                finalEdge = pow(finalEdge, 1.0 / max(_EdgeGlow, 0.1));

                // Mix edge color with background
                float4 color = lerp(_BackgroundColor, _EdgeColor, finalEdge);

                // Apply fog
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
