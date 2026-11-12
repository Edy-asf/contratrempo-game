// Pano de bandeirola (Picos): malha comum de pixel art, com a mesma luz do Lit dos sprites (sem brilho especular nem
// reflexos, recorte por alfa, névoa, sombras recebidas). O pano tremula no vértice: a cor do vértice diz quanto cada
// ponto balança (G: 0 na corda, 1 na ponta solta) e a fase de cada bandeira (R); o vento empurra ao longo da normal do
// pano, em ondas que correm da corda para a ponta. O relógio anda com o multiplicador de tempo (_IcaroCloudTime, o
// relógio das nuvens, anda multiplicador - 1 por segundo), então o pano bate mais rápido com o tempo acelerado.
Shader "Icaro/FlagCloth"
{
    Properties
    {
        [MainTexture] _BaseMap("Textura", 2D) = "white" {}
        [MainColor] _BaseColor("Cor", Color) = (1, 1, 1, 1)
        _Cutoff("Recorte", Range(0, 1)) = 0.5
        _Amplitude("Balanço (m)", Float) = 0.12
        _Speed("Velocidade", Float) = 5
        _TimeFollow("Segue o multiplicador (0 a 1)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            float _Amplitude;
            float _Speed;
            float _TimeFollow;
        CBUFFER_END

        float _IcaroCloudTime;
        // Relógio do mundo (KairosClock): parado fora de combate; sem ele (_IcaroWorldClockOn 0), o relógio normal
        float _IcaroWorldTime;
        float _IcaroWorldClockOn;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Posição no mundo com o tremular do pano
        float3 Wave(Attributes input, out float3 normalWS)
        {
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            normalWS = TransformObjectToWorldNormal(input.normalOS);
            float clock = lerp(_Time.y, _IcaroWorldTime, _IcaroWorldClockOn) + _IcaroCloudTime * _TimeFollow;
            float weight = input.color.g;
            float phase = input.color.r * 6.2832;
            float wave = sin(clock * _Speed + phase - weight * 3.0) + 0.5 * sin(clock * _Speed * 1.7 + phase * 2.0 - weight * 5.0);
            positionWS += normalWS * wave * _Amplitude * weight;
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #define _ALPHATEST_ON 1
            #define _SPECULARHIGHLIGHTS_OFF 1
            #define _ENVIRONMENTREFLECTIONS_OFF 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 normalWS;
                float3 positionWS = Wave(input, normalWS);
                output.positionWS = positionWS;
                output.normalWS = (half3)normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(albedo.a - _Cutoff);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.alpha = 1;
                surface.metallic = 0;
                surface.smoothness = 0;
                surface.occlusion = 1;
                surface.specular = 0;

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                normalWS = IS_FRONT_VFACE(face, normalWS, -normalWS);
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
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
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS;
                output.positionCS = TransformWorldToHClip(Wave(input, normalWS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half Frag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
