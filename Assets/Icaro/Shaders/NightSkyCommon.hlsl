// Funções do céu noturno de Kairós, usadas pelo céu (Icaro/NightSky) e pelo reflexo do mar (Icaro/Sea). Quem inclui
// declara antes as propriedades do céu (as mesmas do NightSky) no próprio CBUFFER.
//
// Visual realista: degradê, planeta com anel (quando o tema tem), estrelas pontuais de tamanhos e brilhos variados que
// cintilam de leve, numa faixa mais densa com nebulosa fraca; elas giram em volta do polo celeste e só deixam um rastro
// curto quando o tempo de Kairós está muito acelerado (como uma foto de longa exposição). Estrelas cadentes cada vez
// mais frequentes com o multiplicador (sorteadas pelo NightSkyMotion: _IcaroMeteors). Aurora em cortinas que dobram e ondulam, em
// camadas, com a borda de baixo forte, raios finos subindo e a cor passando de verde a turquesa e violeta.
#ifndef ICARO_NIGHT_SKY_COMMON
#define ICARO_NIGHT_SKY_COMMON

float _IcaroStarAngle;
float _IcaroStarTrail;
float4 _IcaroMeteors[3];   // estrelas cadentes: xy = sorteio, z = início (s, _Time.y), w = duração (s)

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
half4 SkyPlanet(float3 d)
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

    float2 q = float2(p.x * 0.94 + p.y * 0.34, (-p.x * 0.34 + p.y * 0.94) * 3.6);
    float rr = length(q);
    float ringBands = 0.6 + 0.4 * sin(rr * 26.0);
    float ring = smoothstep(1.3, 1.4, rr) * (1.0 - smoothstep(2.05, 2.2, rr)) * ringBands;
    bool front = q.y < 0.0;

    if (r < 1.0)
    {
        float z = sqrt(1.0 - r * r);
        float3 n = float3(p, z);
        float light = saturate(dot(n, normalize(float3(-0.6, 0.45, 0.65))));
        float bands = sin(p.y * 13.0 + sin(p.x * 3.0) * 0.9) * 0.5 + 0.5;
        half3 color = lerp(_PlanetColorA.rgb, _PlanetColorB.rgb, smoothstep(0.35, 0.75, bands));
        result = half4(color * (0.08 + 0.92 * light) * (0.85 + 0.15 * z), 1.0);
        if (front)
            result.rgb = lerp(result.rgb, _RingColor.rgb * 0.7, ring);
    }
    else
    {
        float atmosphere = saturate(1.0 - (r - 1.0) * 8.0) * 0.5;
        result = half4(lerp(_PlanetColorA.rgb * 0.6, _RingColor.rgb * 0.55, ring), max(atmosphere, ring * 0.9));
    }

    return result;
}

// Densidade de estrelas por região do céu (nas coordenadas que giram com elas): uma faixa densa tipo Via Láctea e
// manchas mais cheias e mais vazias no resto
float SkyRegion(float theta, float phi, out float band)
{
    float dphi = phi - 1.3;
    dphi -= 6.2831853 * floor((dphi + 3.14159265) / 6.2831853);
    band = exp(-(dphi * dphi) / 0.2) * smoothstep(0.08, 0.5, sin(theta));
    float patches = 0.5 + 0.5 * sin(phi * 3.0 + 1.0) * sin(theta * 4.0 + 0.7);
    return lerp(0.25, 0.9, patches) + band * 1.6;
}

// rgb = estrelas; a = intensidade da faixa densa no pixel (para a nebulosa). 'sharp' afina o tamanho em pixels
// (1 no céu; no reflexo, menor, para as estrelas virarem brilhos pequenos)
half4 SkyStars(float3 d, float sharp)
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

    float pixel = max(fwidth(theta), 1e-5) * sharp;
    float pixelPhi = pixel / max(sin(theta), 0.05);
    float trail = max(_IcaroStarTrail, 0.0);

    half3 color = 0;
    float pixelBand;
    SkyRegion(theta, phi, pixelBand);
    [unroll]
    for (int k = 0; k < 4; k++)
    {
        // A estrela pode estar nesta célula ou nas seguintes (o rastro curto fica para trás dela)
        float index = cell + k - 1.0;
        float wrapped = index - cellsInBand * floor(index / cellsInBand);
        float2 h = Hash22(float2(band, wrapped));
        float phi0 = (index + 0.15 + 0.7 * h.y) * cellWidth;
        float starBand;
        if (h.x > _StarDensity * SkyRegion(bandTheta, phi0, starBand))
            continue;

        float2 h2 = Hash22(float2(wrapped * 1.7 + 3.1, band * 0.37 + 11.0));
        float theta0 = bandTheta + (h2.x - 0.5) * 0.6 * bandWidth;
        float behind = phi0 - phi;
        // Brilho com distribuição realista: muitas fracas, poucas fortes
        float magnitude = pow(h2.y, 3.0);
        float size = lerp(0.7, 1.8, magnitude);
        float dist = length(float2((theta - theta0) / pixel, behind / pixelPhi));
        float core = exp(-dist * dist / (size * size));
        float halo = exp(-dist / (size * 2.5)) * 0.12 * magnitude;
        // Rastro curto: só quando o tempo está bem acelerado
        float along = trail > 1e-5 && behind > 0.0 && behind < trail ? (1.0 - behind / trail) : 0.0;
        float across = exp(-pow((theta - theta0) / (pixel * 0.8), 2.0));
        float twinkle = 0.75 + 0.25 * sin(_Time.y * (2.0 + 5.0 * h.y) + h.x * 40.0);
        float light = (core + halo) * twinkle + along * along * across * 0.5;
        half3 tint = h2.x < 0.06 ? half3(1.0, 0.82, 0.66) : h2.x < 0.45 ? half3(0.86, 0.82, 1.0) : half3(0.78, 0.88, 1.0);
        color += tint * light * (0.25 + 1.6 * magnitude);
    }

    return half4(color * _StarBrightness, pixelBand);
}

