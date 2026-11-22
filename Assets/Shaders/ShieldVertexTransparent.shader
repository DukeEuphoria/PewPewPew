Shader "PewPewPew/ShieldVertexTransparent"
{
    Properties
    {
        [MainTexture] _BaseMap("Shield Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Flash Colour", Color) = (1, 1, 1, 1)
        [HideInInspector] _UseVertexColors("Use Vertex Colours", Float) = 1
        [HideInInspector] _ShieldAlpha("Uniform Shield Alpha", Float) = 1
        [HideInInspector] _LatitudeFadeAngle("Latitude Fade Angle", Float) = 0.7853982
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _UseVertexColors;
                half _ShieldAlpha;
                float _LatitudeFadeAngle;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 positionOS : TEXCOORD1;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = lerp(half4(1, 1, 1, 1), input.color, _UseVertexColors);
                output.positionOS = input.positionOS.xyz;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.color;
                color.a *= _ShieldAlpha;
                float latitude = atan2(abs(input.positionOS.z), length(input.positionOS.xy));
                color.a *= saturate(1.0 - latitude / max(_LatitudeFadeAngle, 0.001745329));
                return color;
            }
            ENDHLSL
        }
    }
}
