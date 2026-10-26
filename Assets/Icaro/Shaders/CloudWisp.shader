// Fiapos de névoa (Picos acima das Nuvens): tufos de fumaça que olham para a câmera, enrolados nos picos e na beira das
// praças, por cima do mar de nuvens de volume (Icaro/CloudVolume). A malha é estática: cada tufo é um quadrado com o
// centro na posição, o canto em UV0 (-1 a 1) e em UV1 o tamanho (m), uma semente, a opacidade e quanto pega da luz; a cor
// do vértice tinge. No vértice o tufo vira para a câmera, gira devagar e deriva em volta do centro pelo relógio das
// nuvens (_IcaroCloudTime, parado a 1,0x). A textura é o tufo gerado em jogo (_IcaroCloudPuff: A densidade, G o lado
// claro); o lado virado para a luz principal (o eclipse) acende. Suave onde encosta no cenário (profundidade da cena),
// some perto da câmera (para não tampar a vista ao atravessar) e recebe a névoa da cena.
Shader "Icaro/CloudWisp"
{
    Properties
    {
        [HDR] _LitColor("Lado claro", Color) = (0.78, 0.7, 0.86, 1)
        [HDR] _ShadowColor("Lado escuro", Color) = (0.32, 0.26, 0.42, 1)
        _SunStrength("Força da luz", Float) = 0.9
        _Opacity("Opacidade", Range(0, 1)) = 0.55
        _SoftDistance("Suavizar no cenário (m)", Float) = 6
        _NearFade("Some perto da câmera (m)", Vector) = (3, 14, 0, 0)
        _Drift("Deriva (m)", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Fiapos"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LitColor;
                half4 _ShadowColor;
                float _SunStrength;
                float _Opacity;
                float _SoftDistance;
                float4 _NearFade;
                float _Drift;
            CBUFFER_END

            TEXTURE2D(_IcaroCloudPuff);
            SAMPLER(sampler_IcaroCloudPuff);
            float _IcaroCloudTime;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 corner : TEXCOORD0;
                float4 data : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 extra : TEXCOORD1;   // x opacidade, y luz do lado, z fator de névoa
                float eye : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float size = input.data.x, seed = input.data.y;
                float time = _IcaroCloudTime;
                float3 center = TransformObjectToWorld(input.positionOS.xyz);
                center += float3(sin(time * 0.11 + seed * 6.3), sin(time * 0.07 + seed * 3.1) * 0.3,
                    cos(time * 0.09 + seed * 4.7)) * _Drift;

                float angle = seed * 6.2832 + time * 0.03 * (frac(seed * 7.3) - 0.5);
                float s, c;
                sincos(angle, s, c);
                float2 corner = float2(input.corner.x * c - input.corner.y * s, input.corner.x * s + input.corner.y * c);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 offset = (right * corner.x + up * corner.y) * size * (1 + 0.06 * sin(time * 0.13 + seed * 9.0));
                float3 positionWS = center + offset;

                Light sun = GetMainLight();
                float side = saturate(dot(normalize(right * input.corner.x + up * input.corner.y + 0.001), sun.direction));

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.corner * 0.5 + 0.5;
                output.color = input.color;
                output.extra = float3(input.data.z, side * input.data.w, ComputeFogFactor(output.positionCS.z));
                output.eye = -TransformWorldToView(positionWS).z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 puff = SAMPLE_TEXTURE2D(_IcaroCloudPuff, sampler_IcaroCloudPuff, input.uv);
                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float scene = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float soft = saturate((scene - input.eye) / _SoftDistance);
                float nearFade = saturate((input.eye - _NearFade.x) / max(_NearFade.y - _NearFade.x, 0.01));
                float alpha = puff.a * input.extra.x * _Opacity * soft * nearFade * input.color.a;

                Light sun = GetMainLight();
                half3 color = lerp(_ShadowColor.rgb, _LitColor.rgb, puff.g) * input.color.rgb;
                color += sun.color * _SunStrength * input.extra.y * puff.g;
                color = MixFog(color, input.extra.z);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
