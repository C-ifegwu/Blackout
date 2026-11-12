Shader "Blackout/Glow"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.8, 1, 1)
        _Mode ("Mode (0 flat, 1 vignette, 2 ring)", Float) = 0
        _Strength ("Strength", Float) = 0
        _Black ("Black", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Mode;
                float _Strength;
                float _Black;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float r = length(input.uv - 0.5) * 2.0;
                if (_Mode < 0.5)
                {
                    return _Color * input.color;
                }
                if (_Mode < 1.5)
                {
                    float inner = lerp(1.3, 0.4, _Strength);
                    float outer = lerp(1.5, 0.95, _Strength);
                    float a = smoothstep(inner, outer, r) * step(0.01, _Strength);
                    return half4(0, 0, 0, max(a, _Black));
                }
                float ring = smoothstep(0.62, 0.72, r) * (1.0 - smoothstep(0.88, 1.0, r));
                float core = (1.0 - smoothstep(0.0, 0.3, r)) * 0.6;
                return half4(_Color.rgb, _Color.a * max(ring, core));
            }
            ENDHLSL
        }
    }
}
