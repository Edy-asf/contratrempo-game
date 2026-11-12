using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.FPS.Gameplay;

namespace Icaro
{
    // Braseiros que acendem em cascata quando o jogador entra no gatilho (a praça da basílica de Archen Kaedra): cada
    // passo acende os fogos dele juntos (um de cada lado da colunata) e, depois de 'Interval', o próximo, das pontas
    // perto da basílica até as mais longe. Cada fogo é um objeto desligado com a chama (SpriteFlame) e uma luz; ao
    // acender, a chama cresce com um repique e a luz dá um clarão e assenta tremulando. Acende uma vez só. Só visual.
    [RequireComponent(typeof(Collider))]
    public class BrazierCascade : MonoBehaviour
    {
        [System.Serializable]
        public class Step
        {
            public GameObject[] Fires;
        }

        [Tooltip("Passos da cascata, na ordem; os fogos de um passo acendem juntos")]
        public List<Step> Steps = new List<Step>();

        [Tooltip("Segundos entre um passo e o próximo")]
        public float Interval = 0.32f;

        [Tooltip("Segundos da chama crescendo")]
        public float GrowTime = 0.45f;

        [Tooltip("Clarão da luz ao acender (x a intensidade normal)")]
        public float Flash = 2.2f;

        readonly List<(Light Light, float Base, float Phase)> m_Lit = new List<(Light, float, float)>();
        bool m_Started;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            foreach (Step step in Steps)
                foreach (GameObject fire in step.Fires)
                    if (fire)
                        fire.SetActive(false);
        }

        void OnTriggerEnter(Collider other)
        {
            if (m_Started || !other.GetComponentInParent<PlayerCharacterController>())
                return;
            m_Started = true;
            StartCoroutine(Cascade());
        }

        IEnumerator Cascade()
        {
            foreach (Step step in Steps)
            {
                foreach (GameObject fire in step.Fires)
                    if (fire)
                        StartCoroutine(Ignite(fire));
                yield return new WaitForSeconds(Interval);
            }
        }

        IEnumerator Ignite(GameObject fire)
        {
            fire.SetActive(true);
            SpriteFlame flame = fire.GetComponentInChildren<SpriteFlame>();
            Light light = fire.GetComponentInChildren<Light>();
            Vector3 scale = flame ? flame.transform.localScale : Vector3.one;
            float baseIntensity = light ? light.intensity : 0f;
            for (float t = 0f; t < GrowTime; t += Time.deltaTime)
            {
                float k = t / GrowTime;
                float grow = 1f + Mathf.Sin(k * Mathf.PI) * 0.35f;      // passa um pouco do tamanho e volta
                if (flame)
                    flame.transform.localScale = new Vector3(scale.x * Mathf.Min(1f, k * 1.6f), scale.y * k * grow, scale.z);
                if (light)
                    light.intensity = baseIntensity * Mathf.Lerp(Flash, 1f, k) * Mathf.Min(1f, k * 4f);
                yield return null;
            }

            if (flame)
                flame.transform.localScale = scale;
            if (light)
            {
                light.intensity = baseIntensity;
                m_Lit.Add((light, baseIntensity, Random.value * 10f));
            }
        }

        void Update()
        {
            float t = KairosClock.WorldTime;
            foreach ((Light light, float b, float phase) in m_Lit)
                if (light)
                    light.intensity = b * (0.86f + 0.14f * Mathf.PerlinNoise(t * 5f + phase, phase));
        }
    }
}
