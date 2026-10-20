using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Barra de vida acima da cabeça dos inimigos que o jogador acertou (pelo evento central de dano), no estilo do
    // visor: vidro escuro, barra vermelha e o rastro amarelo-claro que desce suave depois do dano, como a vida do
    // jogador. Elites ganham o losango e a linha na cor do modificador; ícones mostram quando o inimigo está em chamas
    // (EnemyBurn) ou lento (TimeAnchorSlow). Some ShowTime segundos depois do último acerto (logo, ao morrer). Fica em
    // tela (Screen Space Overlay), com tamanho fixo em pixels que encolhe um pouco de longe; chefes não têm (já têm a
    // barra grande do HUD). As barras vêm de um pool fixo. Criado pelo PlayerStats.
    public class EnemyHealthBars : MonoBehaviour
    {
        [Tooltip("Segundos visível depois do último acerto")]
        public float ShowTime = 3f;

        [Tooltip("Segundos do sumiço")]
        public float FadeTime = 0.35f;

        [Tooltip("Largura da barra (pixels de referência, tela de 1080 de altura)")]
        public float Width = 96f;

        public float Height = 7f;

        [Tooltip("Altura acima do topo do inimigo (m)")]
        public float HeadOffset = 0.35f;

        [Tooltip("Altura a mais nos elites, acima do losango deles (m)")]
        public float EliteOffset = 0.85f;

        [Tooltip("Segundos que o rastro espera antes de descer")]
        public float TrailDelay = 0.4f;

        [Tooltip("Velocidade com que o rastro alcança a barra (maior = mais rápido)")]
        public float TrailSpeed = 4f;

        [Tooltip("Distância (m) a partir da qual a barra começa a encolher, e onde chega ao tamanho mínimo")]
        public Vector2 ShrinkDistance = new Vector2(12f, 50f);

        [Range(0.2f, 1f)] public float MinScale = 0.6f;

        public int PoolSize = 24;

        static readonly Color k_Health = new Color(1f, 0.3f, 0.32f, 0.95f);
        static readonly Color k_Trail = new Color(1f, 0.93f, 0.62f, 0.9f);
        static readonly Color k_Back = new Color(0.02f, 0.04f, 0.06f, 0.6f);
        static readonly Color k_Burn = new Color(1f, 0.55f, 0.15f, 1f);
        static readonly Color k_Slow = new Color(0.45f, 0.8f, 1f, 1f);
        const float k_IconSize = 11f;
        const float k_KillHold = 0.25f;

        class Bar
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Image Fill;
            public Image Trail;
            public Image Edge;
            public Image Elite;
            public Image Burn;
            public Image Slow;
            public Image[] Icons;

            public Health Health;
            public EnemyStats Stats;
            public float Top;
            public float LastHit;
            public float LastDamage;
            public float Shown;
            public float TrailShown;
            public bool Dying;
        }

        Canvas m_Canvas;
        RectTransform m_CanvasRect;
        readonly Stack<Bar> m_Free = new Stack<Bar>();
        readonly Dictionary<Health, Bar> m_Active = new Dictionary<Health, Bar>();
        readonly List<Health> m_Release = new List<Health>();
        Camera m_Camera;
        Sprite m_EliteIcon, m_BurnIcon, m_SlowIcon;

        void OnEnable() => CombatEvents.OnDamage += OnDamage;
        void OnDisable() => CombatEvents.OnDamage -= OnDamage;

        void Awake()
        {
            m_Canvas = ItemUI.CreateCanvas(transform, "BarrasInimigos", 40);
            m_CanvasRect = (RectTransform)m_Canvas.transform;
            m_EliteIcon = Icon("Losango", k_Diamond);
            m_BurnIcon = Icon("Fogo", k_Flame);
            m_SlowIcon = Icon("Lento", k_Hourglass);
            for (int i = 0; i < PoolSize; i++)
                m_Free.Push(Create());
        }

        void OnDamage(DamageInfo info)
        {
            if (!info.AttackerIsPlayer || info.VictimIsPlayer || !info.VictimHealth)
                return;
            Health health = info.VictimHealth;
            var stats = health.GetComponent<EnemyStats>();
            if (stats && stats.IsBoss)
                return;

            if (!m_Active.TryGetValue(health, out Bar bar))
            {
                bar = Take();
                if (bar == null)
                    return;
                Attach(bar, health, stats, info.Amount);
            }

            bar.LastHit = bar.LastDamage = Time.time;
            bar.Dying = info.Killed || health.CurrentHealth <= 0f;
        }

        // Uma barra livre; sem nenhuma, a que está mais perto de sumir
        Bar Take()
        {
            if (m_Free.Count > 0)
                return m_Free.Pop();
            Health oldest = null;
            float oldestTime = float.MaxValue;
            foreach (KeyValuePair<Health, Bar> pair in m_Active)
            {
                if (pair.Value.LastHit < oldestTime)
                {
                    oldestTime = pair.Value.LastHit;
                    oldest = pair.Key;
                }
            }

            if (oldest == null)
                return null;
            Bar bar = m_Active[oldest];
            m_Active.Remove(oldest);
            return bar;
        }

        void Attach(Bar bar, Health health, EnemyStats stats, float damage)
        {
            bar.Health = health;
            bar.Stats = stats;
            var collider = health.GetComponentInChildren<Collider>();
            bar.Top = collider ? collider.bounds.max.y - health.transform.position.y : 2f;

            // O rastro começa na vida de antes deste acerto
            float max = Mathf.Max(1f, health.MaxHealth);
            bar.Shown = Mathf.Clamp01(health.CurrentHealth / max);
            bar.TrailShown = Mathf.Clamp01((health.CurrentHealth + damage) / max);

            bool elite = stats && stats.Elite != EliteModifier.None;
            Color edge = elite ? EliteAffix.ColorOf(stats.Elite) : VisorUI.Edge;
            bar.Edge.color = edge;
            bar.Elite.gameObject.SetActive(elite);
            bar.Elite.color = edge;
            bar.Rect.gameObject.SetActive(true);
            m_Active[health] = bar;
        }

        void Release(Bar bar)
        {
            bar.Rect.gameObject.SetActive(false);
            bar.Health = null;
            bar.Stats = null;
            m_Free.Push(bar);
        }

        void LateUpdate()
        {
            if (m_Active.Count == 0)
                return;
            if (!m_Camera || !m_Camera.isActiveAndEnabled)
                m_Camera = Camera.main;

            float now = Time.time;
            m_Release.Clear();
            foreach (KeyValuePair<Health, Bar> pair in m_Active)
            {
                Bar bar = pair.Value;
                Health health = pair.Key;
                if (!health || !health.gameObject.activeInHierarchy)
                {
                    m_Release.Add(health);
                    continue;
                }

                // Morto: segura um instante na barra vazia e some rápido
                if (health.CurrentHealth <= 0f)
                    bar.Dying = true;
                float hold = bar.Dying ? k_KillHold : ShowTime;
                float since = now - bar.LastHit;
                if (since >= hold + FadeTime)
                {
                    m_Release.Add(health);
                    continue;
                }

                UpdateFill(bar, health);
                UpdateStatus(bar);
                bar.Group.alpha = 1f - Mathf.Clamp01((since - hold) / FadeTime);
                Place(bar, health);
            }

            // Inclui inimigos destruídos (a chave continua valendo no dicionário)
            foreach (Health health in m_Release)
            {
                if (m_Active.TryGetValue(health, out Bar bar))
                {
                    m_Active.Remove(health);
                    Release(bar);
                }
            }
        }

        void UpdateFill(Bar bar, Health health)
        {
            float dt = Time.deltaTime;
            float ratio = health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f;

            // Dano: a barra cai na hora; cura: sobe suave. O rastro espera e desce até ela
            bar.Shown = ratio < bar.Shown ? ratio : Mathf.MoveTowards(bar.Shown, ratio, dt * 1.5f);
            if (bar.TrailShown < bar.Shown)
                bar.TrailShown = bar.Shown;
            else if (Time.time - bar.LastDamage > TrailDelay)
                bar.TrailShown = Mathf.Lerp(bar.TrailShown, bar.Shown, 1f - Mathf.Exp(-dt * TrailSpeed));
            if (bar.TrailShown - bar.Shown < 0.002f)
                bar.TrailShown = bar.Shown;
            VisorUI.SetFill(bar.Fill, bar.Shown);
            VisorUI.SetFill(bar.Trail, bar.TrailShown);
        }

        // Ícones acima da barra, da esquerda para a direita: elite, em chamas, lento
        void UpdateStatus(Bar bar)
        {
            bool burning = EnemyBurn.Burning(bar.Health);
            var slow = bar.Stats ? bar.Stats.GetComponent<TimeAnchorSlow>() : null;
            bool slowed = slow && slow.IsSlowed;
            if (bar.Burn.gameObject.activeSelf != burning)
                bar.Burn.gameObject.SetActive(burning);
            if (bar.Slow.gameObject.activeSelf != slowed)
                bar.Slow.gameObject.SetActive(slowed);

            float x = 0f;
            foreach (Image icon in bar.Icons)
            {
                if (!icon.gameObject.activeSelf)
                    continue;
                icon.rectTransform.anchoredPosition = new Vector2(x, 4f);
                x += k_IconSize + 3f;
            }

            // Fogo tremula, lentidão pulsa devagar
            if (burning)
                bar.Burn.color = Color.Lerp(k_Burn, new Color(1f, 0.85f, 0.3f, 1f), Mathf.PingPong(Time.time * 6f, 1f));
            if (slowed)
                bar.Slow.color = new Color(k_Slow.r, k_Slow.g, k_Slow.b, 0.6f + 0.4f * Mathf.Sin(Time.time * 3f));
        }

        void Place(Bar bar, Health health)
        {
            if (!m_Camera)
                return;
            bool elite = bar.Stats && bar.Stats.Elite != EliteModifier.None;
            Vector3 world = health.transform.position + Vector3.up * (bar.Top + HeadOffset + (elite ? EliteOffset : 0f));
            Vector3 screen = m_Camera.WorldToScreenPoint(world);
            if (screen.z <= 0.1f)
            {
                bar.Group.alpha = 0f;
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_CanvasRect, screen, null, out Vector2 local);
            bar.Rect.anchoredPosition = local;
            float shrink = Mathf.InverseLerp(ShrinkDistance.x, ShrinkDistance.y, screen.z);
            bar.Rect.localScale = Vector3.one * Mathf.Lerp(1f, MinScale, shrink);
        }

        // ---------- montagem ----------

        Bar Create()
        {
            var bar = new Bar();
            bar.Rect = ItemUI.Rect("BarraInimigo", m_CanvasRect, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(Width, Height));
            bar.Group = bar.Rect.gameObject.AddComponent<CanvasGroup>();
            bar.Group.blocksRaycasts = bar.Group.interactable = false;

            ItemUI.Fill("Vidro", bar.Rect, -2f, k_Back);
            bar.Trail = ItemUI.Fill("Rastro", bar.Rect, 0f, k_Trail);
            bar.Fill = ItemUI.Fill("Vida", bar.Rect, 0f, k_Health);
            bar.Edge = VisorUI.Line("Base", bar.Rect, Vector2.zero, Vector2.right, 1.5f, VisorUI.Edge);
            bar.Edge.rectTransform.anchoredPosition = new Vector2(0f, -3.5f);

            bar.Elite = StatusIcon("Elite", bar.Rect, m_EliteIcon, Color.white);
            bar.Burn = StatusIcon("Chamas", bar.Rect, m_BurnIcon, k_Burn);
            bar.Slow = StatusIcon("Lento", bar.Rect, m_SlowIcon, k_Slow);
            bar.Icons = new[] { bar.Elite, bar.Burn, bar.Slow };
            bar.Rect.gameObject.SetActive(false);
            return bar;
        }

        static Image StatusIcon(string name, RectTransform parent, Sprite sprite, Color color)
        {
            Image icon = ItemUI.Panel(name, parent, new Vector2(0f, 1f), Vector2.zero, Vector2.one * k_IconSize, color);
            icon.rectTransform.pivot = Vector2.zero;
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.gameObject.SetActive(false);
            return icon;
        }

        // Ícones em pixel (7 x 7, '#' = pixel aceso; a cor vem da Image)
        static readonly string[] k_Diamond =
        {
            "...#...",
            "..###..",
            ".#####.",
            "#######",
            ".#####.",
            "..###..",
            "...#...",
        };

        static readonly string[] k_Flame =
        {
            "...#...",
            "..##...",
            "..###.#",
            ".#####.",
            "###.###",
            "##...##",
            ".#####.",
        };

        static readonly string[] k_Hourglass =
        {
            "#######",
            ".#####.",
            "..###..",
            "...#...",
            "..#.#..",
            ".#...#.",
            "#######",
        };

        static Sprite Icon(string name, string[] rows)
        {
            int size = rows.Length;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool on = rows[size - 1 - y][x] == '#';
                    pixels[y * size + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
