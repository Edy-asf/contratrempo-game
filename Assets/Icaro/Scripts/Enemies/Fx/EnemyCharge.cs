using UnityEngine;

namespace Icaro
{
    // Carga do aviso de ataque presa a um ponto do inimigo (cano, ferrão, cogumelo do visor): núcleo em pixel art que
    // cresce em 4 quadros pelo progresso do aviso, com pixels sendo puxados para ele, de frente para a câmera. A IA
    // chama Show(progresso) a cada frame do aviso e Hide no fim.
    public class EnemyCharge : MonoBehaviour
    {
        SpriteRenderer m_Renderer;
        Sprite[] m_Frames;

        public static EnemyCharge Create(Transform anchor, EnemyFxStyle style, Vector3 localOffset = default)
        {
            if (!anchor)
                return null;
            SpriteRenderer renderer = EnemyFxSprites.CreateRenderer(anchor, "Carga_" + style);
            renderer.transform.localPosition = localOffset;
            var charge = renderer.gameObject.AddComponent<EnemyCharge>();
            charge.m_Renderer = renderer;
            charge.m_Frames = EnemyFxSprites.Charge(style);
            renderer.enabled = false;
            return charge;
        }

        public void Show(float progress)
        {
            int frame = Mathf.Clamp(Mathf.FloorToInt(progress * m_Frames.Length), 0, m_Frames.Length - 1);
            // Pisca nos últimos instantes do aviso
            bool blink = progress > 0.75f && Mathf.FloorToInt(Time.time * 24f) % 2 == 0;
            m_Renderer.sprite = m_Frames[blink ? Mathf.Max(0, frame - 1) : frame];
            m_Renderer.enabled = true;
            Face();
        }

        public void Hide()
        {
            if (m_Renderer)
                m_Renderer.enabled = false;
        }

        void LateUpdate()
        {
            if (m_Renderer.enabled)
                Face();
        }

        void Face()
        {
            Camera camera = Camera.main;
            if (!camera)
                return;
            Vector3 away = transform.position - camera.transform.position;
            if (away.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(away, camera.transform.up);
            // O sprite não herda a escala do inimigo
            Vector3 parent = transform.parent ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(1f / Mathf.Max(0.01f, parent.x), 1f / Mathf.Max(0.01f, parent.y),
                1f / Mathf.Max(0.01f, parent.z));
        }
    }
}
