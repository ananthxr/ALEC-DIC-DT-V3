Shader "Custom/URP/MeteorTrail"
{
    Properties
    {
        [Header(Trail Colors)]
        [HDR] _TrailColor ("Trail Color (HDR)", Color) = (0.8, 1.4, 2.5, 1)
        _TipColor ("Tip Color (Fade End)", Color) = (0.1, 0.3, 0.8, 0)

        [Header(Glow)]
        _GlowIntensity ("Glow Intensity", Range(0, 20)) = 6
        _EdgeGlow ("Edge Glow", Range(0, 5)) = 1.5

        [Header(Animation)]
        _FlowSpeed ("Flow Speed", Range(0, 10)) = 2
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 1.5
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.3

        [Header(Fade)]
        _FadePower ("Fade Power", Range(0.1, 5)) = 1.5
        _AlphaMultiplier ("Alpha Multiplier", Range(0, 2)) = 1
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
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "TrailPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TrailColor;
                half4 _TipColor;
                half _GlowIntensity;
                half _EdgeGlow;
                half _FlowSpeed;
                half _PulseSpeed;
                half _PulseIntensity;
                half _FadePower;
                half _AlphaMultiplier;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = vertexInput.positionCS;
                output.color = input.color;
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // UV.x goes from 0 (newest) to 1 (oldest) along trail
                float trailProgress = input.uv.x;

                // UV.y goes from 0 to 1 across trail width
                float widthGradient = input.uv.y;

                // === EDGE GLOW ===
                // Center of trail is brighter
                float edgeFade = 1.0 - abs(widthGradient - 0.5) * 2.0;
                edgeFade = pow(saturate(edgeFade), _EdgeGlow);

                // === LENGTH FADE ===
                // Trail fades toward the old end
                float lengthFade = 1.0 - pow(saturate(trailProgress), _FadePower);

                // === ANIMATED PULSE ===
                float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                pulse = lerp(1.0, pulse, _PulseIntensity);

                // === FLOWING EFFECT ===
                // Create moving waves along the trail
                float flow = sin(trailProgress * 10.0 - _Time.y * _FlowSpeed) * 0.5 + 0.5;
                flow = pow(saturate(flow), 2.0) * 0.3 + 0.7; // Softened wave

                // === COLOR GRADIENT ===
                // Interpolate from trail color to tip color
                half3 trailGradient = lerp(_TrailColor.rgb, _TipColor.rgb, trailProgress);

                // Apply glow intensity
                half3 finalColor = trailGradient * _GlowIntensity * pulse * flow;

                // === CALCULATE ALPHA ===
                half alpha = lengthFade * edgeFade;
                alpha *= _TrailColor.a;
                alpha *= pulse;
                alpha *= _AlphaMultiplier;
                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
