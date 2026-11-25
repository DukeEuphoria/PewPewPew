Shader "PewPewPew/AsteroidLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Rock Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _SurfaceMapping("Surface Mapping", Vector) = (1, 1, 0, 0)
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float4 _SurfaceMapping;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
                half fog : TEXCOORD4;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half3 Rock(TEXTURE2D_PARAM(map, samplerMap), float3 coordinates, half3 weights)
            {
                return SAMPLE_TEXTURE2D(map, samplerMap, coordinates.yz * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb * weights.x +
                       SAMPLE_TEXTURE2D(map, samplerMap, coordinates.xz * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb * weights.y +
                       SAMPLE_TEXTURE2D(map, samplerMap, coordinates.xy * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb * weights.z;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float3 source = float3(input.positionOS.xy * _SurfaceMapping.xy + _SurfaceMapping.zw,
                    input.positionOS.z * _SurfaceMapping.x);
                half3 weights = pow(abs(normalize(input.normalOS)), 4);
                weights /= max(dot(weights, half3(1, 1, 1)), 0.0001h);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = Rock(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), source, weights) * _BaseColor.rgb;
                surface.alpha = 1;
                surface.occlusion = 1;
                surface.normalTS = half3(0, 0, 1);
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = NormalizeNormalPerPixel(input.normalWS);
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI = SampleSH(lighting.normalWS);
                lighting.vertexLighting = VertexLighting(input.positionWS, lighting.normalWS);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1, 1, 1, 1);
                half4 colour = UniversalFragmentPBR(lighting, surface);
                colour.rgb = MixFog(colour.rgb, input.fog);
                return colour;
            }
            ENDHLSL
        }
        UsePass "PewPewPew/PlayerColourLit/ShadowCaster"
        UsePass "PewPewPew/PlayerColourLit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
