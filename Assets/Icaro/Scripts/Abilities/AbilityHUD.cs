using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Os dois espaços de habilidade no canto inferior direito do visor, logo acima do bloco da arma: ícone em pixel art
    // na cor da habilidade, a tecla em caixa, a recarga escurecendo o espaço de cima para baixo com os segundos no meio,
    // um brilho pulsando enquanto ela age e um aviso curto em vermelho quando é recusada. Espaço vazio fica apagado.
    // Criado pelo PlayerAbilities.
    public class AbilityHUD : MonoBehaviour
    {
        const float k_Size = 74f;
        const float k_Gap = 10f;
        const float k_Bottom = 44f + 118f + 12f;    // em cima do WeaponAmmoHUD

        class Slot
        {
            public RectTransform Root;
            public Image Icon, Shade, Glow, Edge;
            public TextMeshProUGUI Key, Seconds;
            public AbilityType Shown = (AbilityType)(-1);
            public string ShownKey;
            public int ShownSeconds = -1;
            public float Flash;
        }

        readonly Slot[] m_Slots = new Slot[PlayerAbilities.SlotCount];
        RectTransform m_Root;
        TextMeshProUGUI m_Message;
        float m_MessageAge = 99f;
        PlayerAbilities m_Abilities;

        static Sprite s_Rewind, s_Slow;

        bool Build()
        {
            if (!HelmetVisor.TryGroup(VisorAnchor.BottomRight, out RectTransform group))
                return false;
            var anchor = new Vector2(1f, 0f);
            float width = PlayerAbilities.SlotCount * k_Size + (PlayerAbilities.SlotCount - 1) * k_Gap;
            m_Root = ItemUI.Rect("Habilidades", group, anchor, new Vector2(-48f, k_Bottom), new Vector2(width, k_Size + 30f));

            for (int i = 0; i < m_Slots.Length; i++)
            {
                // Espaço 1 à esquerda, espaço 2 à direita
                float x = -(m_Slots.Length - 1 - i) * (k_Size + k_Gap);
                var slot = new Slot();
                slot.Root = ItemUI.Rect("Espaco" + (i + 1), m_Root, anchor, new Vector2(x, 0f), new Vector2(k_Size, k_Size));
                slot.Glow = ItemUI.Panel("Brilho", slot.Root, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(k_Size + 10f, k_Size + 10f), Color.clear);
                slot.Glow.sprite = ItemArt.Glow;
                VisorUI.GlassPanel("Vidro", slot.Root, new Vector2(0.5f, 0.5f), Vector2.zero, slot.Root.sizeDelta);
                slot.Icon = ItemUI.Panel("Icone", slot.Root, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f),
                    new Vector2(48f, 48f), Color.white);
                slot.Icon.preserveAspect = true;

                // Recarga: faixa escura que desce conforme fica pronta
                slot.Shade = ItemUI.Panel("Recarga", slot.Root, Vector2.zero, Vector2.zero, Vector2.zero,
                    new Color(0f, 0.01f, 0.03f, 0.72f));
                RectTransform shade = slot.Shade.rectTransform;
                shade.anchorMin = Vector2.zero;
                shade.anchorMax = new Vector2(1f, 0f);
                shade.pivot = Vector2.zero;
                shade.sizeDelta = Vector2.zero;
                slot.Seconds = VisorUI.Label("Segundos", slot.Root, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f),
                    new Vector2(k_Size, 40f), 30f, TextAlignmentOptions.Center, VisorUI.Text, true);
                slot.Edge = VisorUI.Line("Pronta", slot.Root, Vector2.zero, new Vector2(1f, 0f), 3f, VisorUI.Accent);
                slot.Key = VisorUI.Label("Tecla", slot.Root, new Vector2(0.5f, 0f), new Vector2(0f, 6f),
                    new Vector2(k_Size, 18f), 14f, TextAlignmentOptions.Bottom, VisorUI.Dim, true);
                m_Slots[i] = slot;
            }

            m_Message = VisorUI.Label("Aviso", m_Root, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(300f, 24f),
                16f, TextAlignmentOptions.BottomRight, VisorUI.Danger, true);
            m_Message.gameObject.SetActive(false);
            return true;
        }

        void OnDestroy() => Bind(null);

        void Bind(PlayerAbilities abilities)
        {
            if (m_Abilities)
            {
                m_Abilities.OnUsed -= Used;
                m_Abilities.OnDenied -= Denied;
            }
            m_Abilities = abilities;
            if (m_Abilities)
            {
                m_Abilities.OnUsed += Used;
                m_Abilities.OnDenied += Denied;
            }
        }

        void Used(int slot)
        {
            if (slot >= 0 && slot < m_Slots.Length && m_Slots[slot] != null)
                m_Slots[slot].Flash = 1f;
        }

        void Denied(int slot, string reason)
        {
            if (!m_Message)
                return;
            m_Message.text = reason.ToUpperInvariant();
            m_MessageAge = 0f;
            if (slot >= 0 && slot < m_Slots.Length && m_Slots[slot] != null)
                m_Slots[slot].Flash = -1f;
        }

        void Update()
        {
            if (!m_Root && !Build())
                return;
            if (m_Abilities != PlayerAbilities.Instance)
                Bind(PlayerAbilities.Instance);

            bool any = false;
            for (int i = 0; i < m_Slots.Length; i++)
                any |= m_Abilities && m_Abilities.SlotAt(i) != AbilityType.None;
            if (m_Root.gameObject.activeSelf != any)
                m_Root.gameObject.SetActive(any);
            if (!any)
                return;

            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < m_Slots.Length; i++)
                UpdateSlot(i, m_Slots[i], dt);

            // Aviso de recusa: aparece e some em 1,4 s
            m_MessageAge += dt;
            bool message = m_MessageAge < 1.4f;
            if (m_Message.gameObject.activeSelf != message)
                m_Message.gameObject.SetActive(message);
            if (message)
            {
                Color c = VisorUI.Danger;
                c.a = Mathf.Clamp01((1.4f - m_MessageAge) / 0.4f);
                m_Message.color = c;
            }
        }

        void UpdateSlot(int index, Slot slot, float dt)
        {
            AbilityType type = m_Abilities.SlotAt(index);
            Color color = type == AbilityType.None ? VisorUI.Dim : AbilitySettings.ColorOf(type);
            if (type != slot.Shown)
            {
                slot.Shown = type;
                slot.Icon.sprite = IconFor(type);
                slot.Icon.enabled = type != AbilityType.None;
                slot.ShownSeconds = -1;
            }

            string key = GameSettings.KeyName(PlayerAbilities.KeyFor(index));
            if (key != slot.ShownKey)
            {
                slot.ShownKey = key;
                slot.Key.text = $"[{key}]";
            }

            float cooldown = m_Abilities.CooldownFraction(index);
            bool inUse = m_Abilities.InUse(index);
            bool ready = type != AbilityType.None && cooldown <= 0f && !inUse;

            RectTransform shade = slot.Shade.rectTransform;
            float top = type == AbilityType.None ? 1f : cooldown;
            if (Mathf.Abs(shade.anchorMax.y - top) > 0.002f)
                shade.anchorMax = new Vector2(1f, top);

            int seconds = cooldown > 0f && !inUse ? Mathf.CeilToInt(m_Abilities.Remaining(index)) : 0;
            if (seconds != slot.ShownSeconds)
            {
                slot.ShownSeconds = seconds;
                slot.Seconds.text = seconds > 0 ? seconds.ToString() : "";
            }

            // Ícone: cheio pronto, apagado recarregando; pulsa enquanto age
            float pulse = inUse ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f) : 0f;
            Color icon = color;
            icon.a = ready || inUse ? 1f : 0.45f;
            slot.Icon.color = icon;
            VisorUI.SetScale(slot.Icon.rectTransform, Vector3.one * (1f + 0.12f * pulse + 0.2f * Mathf.Max(0f, slot.Flash)));

            Color glow = color;
            glow.a = inUse ? 0.35f + 0.35f * pulse : Mathf.Max(0f, slot.Flash) * 0.8f;
            if (slot.Flash < 0f)
                glow = new Color(VisorUI.Danger.r, VisorUI.Danger.g, VisorUI.Danger.b, -slot.Flash * 0.7f);
            slot.Glow.color = glow;
            slot.Flash = Mathf.MoveTowards(slot.Flash, 0f, dt * 2.5f);

            Color edge = color;
            edge.a = ready ? 0.95f : 0.15f;
            slot.Edge.color = edge;
        }

        // ---------- ícones (16 x 16, brancos; a cor vem do Image) ----------

        public static Sprite IconFor(AbilityType type) => type switch
        {
            AbilityType.TimeRewind => s_Rewind ? s_Rewind : s_Rewind = ToSprite(PaintRewind(), "Habilidade_Voltar"),
            AbilityType.TimeSlow => s_Slow ? s_Slow : s_Slow = ToSprite(PaintSlow(), "Habilidade_Lenta"),
            _ => null,
        };

        static Sprite ToSprite(PixelCanvas canvas, string name)
        {
            Texture2D texture = ItemArt.ToTexture(canvas, name);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 16f);
        }

        static readonly Px s_White = new Px(255, 255, 255);
        static readonly Px s_Soft = new Px(255, 255, 255, 120);

        // Relógio com o ponteiro voltando e a seta girando para trás em volta dele
        static PixelCanvas PaintRewind()
        {
            var c = new PixelCanvas(16, 16);
            // Aro do relógio (aberto no alto à direita, onde a seta entra)
            for (int a = 0; a < 360; a += 4)
            {
                if (a > 300 || a < 10)
                    continue;
                float r = a * Mathf.Deg2Rad;
                c.Set(Mathf.RoundToInt(8f + Mathf.Cos(r) * 6.5f), Mathf.RoundToInt(8f - Mathf.Sin(r) * 6.5f), s_White);
            }

            // Ponta da seta (girando no sentido anti-horário, para trás)
            c.Set(14, 4, s_White);
            c.Set(13, 3, s_White);
            c.Set(12, 2, s_White);
            c.Set(13, 5, s_White);
            c.Set(14, 6, s_White);
            c.Set(12, 4, s_White);

            // Ponteiros
            c.Line(8, 8, 8, 4, s_White);
            c.Line(8, 8, 5, 10, s_White);
            c.Set(8, 8, s_White);
            c.Set(3, 8, s_Soft);
            c.Set(8, 13, s_Soft);
            return c;
        }

        // Ampulheta com a areia parada no meio
        static PixelCanvas PaintSlow()
        {
            var c = new PixelCanvas(16, 16);
            c.Rect(3, 1, 10, 2, s_White);
            c.Rect(3, 13, 10, 2, s_White);
            for (int y = 3; y <= 12; y++)
            {
                int d = y <= 7 ? y - 3 : 12 - y;    // estreita no meio
                int left = 4 + d, right = 11 - d;
                c.Set(left, y, s_White);
                c.Set(right, y, s_White);
                // Areia: um pouco em cima, a pilha embaixo e o fio parado no meio
                if (y >= 4 && y <= 5)
                    for (int x = left + 1; x < right; x++)
                        c.Set(x, y, s_Soft);
                if (y >= 10)
                    for (int x = left + 1; x < right; x++)
                        c.Set(x, y, s_White);
            }
            c.Set(7, 7, s_White);
            c.Set(8, 8, s_White);
            c.Set(7, 9, s_Soft);
            return c;
        }
    }
}
