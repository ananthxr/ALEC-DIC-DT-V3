Shader "Custom/FresnelGlowEffect"
{
    Properties
    {
        // Base Properties
        _BaseColor ("Base Color", Color) = (0.2, 0.5, 1.0, 1.0)
        _Alpha ("Alpha", Range(0, 1)) = 0.3

        // Emission Properties
        _EmissionColor ("Emission Color", Color) = (0, 1, 1, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 2.0

        // Fresnel Properties
        _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 3.0

        // Grid/Wireframe Properties
        _GridTexture ("Grid Texture", 2D) = "white" {}
        _GridTiling ("Grid Tiling", Range(1, 50)) = 10.0

        // Pulse Properties
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 100

        Pass
        {
            Name "FresnelGlow"

            // Enable transparency
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Properties
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Alpha;
                half4 _EmissionColor;
                half _EmissionIntensity;
                half _FresnelPower;
                float4 _GridTexture_ST;
                half _GridTiling;
                half _PulseSpeed;
            CBUFFER_END

            TEXTURE2D(_GridTexture);
            SAMPLER(sampler_GridTexture);

            // Vertex Input Structure
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            // Vertex Output / Fragment Input Structure
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : NORMAL;
                float3 viewDirWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            // Vertex Shader
            Varyings vert(Attributes input)
            {
                Varyings output;

                // Transform vertex position from object space to clip space
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;

                // Transform normal from object space to world space
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.normalWS = normalInputs.normalWS;

                // Calculate view direction in world space
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);

                // Pass through UV coordinates with tiling
                output.uv = input.uv * _GridTiling;

                return output;
            }

            // Fragment Shader
            half4 frag(Varyings input) : SV_Target
            {
                // Normalize vectors
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                // ===== FRESNEL EFFECT =====
                // Calculate Fresnel: fresnel = 1 - dot(normal, viewDir)
                // This creates edge intensity based on view direction
                half fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                fresnel = pow(fresnel, _FresnelPower); // Apply power for control

                // ===== PULSE EFFECT =====
                // Create pulsing animation using sine wave
                // Sine(Time * PulseSpeed) oscillates between -1 and 1
                // Remap to 0.5 to 1.5 range for subtle pulsing
                half pulseValue = sin(_Time.y * _PulseSpeed) * 0.5 + 1.0;

                // ===== GRID TEXTURE (WIREFRAME) =====
                // Sample the grid texture using UV coordinates
                half4 gridSample = SAMPLE_TEXTURE2D(_GridTexture, sampler_GridTexture, input.uv);
                half gridMask = gridSample.r; // Use red channel as mask

                // ===== EMISSION CALCULATION =====
                // Multiply: EmissionColor * FresnelIntensity * PulseValue * GridMask
                half3 emission = _EmissionColor.rgb * fresnel * _EmissionIntensity * pulseValue * gridMask;

                // ===== BASE COLOR WITH ALPHA =====
                half3 baseColorWithAlpha = _BaseColor.rgb * _Alpha;

                // ===== FINAL OUTPUT =====
                // Final formula: (BaseColor * Alpha) + (Emission * Fresnel * Pulse * Grid)
                half3 finalColor = baseColorWithAlpha + emission;

                // Return final color with alpha for transparency
                return half4(finalColor, _Alpha);
            }

            ENDHLSL
        }
    }

    // Fallback for Built-in Pipeline
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        LOD 100

        Pass
        {
            Name "FresnelGlow_Builtin"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _BaseColor;
            half _Alpha;
            half4 _EmissionColor;
            half _EmissionIntensity;
            half _FresnelPower;
            sampler2D _GridTexture;
            half _GridTiling;
            half _PulseSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normalWS : NORMAL;
                float3 viewDirWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDirWS = normalize(_WorldSpaceCameraPos - worldPos);
                o.uv = v.uv * _GridTiling;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 normalWS = normalize(i.normalWS);
                half3 viewDirWS = normalize(i.viewDirWS);

                // Fresnel
                half fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                fresnel = pow(fresnel, _FresnelPower);

                // Pulse
                half pulseValue = sin(_Time.y * _PulseSpeed) * 0.5 + 1.0;

                // Grid
                half gridMask = tex2D(_GridTexture, i.uv).r;

                // Emission
                half3 emission = _EmissionColor.rgb * fresnel * _EmissionIntensity * pulseValue * gridMask;

                // Base
                half3 baseColorWithAlpha = _BaseColor.rgb * _Alpha;

                // Final
                half3 finalColor = baseColorWithAlpha + emission;

                return half4(finalColor, _Alpha);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Transparent"
}
