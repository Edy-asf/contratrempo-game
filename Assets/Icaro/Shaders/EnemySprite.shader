// Sprite dos inimigos (EnemySpriteView e cadáveres): a mesma luz do Lit dos sprites (recorte por alfa, sem brilho
// especular nem reflexos, emissão pelo mapa de brilho e _EmissionColor, névoa, sombras recebidas), com o destaque
// dos inimigos (EnemyHighlightSettings, força nas configurações):
// - contorno de largura fixa na tela em volta da silhueta, numa cor que o cenário não usa (vermelho); o quad cresce
//   no vértice o bastante para o contorno caber fora da célula, e a célula da folha (_BaseMap_ST) limita as amostras;
// - a névoa pesa menos sobre o inimigo (a fração _IcaroEnemyHighlight.z da névoa normal).
// Globais (EnemyHighlightSettings.Push): _IcaroEnemyHighlight (x força 0 a 1, y largura do contorno em pixels a 1080p,
// z fração da névoa) e _IcaroEnemyOutlineColor. Sem eles (fora do Play), sem contorno e com a névoa normal.
// _OutlineOff 1 (bloco de propriedades) desliga o contorno de um sprite (os cadáveres).
Shader "Icaro/EnemySprite"
{
    Properties
    {
        [MainTexture] _BaseMap("Folha", 2D) = "white" {}
        [MainColor] _BaseColor("Cor", Color) = (1, 1, 1, 1)
        _Cutoff("Recorte", Range(0, 1)) = 0.5
        _EmissionMap("Brilho", 2D) = "black" {}
        [HDR] _EmissionColor("Cor do brilho", Color) = (0.25, 0.25, 0.25, 1)
        _PixelsPerMeter("Pixels por metro da folha", Float) = 16
        _OutlineOff("Sem contorno", Float) = 0
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
        TEXTURE2D(_EmissionMap);
        SAMPLER(sampler_EmissionMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseMap_TexelSize;
            half4 _BaseColor;
            half _Cutoff;
            half4 _EmissionColor;
            float _PixelsPerMeter;
            float _OutlineOff;
        CBUFFER_END

        float4 _IcaroEnemyHighlight;
        half4 _IcaroEnemyOutlineColor;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Cresce o quad para caber o contorno: devolve a posição no objeto, a UV na folha e o raio do contorno na
        // UV da folha (0 = sem contorno)
        void Expand(Attributes input, out float3 positionOS, out float2 uv, out float2 radius)
        {
            positionOS = input.positionOS.xyz;
            float2 quadUV = input.uv;
            radius = 0;

            bool on = _IcaroEnemyHighlight.x > 0.001 && _OutlineOff < 0.5 && _IcaroEnemyHighlight.y > 0;
            if (on)
            {
                float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
                float2 scale = float2(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21));
                float dist = distance(originWS, _WorldSpaceCameraPos);
                // Tamanho de um pixel da tela nessa distância (perspectiva), com a largura pedida a 1080p
                float pixelWS = dist * 2.0 / (abs(UNITY_MATRIX_P._m11) * _ScreenParams.y);
                float widthWS = _IcaroEnemyHighlight.y * (_ScreenParams.y / 1080.0) * pixelWS;

                float2 cellPx = _BaseMap_ST.xy * _BaseMap_TexelSize.zw;
                float2 quadOS = cellPx / max(_PixelsPerMeter, 0.001);
                float2 marginOS = widthWS / max(scale, 0.0001);
                float2 side = input.uv * 2.0 - 1.0;
                positionOS.xy += side * marginOS;
                float2 marginUV = marginOS / max(quadOS, 0.0001);
                quadUV += side * marginUV;
                radius = marginUV * _BaseMap_ST.xy;
            }

            uv = quadUV * _BaseMap_ST.xy + _BaseMap_ST.zw;
        }

        // Alfa da folha só dentro da célula atual (fora dela, vazio)
        half CellAlpha(float2 uv)
        {
            float2 lo = _BaseMap_ST.zw, hi = _BaseMap_ST.zw + _BaseMap_ST.xy;
            if (any(uv < lo) || any(uv > hi))
                return 0;
            return SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 0).a * _BaseColor.a;
        }

        // Algum ponto da silhueta a até o raio (dois anéis de 8 direções, para não pular partes finas)
        half Near(float2 uv, float2 radius)
        {
            static const float2 dirs[8] =
            {
                float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
                float2(0.7071, 0.7071), float2(-0.7071, 0.7071), float2(0.7071, -0.7071), float2(-0.7071, -0.7071),
            };
            half a = 0;
            [unroll]
            for (int i = 0; i < 8; i++)
            {
                a = max(a, CellAlpha(uv + dirs[i] * radius));
                a = max(a, CellAlpha(uv + dirs[i] * radius * 0.5));
            }
            return a;
        }

        // Névoa mais leve sobre o inimigo
        half3 EnemyFog(half3 color, float fogCoord)
        {
            half3 fogged = MixFog(color, fogCoord);
            half keep = _IcaroEnemyHighlight.x > 0.001 ? saturate(_IcaroEnemyHighlight.z) : 1;
            return lerp(color, fogged, keep);
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
                float2 radius : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS;
                Expand(input, positionOS, output.uv, output.radius);
                output.positionWS = TransformObjectToWorld(positionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);

                if (CellAlpha(input.uv) < _Cutoff)
                {
                    // Fora da silhueta: contorno, se houver silhueta perto
                    clip(input.radius.x > 0 ? Near(input.uv, input.radius) - _Cutoff : -1);
                    return half4(EnemyFog(_IcaroEnemyOutlineColor.rgb, fogCoord), 1);
                }

                half4 albedo = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, input.uv, 0) * _BaseColor;
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.alpha = 1;
                surface.metallic = 0;
                surface.smoothness = 0;
                surface.occlusion = 1;
                surface.specular = 0;
                surface.emission = SAMPLE_TEXTURE2D_LOD(_EmissionMap, sampler_EmissionMap, input.uv, 0).rgb * _EmissionColor.rgb;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = fogCoord;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                return half4(EnemyFog(color.rgb, fogCoord), 1);
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
                float3 positionOS;
                float2 radius;
                Expand(input, positionOS, output.uv, radius);
                output.positionCS = TransformObjectToHClip(positionOS);
                return output;
            }

            half Frag(Varyings input) : SV_Target
            {
                clip(CellAlpha(input.uv) - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
