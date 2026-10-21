// Céu noturno de Kairós: gradiente escuro, um planeta gigante com anel perto do horizonte e estrelas que giram em
// volta de um polo celeste deixando rastro (como uma foto de longa exposição), em tons azul-lilás, mais densas numa
// faixa tipo Via Láctea (com uma nebulosa fraca) e em manchas. O fundo é um degradê grande (azul no horizonte, roxo no meio, escuro
// no topo) e, bem perto do horizonte, a cor da névoa, para casar com a floresta distante. O ângulo e o comprimento do rastro vêm
// de variáveis globais (_IcaroStarAngle e _IcaroStarTrail, em radianos), que o NightSkyMotion atualiza conforme o
// multiplicador de tempo: quanto mais rápido o tempo, mais rápido o céu gira e mais longos ficam os rastros.
// Aurora: muitos raios verticais de luz subindo de uma borda ondulada perto do horizonte até o alto da vista (somem
// perto do zênite, onde os raios se encontrariam), correndo para os lados, piscando e ondulando de forma visível.
Shader "Icaro/NightSky"
{
    Properties
    {
        _TopColor("Zênite", Color) = (0.01, 0.01, 0.035, 1)
        _HorizonColor("Horizonte", Color) = (0.07, 0.035, 0.12, 1)
        _GlowColor("Brilho do horizonte", Color) = (0.2, 0.06, 0.28, 1)
        _StarDensity("Densidade das estrelas", Range(0, 1)) = 0.45
        _StarBrightness("Brilho das estrelas", Range(0, 8)) = 2.2
        _PlanetDir("Direção do planeta", Vector) = (-0.55, 0.3, 0.78, 0)
        _PlanetSize("Tamanho do planeta (radianos)", Range(0.02, 0.5)) = 0.17
        _PlanetColorA("Planeta: cor A", Color) = (0.55, 0.32, 0.72, 1)
        _PlanetColorB("Planeta: cor B", Color) = (0.2, 0.55, 0.62, 1)
        _RingColor("Anel", Color) = (0.75, 0.68, 0.9, 1)
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
                float4 _PlanetDir;
                float _PlanetSize;
                half4 _PlanetColorA;
                half4 _PlanetColorB;
                half4 _RingColor;
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
            CBUFFER_END

            float _IcaroStarAngle;
            float _IcaroStarTrail;

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
                const float bands = 140.0;
                float3 axis = normalize(float3(0.25, 0.92, 0.3));
                float3 b = normalize(cross(axis, float3(1, 0, 0)));
                float3 c = cross(axis, b);

                float theta = acos(clamp(dot(d, axis), -1.0, 1.0));
                float phi = atan2(dot(d, c), dot(d, b)) - _IcaroStarAngle;

                float bandWidth = PI / bands;
                float band = floor(theta / bandWidth);
                float bandTheta = (band + 0.5) * bandWidth;
                float cellsInBand = max(1.0, floor(120.0 * sin(bandTheta)));
                float cellWidth = PI2 / cellsInBand;
                float cell = floor(phi / cellWidth);

                float pixel = max(fwidth(theta), 1e-5);
                float pixelPhi = pixel / max(sin(theta), 0.05);
                float trail = max(_IcaroStarTrail, 0.0);

                half3 color = 0;
                float pixelBand;
                Region(theta, phi, pixelBand);
                [unroll]
                for (int k = 0; k < 8; k++)
                {
                    // A estrela pode estar nesta célula ou nas seguintes (o rastro fica para trás dela)
                    float index = cell + k;
                    float wrapped = index - cellsInBand * floor(index / cellsInBand);
                    float2 h = Hash22(float2(band, wrapped));
                    float phi0 = (index + 0.15 + 0.7 * h.y) * cellWidth;
                    float starBand;
                    if (h.x > _StarDensity * Region(bandTheta, phi0, starBand))
                        continue;

                    float2 h2 = Hash22(float2(wrapped * 1.7 + 3.1, band * 0.37 + 11.0));
                    float theta0 = bandTheta + (h2.x - 0.5) * 0.6 * bandWidth;
                    float behind = phi0 - phi;
                    if (behind < -pixelPhi * 2.0 || behind > trail + pixelPhi * 2.0)
                        continue;

                    float across = 1.0 - smoothstep(pixel * 0.6, pixel * 1.6, abs(theta - theta0));
                    float head = 1.0 - smoothstep(pixelPhi * 0.8, pixelPhi * 2.2, abs(behind));
                    float along = trail > 1e-5 && behind > 0.0 ? 1.0 - behind / trail : 0.0;
                    float light = max(head, along * along * 0.7) * across * (0.35 + 0.65 * h2.y);
                    // Tons azul-lilás (combinam com a floresta); poucas estrelas quentes
                    half3 tint = h2.x < 0.06 ? half3(1.0, 0.8, 0.65) : h2.x < 0.45 ? half3(0.82, 0.72, 1.0) : half3(0.7, 0.82, 1.0);
                    color += tint * light;
                }

                return half4(color * _StarBrightness, pixelBand);
            }

            // Aurora: muitos raios verticais (cada um segue a linha do horizonte até o alto) saindo de uma borda
            // ondulada perto do horizonte; os raios correm para os lados e piscam, a borda ondula e manchas mais fortes
            // passam ao longo do céu. Some perto do zênite (onde os raios se encontrariam).
            half3 Aurora(float3 d)
            {
                float h = d.y;
                float az = atan2(d.x, d.z);
                float t = _Time.y * _AuroraSpeed;
                float bottom = _AuroraHeight * 0.45 + 0.06 * sin(az * 2.0 + t * 0.9) + 0.035 * sin(az * 5.3 - t * 1.4) +
                               0.015 * sin(az * 13.0 + t * 2.3);
                float above = h - bottom;
                if (above < -0.04)
                    return 0;

                float edge = smoothstep(-0.04, 0.015, above);
                // Forte logo acima da borda e ainda visível bem alto
                float rise = 0.8 * exp(-max(above, 0.0) / 0.35) + 0.25 * exp(-max(above, 0.0) / 1.4);
                float zenith = 1.0 - smoothstep(0.8, 0.97, h);

                // Raios: dois tamanhos de listras verticais que correm para os lados em velocidades diferentes
                float wobble = sin(az * 3.0 + t * 0.7) * 1.5;
                float wide = Noise1(az * 40.0 + t * 1.8 + wobble);
                float thin = Noise1(az * 140.0 - t * 3.2 + wobble * 2.0);
                float rays = pow(wide, 2.2) * 1.3 + pow(thin, 3.5) * 0.9 + 0.1;
                float flicker = 0.7 + 0.3 * sin(t * 4.5 + az * 25.0 + wide * 6.0);
                float patches = saturate(0.2 + Noise1(az * 1.3 + t * 0.35) * Noise1(az * 0.6 - t * 0.2 + 7.0) * 1.6);

                half3 tint = lerp(_AuroraColorA.rgb, _AuroraColorB.rgb, saturate(above / 0.7));
                return tint * (edge * rise * zenith * rays * flicker * patches * _AuroraStrength);
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

                if (_AuroraStrength > 0.0 && h > 0.0)
                    sky += Aurora(d);

                half4 planet = Planet(d);
                half4 stars = Stars(d);
                sky += _NebulaColor.rgb * stars.a * saturate(h * 3.0) + stars.rgb;

                sky = lerp(sky, planet.rgb, planet.a);

                // Perto do horizonte o céu vira a cor da névoa (como as árvores distantes): os vãos entre as árvores
                // não mostram estrelas, e a floresta se funde com o céu
                half haze = 1.0 - smoothstep(0.0, _HorizonFog, h);
                sky = lerp(sky, unity_FogColor.rgb, haze);
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
