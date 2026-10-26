// Céu noturno de Kairós: gradiente escuro, um planeta gigante com anel perto do horizonte e estrelas que giram em
// volta de um polo celeste deixando rastro (como uma foto de longa exposição), em tons azul-lilás, mais densas numa
// faixa tipo Via Láctea (com uma nebulosa fraca) e em manchas. O fundo é um degradê grande (azul no horizonte, roxo no meio, escuro
// no topo) e, bem perto do horizonte, a cor da névoa, para casar com a floresta distante. O ângulo e o comprimento do rastro vêm
// de variáveis globais (_IcaroStarAngle e _IcaroStarTrail, em radianos), que o NightSkyMotion atualiza conforme o
// multiplicador de tempo: quanto mais rápido o tempo, mais rápido o céu gira e mais longos ficam os rastros. O polo
// (_IcaroStarAxis, também do NightSkyMotion) fica baixo, perto do horizonte: no alto do céu os rastros são quase
// retos (círculos grandes); perto dos polos o rastro encurta até sumir (_IcaroPoleFade), sem redemoinho.
// Aurora: cortinas verticais estendidas do sul ao norte, ondulando, com a borda de baixo clara e raios em pé, e
// pedaços menores ao lado delas (ajustes em SkySettings).
Shader "Icaro/NightSky"
{
    Properties
    {
        _TopColor("Zênite", Color) = (0.01, 0.01, 0.035, 1)
        _HorizonColor("Horizonte", Color) = (0.07, 0.035, 0.12, 1)
        _GlowColor("Brilho do horizonte", Color) = (0.2, 0.06, 0.28, 1)
        _StarDensity("Densidade das estrelas", Range(0, 1)) = 0.45
        _StarBrightness("Brilho das estrelas", Range(0, 8)) = 2.2
        _StarThickness("Espessura das estrelas e dos rastros", Range(0.3, 3)) = 1
        _PlanetDir("Direção do planeta", Vector) = (-0.55, 0.3, 0.78, 0)
        _PlanetSize("Tamanho do planeta (radianos)", Range(0, 1.4)) = 0.17
        _PlanetColorA("Planeta: cor A", Color) = (0.55, 0.32, 0.72, 1)
        _PlanetColorB("Planeta: cor B", Color) = (0.2, 0.55, 0.62, 1)
        _RingColor("Anel", Color) = (0.75, 0.68, 0.9, 1)
        _EclipseDir("Eclipse: direção", Vector) = (0.3, 0.35, 0.89, 0)
        _EclipseSize("Eclipse: tamanho do disco (radianos, 0 = sem)", Range(0, 0.6)) = 0
        [HDR] _CoronaColor("Eclipse: cor da coroa", Color) = (3, 2.2, 1.2, 1)
        _CoronaSize("Eclipse: alcance da coroa (raios do disco)", Range(0.2, 8)) = 2.5
        _HorizonFog("Faixa de névoa no horizonte (altura)", Range(0.01, 0.6)) = 0.28
        _HorizonTint("Degradê: azul (logo acima do horizonte)", Color) = (0.12, 0.28, 0.62, 1)
        _MidColor("Degradê: roxo (meio do céu)", Color) = (0.24, 0.07, 0.38, 1)
        _GradientBlue("Degradê: altura em que o azul vira roxo", Range(0.05, 0.9)) = 0.22
        _GradientTop("Degradê: altura em que o roxo vira o zênite", Range(0.1, 1.2)) = 0.55
        _NebulaColor("Nebulosa da faixa de estrelas", Color) = (0.12, 0.06, 0.2, 1)
        _AuroraStrength("Aurora: intensidade (0 = sem aurora)", Range(0, 4)) = 0
        [HDR] _AuroraColorA("Aurora: cor de baixo", Color) = (0.2, 1.4, 0.8, 1)
        [HDR] _AuroraColorB("Aurora: cor de cima", Color) = (0.5, 0.3, 1.2, 1)
        _AuroraHeight("Aurora: altura no céu", Range(0.1, 0.9)) = 0.42
        _AuroraSpeed("Aurora: velocidade do movimento", Range(0, 4)) = 1
        _SkyBrightness("Claridade do céu", Range(0, 4)) = 1
        _GlowStrength("Brilho do horizonte: força (0 = sem)", Range(0, 4)) = 0
        _GlowHeight("Brilho do horizonte: altura (seno da elevação)", Range(0.01, 1)) = 0.3
        _GlowFalloff("Brilho do horizonte: suavidade (maior = cai mais rápido)", Range(0.3, 8)) = 2
        [HDR] _GlowColorLow("Brilho do horizonte: cor de baixo", Color) = (0.2, 1.2, 0.7, 1)
        [HDR] _GlowColorHigh("Brilho do horizonte: cor de cima", Color) = (0.45, 0.3, 1.1, 1)
        _GlowAzimuth("Brilho do horizonte: direção (graus, 0 = norte)", Range(0, 360)) = 0
        _GlowDirectional("Brilho do horizonte: concentrado numa direção (0 = em volta toda)", Range(0, 1)) = 0
        _GlowWidth("Brilho do horizonte: largura em volta da direção (graus)", Range(5, 180)) = 70
        _GlowOverFog("Brilho do horizonte: quanto aparece por cima da névoa do horizonte", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _GlowColor;
                half _StarDensity;
                half _StarBrightness;
                half _StarThickness;
                float4 _PlanetDir;
                float _PlanetSize;
                half4 _PlanetColorA;
                half4 _PlanetColorB;
                half4 _RingColor;
                float4 _EclipseDir;
                float _EclipseSize;
                half4 _CoronaColor;
                float _CoronaSize;
                half _HorizonFog;
                half4 _HorizonTint;
                half4 _MidColor;
                half _GradientBlue;
                half _GradientTop;
                half4 _NebulaColor;
                half _AuroraStrength;
                half4 _AuroraColorA;
                half4 _AuroraColorB;
                half _AuroraHeight;
                half _AuroraSpeed;
                half _SkyBrightness;
                half _GlowStrength;
                half _GlowHeight;
                half _GlowFalloff;
                half4 _GlowColorLow;
                half4 _GlowColorHigh;
                half _GlowAzimuth;
                half _GlowDirectional;
                half _GlowWidth;
                half _GlowOverFog;
            CBUFFER_END

            float _IcaroStarAngle;
            float _IcaroStarTrail;
            float4 _IcaroStarAxis;   // polo celeste (xyz); sem valor, o polo antigo quase no zênite
            float _IcaroPoleFade;    // raio (radianos) em volta dos polos em que o rastro encurta até sumir
            float4 _IcaroStarLook;   // ajustes do céu: densidade, brilho e espessura (multiplicam); w = 1 se definido
            float4 _IcaroAurora0;    // aurora: intensidade, cortinas, distância entre elas, ondulação
            float4 _IcaroAurora1;    // aurora: comprimento da onda, altura, velocidade, raios (0 a 1)
            float4 _IcaroAurora2;    // aurora: densidade dos raios, pedaços, comprimento e altura dos pedaços
            float4 _IcaroAurora3;    // aurora: força dos pedaços, distância deles, deslocamento leste-oeste; w = 1 se definido
            float4 _IcaroAurora4;    // pedaços: deslocamento leste-oeste, deslocamento sul-norte, espalhamento, deslize

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            // Ruído suave de uma dimensão (0 a 1)
            float Noise1(float x)
            {
                float i = floor(x), f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(Hash11(i), Hash11(i + 1.0), u);
            }

            // Eclipse: disco escuro (com um fio de luz refletida) e a coroa em volta, em raios irregulares e um aro fino e
            // claro colado no disco; rgb = luz da coroa (somada), a = quanto o disco cobre o céu
            half4 Eclipse(float3 d)
            {
                if (_EclipseSize < 0.005)
                    return 0;
                float3 forward = normalize(_EclipseDir.xyz);
                float c = dot(d, forward);
                if (c < 0.0)
                    return 0;
                float r = acos(saturate(c)) / _EclipseSize;
                float3 right = normalize(cross(float3(0, 1, 0), forward));
                float3 up = cross(forward, right);
                float angle = atan2(dot(d, up), dot(d, right));
                if (r < 1.0)
                {
                    // Disco: quase preto, com a borda de baixo levemente acesa
                    half3 disc = half3(0.015, 0.012, 0.02) + _CoronaColor.rgb * 0.01 * saturate(r * r);
                    return half4(disc, 1.0);
                }

                float outside = r - 1.0;
                float rays = 0.55 + 0.45 * Noise1(angle * 6.0 + 3.0) * Noise1(angle * 17.0 + 11.0)
                           + 0.25 * pow(abs(sin(angle * 2.0 + 0.4)), 6.0);
                float corona = exp(-outside / max(_CoronaSize * rays, 0.05) * 2.2);
                float rim = exp(-outside * 40.0) * 2.5;
                return half4(_CoronaColor.rgb * (corona * 0.9 + rim), 0.0);
            }

            // Planeta com faixas, sombreado de um lado, e um anel inclinado; alpha = quanto cobre o céu
            half4 Planet(float3 d)
            {
                if (_PlanetSize < 0.02)
                    return 0;
                float3 forward = normalize(_PlanetDir.xyz);
                float3 right = normalize(cross(float3(0, 1, 0), forward));
                float3 up = cross(forward, right);
                if (dot(d, forward) <= 0.0)
                    return 0;

                float2 p = float2(dot(d, right), dot(d, up)) / sin(_PlanetSize);
                float r = length(p);
                half4 result = 0;

                // Anel (atrás do planeta só aparece fora do disco)
                float2 q = float2(p.x * 0.94 + p.y * 0.34, (-p.x * 0.34 + p.y * 0.94) * 3.6);
                float rr = length(q);
                bool ring = rr > 1.35 && rr < 2.15 && fmod(floor(rr * 9.0), 3.0) != 0.0;
                bool front = q.y < 0.0;

                if (r < 1.0)
                {
                    float z = sqrt(1.0 - r * r);
                    float3 n = float3(p, z);
                    float light = saturate(dot(n, normalize(float3(-0.6, 0.45, 0.65))));
                    float bands = sin(p.y * 13.0 + sin(p.x * 3.0) * 0.9) * 0.5 + 0.5;
                    half3 color = lerp(_PlanetColorA.rgb, _PlanetColorB.rgb, step(0.55, bands));
                    result = half4(color * (0.12 + 0.88 * light), 1.0);
                    if (ring && front)
                        result.rgb = _RingColor.rgb * 0.7;
                }
                else if (ring)
                {
                    result = half4(_RingColor.rgb * 0.55, 1.0);
                }
                else
                {
                    // Atmosfera em volta do disco
                    result = half4(_PlanetColorA.rgb * 0.6, saturate(1.0 - (r - 1.0) * 8.0) * 0.5);
                }

                return result;
            }

            // Densidade de estrelas por região do céu (nas coordenadas que giram com elas): uma faixa densa tipo Via
            // Láctea passando pelo polo e manchas mais cheias e mais vazias no resto
            float Region(float theta, float phi, out float band)
            {
                float dphi = phi - 1.3;
                dphi -= 6.2831853 * floor((dphi + 3.14159265) / 6.2831853);
                band = exp(-(dphi * dphi) / 0.2) * smoothstep(0.08, 0.5, sin(theta));
                float patches = 0.5 + 0.5 * sin(phi * 3.0 + 1.0) * sin(theta * 4.0 + 0.7);
                return lerp(0.25, 0.9, patches) + band * 1.6;
            }

            // rgb = estrelas e rastros; a = intensidade da faixa densa no pixel (para a nebulosa)
            half4 Stars(float3 d)
            {
                const float PI2 = 6.2831853;
                const float bands = 220.0;
                float3 axis = dot(_IcaroStarAxis.xyz, _IcaroStarAxis.xyz) > 0.25
                    ? normalize(_IcaroStarAxis.xyz) : normalize(float3(0.25, 0.92, 0.3));
                float3 b = normalize(cross(axis, abs(axis.x) > 0.95 ? float3(0, 0, 1) : float3(1, 0, 0)));
                float3 c = cross(axis, b);

                float theta = acos(clamp(dot(d, axis), -1.0, 1.0));
                float phi = atan2(dot(d, c), dot(d, b)) - _IcaroStarAngle;

                float bandWidth = PI / bands;
                float band = floor(theta / bandWidth);
                float bandTheta = (band + 0.5) * bandWidth;
                float cellsInBand = max(1.0, floor(190.0 * sin(bandTheta)));
                float cellWidth = PI2 / cellsInBand;
                float cell = floor(phi / cellWidth);

                float pixel = max(fwidth(theta), 1e-5);
                float pixelPhi = pixel / max(sin(theta), 0.05);
                // Perto dos dois polos o rastro encurta até sumir (lá ele viraria um redemoinho): só estrelas paradas
                float poleDistance = min(theta, PI - theta);
                // Rastro negativo = giro invertido (o rastro fica do outro lado da estrela)
                float direction = _IcaroStarTrail < 0.0 ? -1.0 : 1.0;
                float trail = abs(_IcaroStarTrail) *
                              smoothstep(_IcaroPoleFade * 0.35, max(_IcaroPoleFade, 1e-3), poleDistance);
                // Ajustes do céu (SkySettings); sem eles (fora do Play), os valores do material
                float3 look = _IcaroStarLook.w > 0.5 ? _IcaroStarLook.xyz : float3(1, 1, 1);
                float density = _StarDensity * look.x;

                half3 color = 0;
                float pixelBand;
                Region(theta, phi, pixelBand);
                // Traço fino (cerca de 1 pixel), suave nas bordas
                float width = pixel * _StarThickness * look.z;
                float widthPhi = pixelPhi * _StarThickness * look.z;
                [unroll]
                for (int k = 0; k < 14; k++)
                {
                    // A estrela pode estar nesta célula ou nas seguintes (o rastro fica para trás dela)
                    float index = cell + k * direction;
                    float wrapped = index - cellsInBand * floor(index / cellsInBand);
                    float2 h = Hash22(float2(band, wrapped));
                    float phi0 = (index + 0.15 + 0.7 * h.y) * cellWidth;
                    float starBand;
                    if (h.x > density * Region(bandTheta, phi0, starBand))
                        continue;

                    float2 h2 = Hash22(float2(wrapped * 1.7 + 3.1, band * 0.37 + 11.0));
                    float2 h3 = Hash22(float2(wrapped * 0.61 + 17.3, band * 1.9 + 5.7));
                    float theta0 = bandTheta + (h2.x - 0.5) * 0.6 * bandWidth;
                    float behind = (phi0 - phi) * direction;
                    if (behind < -widthPhi * 2.0 || behind > trail + widthPhi * 2.0)
                        continue;

                    float across = 1.0 - smoothstep(width * 0.25, width * 1.0, abs(theta - theta0));
                    float head = 1.0 - smoothstep(widthPhi * 0.35, widthPhi * 1.2, abs(behind));
                    // Rastro de longa exposição: quase uniforme, um pouco mais claro perto da cabeça, sumindo na ponta
                    float along = trail > 1e-5 && behind > 0.0
                        ? (0.55 + 0.45 * (1.0 - behind / trail)) * (1.0 - smoothstep(trail * 0.8, trail, behind))
                        : 0.0;
                    // Muitas fracas e poucas fortes
                    float magnitude = 0.12 + 1.5 * pow(h3.x, 2.6);
                    float light = max(head, along * 0.75) * across * magnitude;
                    // Coladas no polo as estrelas viram bolas grandes (a largura em phi estica): somem ali
                    light *= smoothstep(0.06, 0.16, poleDistance);
                    // Branco e azul-claro, poucas quentes e poucas ciano
                    half3 tint = h3.y < 0.06 ? half3(1.0, 0.82, 0.66) : h3.y < 0.14 ? half3(0.72, 1.0, 0.95)
                        : h3.y < 0.5 ? half3(0.72, 0.82, 1.0) : half3(0.92, 0.95, 1.0);
                    color += tint * light;
                }

                return half4(color * _StarBrightness * look.y, pixelBand);
            }

            // Aurora: cortinas verticais de luz no alto do céu. Cada cortina é uma folha em pé, estendida do sul ao norte
            // (Z) e ondulando para os lados (X), da altura 1 (a borda de baixo, irregular) até 1 + altura (unidades = a
            // altura da borda de baixo). Para cada direção do olhar, acha a altura y em que o raio de visão cruza a folha
            // (algumas iterações) e acende pela altura relativa: borda de baixo clara e definida, luz sumindo para cima
            // e raios verticais (ruído ao longo de Z, então cada raio é uma linha em pé). Cortinas grandes atravessam o
            // céu; os pedaços (menores e mais baixos) ficam ao lado delas e só cobrem um trecho de Z.
            // Ajustes no SkySettings (globais _IcaroAurora0 a 3); a cor e a força do bioma vêm do material.
            // Quanto z (posição ao longo das cortinas por unidade de altura) anda num pixel: calculado no Frag, fora dos
            // testes, porque a derivada de tela não vale dentro deles
            static float s_AuroraZPixel;

            struct AuroraLook
            {
                float wave, waveLength, speed, rays, rayDensity, time;
            };

            // Ondulação: a linha da cortina no chão (x em função de z, sul-norte) é uma corda sacudida pela ponta do sul:
            // ondas que viajam para o norte (duas harmônicas). Os raios continuam verticais: cada um é uma linha em pé
            // num ponto dessa curva.
            float CurtainOffset(float z, float seed, float wave, AuroraLook a)
            {
                float k = 6.2831853 / max(a.waveLength, 0.1);
                float t = a.time * 0.6;
                return wave * (0.7 * sin(k * z - t + seed * 1.9) + 0.3 * sin(k * 2.1 * z - t * 1.6 + seed * 4.1));
            }

            // Ruído que some quando fica mais fino que um pixel (evita o chuvisco perto do horizonte); 'footprint' é
            // quanto x anda dentro de um pixel (calculado fora dos testes, onde a derivada de tela vale)
            float SoftNoise(float x, float footprint)
            {
                float fade = saturate(1.5 - footprint * 1.5);
                return lerp(0.5, Noise1(x), fade);
            }

            // Luz de um ponto da cortina: altura y (1 = borda de baixo) e posição z ao longo dela
            half3 CurtainShade(float y, float z, float zPixel, float seed, float height, float zMin, float zMax,
                               AuroraLook a)
            {
                float segment = smoothstep(zMin, zMin + 0.5, z) * (1.0 - smoothstep(zMax - 0.5, zMax, z));
                if (segment <= 0.0)
                    return 0;
                float bottom = 1.0 + 0.14 * (SoftNoise(z * 1.7 + seed * 3.0 + a.time * 0.2, zPixel * 1.7) - 0.5);
                float k = (y - bottom) / max(height, 0.05);
                if (k < -0.15 || k > 1.3)
                    return 0;
                float edge = smoothstep(-0.07, 0.01, k);
                float body = 0.7 * exp(-max(k, 0.0) * 3.0) + 0.3 * exp(-max(k, 0.0) * 0.9);
                float top = 1.0 - smoothstep(0.7, 1.2, k);
                float streaks = 0.25 + 1.1 * pow(SoftNoise(z * a.rayDensity + a.time * 0.8 + seed * 30.0, zPixel * a.rayDensity), 1.6) *
                                (0.6 + 0.4 * SoftNoise(z * a.rayDensity * 0.31 - a.time * 0.5 + seed * 7.0, zPixel * a.rayDensity * 0.31));
                float rays = lerp(1.0, streaks, a.rays);
                float folds = 0.35 + 0.9 * SoftNoise(z * 0.9 + a.time * 0.3 + seed * 5.0, zPixel * 0.9);
                half3 tint = lerp(_AuroraColorA.rgb, _AuroraColorB.rgb, saturate(k * 0.9));
                return tint * (edge * body * top * rays * folds * segment);
            }

            // Cortina: varre a faixa de altura dela em passos e, em cada passo em que o olhar cruza a folha
            // (x = baseX + ondulação(z)), soma a luz daquele ponto (dobras vistas de lado somam mais de um cruzamento)
            half3 Curtain(float3 d, float baseX, float seed, float height, float strength, float zMin, float zMax,
                          AuroraLook a)
            {
                float2 perHeight = d.xz / max(d.y, 0.01);   // x e z por unidade de altura ao longo do olhar
                float zPixel = s_AuroraZPixel;              // quanto z anda num pixel, na altura da cortina
                if (strength <= 0.0 || d.y < 0.01)
                    return 0;
                float y0 = 0.8, y1 = 1.0 + height * 1.25 + 0.1;
                // Perto do horizonte z varia demais dentro da faixa: a onda diminui (senão viraria ruído)
                float zSpan = abs(perHeight.y) * (y1 - y0);
                float wave = a.wave * saturate(1.5 - zSpan / max(a.waveLength, 0.1) * 1.5);

                half3 sum = 0;
                float yPrev = y0;
                float gPrev = perHeight.x * y0 - baseX - CurtainOffset(perHeight.y * y0, seed, wave, a);
                [unroll]
                for (int n = 1; n <= 10; n++)
                {
                    float y = lerp(y0, y1, n / 10.0);
                    float g = perHeight.x * y - baseX - CurtainOffset(perHeight.y * y, seed, wave, a);
                    if (gPrev * g <= 0.0 && gPrev != g)
                    {
                        float yr = yPrev + (y - yPrev) * gPrev / (gPrev - g);
                        sum += CurtainShade(yr, perHeight.y * yr, zPixel, seed, height, zMin, zMax, a);
                    }

                    yPrev = y;
                    gPrev = g;
                }

                return sum * strength;
            }

            half3 Aurora(float3 d)
            {
                bool tuned = _IcaroAurora3.w > 0.5;
                float intensity = tuned ? _IcaroAurora0.x : 0.5;
                int bands = tuned ? (int)round(_IcaroAurora0.y) : 3;
                float spacing = tuned ? _IcaroAurora0.z : 1.3;
                AuroraLook a;
                a.wave = tuned ? _IcaroAurora0.w : 0.6;
                a.waveLength = tuned ? _IcaroAurora1.x : 3.0;
                float height = tuned ? _IcaroAurora1.y : 1.6;
                a.speed = tuned ? _IcaroAurora1.z : 1.0;
                a.rays = tuned ? _IcaroAurora1.w : 0.7;
                a.rayDensity = tuned ? _IcaroAurora2.x : 25.0;
                int pieces = tuned ? (int)round(_IcaroAurora2.y) : 3;
                float pieceLength = tuned ? _IcaroAurora2.z : 2.5;
                float pieceHeight = tuned ? _IcaroAurora2.w : 0.5;
                float pieceStrength = tuned ? _IcaroAurora3.x : 0.6;
                float pieceDistance = tuned ? _IcaroAurora3.y : 0.5;
                float offset = tuned ? _IcaroAurora3.z : 0.35;
                float pieceShiftX = tuned ? _IcaroAurora4.x : 0.0;
                float pieceShiftZ = tuned ? _IcaroAurora4.y : 0.0;
                float pieceSpread = tuned ? _IcaroAurora4.z : 7.0;
                float pieceDrift = tuned ? _IcaroAurora4.w : 0.8;
                a.time = _Time.y * a.speed * _AuroraSpeed;

                half3 sum = 0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    if (i >= bands)
                        continue;
                    float baseX = (i - (bands - 1) * 0.5) * spacing + offset;
                    sum += Curtain(d, baseX, i, height, i == 0 ? 1.0 : 0.6, -1e5, 1e5, a);
                }

                // Pedaços ao lado das cortinas grandes, num trecho de Z que desliza devagar
                [unroll]
                for (int p = 0; p < 6; p++)
                {
                    if (p >= pieces)
                        continue;
                    int owner = p % max(bands, 1);
                    float side = (p / max(bands, 1)) % 2 == 0 ? 1.0 : -1.0;
                    float baseX = (owner - (bands - 1) * 0.5) * spacing + offset + side * pieceDistance + pieceShiftX;
                    float center = (Hash11(p * 7.31 + 1.7) - 0.5) * pieceSpread + pieceShiftZ +
                                   sin(a.time * 0.05 + p * 1.3) * pieceDrift;
                    sum += Curtain(d, baseX, 10.0 + p, height * pieceHeight, pieceStrength,
                                   center - pieceLength * 0.5, center + pieceLength * 0.5, a);
                }

                // Some perto do horizonte (a névoa cobre a ponta das cortinas)
                return sum * (intensity * _AuroraStrength * saturate(d.y * 4.0));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float h = d.y;
                // Degradê grande e visível acima das árvores: azul no horizonte, roxo no meio, preto da metade do céu para cima
                half3 sky = lerp(_HorizonTint.rgb, _MidColor.rgb, smoothstep(0.0, _GradientBlue, h));
                sky = lerp(sky, _TopColor.rgb, smoothstep(_GradientBlue, _GradientTop, h));
                sky += _GlowColor.rgb * exp(-abs(h) * 9.0);
                if (h < 0.0)
                    sky = lerp(_HorizonColor.rgb * 0.6, half3(0, 0, 0), saturate(-h * 4.0));

                s_AuroraZPixel = fwidth(d.z / max(d.y, 0.01)) * 1.3;
                if (_AuroraStrength > 0.0 && h > 0.0)
                    sky += Aurora(d);

                half4 planet = Planet(d);
                half4 stars = Stars(d);
                sky += _NebulaColor.rgb * stars.a * saturate(h * 3.0) + stars.rgb;

                sky = lerp(sky, planet.rgb, planet.a);
                half4 eclipse = Eclipse(d);
                sky = lerp(sky, eclipse.rgb, eclipse.a) + eclipse.rgb * (1.0 - eclipse.a);

                // Perto do horizonte o céu vira a cor da névoa (como as árvores distantes): os vãos entre as árvores
                // não mostram estrelas, e a floresta se funde com o céu
                sky *= _SkyBrightness;
                half haze = 1.0 - smoothstep(0.0, _HorizonFog, h);
                sky = lerp(sky, unity_FogColor.rgb, haze);

                // Brilho do horizonte: degradê de luz subindo do horizonte (cor de baixo para a de cima), mais forte
                // embaixo; pode se concentrar numa direção e aparece por cima da névoa do horizonte
                if (_GlowStrength > 0.0)
                {
                    float e = saturate(max(h, 0.0) / max(_GlowHeight, 0.01));
                    float rise = pow(1.0 - e, _GlowFalloff) * saturate(1.0 + h * 25.0);
                    float az = degrees(atan2(d.x, d.z));
                    float delta = abs(fmod(az - _GlowAzimuth + 540.0, 360.0) - 180.0);
                    float around = lerp(1.0, exp(-pow(delta / max(_GlowWidth, 1.0), 2.0)), _GlowDirectional);
                    half3 glow = lerp(_GlowColorLow.rgb, _GlowColorHigh.rgb, e) * (rise * around * _GlowStrength);
                    sky += glow * lerp(1.0 - haze, 1.0, _GlowOverFog);
                }
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
