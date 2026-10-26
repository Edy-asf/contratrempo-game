using UnityEngine;

namespace Icaro
{
    // Cadáver de um inimigo em sprite: um quad solto (o inimigo pode ser destruído ou voltar ao pool na hora) que toca
    // o clipe "Death" na direção em que o inimigo morreu, fica parado um pouco e afunda no chão antes de sumir.
    public class EnemySpriteCorpse : MonoBehaviour
    {
        const float k_Hold = 0.8f, k_Sink = 0.6f;

        static readonly int k_BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        EnemySpriteSet m_Set;
        EnemySpriteSet.Clip m_Clip;
        MeshRenderer m_Renderer;
        MaterialPropertyBlock m_Block;
        int m_Direction;
        float m_Time;
        float m_MinBrightness;

        public static void Spawn(EnemySpriteSet set, Transform sprite, int direction, float minBrightness)
        {
            EnemySpriteSet.Clip clip = set ? set.Find("Death") : null;
            if (clip == null)
                return;

            var corpse = new GameObject("Cadaver_" + set.name).AddComponent<EnemySpriteCorpse>();
            corpse.transform.SetPositionAndRotation(sprite.position, sprite.rotation);
            corpse.gameObject.AddComponent<MeshFilter>().sharedMesh = EnemySpriteView.MeshFor(set);
            corpse.m_Renderer = corpse.gameObject.AddComponent<MeshRenderer>();
            corpse.m_Renderer.sharedMaterial = set.Material;
            corpse.m_Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            corpse.m_Set = set;
            corpse.m_Clip = clip;
            corpse.m_Direction = direction;
            corpse.m_MinBrightness = minBrightness;
            corpse.m_Block = new MaterialPropertyBlock();
            corpse.Apply();
        }

        void LateUpdate()
        {
            m_Time += Time.deltaTime * CombatTiming.Multiplier;
            Camera camera = Camera.main;
            if (camera)
            {
                Vector3 away = transform.position - camera.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(away);
            }

            float end = m_Clip.Duration + k_Hold;
            if (m_Time > end)
                transform.position += Vector3.down * (m_Set.WorldHeight / k_Sink) * Time.deltaTime * CombatTiming.Multiplier;
            if (m_Time > end + k_Sink)
            {
                Destroy(gameObject);
                return;
            }

            Apply();
        }

        void Apply()
        {
            int frame = EnemySpriteView.FrameAt(m_Clip, m_Time, -1f);
            m_Block.SetVector(k_BaseMapST, m_Set.CellST(m_Clip.Row + m_Direction, frame));
            m_Block.SetColor(k_EmissionColor, Color.white * m_MinBrightness / Mathf.Max(0.01f, m_Set.BodyGlow));
            m_Renderer.SetPropertyBlock(m_Block);
        }
    }
}
