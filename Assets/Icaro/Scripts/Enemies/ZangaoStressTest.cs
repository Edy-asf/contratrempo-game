using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Teste de desempenho (menu "Ícaro > Teste: 30 Zangões", em Play): solta Count Zangões pelo PrefabPool em volta
    // do jogador e mede o FPS por Duration segundos (média, pior frame e 1% mais lentos). O resultado vai para o
    // Console e para a tela. Os Zangões ficam na cena depois do teste.
    public class ZangaoStressTest : MonoBehaviour
    {
        public GameObject Prefab;
        public EnemyDefinition Definition;
        public int Count = 30;
        public float Duration = 10f;

        string m_Result = "Medindo...";

        IEnumerator Start()
        {
            var player = FindAnyObjectByType<PlayerCharacterController>();
            Vector3 center = player ? player.transform.position : Vector3.zero;
            for (int i = 0; i < Count; i++)
            {
                Vector2 around = Random.insideUnitCircle.normalized * Random.Range(10f, 16f);
                Vector3 position = center + new Vector3(around.x, 4f, around.y);
                GameObject zangao = PrefabPool.Get(Prefab, position, Quaternion.identity);
                var stats = zangao ? zangao.GetComponent<EnemyStats>() : null;
                if (stats)
                {
                    if (!stats.Definition)
                        stats.Definition = Definition;
                    stats.Configure(1f, 1f);
                }
            }

            // Um segundo para estabilizar, depois mede
            yield return new WaitForSeconds(1f);
            var frames = new List<float>();
            float elapsed = 0f;
            while (elapsed < Duration)
            {
                frames.Add(Time.unscaledDeltaTime);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            frames.Sort();
            float total = 0f;
            foreach (float frame in frames)
                total += frame;
            int slowCount = Mathf.Max(1, frames.Count / 100);
            float slowTotal = 0f;
            for (int i = frames.Count - slowCount; i < frames.Count; i++)
                slowTotal += frames[i];

            m_Result = $"{ZangaoAI.ActiveCount} Zangões: média {frames.Count / total:0} FPS, " +
                       $"1% mais lentos {slowCount / slowTotal:0} FPS, pior frame {frames[frames.Count - 1] * 1000f:0.0} ms";
            Debug.Log("[Ícaro] Teste de desempenho: " + m_Result);
            Destroy(this, 8f);
        }

        void OnGUI()
        {
            GUI.Label(new Rect(20f, Screen.height - 50f, 900f, 30f), "[Teste] " + m_Result);
        }
    }
}
