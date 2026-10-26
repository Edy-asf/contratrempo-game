// Rocha nevada das montanhas dos Picos: rocha e neve em pixel art misturadas pixel a pixel, sem depender de UV. As
// texturas são projetadas pelo mundo (a face mais alinhada com cada eixo escolhe a projeção; nas paredes o "para
// cima" da textura é o do mundo), então nada estica. Neve onde a face é plana ou virada para cima (normal suave da
// malha), mais nas alturas, mais onde o gerador marcou (UV0.x: topos e degraus), em riscos que descem pelas fendas
// e com a borda recortada por ruído medido no grão dos pixels da textura (bordas em degraus de pixel, sem borrão).
// Na borda da neve, uma fileira de pixels de neve à sombra e, logo abaixo, a rocha escurecida (contorno de pixel art).
// Luz do URP (sol com sombras, luzes extras, ambiente) e névoa.
Shader "Icaro/SnowRock"
{
    Properties
    {
        [NoScaleOffset] _RockMap("Rocha", 2D) = "gray" {}
        [NoScaleOffset] _SnowMap("Neve", 2D) = "white" {}
        _Tile("Metros por repetição", Float) = 4
        _TexelsPerMeter("Pixels por metro (grão das bordas)", Float) = 16
        _RockTint("Tom da rocha", Color) = (1, 1, 1, 1)
        _SnowTint("Tom da neve", Color) = (1, 1, 1, 1)
        _SnowEdge("Neve da borda", Color) = (0.72, 0.76, 0.92, 1)
        _RockEdge("Rocha da borda", Range(0, 1)) = 0.55
        _SnowAmount("Quantidade de neve", Range(0, 1.5)) = 0.42
        _SnowNoise("Recorte da borda", Range(0, 1)) = 0.3
        _NoiseScale("Escala do recorte (m)", Float) = 3
        _Streaks("Riscos nas fendas", Range(0, 1)) = 0.08
        _HeightLow("Altura sem bônus (m)", Float) = -110
        _HeightHigh("Altura com bônus cheio (m)", Float) = 80
        _HeightBonus("Bônus da altura", Range(0, 1)) = 0.1
        _EdgeWidth("Largura da borda", Range(0, 0.3)) = 0.06

        // Usados pelas passagens de sombra e profundidade do Lit
        [HideInInspector] _BaseMap("Base", 2D) = "white" {}
        [HideInInspector] _BaseColor("Cor", Color) = (1, 1, 1, 1)
        [HideInInspector] _Cutoff("Recorte", Float) = 0.5
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
                float _Tile;
                float _TexelsPerMeter;
                half4 _RockTint;
                half4 _SnowTint;
                half4 _SnowEdge;
                half _RockEdge;
                half _SnowAmount;
                half _SnowNoise;
                float _NoiseScale;
                half _Streaks;
                float _HeightLow;
                float _HeightHigh;
                half _HeightBonus;
                half _EdgeWidth;
            CBUFFER_END

            TEXTURE2D(_RockMap);
            SAMPLER(sampler_RockMap);
            TEXTURE2D(_SnowMap);
            SAMPLER(sampler_SnowMap);

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
                float bias : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.bias = input.uv.x;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
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

                // Projeção pelo eixo dominante (nas paredes, v = altura do mundo)
                float2 uv;
                if (a.y >= a.x && a.y >= a.z)
                    uv = p.xz;
                else if (a.x >= a.z)
                    uv = float2(p.z * sign(n.x), p.y);
                else
                    uv = float2(-p.x * sign(n.z), p.y);

                // Ruído do recorte medido no grão dos pixels (a borda anda em degraus de pixel)
                float2 grain = (floor(uv * _TexelsPerMeter) + 0.5) / _TexelsPerMeter;
                float edgeNoise = Fbm(grain / _NoiseScale) - 0.5;
                float streak = ValueNoise(float2(grain.x * 1.6, grain.y * 0.08)) ;
                streak = saturate((streak - 0.62) * 4) * _Streaks * saturate(1 - a.y * 1.5);

                float height = saturate((p.y - _HeightLow) / (_HeightHigh - _HeightLow)) * _HeightBonus;
                float mask = n.y - (1 - _SnowAmount) + edgeNoise * _SnowNoise + height + input.bias * 0.45 + streak;

                half3 rock = SAMPLE_TEXTURE2D(_RockMap, sampler_RockMap, uv / _Tile).rgb * _RockTint.rgb;
                half3 snow = SAMPLE_TEXTURE2D(_SnowMap, sampler_SnowMap, uv / _Tile).rgb * _SnowTint.rgb;
                half3 albedo;
                if (mask > _EdgeWidth)
                    albedo = snow;
                else if (mask > 0)
                    albedo = snow * _SnowEdge.rgb;
                else if (mask > -_EdgeWidth)
                    albedo = rock * _RockEdge;
                else
                    albedo = rock;

                InputData inputData = (InputData)0;
                inputData.positionWS = p;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(p);
                inputData.shadowCoord = TransformWorldToShadowCoord(p);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.alpha = 1;
                surface.occlusion = 1;
                surface.specular = 0;
                surface.smoothness = 0;

                half4 color = UniversalFragmentBlinnPhong(inputData, surface);
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
