using TMPro;
using UnityEngine;

namespace Icaro
{
    // Números de dano no ponto do impacto: sobem, encolhem do "pop" inicial e somem em Lifetime segundos, sempre
    // virados para a câmera. Brancos no dano normal, amarelos e maiores no crítico. Fonte do visor (Kenney Future,
    // ItemFonts.VisorWide) com o shader e o contorno do FontMaterial. Os textos vêm de um pool fixo (sem instanciar
    // durante o combate); se faltar texto, o mais antigo é reaproveitado.
    public class DamageNumbers : MonoBehaviour
    {
        [Tooltip("Material do texto; use um com shader Overlay para os números não sumirem atrás da geometria")]
        public Material FontMaterial;

        public int PoolSize = 48;
        public float Lifetime = 0.8f;

        [Tooltip("Quanto o número sobe ao longo da vida, em metros (a 10 m da câmera)")]
        public float Rise = 0.9f;

        public Color NormalColor = Color.white;
        public Color CriticalColor = new Color(1f, 0.85f, 0.15f);

        [Tooltip("Tamanho do número a 10 m da câmera; o tamanho acompanha a distância para continuar legível")]
        public float Size = 0.45f;
        public float CriticalSizeMultiplier = 1.5f;

        [Tooltip("Escala no instante em que aparece (pop)")]
        public float PopScale = 1.6f;

        struct Entry
        {
            public TextMeshPro Text;
            public Vector3 Origin;
            public Vector3 Drift;
            public float Age;
            public float Size;
            public Color Color;
        }

        Entry[] m_Pool;
        int m_Next;
        Camera m_Camera;

        public void Spawn(Vector3 point, float amount, bool critical)
        {
            EnsurePool();
            ref Entry entry = ref m_Pool[m_Next];
            m_Next = (m_Next + 1) % m_Pool.Length;

            int value = Mathf.Max(1, Mathf.RoundToInt(amount));
            entry.Text.text = critical ? value + "!" : value.ToString();
            entry.Origin = point;
            entry.Drift = Random.insideUnitSphere * 0.25f;
            entry.Drift.y = Mathf.Abs(entry.Drift.y);
            entry.Age = 0f;
            entry.Size = critical ? Size * CriticalSizeMultiplier : Size;
            entry.Color = critical ? CriticalColor : NormalColor;
            entry.Text.fontStyle = critical ? FontStyles.Bold : FontStyles.Normal;
            entry.Text.gameObject.SetActive(true);
            Place(ref entry);
        }

        void Awake() => EnsurePool();

        // O pool não é serializável: recria se tiver se perdido (ex.: recarga de scripts no Play mode)
        void EnsurePool()
        {
            if (m_Pool != null)
                return;

            m_Pool = new Entry[PoolSize];
            TMP_FontAsset font = ItemFonts.VisorWide;
            Material material = font ? VisorMaterial(font) : FontMaterial;
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("NumeroDano", typeof(TextMeshPro));
                go.transform.SetParent(transform, false);
                var text = go.GetComponent<TextMeshPro>();
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = 10f;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                if (font)
                    text.font = font;
                if (material)
                    text.fontSharedMaterial = material;
                go.SetActive(false);
                m_Pool[i] = new Entry { Text = text, Age = Lifetime };
            }
        }

        // Material da fonte do visor com o shader Overlay (por cima de tudo) e o contorno do FontMaterial
        Material VisorMaterial(TMP_FontAsset font)
        {
            font.TryAddCharacters("0123456789!");
            if (!FontMaterial)
                return font.material;
            var material = new Material(font.material) { name = "NumeroDano_Visor" };
            material.shader = FontMaterial.shader;
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, FontMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth));
            material.SetColor(ShaderUtilities.ID_OutlineColor, FontMaterial.GetColor(ShaderUtilities.ID_OutlineColor));
            material.SetFloat(ShaderUtilities.ID_FaceDilate, FontMaterial.GetFloat(ShaderUtilities.ID_FaceDilate));
            ShaderUtilities.UpdateShaderRatios(material);
            return material;
        }

        void LateUpdate()
        {
            if (m_Pool == null)
                return;

            for (int i = 0; i < m_Pool.Length; i++)
            {
                ref Entry entry = ref m_Pool[i];
                if (entry.Age >= Lifetime)
                    continue;

                entry.Age += Time.deltaTime;
                if (entry.Age >= Lifetime)
                {
                    entry.Text.gameObject.SetActive(false);
                    continue;
                }

                Place(ref entry);
            }
        }

        void Place(ref Entry entry)
        {
            if (!m_Camera)
                m_Camera = Camera.main;
            if (!m_Camera)
                return;

            float t = entry.Age / Lifetime;
            Transform cam = m_Camera.transform;
            float distance = Mathf.Max(1f, Vector3.Distance(cam.position, entry.Origin));
            float distanceScale = distance / 10f;

            // Sobe desacelerando; o deslocamento lateral espalha números de acertos seguidos
            float rise = (1f - (1f - t) * (1f - t)) * Rise * distanceScale;
            Vector3 position = entry.Origin + (Vector3.up * rise) + entry.Drift * distanceScale * t;

            float pop = Mathf.Lerp(PopScale, 1f, Mathf.Clamp01(t * 6f));
            float fade = t < 0.6f ? 1f : Mathf.InverseLerp(1f, 0.6f, t);

            Transform tr = entry.Text.transform;
            tr.SetPositionAndRotation(position, cam.rotation);
            // Fonte 10 no TextMeshPro 3D mede cerca de 1 m de altura
            tr.localScale = Vector3.one * (entry.Size * distanceScale * pop);
            Color color = entry.Color;
            color.a = fade;
            entry.Text.color = color;
        }
    }
}