// Estrelas cadentes: até três ao mesmo tempo, sorteadas pelo NightSkyMotion (cada vez mais frequentes com o
// multiplicador): risco fino que cruza um pedaço do céu, mais claro na cabeça, acendendo e apagando
half3 SkyMeteors(float3 d)
{
    if (d.y < 0.05)
        return 0;
    float az = atan2(d.x, d.z), el = asin(saturate(d.y));
    half3 color = 0;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        float4 meteor = _IcaroMeteors[i];
        float t = (_Time.y - meteor.z) / max(meteor.w, 0.01);
        if (t < 0.0 || t > 1.0)
            continue;
        float2 start = float2(meteor.x * 6.2831853 - 3.14159265, lerp(0.35, 1.0, meteor.y));
        float2 dir = normalize(float2(lerp(-1.0, 1.0, frac(meteor.x * 7.31 + meteor.y * 3.7)), -0.55));
        float2 head = start + dir * 0.35 * t;
        float2 tail = head - dir * 0.12 * (0.3 + t);
        float2 p = float2((az - head.x) * cos(el), el - head.y);
        float2 seg = float2((tail.x - head.x) * cos(el), tail.y - head.y);
        float along = saturate(dot(p, seg) / dot(seg, seg));
        float across = length(p - seg * along);
        float width = max(fwidth(el), 1e-5) * 1.2;
        float fade = sin(t * 3.14159265);
        color += half3(0.9, 0.95, 1.0) * exp(-across * across / (width * width)) * (1.0 - along) * fade * 2.0;
    }

    return color;
}

// Aurora: três cortinas em camadas, cada uma uma fita que dobra (deslocamento do azimute por ruído) e ondula devagar;
// borda de baixo forte e definida, luz sumindo para cima, raios finos e cor de verde a turquesa e violeta
half3 SkyAurora(float3 d)
{
    if (_AuroraStrength <= 0.0 || d.y <= 0.0)
        return 0;
    float az = atan2(d.x, d.z), h = d.y;
    float t = _Time.y * 0.05;
    half3 sum = 0;
    [unroll]
    for (int j = 0; j < 3; j++)
    {
        float layer = j;
        float fold = az * (1.6 + layer * 0.4) + layer * 2.1;
        float warp = fold + 0.9 * sin(fold * 2.3 + t * (2.0 + layer)) + 0.4 * sin(fold * 5.1 - t * 3.0);
        float bottom = _AuroraHeight - 0.06 + layer * 0.05 + 0.07 * sin(warp * 1.3 + t * 1.5) +
                       0.03 * sin(warp * 3.7 - t * 2.2);
        float above = h - bottom;
        if (above < -0.02)
            continue;
        float height = 0.16 + 0.1 * Noise1(warp * 1.5 + 10.0 + layer * 7.0);
        float edge = smoothstep(-0.012, 0.008, above);
        float rise = exp(-max(above, 0.0) / height);
        // Brilho ao longo da fita: mais forte onde a cortina dobra (vista de lado)
        float folds = pow(Noise1(warp * 2.2 + t * 4.0 + layer * 13.0), 2.0) * 1.4 + 0.15;
        float rays = 0.8 + 0.2 * Noise1(az * 140.0 + t * 20.0 + layer * 50.0) * Noise1(az * 37.0 - t * 6.0);
        half3 low = _AuroraColorA.rgb, high = _AuroraColorB.rgb;
        half3 mid = lerp(low, half3(0.15, 1.0, 1.1), 0.5);
        float k = saturate(above / (height * 1.8));
        half3 tint = k < 0.5 ? lerp(low, mid, k * 2.0) : lerp(mid, high, (k - 0.5) * 2.0);
        sum += tint * edge * rise * folds * rays * (1.0 - layer * 0.25);
    }

    float patches = saturate(0.3 + 0.9 * Noise1(az * 0.8 + t * 0.5));
    return sum * patches * _AuroraStrength * saturate(h * 6.0);
}

// Cor do céu na direção 'd' (sem a faixa de névoa do horizonte; quem chama mistura com a névoa)
half3 SkyColor(float3 d, float starSharp)
{
    float h = d.y;
    half3 sky = lerp(_HorizonTint.rgb, _MidColor.rgb, smoothstep(0.0, _GradientBlue, h));
    sky = lerp(sky, _TopColor.rgb, smoothstep(_GradientBlue, _GradientTop, h));
    sky += _GlowColor.rgb * exp(-abs(h) * 9.0);
    if (h < 0.0)
        sky = lerp(_HorizonColor.rgb * 0.6, half3(0, 0, 0), saturate(-h * 4.0));

    sky += SkyAurora(d);
    half4 stars = SkyStars(d, starSharp);
    sky += _NebulaColor.rgb * stars.a * saturate(h * 3.0) + stars.rgb * saturate(h * 8.0 + 0.2);
    sky += SkyMeteors(d);
    half4 planet = SkyPlanet(d);
    return lerp(sky, planet.rgb, planet.a);
}

#endif
