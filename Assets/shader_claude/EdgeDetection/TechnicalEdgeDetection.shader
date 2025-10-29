Shader "Custom/TechnicalEdgeDetection"
{
    Properties
    {
        _EdgeColor ("Edge Color", Color) = (1,1,1,1)
        _FillColor ("Fill Color", Color) = (0.15,0.15,0.15,1)
        _EdgeThickness ("Edge Thickness", Range(0.001, 0.1)) = 0.02
        _EdgeSensitivity ("Edge Sensitivity", Range(0.1, 10)) = 2.0
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        // PASS 1: Draw black outline using inverted hull method
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _EdgeColor;
                float4 _FillColor;
                float _EdgeThickness;
                float _EdgeSensitivity;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Expand mesh along normals for outline
                float3 expandedPos = input.positionOS.xyz + input.normalOS * _OutlineWidth;
                output.positionHCS = TransformObjectToHClip(expandedPos);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _EdgeColor;
            }
            ENDHLSL
        }

        // PASS 2: Draw internal edges based on geometry
        Pass
        {
            Name "InternalEdges"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

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
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _EdgeColor;
                float4 _FillColor;
                float _EdgeThickness;
                float _EdgeSensitivity;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            // Detect edges based on normal derivatives (how quickly normals change)
            float DetectGeometricEdge(float3 normalWS, float3 positionWS)
            {
                // Calculate change in normals using screen-space derivatives
                float3 dNdx = ddx(normalWS);
                float3 dNdy = ddy(normalWS);

                // Measure how much the normal is changing
                float normalChange = length(dNdx) + length(dNdy);

                // Amplify based on sensitivity
                normalChange *= _EdgeSensitivity;

                // Create sharp edge threshold
                float edge = smoothstep(0.0, _EdgeThickness, normalChange);

                return edge;
            }

            // Additional edge detection using world position changes
            float DetectPositionEdge(float3 positionWS)
            {
                // Detect sharp position changes (creases, corners)
                float3 dPdx = ddx(positionWS);
                float3 dPdy = ddy(positionWS);

                float posChange = length(dPdx) + length(dPdy);
                posChange *= _EdgeSensitivity * 10.0;

                float edge = smoothstep(0.0, _EdgeThickness * 2.0, posChange);

                return edge;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize normal
                float3 normalWS = normalize(input.normalWS);

                // Detect edges based on geometry changes
                float geometricEdge = DetectGeometricEdge(normalWS, input.positionWS);
                float positionEdge = DetectPositionEdge(input.positionWS);

                // Combine edge detections
                float finalEdge = saturate(geometricEdge + positionEdge * 0.3);

                // Mix edge color with fill color
                float4 color = lerp(_FillColor, _EdgeColor, finalEdge);

                // Apply basic lighting for depth perception
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float lighting = NdotL * 0.5 + 0.5; // Wrap lighting

                color.rgb *= lighting;

                // Apply fog
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
