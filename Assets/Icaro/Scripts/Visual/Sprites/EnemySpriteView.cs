using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Inimigo desenhado como sprite animado (estilo Doom), no lugar do visual 3D: um quad que gira só no eixo vertical
    // para a câmera e mostra a célula da folha (EnemySpriteSet) do clipe e da direção atuais. A direção (frente,
    // direita, costas, esquerda) vem do ângulo entre a frente do inimigo e a câmera. O clipe vem da IA
    // (ISpriteActionSource: parado, andando, aviso e golpe, estes presos à duração da ação) ou, sem ela, do
    // NavMeshAgent; ao levar dano, pisca e toca o clipe "Hit" (se estiver parado ou andando); ao morrer, deixa um
    // cadáver (EnemySpriteCorpse) tocando "Death". A animação segue o multiplicador de tempo (andar pelo teto de
    // movimento da ficha). O aviso de ataque pulsa em vermelho. Fica no filho "Visual" do inimigo; colisor, hitbox e
    // ponto de mira continuam os do inimigo.
    public class EnemySpriteView : MonoBehaviour
    {
        public EnemySpriteSet Set;

        [Tooltip("Brilho mínimo (o sprite nunca some no escuro): soma a própria cor vezes este valor; as partes que " +
                 "brilham (mapa de brilho da folha) acendem na cor cheia")]
        [Range(0f, 1f)] public float MinBrightness = 0.25f;

        [Tooltip("Brilho do piscar ao levar dano")]
        public float FlashBrightness = 3f;
        public float FlashDuration = 0.08f;

        [Tooltip("Cor do aviso de ataque (pulsa por cima do sprite)")]
        [ColorUsage(false, true)] public Color TelegraphColor = new Color(2.2f, 0.15f, 0.1f);

        [Tooltip("Velocidade acima da qual o inimigo conta como andando (m/s), sem IA que diga")]
        public float MoveThreshold = 0.2f;

        static readonly int k_BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly Dictionary<EnemySpriteSet, Mesh> s_Meshes = new Dictionary<EnemySpriteSet, Mesh>();

        Transform m_Root;
        Transform m_Sprite;
        MeshRenderer m_Renderer;
        MaterialPropertyBlock m_Block;
        ISpriteActionSource m_Source;
        NavMeshAgent m_Agent;
        EnemyStats m_Stats;
        Health m_Health;
        EnemySpriteSet.Clip m_Clip;
        float m_ClipTime;
        float m_FlashTime;
        float m_HitTime;
        int m_Direction;
        int m_Frame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Meshes.Clear();

        void Awake()
        {
            m_Health = GetComponentInParent<Health>();
            m_Root = m_Health ? m_Health.transform : transform.root;
            m_Source = m_Root.GetComponent<ISpriteActionSource>();
            m_Agent = m_Root.GetComponent<NavMeshAgent>();
            m_Stats = m_Root.GetComponent<EnemyStats>();
            m_Block = new MaterialPropertyBlock();
            if (!Set)
                return;

            m_Sprite = new GameObject("Sprite").transform;
            m_Sprite.SetParent(transform, false);
            m_Sprite.gameObject.AddComponent<MeshFilter>().sharedMesh = MeshFor(Set);
            m_Renderer = m_Sprite.gameObject.AddComponent<MeshRenderer>();
            m_Renderer.sharedMaterial = Set.Material;
            m_Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            if (m_Health)
            {
                m_Health.OnDamaged += OnDamaged;
                m_Health.OnDie += OnDie;
            }
        }

        void OnEnable()
        {
            // Inimigos do pool voltam a nascer: sprite visível e sem piscar
            if (m_Renderer)
                m_Renderer.enabled = true;
            m_FlashTime = m_HitTime = 0f;
            m_Clip = null;
        }

        void OnDestroy()
        {
            if (m_Health)
            {
                m_Health.OnDamaged -= OnDamaged;
                m_Health.OnDie -= OnDie;
            }
        }

        // Quad do tamanho da célula com o pivô no chão; a frente olha para -Z, como o Quad do Unity
        public static Mesh MeshFor(EnemySpriteSet set)
        {
            if (s_Meshes.TryGetValue(set, out Mesh cached) && cached)
                return cached;

            float ppm = set.PixelsPerMeter;
            float x0 = -set.Pivot.x / ppm, x1 = (set.CellWidth - set.Pivot.x) / ppm;
            float y0 = -set.Pivot.y / ppm, y1 = (set.CellHeight - set.Pivot.y) / ppm;
            var mesh = new Mesh { name = set.name + "_Quad" };
            mesh.vertices = new[]
            {
                new Vector3(x0, y0, 0f), new Vector3(x1, y0, 0f), new Vector3(x0, y1, 0f), new Vector3(x1, y1, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.tangents = new[]
            {
                new Vector4(1f, 0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f),
                new Vector4(1f, 0f, 0f, -1f),
            };
            mesh.triangles = new[] { 0, 3, 1, 3, 0, 2 };
            mesh.RecalculateBounds();
            s_Meshes[set] = mesh;
            return mesh;
        }

        void OnDamaged(float damage, GameObject source)
        {
            if (!isActiveAndEnabled)
                return;
            m_FlashTime = FlashDuration;
            EnemySpriteSet.Clip hit = Set ? Set.Find("Hit") : null;
            if (hit != null)
                m_HitTime = hit.Duration / Mathf.Max(0.01f, CombatTiming.Multiplier);
        }

        void OnDie()
        {
            if (!m_Renderer || !m_Renderer.enabled)
                return;
            EnemySpriteCorpse.Spawn(Set, m_Sprite, m_Direction, MinBrightness);
            m_Renderer.enabled = false;
        }

        void LateUpdate()
        {
            if (!m_Renderer || !m_Renderer.enabled)
                return;
            Camera camera = Camera.main;
            if (!camera)
                return;

            // Gira só no eixo vertical, de frente para a câmera
            Vector3 eye = camera.transform.position;
            Vector3 away = m_Sprite.position - eye;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f)
                m_Sprite.rotation = Quaternion.LookRotation(away);

            m_Direction = DirectionTo(eye);
            float dt = Time.deltaTime;
            m_FlashTime -= dt;
            m_HitTime -= dt;

            // Clipe: aviso e golpe da IA; dano por cima de parado e andando; senão parado ou andando
            SpriteAction action = m_Source != null ? m_Source.SpriteAction : SpriteAction.Idle;
            float progress = m_Source != null ? m_Source.SpriteActionProgress : -1f;
            if (m_Source == null && m_Agent && m_Agent.enabled && m_Agent.velocity.sqrMagnitude > MoveThreshold * MoveThreshold)
                action = SpriteAction.Move;

            string name = action.ToString();
            bool warning = action == SpriteAction.Windup || action == SpriteAction.Windup2;
            bool busy = warning || action == SpriteAction.Attack || action == SpriteAction.Attack2;
            if (!busy && m_HitTime > 0f && Set.Find("Hit") != null)
            {
                name = "Hit";
                progress = -1f;
            }

            EnemySpriteSet.Clip clip = Set.Find(name) ?? Set.Find("Idle");
            if (clip == null)
                return;
            if (clip != m_Clip)
            {
                m_Clip = clip;
                m_ClipTime = 0f;
            }

            float speed = action == SpriteAction.Move && name == "Move"
                ? CombatTiming.Movement(m_Stats)
                : CombatTiming.Multiplier;
            m_ClipTime += dt * speed;
            m_Frame = FrameAt(clip, m_ClipTime, progress);

            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetVector(k_BaseMapST, Set.CellST(clip.Row + m_Direction, m_Frame));
            Color emission = Color.white * MinBrightness;
            if (warning)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 22f);
                emission = Color.Lerp(emission, TelegraphColor, 0.35f + 0.65f * pulse);
            }
            if (m_FlashTime > 0f)
                emission = Color.white * FlashBrightness;
            m_Block.SetColor(k_EmissionColor, emission / Mathf.Max(0.01f, Set.BodyGlow));
            m_Renderer.SetPropertyBlock(m_Block);
        }

        // 0 frente, 1 direita, 2 costas, 3 esquerda: de que lado do inimigo a câmera está
        int DirectionTo(Vector3 eye)
        {
            Vector3 forward = m_Root.forward;
            forward.y = 0f;
            Vector3 toEye = eye - m_Root.position;
            toEye.y = 0f;
            float angle = Vector3.SignedAngle(forward, toEye, Vector3.up);
            if (Mathf.Abs(angle) <= 45f)
                return 0;
            if (Mathf.Abs(angle) >= 135f)
                return 2;
            return angle > 0f ? 1 : 3;
        }

        public static int FrameAt(EnemySpriteSet.Clip clip, float time, float progress)
        {
            if (progress >= 0f)
                return Mathf.Clamp(Mathf.FloorToInt(progress * clip.Frames), 0, clip.Frames - 1);
            int frame = Mathf.FloorToInt(time * clip.Fps);
            return clip.Loop ? frame % clip.Frames : Mathf.Min(frame, clip.Frames - 1);
        }
    }
}
