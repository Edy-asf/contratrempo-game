using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Orbe do tempo: sai de um inimigo marcado (TimeMark) ou do chefe ao morrer e sobe sem parar, derivando para os
    // lados como uma partícula no ar. Um anel em volta dele encolhe até encostar no orbe, algumas vezes (Cycles);
    // acertar um tiro no orbe reduz o multiplicador de Kairós pela precisão do momento do impacto em relação ao toque
    // do anel: Perfeito, Bom ou fora do tempo (reduz pouco). Só conta depois que o primeiro anel aparece (antes, o tiro
    // atravessa). Qualquer acerto consome o orbe; sem acerto, some depois do último anel. O timing é em tempo de jogo,
    // sem o multiplicador. Visual: núcleo âmbar, mostrador de relógio em pixel art com um ponteiro que dá uma volta por
    // anel e aponta para o 12 no toque, halo pulsando e faíscas que ficam para trás; ao acertar, onda e estouro na cor
    // da nota. Só os tiros do jogador (HomingProjectile) o acertam: fica na camada Ignore Raycast, como gatilho, fora da
    // física e dos tiros dos inimigos, e a mira assistida não desvia o tiro para um inimigo quando a mira está nele
    // (UnderCrosshair).
    [RequireComponent(typeof(SphereCollider))]
    public class TimeOrb : MonoBehaviour
    {
        public Transform Core;
        public LineRenderer Ring;
        public Light Glow;
        public AudioClip HitSfx;

        [ColorUsage(false, true)] public Color RingColor = new Color(0.3f, 1.6f, 2f);
        [ColorUsage(false, true)] public Color GoodColor = new Color(2.4f, 1.4f, 0.3f);
        [ColorUsage(false, true)] public Color PerfectColor = new Color(3f, 3f, 3f);
        [ColorUsage(false, true)] public Color OffColor = new Color(0.6f, 0.8f, 1.2f);

        static readonly List<TimeOrb> s_Active = new List<TimeOrb>();
        static bool s_WarnedFallback;
        static Sprite s_Face, s_Hand;
        static Texture2D s_GlowTexture;

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        const float k_RiseTime = 0.5f;
        const int k_RingPoints = 40;

        TimeOrbSettings m_Settings;
        Vector3 m_Start;
        float m_Age;
        float m_DoneTime = -1f;
        float m_Seed;
        bool m_Hit;
        bool m_Fading;
        Color m_HitColor;

        Transform m_Face, m_Hand, m_Halo;
        Renderer m_HaloRenderer;
        MaterialPropertyBlock m_Block;
        ParticleSystem m_Motes, m_Burst;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Active.Clear();
            s_WarnedFallback = false;
        }

        public static TimeOrb Spawn(TimeOrbSettings settings, Vector3 position)
        {
            if (settings == null)
                return null;
            TimeOrb orb = settings.Prefab
                ? Instantiate(settings.Prefab, position, Quaternion.identity).GetComponent<TimeOrb>()
                : BuildFallback(position);
            if (orb)
                orb.Begin(settings, position);
            return orb;
        }

        // A linha do centro da tela passa pelo orbe? (a mira assistida então atira reto nele)
        public static bool UnderCrosshair(Vector3 origin, Vector3 direction, float range)
        {
            foreach (TimeOrb orb in s_Active)
            {
                if (!orb || orb.m_Hit || orb.m_Settings == null)
                    continue;
                Vector3 offset = orb.transform.position - origin;
                float along = Vector3.Dot(offset, direction);
                if (along < 0f || along > range)
                    continue;
                if ((offset - direction * along).sqrMagnitude <= Sq(orb.m_Settings.HitRadius * 1.5f))
                    return true;
            }

            return false;
        }

        static float Sq(float v) => v * v;

        void OnEnable() => s_Active.Add(this);
        void OnDisable() => s_Active.Remove(this);

        void OnDestroy()
        {
            if (m_HaloRenderer && m_HaloRenderer.sharedMaterial)
                Destroy(m_HaloRenderer.sharedMaterial);
        }

        void Begin(TimeOrbSettings settings, Vector3 position)
        {
            m_Settings = settings;
            m_Start = position;
            m_Seed = Random.value * 100f;
            m_Block = new MaterialPropertyBlock();
            gameObject.layer = 2; // Ignore Raycast
            var trigger = GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = settings.HitRadius;
            if (Core)
                Core.localScale = Vector3.one * settings.OrbRadius * 2f;
            if (Ring)
            {
                Ring.positionCount = k_RingPoints;
                Ring.loop = true;
                Ring.useWorldSpace = true;
            }

            BuildEffects();
        }

        // ---------- efeitos (montados em jogo: só precisam do material aditivo do anel) ----------

        void BuildEffects()
        {
            float size = m_Settings.OrbRadius * 2f;
            Material additive = Ring ? Ring.sharedMaterial : null;

            // Halo: quadrado com brilho redondo, aditivo, sempre de frente para a câmera
            if (additive)
            {
                var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(halo.GetComponent<Collider>());
                halo.name = "Halo";
                halo.layer = 2;
                m_Halo = halo.transform;
                m_Halo.SetParent(transform, false);
                m_Halo.localScale = Vector3.one * size * 3.2f;
                m_HaloRenderer = halo.GetComponent<Renderer>();
                var material = new Material(additive);
                material.SetTexture("_BaseMap", GlowTexture());
                m_HaloRenderer.sharedMaterial = material;
                m_HaloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Mostrador e ponteiro do relógio (pixel art), por cima do núcleo
            m_Face = new GameObject("Mostrador").transform;
            m_Face.SetParent(transform, false);
            m_Face.localScale = Vector3.one * size * 1.9f;
            var face = m_Face.gameObject.AddComponent<SpriteRenderer>();
            face.sprite = FaceSprite();
            face.sortingOrder = 1;
            m_Hand = new GameObject("Ponteiro").transform;
            m_Hand.SetParent(m_Face, false);
            var hand = m_Hand.gameObject.AddComponent<SpriteRenderer>();
            hand.sprite = HandSprite();
            hand.sortingOrder = 2;

            // Faíscas soltas que ficam para trás enquanto ele sobe, e o estouro do acerto
            if (additive)
            {
                m_Motes = Particles("Faiscas", additive, burst: false);
                m_Burst = Particles("Estouro", additive, burst: true);
            }
        }

        ParticleSystem Particles(string name, Material material, bool burst)
        {
            var go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = !burst;
            main.playOnAwake = false;
            main.startLifetime = burst ? new ParticleSystem.MinMaxCurve(0.35f, 0.7f) : new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = burst ? new ParticleSystem.MinMaxCurve(4f, 9f) : new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            main.startSize = burst ? new ParticleSystem.MinMaxCurve(0.06f, 0.14f) : new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f), new Color(0.4f, 0.95f, 1f));
            main.maxParticles = burst ? 60 : 40;
            main.gravityModifier = burst ? 0.4f : -0.05f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = burst ? 0f : 16f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = m_Settings.OrbRadius * (burst ? 0.6f : 1.2f);

            ParticleSystem.SizeOverLifetimeModule sizes = system.sizeOverLifetime;
            sizes.enabled = true;
            sizes.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = !burst;
            noise.strength = 0.4f;
            noise.frequency = 1.2f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!burst)
                system.Play();
            return system;
        }

        // ---------- timing ----------

        // Segundos do ciclo de anéis: começa quando o orbe termina o primeiro impulso de subida
        float RingTime => m_Age - k_RiseTime;
        float CycleLength => m_Settings.RingDuration + m_Settings.CycleGap;

        // Distância em segundos até o toque do anel mais próximo (antes ou depois)
        float FromTouch()
        {
            float cycle = CycleLength;
            int k = Mathf.Clamp(Mathf.FloorToInt(RingTime / cycle), 0, m_Settings.Cycles - 1);
            float best = float.MaxValue;
            for (int i = k - 1; i <= k + 1; i++)
            {
                if (i < 0 || i >= m_Settings.Cycles)
                    continue;
                float touch = i * cycle + m_Settings.RingDuration;
                best = Mathf.Min(best, Mathf.Abs(RingTime - touch));
            }

            return best;
        }

        // Um tiro do jogador acertou: reduz pela precisão e o orbe estoura. Falso (o tiro atravessa) se já foi usado
        // ou se o primeiro anel ainda não apareceu.
        public bool Hit()
        {
            if (m_Hit || m_Settings == null || RingTime < 0f)
                return false;
            m_Hit = true;
            m_DoneTime = m_Age;

            float off = FromTouch();
            float reduction;
            string title;
            BannerTone tone;
            if (off <= m_Settings.PerfectWindow)
            {
                reduction = m_Settings.PerfectReduction;
                title = "Perfeito";
                tone = BannerTone.Success;
                m_HitColor = PerfectColor;
            }
            else if (off <= m_Settings.GoodWindow)
            {
                reduction = m_Settings.GoodReduction;
                title = "Bom";
                tone = BannerTone.Normal;
                m_HitColor = GoodColor;
            }
            else
            {
                reduction = m_Settings.OffReduction;
                title = "Fora do tempo";
                tone = BannerTone.Warning;
                m_HitColor = OffColor;
            }

            if (TimeManager.Instance)
                TimeManager.Instance.Reduce(reduction);
            VisorBanner.Show(title, $"Kairós -{reduction:0.0#}x".Replace('.', ','), tone);
            if (HitSfx)
                AudioUtility.CreateSFX(HitSfx, transform.position, AudioUtility.AudioGroups.Pickup, 0f);

            if (m_Burst)
            {
                ParticleSystem.MainModule main = m_Burst.main;
                Color color = m_HitColor.maxColorComponent > 1f ? m_HitColor / m_HitColor.maxColorComponent : m_HitColor;
                main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
                m_Burst.Emit(off <= m_Settings.PerfectWindow ? 60 : 35);
            }
            if (m_Motes)
                m_Motes.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            return true;
        }

        // ---------- quadro a quadro ----------

        void Update()
        {
            if (m_Settings == null)
                return;
            m_Age += Time.deltaTime;
            Camera camera = Camera.main;

            if (!m_Hit)
                Drift();
            Billboard(camera);

            if (m_Hit)
            {
                Explode(camera);
                return;
            }

            float time = RingTime;
            int cycleIndex = time < 0f ? -1 : Mathf.FloorToInt(time / CycleLength);
            if (cycleIndex >= m_Settings.Cycles)
            {
                Fade(time);
                return;
            }

            UpdateRing(camera, time, cycleIndex);
            UpdateClock(time, cycleIndex);
        }

        // Sobe sem parar (um impulso no começo e depois devagar) e deriva para os lados com ruído suave
        void Drift()
        {
            float rise = Mathf.Clamp01(m_Age / k_RiseTime);
            rise = rise * rise * (3f - 2f * rise);
            float t = m_Age * m_Settings.WanderSpeed;
            float settle = Mathf.Clamp01(m_Age / 1.5f);
            var wander = new Vector3(Mathf.PerlinNoise(m_Seed, t) - 0.5f, 0f, Mathf.PerlinNoise(t, m_Seed) - 0.5f) *
                         (2f * m_Settings.Wander * settle);
            wander.y = Mathf.Sin(m_Age * 2.4f) * 0.08f;
            transform.position = m_Start + wander +
                                 Vector3.up * (m_Settings.RiseHeight * rise + m_Settings.RiseSpeed * m_Age);
        }

        void Billboard(Camera camera)
        {
            if (!camera)
                return;
            Quaternion facing = camera.transform.rotation;
            if (m_Face)
                m_Face.rotation = facing;
            if (m_Halo)
                m_Halo.rotation = facing;
        }

        void UpdateRing(Camera camera, float time, int cycleIndex)
        {
            if (!Ring)
                return;
            if (cycleIndex < 0 || !camera)
            {
                Ring.enabled = false;
                return;
            }

            float within = time - cycleIndex * CycleLength;
            float phase = Mathf.Clamp01(within / m_Settings.RingDuration);
            float radius = Mathf.Lerp(m_Settings.RingStartRadius, m_Settings.OrbRadius, phase);
            float off = FromTouch();
            Color color = off <= m_Settings.PerfectWindow ? PerfectColor
                : off <= m_Settings.GoodWindow ? GoodColor
                : RingColor * Mathf.Lerp(0.5f, 1f, phase);
            Ring.enabled = true;
            Ring.startColor = Ring.endColor = color;
            Ring.widthMultiplier = Mathf.Lerp(0.05f, 0.1f, phase);
            DrawCircle(camera, radius);

            if (Glow)
                Glow.intensity = Mathf.Lerp(1.5f, 4f, phase);
        }

        void DrawCircle(Camera camera, float radius)
        {
            Vector3 center = transform.position, right = camera.transform.right, up = camera.transform.up;
            for (int i = 0; i < k_RingPoints; i++)
            {
                float a = i * Mathf.PI * 2f / k_RingPoints;
                Ring.SetPosition(i, center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
            }
        }

        // Ponteiro: uma volta por anel, no 12 no toque; halo e núcleo pulsam mais forte perto do toque
        void UpdateClock(float time, int cycleIndex)
        {
            float near = 0f;
            if (cycleIndex >= 0)
            {
                float within = time - cycleIndex * CycleLength;
                float phase = Mathf.Clamp01(within / m_Settings.RingDuration);
                if (m_Hand)
                    m_Hand.localRotation = Quaternion.Euler(0f, 0f, -360f * phase);
                near = Mathf.Clamp01(1f - FromTouch() / Mathf.Max(0.01f, m_Settings.GoodWindow * 2f));
            }
            else if (m_Hand)
            {
                m_Hand.localRotation = Quaternion.Euler(0f, 0f, -720f * m_Age);
            }

            float pulse = 1f + Mathf.Sin(m_Age * 9f) * 0.06f + near * 0.25f;
            if (Core)
                Core.localScale = Vector3.one * m_Settings.OrbRadius * 2f * pulse;
            if (m_Halo)
            {
                m_Halo.localScale = Vector3.one * m_Settings.OrbRadius * 2f * 3.2f * (pulse + near * 0.4f);
                SetHalo(Color.Lerp(new Color(1f, 0.65f, 0.25f, 0.55f), new Color(1f, 1f, 1f, 0.9f), near));
            }
        }

        void SetHalo(Color color)
        {
            if (!m_HaloRenderer)
                return;
            m_HaloRenderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_BaseColor, color);
            m_HaloRenderer.SetPropertyBlock(m_Block);
        }

        // Acertado: onda que se abre na cor da nota, núcleo que cresce e some
        void Explode(Camera camera)
        {
            float t = (m_Age - m_DoneTime) / 0.35f;
            float k = Mathf.Clamp01(t);
            if (Core)
                Core.localScale = Vector3.one * m_Settings.OrbRadius * 2f * Mathf.Max(0f, 1f + k * 1.5f - k * k * 2.5f);
            if (m_Face)
                m_Face.localScale = Vector3.one * m_Settings.OrbRadius * 2f * 1.9f * (1f + k * 2f);
            if (m_Face && m_Face.TryGetComponent(out SpriteRenderer face))
                face.color = new Color(1f, 1f, 1f, 1f - k);
            if (m_Hand && m_Hand.TryGetComponent(out SpriteRenderer hand))
                hand.color = new Color(1f, 1f, 1f, 1f - k);
            if (Ring && camera)
            {
                Ring.enabled = k < 1f;
                Color color = m_HitColor * (1f - k);
                Ring.startColor = Ring.endColor = color;
                Ring.widthMultiplier = Mathf.Lerp(0.18f, 0.02f, k);
                DrawCircle(camera, Mathf.Lerp(m_Settings.OrbRadius, m_Settings.RingStartRadius * 1.3f, k));
            }
            if (m_Halo)
            {
                m_Halo.localScale = Vector3.one * m_Settings.OrbRadius * 2f * 3.2f * (1f + k * 2.5f);
                Color halo = m_HitColor.maxColorComponent > 1f ? m_HitColor / m_HitColor.maxColorComponent : m_HitColor;
                halo.a = 0.9f * (1f - k);
                SetHalo(halo);
            }
            if (Glow)
                Glow.intensity = Mathf.Lerp(8f, 0f, k);
            if (t >= 2.5f)
                Destroy(gameObject);
        }

        // Último anel passou sem acerto: encolhe e some
        void Fade(float time)
        {
            float t = (time - m_Settings.Cycles * CycleLength) / 0.4f;
            float k = Mathf.Max(0f, 1f - t);
            if (Core)
                Core.localScale = Vector3.one * m_Settings.OrbRadius * 2f * k;
            if (m_Face)
                m_Face.localScale = Vector3.one * m_Settings.OrbRadius * 2f * 1.9f * k;
            if (m_Halo)
                m_Halo.localScale = Vector3.one * m_Settings.OrbRadius * 2f * 3.2f * k;
            if (Ring)
                Ring.enabled = false;
            if (m_Motes)
                m_Motes.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            if (t >= 1f && !m_Fading)
            {
                // Espera as faíscas que já estão no ar sumirem
                m_Fading = true;
                Destroy(gameObject, 1.2f);
            }
        }

        // ---------- arte ----------

        // Mostrador de 32 x 32 (o tamanho vem da escala): aro âmbar, doze marcas (o 12 branco) e centro vazado
        static Sprite FaceSprite()
        {
            if (s_Face)
                return s_Face;
            var canvas = new PixelCanvas(32, 32);
            canvas.Fill(PixelCanvas.Ring(16f, 16f, 15.5f, 12.5f), PixelRamps.Amber);
            canvas.Fill(PixelCanvas.Ring(16f, 16f, 12.5f, 11.5f), PixelRamps.Gold, -0.3f);
            for (int i = 0; i < 12; i++)
            {
                float a = (i * 30f - 90f) * Mathf.Deg2Rad;
                Px color = i == 0 ? PixelRamps.White : i % 3 == 0 ? PixelRamps.Amber[4] : PixelRamps.Amber[2];
                int x = Mathf.FloorToInt(16f + Mathf.Cos(a) * 10f), y = Mathf.FloorToInt(16f + Mathf.Sin(a) * 10f);
                canvas.Set(x, y, color);
                if (i % 3 == 0)
                    canvas.Set(x + (i == 3 ? -1 : i == 9 ? 1 : 0), y + (i == 0 ? 1 : i == 6 ? -1 : 0), color);
            }
            // Coroa do relógio em cima
            canvas.Rect(14, 0, 4, 2, PixelRamps.Amber[3]);
            canvas.Outline(PixelRamps.Outline);
            s_Face = Sprite.Create(ItemArt.ToTexture(canvas, "Orbe_Mostrador"), new Rect(0f, 0f, 32f, 32f),
                new Vector2(0.5f, 0.5f), 32f);
            return s_Face;
        }

        // Ponteiro do centro para o 12, com a ponta acesa
        static Sprite HandSprite()
        {
            if (s_Hand)
                return s_Hand;
            var canvas = new PixelCanvas(32, 32);
            canvas.Rect(15, 6, 2, 10, PixelRamps.White);
            canvas.Rect(15, 5, 2, 2, PixelRamps.Cyan[4]);
            canvas.Rect(14, 15, 4, 3, PixelRamps.Amber[3]);
            canvas.Outline(PixelRamps.Outline);
            s_Hand = Sprite.Create(ItemArt.ToTexture(canvas, "Orbe_Ponteiro"), new Rect(0f, 0f, 32f, 32f),
                new Vector2(0.5f, 0.5f), 32f);
            return s_Hand;
        }

        // Brilho redondo e suave (branco; a cor vem do material)
        static Texture2D GlowTexture()
        {
            if (s_GlowTexture)
                return s_GlowTexture;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Orbe_Halo", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            s_GlowTexture = texture;
            return texture;
        }

        // ---------- sem prefab ----------

        // Sem o prefab (Ícaro > Gerar prefabs ainda não rodou): o mesmo orbe montado em jogo, com materiais simples
        static TimeOrb BuildFallback(Vector3 position)
        {
            if (!s_WarnedFallback)
            {
                s_WarnedFallback = true;
                Debug.LogWarning("[Ícaro] CombatBalance.TimeOrbs.Prefab vazio: o orbe do tempo foi montado em jogo. " +
                                 "Rode Ícaro > Gerar prefabs para criar o Orbe_Tempo.");
            }

            var root = new GameObject("Orbe_Tempo");
            root.transform.position = position;
            root.AddComponent<SphereCollider>();

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(core.GetComponent<Collider>());
            core.name = "Nucleo";
            core.transform.SetParent(root.transform, false);
            var renderer = core.GetComponent<Renderer>();
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit)
            {
                var material = new Material(lit);
                material.SetColor("_BaseColor", new Color(1f, 0.75f, 0.3f));
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", new Color(4f, 2.2f, 0.5f));
                renderer.sharedMaterial = material;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var line = new GameObject("Anel").AddComponent<LineRenderer>();
            line.transform.SetParent(root.transform, false);
            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            if (unlit)
                line.sharedMaterial = new Material(unlit);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;

            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.35f);
            light.range = 6f;
            light.shadows = LightShadows.None;

            var orb = root.AddComponent<TimeOrb>();
            orb.Core = core.transform;
            orb.Ring = line;
            orb.Glow = light;
            return orb;
        }
    }
}
