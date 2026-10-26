// Domo de energia em volta da arena (ArenaDome): parede cilíndrica com uma grade de hexágonos ciano, aditiva e sem
// luz. UV0 traz as coordenadas em metros (u ao longo da volta, v do chão para cima), então a grade é uma só, contínua e
// sem deformar; no alto ela se apaga aos poucos (_FadeStart a _FadeEnd, em metros). As linhas têm a espessura medida
// em pixels da tela (_LinePixels), iguais de perto e de longe; quando as células ficam pequenas demais na tela
// (longe ou de lado), a grade some aos poucos para não virar ruído. Visibilidade: _Reveal acende tudo até a altura
// _Sweep (fração da altura visível, com uma faixa clara na frente da subida); perto do jogador (_PlayerPos,
// _RevealRadius) os hexágonos acendem pela distância; até 4 ondulações (_Ripple0..3: ponto e hora de início em w)
// abrem anéis de luz; _Dissolve apaga célula por célula (sorteio por hexágono) com a borda acesa, para o estilhaço.
Shader "Icaro/ArenaDome"
{
    Properties
    {
        [HDR] _Color("Cor", Color) = (0.25, 1.4, 2.2, 1)
        _CellSize("Tamanho do hexágono (m)", Float) = 4
        _LinePixels("Espessura da linha (pixels)", Range(0.5, 4)) = 1
        _Fill("Preenchimento", Range(0, 1)) = 0.05
        _FadeStart("Começa a apagar (m)", Float) = 30
        _FadeEnd("Apagado (m)", Float) = 50
        _Reveal("Revelado", Range(0, 1)) = 0
        _Sweep("Subida", Range(0, 1.2)) = 1.2
        _RevealRadius("Raio perto do jogador (m)", Float) = 9
        _PlayerPos("Jogador", Vector) = (0, -1000, 0, 0)
        _RippleSpeed("Velocidade da ondulação", Float) = 7
        _Dissolve("Dissolver", Range(0, 1)) = 0
        _Ripple0("Ondulação 0", Vector) = (0, 0, 0, -100)
        _Ripple1("Ondulação 1", Vector) = (0, 0, 0, -100)
        _Ripple2("Ondulação 2", Vector) = (0, 0, 0, -100)
        _Ripple3("Ondulação 3", Vector) = (0, 0, 0, -100)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Domo"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _CellSize;
                float _LinePixels;
                float _Fill;
                float _FadeStart;
                float _FadeEnd;
                float _Reveal;
                float _Sweep;
                float _RevealRadius;
                float4 _PlayerPos;
                float _RippleSpeed;
                float _Dissolve;
                float4 _Ripple0;
                float4 _Ripple1;
                float4 _Ripple2;
                float4 _Ripple3;
            CBUFFER_END

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
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Grade de hexágonos: posição dentro da célula (gv) e a célula (id)
            void Hex(float2 p, out float2 gv, out float2 id)
            {
                const float2 r = float2(1.0, 1.7320508);
                const float2 h = r * 0.5;
                float2 a = p - r * floor(p / r) - h;
                float2 b = (p - h) - r * floor((p - h) / r) - h;
                gv = dot(a, a) < dot(b, b) ? a : b;
                id = p - gv;
            }

            float Ripple(float4 ripple, float3 positionWS)
            {
                float t = _Time.y - ripple.w;
                if (t < 0.0 || t > 1.2)
                    return 0.0;
                float d = distance(positionWS, ripple.xyz);
                float ring = exp(-abs(d - t * _RippleSpeed) * 2.5);
                float core = exp(-d * 1.2) * saturate(1.0 - t * 3.0) * 2.0;
                return (ring + core) * (1.0 - t / 1.2);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv / _CellSize;
                float2 gv, id;
                Hex(p, gv, id);
                float edge = 0.5 - max(dot(abs(gv), normalize(float2(1.0, 1.7320508))), abs(gv.x));

                // Linha com a espessura em pixels: a distância até a borda dividida pelo quanto ela muda por pixel
                float perPixel = max(fwidth(edge), 1e-5);
                float grid = saturate(1.0 - edge / (perPixel * _LinePixels));

                // Células pequenas demais na tela (longe ou de lado): a grade some aos poucos
                float cellPixels = 1.0 / max(max(fwidth(p.x), fwidth(p.y)), 1e-5);
                float legible = saturate((cellPixels - 6.0) / 10.0);
                grid *= legible;

                float cell = Hash(id);

                // Dissolve célula por célula; a borda que ainda resta brilha mais
                if (cell < _Dissolve)
                    discard;
                float dissolveEdge = saturate(1.0 - (cell - _Dissolve) * 8.0) * step(0.001, _Dissolve) * legible;

                // Revelado de baixo para cima, com a faixa clara na frente da subida
                float height = saturate(input.uv.y / max(_FadeEnd, 0.01));
                float sweep = step(height, _Sweep);
                float front = exp(-abs(height - _Sweep) * 30.0) * step(_Sweep, 1.05);
                float visible = _Reveal * (sweep + front * 2.0);

                // Perto do jogador
                float near = saturate(1.0 - distance(input.positionWS, _PlayerPos.xyz) / max(_RevealRadius, 0.01));
                visible = max(visible, near * near * 1.4);

                // Ondulações onde foi tocado ou acertado
                visible += Ripple(_Ripple0, input.positionWS) + Ripple(_Ripple1, input.positionWS)
                         + Ripple(_Ripple2, input.positionWS) + Ripple(_Ripple3, input.positionWS);

                // Apaga no alto (a parede continua, só não aparece)
                visible *= 1.0 - smoothstep(_FadeStart, _FadeEnd, input.uv.y);

                // Cintilado leve por célula
                float shimmer = 0.75 + 0.25 * sin(_Time.y * 2.0 + cell * 6.2831);
                float intensity = (grid + _Fill + dissolveEdge * 1.5) * visible * shimmer;
                return half4(_Color.rgb * intensity, 1.0);
            }
            ENDHLSL
        }
    }
}
