Shader "Blackout/Reveal"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.6, 0.6, 0.6, 1)
        _BaseMap ("Base Map", 2D) = "white" {}
        _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _Pattern ("Pattern (0 surface, 1 windows, 2 powered glow, 3 always glow)", Float) = 0
        _LineDensity ("Line Density", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _EmissionColor;
            float _Pattern;
            float _LineDensity;
            float4 _BaseMap_ST;
        CBUFFER_END
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #define PULSES 12
            #define LAMPS 16

            float4 _PulseOrigin[PULSES];
            float4 _PulseShape[PULSES];
            float4 _LampPos[LAMPS];
            float _LampCount;
            float _RevealTime;
            float _Ambient;
            float _HighContrast;
            float _SoftPulse;
            float _CityPower;
            float _Dawn;
            float4 _SkyAmbient;
            float4 _SunColor;
            float4 _SunDir;
            float4 _MarketCenter;
            float4 _Lantern;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float LineMask(float x, float width)
            {
                float f = abs(frac(x) - 0.5) * 2.0;
                float w = max(fwidth(x) * 2.0, 0.0001);
                return smoothstep(1.0 - width - w, 1.0 - width + w, f);
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            half3 KindColor(float kind)
            {
                if (kind < 0.5) return half3(0.30, 0.80, 1.00);
                if (kind < 1.5) return half3(1.00, 0.62, 0.08);
                return half3(0.88, 0.90, 0.96);
            }

            half3 Windows(float3 positionWS, float3 an)
            {
                float across = an.x > an.z ? positionWS.z : positionWS.x;
                float depth = an.x > an.z ? positionWS.x : positionWS.z;
                float2 grid = float2(across, positionWS.y) / float2(2.4, 3.2);
                float2 cell = floor(grid);
                float2 f = frac(grid);
                float inside = step(0.28, f.x) * step(f.x, 0.72) * step(0.3, f.y) * step(f.y, 0.78) * (1.0 - an.y) * step(1.5, positionWS.y);
                float2 id = cell + floor(depth) * 0.37;
                float wave = Hash(id);
                float kind = Hash(id + 17.3);
                float distanceToMarket = distance(positionWS.xz, _MarketCenter.xz);
                float onAt = saturate(distanceToMarket / 110.0) * 0.7 + wave * 0.25;
                float lit = smoothstep(onAt, onAt + 0.04, _CityPower) * step(kind, 0.82);
                half3 warm = lerp(half3(1.0, 0.74, 0.42), half3(0.72, 0.84, 1.0), step(0.6, kind));
                float candle = step(0.955, kind) * 0.22 * (0.8 + 0.2 * sin(_RevealTime * 2.7 + wave * 40.0));
                return inside * (lit * warm * 1.5 + candle * half3(1.0, 0.45, 0.12));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                float3 v = normalize(GetCameraPositionWS() - input.positionWS);
                float3 an = abs(n);
                float3 p = input.positionWS * _LineDensity;
                float contour = LineMask(p.y, 0.07) * (1.0 - an.y);
                float grid = max(LineMask(p.x, 0.05), LineMask(p.z, 0.05)) * saturate(an.y * 2.0 - 1.0);
                float pattern = max(contour, grid);
                float rim = pow(1.0 - saturate(dot(n, v)), 3.0);

                half3 reveal = 0;
                [unroll]
                for (int k = 0; k < PULSES; k++)
                {
                    float4 o = _PulseOrigin[k];
                    float4 s = _PulseShape[k];
                    float age = _RevealTime - o.w;
                    float valid = step(0.001, s.x) * step(0.0, age);
                    float speed = max(s.y, 0.01);
                    float d = distance(input.positionWS, o.xyz);
                    float front = age * speed;
                    float fade = (s.z > 0.5 && s.z < 1.5) ? 1.4 : 2.8;
                    float since = age - d / speed;
                    float inside = step(d, min(front, s.x));
                    float after = inside * saturate(1.0 - since / fade);
                    float width = 0.28 + 0.4 * _SoftPulse;
                    float ringShape = (d - front) / width;
                    float ring = exp(-ringShape * ringShape) * step(front, s.x + 0.6) * (1.0 - 0.65 * _SoftPulse);
                    float reach = saturate(1.0 - d / max(s.x, 0.01));
                    float amount = (after * after * 0.55 + ring) * s.w * (0.2 + 0.8 * reach) * valid;
                    reveal += KindColor(s.z) * amount;
                }

                float contrast = 1.0 + _HighContrast * 0.9;
                half3 surface = reveal * (0.08 + pattern * 1.15 + rim * 0.75) * contrast;
                half3 dark = surface + (albedo * 0.5 + 0.25) * _Ambient * (0.08 + pattern * 2.2 + rim * 1.2);
                float glow = saturate(1.0 - distance(input.positionWS, _Lantern.xyz) / max(_Lantern.w, 0.01));
                dark += albedo * half3(1.0, 0.72, 0.42) * glow * glow * 1.6 * step(0.01, _Lantern.w);

                half3 lamps = 0;
                [unroll]
                for (int l = 0; l < LAMPS; l++)
                {
                    float4 lamp = _LampPos[l];
                    float a = saturate(1.0 - distance(input.positionWS, lamp.xyz) / max(lamp.w, 0.01));
                    lamps += half3(1.0, 0.7, 0.4) * a * a * 1.8 * step((float)l + 0.5, _LampCount);
                }
                lamps *= _CityPower;

                float litAmount = saturate(max(_CityPower, _Dawn));
                half3 light = _SkyAmbient.rgb * (0.55 + 0.45 * n.y) + _SunColor.rgb * saturate(dot(n, normalize(_SunDir.xyz))) + lamps;
                half3 lit = albedo * light * (1.0 + pattern * 0.08);
                half3 color = lerp(dark, lit + surface * 0.4, litAmount);

                if (_Pattern > 0.5 && _Pattern < 1.5) color += Windows(input.positionWS, an);
                if (_Pattern > 1.5 && _Pattern < 2.5) color += _EmissionColor.rgb * (0.4 + albedo) * _CityPower;
                if (_Pattern > 2.5) color += _EmissionColor.rgb * (0.85 + 0.15 * sin(_RevealTime * 3.1));
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
