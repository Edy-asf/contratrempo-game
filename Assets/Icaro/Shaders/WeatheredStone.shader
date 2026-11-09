// Pedra das peças góticas dos Picos com desgaste e fuligem (pedido do usuário em 2026-11-08): a textura em pixel art da
// peça (UV do Blender, como o Lit) e, por cima, no grão dos pixels do mundo (16 px/m, projeção pelo eixo dominante):
// sujeira subindo do chão (pela altura acima da origem da peça, que fica no chão), escorridos verticais de chuva nas
// paredes, manchas de pedra gasta e manchas de fuligem (mais fortes em cima, como a fumaça que subiu, com a beira em
// degraus de pixel e xadrez na passagem). Tudo escurece a cor, sem textura nova. A força de cada efeito fica no
// material, e _IcaroSoot (global, 0 = só a do material) soma fuligem para as áreas do ataque. Luz PBR do URP e névoa.
Shader "Icaro/WeatheredStone"
{
    Properties
    {
        _BaseMap("Textura", 2D) = "white" {}
        _BaseColor("Cor", Color) = (1, 1, 1, 1)
        _Smoothness("Lisura", Range(0, 1)) = 0
        _Metallic("Metálico", Range(0, 1)) = 0
        _TexelsPerMeter("Pixels por metro", Float) = 16

        [Header(Sujeira do chao)]
        _GrimeColor("Cor da sujeira", Color) = (0.36, 0.33, 0.31, 1)
        _GrimeAmount("Força", Range(0, 1)) = 0.5
        _GrimeHeight("Altura (m)", Float) = 2.2

        [Header(Escorridos)]
        _StreakAmount("Força", Range(0, 1)) = 0.35
        _StreakDensity("Quantidade", Range(0, 1)) = 0.18
        _StreakLength("Comprimento (m)", Float) = 2.5

        [Header(Pedra gasta)]
        _WearAmount("Força", Range(0, 1)) = 0.3
        _WearScale("Tamanho das manchas (m)", Float) = 3

        [Header(Fuligem)]
        _SootColor("Cor da fuligem", Color) = (0.16, 0.14, 0.14, 1)
        _SootAmount("Quantidade", Range(0, 1)) = 0.22
        _SootStrength("Força", Range(0, 1)) = 0.75
        _SootScale("Tamanho das manchas (m: largura, altura)", Vector) = (5, 9, 0, 0)

        [HideInInspector] _Cutoff("Recorte", Float) = 0.5
        [HideInInspector] _SpecularHighlights("Brilho", Float) = 0
        [HideInInspector] _EnvironmentReflections("Reflexo", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _Smoothness;
                half _Metallic;
                float _TexelsPerMeter;
                half4 _GrimeColor;
                half _GrimeAmount;
                float _GrimeHeight;
                half _StreakAmount;
                half _StreakDensity;
                float _StreakLength;
                half _WearAmount;
                float _WearScale;
                half4 _SootColor;
                half _SootAmount;
                half _SootStrength;
                float4 _SootScale;
                half _SpecularHighlights;
                half _EnvironmentReflections;
            CBUFFER_END

            half _IcaroSoot;

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

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
                float groundY : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.groundY = TransformObjectToWorld(float3(0, 0, 0)).y;   // a origem das peças fica no chão
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            // Hash inteiro (as Vilas do Alto ficam a ~5 km da origem: seno/frac perderia a precisão lá)
            float Hash(float2 p)
            {
                uint2 x = asuint(int2(floor(p)));
                uint2 q = 1103515245u * ((x >> 1u) ^ x.yx);
                uint n = 1103515245u * (q.x ^ (q.y >> 3u));
                return float(n) * (1.0 / 4294967295.0);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = p - i;
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                return ValueNoise(p) * 0.55 + ValueNoise(p * 2.3 + 17.1) * 0.3 + ValueNoise(p * 5.1 + 3.7) * 0.15;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 p = input.positionWS;
                float3 a = abs(n);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;

                // Grão dos pixels no mundo (nas paredes, v = altura): os efeitos andam em degraus de pixel como a textura
                bool wall = a.y < 0.6;
                float2 q;
                if (!wall)
                    q = p.xz;
                else if (a.x >= a.z)
                    q = float2(p.z * sign(n.x), p.y);
                else
                    q = float2(-p.x * sign(n.z), p.y);
                float texels = _TexelsPerMeter;
                float2 pixel = floor(q * texels);
                float2 grain = (pixel + 0.5) / texels;
                float checker = frac((pixel.x + pixel.y) * 0.5) > 0.25 ? 0.06 : -0.06;
                float height = p.y - input.groundY;

                // Sujeira subindo do chão, borda de cima recortada por ruído, em três degraus
                float grime = saturate(1 - height / _GrimeHeight + (Fbm(grain * 1.3 + 4.1) - 0.5) * 0.9 + checker);
                albedo = lerp(albedo, albedo * _GrimeColor.rgb, floor(grime * 3) / 3 * _GrimeAmount);

                // Pedra gasta: manchas grandes um pouco mais escuras, em dois degraus
                float wear = Fbm(grain / _WearScale + 23.7) + checker * 0.5;
                albedo *= 1 - (wear > 0.62 ? 1 : wear > 0.55 ? 0.5 : 0) * _WearAmount * 0.6;

                // Escorridos de chuva: colunas de 1 pixel nas paredes, cada uma com o próprio comprimento, mais fortes em cima
                if (wall)
                {
                    float column = pixel.x;
                    if (Hash(float2(column, 91)) < _StreakDensity)
                    {
                        float run = frac(grain.y / _StreakLength + Hash(float2(column, 17)));
                        float fade = 1 - run;
                        if (ValueNoise(float2(column * 0.37, grain.y / _StreakLength * 3)) > 0.35)
                            albedo *= 1 - floor(fade * 3) / 3 * _StreakAmount * 0.8;
                    }
                }

                // Fuligem: manchas esticadas na vertical, mais densas em cima (a fumaça sobe), beira em degraus com xadrez
                half sootAmount = saturate(_SootAmount + _IcaroSoot);
                if (sootAmount > 0)
                {
                    float field = Fbm(grain / _SootScale.xy + 71.9) + saturate(height / 12) * 0.18;
                    float edge = field - (0.8 - sootAmount * 0.8) + checker * 0.3;   // 0,2 cobre ~1/5 da parede
                    half soot = edge > 0.06 ? 1 : edge > 0.02 ? 0.65 : edge > 0 ? 0.35 : 0;
                    albedo = lerp(albedo, _SootColor.rgb * (0.8 + Hash(pixel + 5.5) * 0.4), soot * _SootStrength);
                }

                float3 view = GetWorldSpaceNormalizeViewDir(p);
                InputData inputData = (InputData)0;
                inputData.positionWS = p;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = view;
                inputData.shadowCoord = TransformWorldToShadowCoord(p);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.alpha = 1;
                surface.occlusion = 1;
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.specular = 0;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }
}
