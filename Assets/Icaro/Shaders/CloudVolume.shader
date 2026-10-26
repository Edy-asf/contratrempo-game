// Mar de nuvens de volume (CloudSea): desenhado numa caixa enorme vista por dentro, percorre o raio de cada pixel dentro
// da camada de nuvem (_CloudBottom a _CloudTop, em metros do mundo) e acumula fumaça. Densidade: ruído 3D que se repete
// (_IcaroCloudNoise; R e G dão as formas grandes, B corrói as bordas em fiapos), cheia embaixo e cada vez mais rala para
// cima, de modo que só os montes mais altos chegam ao topo (borda de cima em ondas de fumaça). Luz: a luz principal (o
// eclipse) atravessa a nuvem até o ponto (duas amostras na direção dela, lei de Beer) e brilha mais olhando contra ela
// (borda prateada); o ambiente vai de _ShadowColor embaixo a _LitColor em cima. A névoa da cena (exponencial ao
// quadrado) cobre cada amostra pela distância. O raio para no cenário pela profundidade da cena (_CameraDepthTexture,
// ligada pelo CloudSea), então os picos atravessam a camada e somem dentro dela. Passos crescem com a distância e
// começam num deslocamento por pixel (sem faixas). _IcaroCloudTime (relógio do CloudSea) empurra o ruído pelo vento.
Shader "Icaro/CloudVolume"
{
    Properties
    {
        _CloudBottom("Base (m)", Float) = -260
        _CloudTop("Topo (m)", Float) = -72
        _ShapeScale("Escala das formas (m)", Float) = 900
        _DetailScale("Escala dos fiapos (m)", Float) = 160
        _Coverage("Cobertura", Range(0, 1)) = 0.38
        _Sharpness("Contraste da borda", Range(0.5, 8)) = 3
        _Erosion("Corrosão dos fiapos", Range(0, 1)) = 0.45
        _Extinction("Densidade (1/m)", Float) = 0.035
        _LightAbsorb("Absorção da luz", Float) = 0.6
        [HDR] _LitColor("Ambiente em cima", Color) = (0.62, 0.55, 0.74, 1)
        [HDR] _ShadowColor("Ambiente embaixo", Color) = (0.16, 0.12, 0.24, 1)
        _SunStrength("Força da luz", Float) = 1.4
        _Silver("Borda prateada", Float) = 2.5
        _Wind("Vento (m/s do relógio)", Vector) = (3, 0.4, 1.2, 0)
        _MinStep("Passo mínimo (m)", Float) = 2
        _StepGrowth("Passo por metro", Float) = 0.02
        _MaxStep("Passo máximo (m)", Float) = 32
        _MaxDistance("Distância máxima (m)", Float) = 3000
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Nuvens"
            Tags { "LightMode" = "UniversalForward" }
            Blend One SrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CloudBottom;
                float _CloudTop;
                float _ShapeScale;
                float _DetailScale;
                float _Coverage;
                float _Sharpness;
                float _Erosion;
                float _Extinction;
                float _LightAbsorb;
                half4 _LitColor;
                half4 _ShadowColor;
                float _SunStrength;
                float _Silver;
                float4 _Wind;
                float _MinStep;
                float _StepGrowth;
                float _MaxStep;
                float _MaxDistance;
            CBUFFER_END

            TEXTURE3D(_IcaroCloudNoise);
            SAMPLER(sampler_IcaroCloudNoise);
            float _IcaroCloudTime;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            // Densidade (0 a 1) no ponto; 'detail' liga a corrosão dos fiapos (fora dela, só as formas: luz e saltos)
            float Density(float3 p, bool detail)
            {
                float h = saturate((p.y - _CloudBottom) / (_CloudTop - _CloudBottom));
                float3 drift = _Wind.xyz * _IcaroCloudTime;
                float4 n = SAMPLE_TEXTURE3D_LOD(_IcaroCloudNoise, sampler_IcaroCloudNoise, (p + drift) / _ShapeScale, 0);
                float shape = n.r * 0.7 + n.g * 0.3;
                float threshold = smoothstep(0.4, 1.0, h) * 1.05;
                float d = saturate((shape + _Coverage - threshold) * _Sharpness);
                if (detail && d > 0.0 && d < 1.0)
                {
                    float fine = SAMPLE_TEXTURE3D_LOD(_IcaroCloudNoise, sampler_IcaroCloudNoise,
                        (p + drift * 1.6) / _DetailScale, 0).b;
                    d = saturate((d - fine * _Erosion * (1.0 - d)) / max(1.0 - fine * _Erosion * (1.0 - d), 0.001));
                }
                return d;
            }

            // Ruído por pixel (gradiente intercalado): espalha o começo dos passos e tira as faixas
            float Jitter(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 ro = GetCameraPositionWS();
                float3 rd = normalize(input.positionWS - ro);

                // Trecho do raio dentro da camada
                float tEnter, tExit;
                if (abs(rd.y) < 1e-5)
                {
                    if (ro.y < _CloudBottom || ro.y > _CloudTop)
                        return half4(0, 0, 0, 1);
                    tEnter = 0;
                    tExit = _MaxDistance;
                }
                else
                {
                    float t1 = (_CloudBottom - ro.y) / rd.y, t2 = (_CloudTop - ro.y) / rd.y;
                    tEnter = max(min(t1, t2), 0);
                    tExit = max(t1, t2);
                }

                // Para no cenário (os picos dentro da nuvem)
                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float eye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float3 forward = -UNITY_MATRIX_V[2].xyz;
                float sceneDistance = eye / max(dot(rd, forward), 1e-4);
                tExit = min(min(tExit, _MaxDistance), sceneDistance);
                if (tEnter >= tExit)
                    return half4(0, 0, 0, 1);

                Light sun = GetMainLight();
                float3 L = sun.direction;
                float facing = saturate(dot(rd, L));
                float phase = 0.75 + _Silver * pow(facing, 6) + 0.35 * facing;
                float3 sunColor = sun.color * _SunStrength;
                float fogDensity = unity_FogParams.x;

                float jitter = Jitter(input.positionCS.xy);
                float transmittance = 1;
                float3 color = 0;
                float t = tEnter;

                [loop]
                for (int i = 0; i < 110; i++)
                {
                    if (t >= tExit || transmittance < 0.01)
                        break;
                    float dt = clamp(t * _StepGrowth, _MinStep, _MaxStep);
                    float3 p = ro + rd * (t + dt * jitter);
                    float d = Density(p, true);
                    if (d > 0.002)
                    {
                        // Luz do eclipse atravessando a nuvem até aqui
                        float lightDepth = Density(p + L * 18, false) * 18 + Density(p + L * 60, false) * 42;
                        float sunTrans = exp(-lightDepth * _Extinction * _LightAbsorb);
                        float powder = 1 - exp(-d * 4);
                        float h = saturate((p.y - _CloudBottom) / (_CloudTop - _CloudBottom));
                        float3 ambient = lerp(_ShadowColor.rgb, _LitColor.rgb, pow(h, 0.8));
                        float3 lit = ambient + sunColor * sunTrans * phase * lerp(0.6, 1, powder);

                        // Névoa da cena pela distância da amostra
                        float fog = saturate(exp2(-(fogDensity * t) * (fogDensity * t)));
                        lit = lerp(unity_FogColor.rgb, lit, fog);

                        float stepTrans = exp(-d * _Extinction * dt);
                        color += transmittance * (1 - stepTrans) * lit;
                        transmittance *= stepTrans;
                    }
                    t += dt;
                }

                return half4(color, transmittance);
            }
            ENDHLSL
        }
    }
}
