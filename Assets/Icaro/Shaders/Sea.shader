// Mar do Arquipélago: a textura da água (com o deslocamento do WaterRipple), iluminada pela luz ambiente e pelo
// luar, e o reflexo do céu noturno (o mesmo céu do Icaro/NightSky: degradê, estrelas, estrelas cadentes, aurora e
// planeta) pela direção refletida numa superfície com ondas (somas de senos em várias direções, que se mexem com o
// tempo). Fresnel: de cima quase só a água, rente ao horizonte quase só o reflexo. As propriedades do céu são
// copiadas do céu da cena a cada frame pelo SeaSkySync (assim o reflexo segue as transições de bioma).
Shader "Icaro/Sea"
{
    Properties
    {
        _BaseMap("Textura da água", 2D) = "white" {}
        _BaseColor("Cor (alpha = opacidade)", Color) = (1, 1, 1, 0.88)
        _Reflection("Força do reflexo", Range(0, 2)) = 1
        _WaveStrength("Força das ondas no reflexo", Range(0, 1)) = 0.18
        _WaveScale("Tamanho das ondas (m)", Range(0.5, 40)) = 6
        _WaveSpeed("Velocidade das ondas", Range(0, 4)) = 0.6

        _TopColor("Zênite", Color) = (0.01, 0.01, 0.035, 1)
        _HorizonColor("Horizonte", Color) = (0.07, 0.035, 0.12, 1)
        _GlowColor("Brilho do horizonte", Color) = (0.2, 0.06, 0.28, 1)
        _StarDensity("Densidade das estrelas", Range(0, 1)) = 0.45
        _StarBrightness("Brilho das estrelas", Range(0, 8)) = 2.2
        _PlanetDir("Direção do planeta", Vector) = (-0.55, 0.3, 0.78, 0)
        _PlanetSize("Tamanho do planeta (radianos)", Range(0, 0.5)) = 0
        _PlanetColorA("Planeta: cor A", Color) = (0.55, 0.32, 0.72, 1)
        _PlanetColorB("Planeta: cor B", Color) = (0.2, 0.55, 0.62, 1)
        _RingColor("Anel", Color) = (0.75, 0.68, 0.9, 1)
        _HorizonFog("Faixa de névoa no horizonte (altura)", Range(0.01, 0.6)) = 0.28
        _HorizonTint("Degradê: azul", Color) = (0.12, 0.28, 0.62, 1)
        _MidColor("Degradê: roxo", Color) = (0.24, 0.07, 0.38, 1)
        _GradientBlue("Degradê: altura do azul", Range(0.05, 0.9)) = 0.22
        _GradientTop("Degradê: altura do zênite", Range(0.1, 1.2)) = 0.55
        _NebulaColor("Nebulosa", Color) = (0.12, 0.06, 0.2, 1)
        _AuroraStrength("Aurora: intensidade", Range(0, 4)) = 0
        [HDR] _AuroraColorA("Aurora: cor de baixo", Color) = (0.2, 1.4, 0.8, 1)
        [HDR] _AuroraColorB("Aurora: cor de cima", Color) = (0.5, 0.3, 1.2, 1)
        _AuroraHeight("Aurora: altura no céu", Range(0.1, 0.9)) = 0.42
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Reflection;
                half _WaveStrength;
                half _WaveScale;
                half _WaveSpeed;
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
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            #include "NightSkyCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // Inclinação da superfície: ondas de senos em quatro direções e tamanhos, mais uma marola fina
            float2 WaveSlope(float2 p, float t)
            {
                static const float2 dirs[4] = { float2(1.0, 0.25), float2(-0.45, 1.0), float2(0.8, -0.7), float2(-1.0, -0.2) };
                static const float freqs[4] = { 1.0, 1.7, 2.9, 4.3 };
                static const float amps[4] = { 1.0, 0.6, 0.35, 0.2 };
                float2 slope = 0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float2 dir = normalize(dirs[i]);
                    float k = 6.2831853 / _WaveScale * freqs[i];
                    slope += dir * (amps[i] * cos(dot(dir, p) * k + t * (1.0 + i * 0.7)));
                }

                return slope / 2.15;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 water = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float t = _Time.y * _WaveSpeed;
                float2 slope = WaveSlope(input.positionWS.xz, t) * _WaveStrength;
                float3 normal = normalize(float3(-slope.x, 1.0, -slope.y));

                // Água: luz ambiente e luar
                Light moon = GetMainLight();
                half3 lit = water.rgb * (SampleSH(normal) + moon.color * saturate(dot(normal, moon.direction)) * 0.5);

                // Reflexo do céu (a direção refletida nunca aponta para baixo do horizonte)
                float3 view = normalize(input.positionWS - GetCameraPositionWS());
                float3 reflected = reflect(view, normal);
                reflected.y = abs(reflected.y);
                reflected = normalize(reflected);
                half3 sky = SkyColor(reflected, 0.6);
                half haze = 1.0 - smoothstep(0.0, _HorizonFog, reflected.y);
                sky = lerp(sky, unity_FogColor.rgb, haze);

                float cosine = saturate(dot(-view, normal));
                half fresnel = 0.02 + 0.98 * pow(1.0 - cosine, 5.0);
                fresnel = saturate(fresnel * _Reflection + 0.06 * _Reflection);
                half3 color = lerp(lit, sky, fresnel);
                half alpha = lerp(water.a, 1.0, fresnel);
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
