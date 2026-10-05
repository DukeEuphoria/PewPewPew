Shader "PewPewPew/PlayerColourLit"
{
    // Pixels close to Key0 / Key1 (by hue, any brightness) are replaced by the player's colours, then lit with URP lights.
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1, 1, 1, 1)
        _Key0("Key Colour 0", Color) = (1, 0, 1, 1)
        _Key1("Key Colour 1", Color) = (0, 1, 0, 1)
        _KeyTolerance("Key Tolerance", Range(0.01, 1)) = 0.25
        _PlayerColour0("Player Colour 0", Color) = (1, 0, 1, 1)
        _PlayerColour1("Player Colour 1", Color) = (0, 1, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Key0;
                half4 _Key1;
                half _KeyTolerance;
                half4 _PlayerColour0;
                half4 _PlayerColour1;
            CBUFFER_END

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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half3 Hue(half3 colour)
            {
                return colour / max(max(colour.r, max(colour.g, colour.b)), 1e-4h);
            }

            // 1 where the pixel's hue is the key's, fading to 0 across the tolerance.
            half KeyWeight(half3 colour, half3 key)
            {
                half distanceToKey = distance(Hue(colour), Hue(key));
                return 1.0h - smoothstep(_KeyTolerance * 0.5h, _KeyTolerance, distanceToKey);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 albedo = (SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor).rgb;
                half brightness = max(albedo.r, max(albedo.g, albedo.b));

                albedo = lerp(albedo, _PlayerColour0.rgb * brightness, KeyWeight(albedo, _Key0.rgb));
                albedo = lerp(albedo, _PlayerColour1.rgb * brightness, KeyWeight(albedo, _Key1.rgb));

                half3 normal = normalize(input.normalWS);
                uint meshRenderingLayers = GetMeshRenderingLayer();
                half3 lighting = SampleSH(normal);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                #if defined(_LIGHT_LAYERS)
                if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
                #endif
                {
                    lighting += mainLight.color * (saturate(dot(normal, mainLight.direction)) * mainLight.distanceAttenuation * mainLight.shadowAttenuation);
                }

                #if defined(_ADDITIONAL_LIGHTS)
                // LIGHT_LOOP_BEGIN reads these two fields to find the Forward+ cluster.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();

                // Under Forward+ directional lights sit outside the cluster loop and need their own pass.
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint directionalIndex = 0; directionalIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); directionalIndex++)
                {
                    Light light = GetAdditionalLight(directionalIndex, input.positionWS);
                    #if defined(_LIGHT_LAYERS)
                    if (!IsMatchingLightLayer(light.layerMask, meshRenderingLayers)) continue;
                    #endif
                    lighting += light.color * (saturate(dot(normal, light.direction)) * light.distanceAttenuation * light.shadowAttenuation);
                }
                #endif

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    #if defined(_LIGHT_LAYERS)
                    if (!IsMatchingLightLayer(light.layerMask, meshRenderingLayers)) continue;
                    #endif
                    lighting += light.color * (saturate(dot(normal, light.direction)) * light.distanceAttenuation * light.shadowAttenuation);
                LIGHT_LOOP_END
                #endif

                return half4(albedo * lighting, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirection = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
