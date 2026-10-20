using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Aviso de carta escolhida (o jogo não tem itens no chão): quando a escolha entre salas fecha, cada carta
    // escolhida (ItemDraft.OnCardPicked) aparece no visor, embaixo no centro, num painel com o ícone, "NOVA CARTA", o
    // nome e a raridade na cor dela. Entra crescendo com Ease.OutBack e um brilho passando, fica HoldTime segundos e
    // some subindo devagar. Avisos seguidos entram um pouco depois do anterior e empilham para cima. Usa o tempo sem
    // escala. Criado pelo PlayerStats.
    public class CardPickupToast : MonoBehaviour
    {
        public ItemCatalog Catalog;

        [Tooltip("Segundos da entrada (escala com Ease.OutBack)")]
        public float AppearTime = 0.35f;

        [Tooltip("Segundos parado na tela")]
        public float HoldTime = 2.4f;

        [Tooltip("Segundos do sumiço")]
        public float FadeTime = 0.5f;

        [Tooltip("Segundos entre um aviso e o próximo")]
        public float Stagger = 0.3f;

        const float k_Width = 460f;
        const float k_Height = 76f;
        const float k_BaseY = 180f;

        class Toast
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public RectTransform Shine;
            public float Start;
            public float Y;
        }

        readonly Queue<ItemDefinition> m_Pending = new Queue<ItemDefinition>();
        readonly List<Toast> m_Toasts = new List<Toast>();
        float m_NextAllowed;

        void OnEnable() => ItemDraft.OnCardPicked += Enqueue;
        void OnDisable() => ItemDraft.OnCardPicked -= Enqueue;

        void Enqueue(ItemDefinition item)
        {
            if (item)
                m_Pending.Enqueue(item);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (m_Pending.Count > 0 && now >= m_NextAllowed &&
                HelmetVisor.TryGroup(VisorAnchor.BottomCenter, out RectTransform group))
            {
                Show(m_Pending.Dequeue(), group, now);
                m_NextAllowed = now + Stagger;
            }

            float life = AppearTime + HoldTime + FadeTime;
            for (int i = m_Toasts.Count - 1; i >= 0; i--)
            {
                Toast toast = m_Toasts[i];
                float t = now - toast.Start;
                if (t >= life || !toast.Rect)
                {
                    if (toast.Rect)
                        Destroy(toast.Rect.gameObject);
                    m_Toasts.RemoveAt(i);
                    continue;
                }

                Animate(toast, t);
            }

            // Os mais antigos sobem para dar lugar ao novo
            for (int i = 0; i < m_Toasts.Count; i++)
            {
                float target = k_BaseY + (m_Toasts.Count - 1 - i) * (k_Height + 10f);
                m_Toasts[i].Y = Mathf.Lerp(m_Toasts[i].Y, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
            }
        }

        void Animate(Toast toast, float t)
        {
            float appear = Mathf.Clamp01(t / AppearTime);
            float fade = Mathf.Clamp01((t - AppearTime - HoldTime) / FadeTime);
            float scale = Mathf.LerpUnclamped(0.55f, 1f, VisorUI.EaseOutBack(appear));
            toast.Rect.localScale = Vector3.one * scale;
            toast.Group.alpha = Mathf.Clamp01(appear * 2f) * (1f - fade);
            toast.Rect.anchoredPosition = new Vector2(0f, toast.Y + fade * 24f);

            // Brilho cruzando o painel logo depois de entrar
            float sweep = Mathf.Clamp01((t - AppearTime * 0.6f) / 0.45f);
            toast.Shine.gameObject.SetActive(sweep > 0f && sweep < 1f);
            toast.Shine.anchoredPosition = new Vector2(Mathf.Lerp(-60f, k_Width + 60f, sweep), 0f);
        }

        void Show(ItemDefinition item, RectTransform group, float now)
        {
            Color rarity = Catalog ? Catalog.ColorOf(item.Rarity) : Color.white;
            string rarityName = Catalog ? Catalog.RarityName(item.Rarity) : "";

            RectTransform rect = ItemUI.Rect("CartaNova", group, new Vector2(0.5f, 0f), new Vector2(0f, k_BaseY),
                new Vector2(k_Width, k_Height));
            rect.pivot = new Vector2(0.5f, 0.5f);
            var canvasGroup = rect.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            var mask = rect.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;

            Image glass = VisorUI.GlassPanel("Vidro", rect, new Vector2(0.5f, 0.5f), Vector2.zero, rect.sizeDelta);
            glass.color = Color.Lerp(VisorUI.Glass, new Color(rarity.r, rarity.g, rarity.b, 0.5f), 0.18f);
            ItemUI.Panel("Faixa", rect, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5f, k_Height), rarity);
            VisorUI.Line("Brilho", rect, Vector2.zero, Vector2.right, 2f, rarity);

            // Ícone
            Image frame = ItemUI.Panel("Icone", rect, new Vector2(0f, 0.5f), new Vector2(16f, 0f), Vector2.one * 58f,
                new Color(0f, 0.02f, 0.04f, 0.6f));
            Sprite sprite = ItemArt.IconFor(item);
            if (sprite)
            {
                Image icon = ItemUI.Fill("Sprite", frame.transform, 3f, Color.white);
                icon.sprite = sprite;
                icon.preserveAspect = true;
            }

            VisorUI.Label("Titulo", rect, new Vector2(0f, 1f), new Vector2(88f, -10f), new Vector2(240f, 18f), 14f,
                TextAlignmentOptions.TopLeft, rarity, true).text = "NOVA CARTA";
            VisorUI.Label("Nome", rect, new Vector2(0f, 0f), new Vector2(88f, 10f), new Vector2(260f, 40f), 28f,
                TextAlignmentOptions.BottomLeft, VisorUI.Text).text = item.DisplayName.ToUpperInvariant();
            VisorUI.Label("Raridade", rect, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(120f, 24f), 16f,
                TextAlignmentOptions.Right, rarity, true).text = rarityName.ToUpperInvariant();

            // Faixa de brilho inclinada que cruza o painel
            Image shine = ItemUI.Panel("Reflexo", rect, new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(36f, k_Height * 1.6f), new Color(1f, 1f, 1f, 0.16f));
            shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);

            m_Toasts.Add(new Toast
            {
                Rect = rect, Group = canvasGroup, Shine = shine.rectTransform, Start = now, Y = k_BaseY,
            });
        }
    }
}
