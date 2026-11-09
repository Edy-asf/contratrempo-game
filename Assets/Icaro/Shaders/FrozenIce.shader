// Água congelada dos Picos (cascata e rio da Vila-Ponte): a textura de gelo em pixel art projetada pelo mundo (a face
// mais alinhada com cada eixo escolhe a projeção; nas paredes o "para cima" da textura é o do mundo) e, para não se ver
// a repetição (pedido do usuário em 2026-11-07: o gelo de 4 x 4 m repetido pela cascata inteira ficava "repetitivo"),
// o mundo é dividido em placas (Voronoi no grão dos pixels, com a beira torta): cada placa desloca a textura por um
// número inteiro de pixels, pode girá-la de 90 em 90 graus e muda um pouco o tom (umas mais leitosas); a divisa entre
// placas vira uma rachadura (rio) ou um sulco entre colunas de gelo (cascata). Por cima, manchas grandes de tom (gelo
// fundo e azul ou leitoso, em cinco degraus com xadrez na passagem), neve nas faces viradas para cima (borda recortada
// em degraus de pixel), brilho na beira contra a luz e cristais que piscam com o ângulo da câmera. Luz PBR do URP (sol
// com sombras, luzes extras, ambiente) e névoa. A UV da malha não é usada.
Shader "Icaro/FrozenIce"
{
    Properties
    {
        [NoScaleOffset] _IceMap("Gelo", 2D) = "white" {}
        _Tile("Metros por repetição (largura, altura)", Vector) = (8, 8, 0, 0)
        _TexelsPerMeter("Pixels por metro", Float) = 16
        _Smoothness("Lisura do gelo", Range(0, 1)) = 0.55

        [Header(Placas)]
        _CellSize("Tamanho das placas (m: largura, altura)", Vector) = (10, 10, 0, 0)
        _CellRotate("Girar a textura nas placas", Range(0, 1)) = 1
        _CellTint("Variação de tom por placa", Range(0, 0.5)) = 0.1
        _CellMilk("Placas leitosas", Range(0, 1)) = 0.35
        _CrackAmount("Divisas desenhadas", Range(0, 1)) = 0.75
        _CrackWobble("Tortura das divisas (m)", Float) = 0.9
        _CrackColor("Cor da divisa", Color) = (0.85, 0.9, 0.98, 1)
        _CrackSide("Cor ao lado da divisa", Color) = (0.1, 0.21, 0.36, 1)

        [Header(Manchas grandes)]
        _MacroScale("Tamanho das manchas (m: largura, altura)", Vector) = (30, 30, 0, 0)
        _MacroAmount("Força das manchas", Range(0, 1)) = 0.55
        _DeepTint("Tom do gelo fundo", Color) = (0.72, 0.84, 1, 1)
        _MilkTint("Tom do gelo leitoso", Color) = (1.18, 1.16, 1.1, 1)

        [Header(Neve)]
        _SnowColor("Neve", Color) = (0.86, 0.89, 0.97, 1)
        _SnowShade("Sombra da neve", Color) = (0.7, 0.76, 0.92, 1)
        _SnowAmount("Quantidade de neve", Range(0, 1)) = 0.32
        _SnowScale("Tamanho das manchas de neve (m)", Float) = 9
        _SnowSlope("Só nas faces viradas para cima", Float) = 1.5
        _EdgeWidth("Largura da borda da neve", Range(0, 0.2)) = 0.04

        [Header(Brilho)]
        _RimColor("Cor da beira contra a luz", Color) = (0.55, 0.75, 0.95, 1)
        _Rim("Força da beira", Range(0, 1)) = 0.3
        _SparkleAmount("Cristais", Range(0, 3)) = 1

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
                float4 _Tile;
                float _TexelsPerMeter;
                half _Smoothness;
                float4 _CellSize;
                half _CellRotate;
                half _CellTint;
                half _CellMilk;
                half _CrackAmount;
                float _CrackWobble;
                half4 _CrackColor;
                half4 _CrackSide;
                float4 _MacroScale;
                half _MacroAmount;
                half4 _DeepTint;
                half4 _MilkTint;
                half4 _SnowColor;
                half4 _SnowShade;
                half _SnowAmount;
                float _SnowScale;
                float _SnowSlope;
                half _EdgeWidth;
                half4 _RimColor;
                half _Rim;
                half _SparkleAmount;
            CBUFFER_END

            TEXTURE2D(_IceMap);
            SAMPLER(sampler_IceMap);

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
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            // Hash inteiro (as Vilas do Alto ficam a ~5 km da origem: o hash com seno/frac perderia a precisão lá)
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

            float2 Jitter(float2 c)
            {
                return float2(Hash(c), Hash(c + float2(37, 113))) * 0.7 + 0.15;
            }

            // Placas: Voronoi numa grade esticada (size em metros); devolve a placa mais perto e a distância em metros até
            // a divisa com a vizinha mais perto (a divisa é reta na grade esticada e continua reta em metros)
            void Plates(float2 q, float2 size, out float2 cell, out float border)
            {
                float2 g = q / size;
                float2 ip = floor(g), fp = g - ip;
                float best = 1e9;
                float2 r1 = 0;
                cell = ip;
                [unroll] for (int j = -1; j <= 1; j++)
                [unroll] for (int i = -1; i <= 1; i++)
                {
                    float2 o = float2(i, j);
                    float2 r = o + Jitter(ip + o) - fp;
                    float d = dot(r, r);
                    if (d < best)
                    {
                        best = d;
                        r1 = r;
                        cell = ip + o;
                    }
                }

                border = 1e9;
                [unroll] for (int j2 = -1; j2 <= 1; j2++)
                [unroll] for (int i2 = -1; i2 <= 1; i2++)
                {
                    float2 o = float2(i2, j2);
                    float2 r = o + Jitter(ip + o) - fp;
                    float2 d = r - r1;
                    if (dot(d, d) > 1e-5)
                        border = min(border, dot(0.5 * (r1 + r), d) / length(d / size));
                }
            }

            float2 Rotate90(float2 v, float k)
            {
                return k < 0.5 ? v : k < 1.5 ? float2(-v.y, v.x) : k < 2.5 ? -v : float2(v.y, -v.x);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 p = input.positionWS;
                float3 a = abs(n);

                // Projeção pelo eixo dominante (nas paredes, v = altura do mundo)
                float2 q;
                if (a.y >= a.x && a.y >= a.z)
                    q = p.xz;
                else if (a.x >= a.z)
                    q = float2(p.z * sign(n.x), p.y);
                else
                    q = float2(-p.x * sign(n.z), p.y);

                float texels = _TexelsPerMeter;
                float2 pixel = floor(q * texels);
                float2 grain = (pixel + 0.5) / texels;
                float checker = frac((pixel.x + pixel.y) * 0.5) > 0.25 ? 1 : -1;

                // Placa deste pixel (a beira torta por ruído, em degraus de pixel)
                float2 wobble = (float2(Fbm(grain / 2.1 + 5.3), Fbm(grain / 2.1 + 41.7)) - 0.5) * _CrackWobble * 2;
                float2 cell;
                float border;
                Plates(grain + wobble, _CellSize.xy, cell, border);

                // Na placa: textura girada (de 90 em 90 graus) e deslocada por pixels inteiros; a amostra usa a posição
                // contínua (o filtro de ponto acha o pixel) e as derivadas giradas junto, para o mipmap não errar na divisa
                float turn = _CellRotate > 0.5 ? floor(Hash(cell + float2(5, 71)) * 4) : 0;
                float2 shift = floor(float2(Hash(cell + float2(11, 3)), Hash(cell + float2(29, 17))) * _Tile.xy * texels) / texels;
                float2 uv = (Rotate90(q, turn) + shift) / _Tile.xy;
                float2 dx = Rotate90(ddx(q), turn) / _Tile.xy, dy = Rotate90(ddy(q), turn) / _Tile.xy;
                half3 ice = SAMPLE_TEXTURE2D_GRAD(_IceMap, sampler_IceMap, uv, dx, dy).rgb;

                // Tom da placa: um pouco mais claro ou escuro, e algumas leitosas
                ice *= 1 + (Hash(cell + float2(19, 59)) - 0.5) * 2 * _CellTint;
                if (Hash(cell + float2(23, 7)) < _CellMilk)
                    ice = lerp(ice, ice * _MilkTint.rgb, 0.55);

                // Manchas grandes: gelo fundo a leitoso em cinco degraus, com xadrez na passagem
                float macro = saturate((Fbm(grain / _MacroScale.xy + 61.3) - 0.5) * 3.2 * _MacroAmount + 0.5 + checker * 0.035);
                ice *= lerp(_DeepTint.rgb, _MilkTint.rgb, floor(macro * 4.999) / 4);

                // Quantos pixels do gelo cabem num pixel da tela: de longe, linhas de 1 pixel e cristais viram chuvisco,
                // então somem aos poucos
                float footprint = max(length(ddx(q)), length(ddy(q))) * texels;
                half fineDetail = saturate(2 - footprint);

                // Divisa entre placas: a linha de 1 pixel e, ao lado, outra na cor do lado (só onde o ruído deixa)
                float pixelSize = 1 / texels;
                if (ValueNoise(grain / 6 + 13.1) < _CrackAmount * 1.2 - 0.1)
                {
                    if (border < pixelSize * 0.75)
                        ice = lerp(ice, _CrackColor.rgb, 0.35 + 0.65 * fineDetail);
                    else if (border < pixelSize * 1.75)
                        ice = lerp(ice, _CrackSide.rgb, 0.7 * fineDetail);
                }

                // Beira contra a luz: o gelo clareia quando visto de lado
                float3 view = GetWorldSpaceNormalizeViewDir(p);
                half rim = pow(1 - saturate(dot(n, view)), 4) * _Rim;
                ice = lerp(ice, _RimColor.rgb, rim);

                // Neve nas faces viradas para cima, com a borda recortada no grão dos pixels
                float snowMask = Fbm(grain / _SnowScale + 7.3) - (1 - _SnowAmount) - (1 - n.y) * _SnowSlope;
                half3 snow = _SnowColor.rgb * (Fbm(grain / 2.7 + 3.1) > 0.62 ? _SnowShade.rgb : (half3)1);
                half3 albedo;
                half gloss = _Smoothness;
                if (snowMask > _EdgeWidth)
                {
                    albedo = snow;
                    gloss = 0.08;
                }
                else if (snowMask > 0)
                {
                    albedo = snow * _SnowShade.rgb;
                    gloss = 0.1;
                }
                else if (snowMask > -_EdgeWidth)
                    albedo = lerp(ice, _SnowColor.rgb, 0.4);
                else
                    albedo = ice;

                // Cristais: pixels isolados que brilham conforme o ângulo da câmera
                float glint = Hash(pixel * 3 + floor(dot(view, float3(3.1, 5.7, 2.3)) * 4) * 7919);
                albedo += glint > 0.988 ? _SparkleAmount * 0.6 * saturate(1.5 - footprint) : 0;

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
                surface.metallic = 0;
                surface.smoothness = gloss;

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
