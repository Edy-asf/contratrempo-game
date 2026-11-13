using System.Collections.Generic;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Congela o mundo fora de combate (KairosClock): a escala vai a 0 quando a sala congela o mundo, antes das cartas
    // (desacelerando em FreezeTime), e volta a 1 quando a sala acorda o mundo, antes do domo (UnfreezeTime). Fora o que os
    // scripts do cenário leem do KairosClock, este componente para as partículas que ficam em loop no cenário (neve,
    // fumaça, brasas; os esporos são da TimeAtmosphere) e abaixa os sons em loop do cenário. O TimeManager o adiciona.
    public class WorldFreeze : MonoBehaviour
    {
        [Tooltip("Segundos para o mundo parar depois da luta")]
        [Min(0.05f)] public float FreezeTime = 1.4f;

        [Tooltip("Segundos para o mundo voltar a andar quando a luta começa")]
        [Min(0.05f)] public float UnfreezeTime = 0.45f;

        [Tooltip("Volume dos sons em loop do cenário com o mundo congelado (1 = normal)")]
        [Range(0f, 1f)] public float FrozenAmbience = 0f;

        static readonly int s_WorldTime = Shader.PropertyToID("_IcaroWorldTime");
        static readonly int s_ClockOn = Shader.PropertyToID("_IcaroWorldClockOn");

        readonly Dictionary<ParticleSystem, float> m_Particles = new Dictionary<ParticleSystem, float>();
        readonly Dictionary<AudioSource, float> m_Sounds = new Dictionary<AudioSource, float>();
        readonly List<ParticleSystem> m_Dead = new List<ParticleSystem>();
        float m_Progress;         // 0 congelado, 1 andando (antes da curva)
        float m_AppliedScale = -1f;
        float m_NextScan;

        void Start()
        {
            m_Progress = TimeManager.WorldAwakeNow ? 1f : 0f;
            KairosClock.Scale = m_Progress;
            Scan();
        }

        void OnDisable()
        {
            Shader.SetGlobalFloat(s_ClockOn, 0f);
            KairosClock.Scale = 1f;
            Apply(1f);
        }

        void Update()
        {
            bool running = TimeManager.WorldAwakeNow;
            float step = Time.unscaledDeltaTime / (running ? UnfreezeTime : FreezeTime);
            m_Progress = Mathf.MoveTowards(m_Progress, running ? 1f : 0f, step);
            // Rampa suave nas duas pontas (como uma fita parando ou voltando a rodar)
            float scale = m_Progress * m_Progress * (3f - 2f * m_Progress);
            KairosClock.Scale = scale;
            KairosClock.WorldTime += Time.deltaTime * scale * CombatTiming.WorldSpeed;
            Shader.SetGlobalFloat(s_WorldTime, KairosClock.WorldTime);
            Shader.SetGlobalFloat(s_ClockOn, 1f);

            // Cenário que aparece depois (neve, fumaça criadas em jogo): procura de novo de vez em quando
            if (Time.unscaledTime >= m_NextScan)
                Scan();

            Apply(scale);
        }

        void Apply(float scale)
        {
            if (Mathf.Abs(scale - m_AppliedScale) < 0.002f)
                return;
            m_AppliedScale = scale;

            m_Dead.Clear();
            foreach (KeyValuePair<ParticleSystem, float> pair in m_Particles)
            {
                if (!pair.Key)
                {
                    m_Dead.Add(pair.Key);
                    continue;
                }

                ParticleSystem.MainModule main = pair.Key.main;
                main.simulationSpeed = pair.Value * scale;
            }

            foreach (ParticleSystem dead in m_Dead)
                m_Particles.Remove(dead);

            float volume = Mathf.Lerp(FrozenAmbience, 1f, scale);
            foreach (KeyValuePair<AudioSource, float> pair in m_Sounds)
            {
                if (pair.Key)
                    pair.Key.volume = pair.Value * volume;
            }
        }

        // Partículas em loop e sons em loop do cenário (fora do jogador, da música e dos menus)
        void Scan()
        {
            m_NextScan = Time.unscaledTime + 4f;
            bool added = false;
            foreach (ParticleSystem system in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include))
            {
                if (m_Particles.ContainsKey(system) || !system.main.loop || system.name.StartsWith("Esporos_") ||
                    IsOwned(system.transform))
                    continue;
                m_Particles.Add(system, system.main.simulationSpeed);
                added = true;
            }

            foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            {
                if (m_Sounds.ContainsKey(source) || !source.loop || source.spatialBlend < 0.5f ||
                    IsOwned(source.transform))
                    continue;
                m_Sounds.Add(source, source.volume);
                added = true;
            }

            if (added)
                m_AppliedScale = -1f;
        }

        // Do jogador, do próprio TimeManager (música), de inimigos ou de algo que não é do cenário
        bool IsOwned(Transform target)
        {
            if (target.IsChildOf(transform) || target.GetComponentInParent<PlayerCharacterController>(true) ||
                target.GetComponentInParent<EnemyStats>(true) || target.GetComponentInParent<Camera>(true))
                return true;
            return target.gameObject.scene.buildIndex == -1 && target.gameObject.scene.name == "DontDestroyOnLoad";
        }
    }
}
