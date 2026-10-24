// Ruínas gigantes ao fundo (Arquipélago): sem luz em tempo real, só o tom da textura, um sombreado fixo por face (as
// facetas continuam legíveis) e uma névoa própria que nunca cobre tudo (_MaxFog). Assim, estruturas a centenas de
// metros aparecem como vultos na névoa, enquanto a névoa do jogo continua densa perto do jogador.
Shader "Icaro/RuinSilhouette"
{
    Properties
    {
        [MainTexture] _BaseMap("Textura", 2D) = "white" {}
        [MainColor] _BaseColor("Cor", Color) = (1, 1, 1, 1)
        _FogDensity("Densidade da névoa própria (por metro)", Range(0, 0.02)) = 0.004
        _MaxFog("Névoa máxima (1 = some)", Range(0, 1)) = 0.82
        _LightDir("Direção da luz fixa", Vector) = (-0.4, 0.8, -0.3, 0)
        _Shade("Contraste das faces", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Silhouette"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _FogDensity;
                half _MaxFog;
                float4 _LightDir;
                half _Shade;
            CBUFFER_END

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
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half light = saturate(dot(normalize(input.normalWS), normalize(_LightDir.xyz)));
                half3 color = albedo * lerp(1.0 - _Shade, 1.0, light);

                float distance = length(input.positionWS - _WorldSpaceCameraPos);
                float d = distance * _FogDensity;
                half fog = (1.0 - exp(-d * d)) * _MaxFog;
                return half4(lerp(color, unity_FogColor.rgb, fog), 1.0);
            }
            ENDHLSL
        }
    }
}
