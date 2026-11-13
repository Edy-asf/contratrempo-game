using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Icaro
{
    // NPC com quem o jogador conversa. Visual: sprite em pixel art (NpcArt, pela chave 'Art') num quad iluminado pela
    // cena, com pivô no chão, que gira só no eixo vertical para a câmera e respira de leve. Com o jogador perto
    // (DialogueSettings.InteractRadius) e fora da luta, o HUD mostra "[G] Conversar" e a tecla de interagir
    // (GameSettings.ShopKey) abre a conversa (DialogueUI), que prende a câmera olhando para ele (LookPoint). Depois da
    // primeira vez, toca Dialogue.Repeat, se houver. Não é afetado pelo tempo de Kairós.
    public class DialogueNpc : MonoBehaviour
    {
        public DialogueDefinition Dialogue;

        [Tooltip("Desenho do NPC (chave do NpcArt)")]
        public string Art = "Peregrino";

        [Tooltip("Altura do rosto, para onde a câmera olha na conversa (metros acima do pé)")]
        public float LookHeight = 1.6f;

        [Tooltip("Cria um colisor de cápsula do tamanho do desenho (o jogador não atravessa)")]
        public bool Solid = true;

        // Quantas vezes já conversou nesta cena
        public int Talks { get; set; }

        public Vector3 LookPoint => transform.position + Vector3.up * LookHeight;

        // Nome mostrado (o da conversa)
        public string DisplayName => Dialogue ? Dialogue.Speaker : name;

        Transform m_Visual;
        float m_Phase;

        // NPCs ativos (para a guarda baixa do jogador)
        public static readonly List<DialogueNpc> All = new List<DialogueNpc>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        void OnEnable() => All.Add(this);

        static readonly Dictionary<string, Material> s_Materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, Vector2> s_Sizes = new Dictionary<string, Vector2>();

        void Start()
        {
            BuildVisual();
            m_Phase = Random.value * 10f;
            DialogueUI.Ensure();
        }

        void OnDisable()
        {
            All.Remove(this);
            if (DialogueUI.Instance)
                DialogueUI.Instance.SetPrompt(this, false);
        }

        void Update()
        {
            DialogueUI ui = DialogueUI.Instance;
            if (!ui)
                return;

            float radius = DialogueSettings.Current.InteractRadius;
            bool near = BossTarget.Exists && BossTarget.FlatDistance(transform.position) <= radius &&
                        Mathf.Abs(BossTarget.Feet.y - transform.position.y) < 2.5f && !TimeManager.CurrentlyRunning;
            bool free = !ui.IsOpen && !(ShopUI.Instance && ShopUI.Instance.IsOpen) &&
                        !(ItemDraft.Instance && ItemDraft.Instance.IsOpen) && Cursor.lockState == CursorLockMode.Locked;
            ui.SetPrompt(this, near && free);

            Keyboard keyboard = Keyboard.current;
            if (near && free && keyboard != null && keyboard[GameSettings.ShopKey].wasPressedThisFrame && Dialogue)
            {
                bool heard = DialogueMemory.HasHeard(Dialogue);
                DialogueDefinition dialogue = heard && Dialogue.Repeat ? Dialogue.Repeat : Dialogue;
                Talks++;
                if (PlayerTranslator.Owned || !Dialogue.IgnoreWithoutTranslator)
                    DialogueMemory.Mark(Dialogue);
                ui.Open(this, dialogue);
            }
        }

        void LateUpdate()
        {
            if (!m_Visual)
                return;

            // Gira só no eixo vertical para a câmera e respira (tempo sem escala: o NPC não sente o tempo de Kairós)
            Camera cam = Camera.main;
            if (cam)
            {
                Vector3 to = cam.transform.position - m_Visual.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f)
                    m_Visual.rotation = Quaternion.LookRotation(-to.normalized, Vector3.up);
            }

            float breath = Mathf.Sin(Time.unscaledTime * 1.6f + m_Phase);
            m_Visual.localScale = new Vector3(1f - 0.01f * breath, 1f + 0.015f * breath, 1f);
        }

        void BuildVisual()
        {
            if (m_Visual)
                return;
            Material material = MaterialFor(Art, out Vector2 size);
            var go = new GameObject("Visual");
            go.transform.SetParent(transform, false);
            m_Visual = go.transform;
            var quad = new GameObject("Sprite");
            quad.transform.SetParent(m_Visual, false);
            quad.AddComponent<MeshFilter>().sharedMesh = QuadMesh(size);
            var renderer = quad.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;

            if (Solid && !GetComponent<Collider>())
            {
                var capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.radius = Mathf.Min(0.45f, size.x * 0.3f);
                capsule.height = size.y;
                capsule.center = new Vector3(0f, size.y * 0.5f, 0f);
            }
        }

        // Quad com pivô no meio da base
        static Mesh QuadMesh(Vector2 size)
        {
            float w = size.x * 0.5f;
            var mesh = new Mesh { name = "NPC_Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-w, 0f, 0f), new Vector3(w, 0f, 0f), new Vector3(-w, size.y, 0f), new Vector3(w, size.y, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        // Material URP Lit recortado, com o mapa de brilho na emissão (as partes acesas na cor cheia e o resto do corpo
        // a 18%, um brilho mínimo para não sumir no escuro). Um por desenho.
        static Material MaterialFor(string key, out Vector2 size)
        {
            if (s_Materials.TryGetValue(key, out Material cached) && cached)
            {
                size = s_Sizes[key];
                return cached;
            }

            (PixelCanvas color, PixelCanvas glow) = NpcArt.Paint(key);
            size = new Vector2(color.Width, color.Height) / NpcArt.PixelsPerMeter;
            Texture2D baseMap = ItemArt.ToTexture(color, "NPC_" + key);
            for (int i = 0; i < color.Pixels.Length; i++)
            {
                // O resto do corpo brilha fraco (mínimo no escuro)
                if (glow.Pixels[i].A == 0 && color.Pixels[i].A > 0)
                {
                    Px p = color.Pixels[i];
                    glow.Pixels[i] = new Px((byte)(p.R * 0.18f), (byte)(p.G * 0.18f), (byte)(p.B * 0.18f));
                }
            }
            Texture2D emission = ItemArt.ToTexture(glow, "NPC_" + key + "_Brilho");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = "NPC_" + key };
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.SetTexture("_EmissionMap", emission);
            material.SetColor("_EmissionColor", Color.white * 1.4f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.renderQueue = (int)RenderQueue.AlphaTest;
            s_Materials[key] = material;
            s_Sizes[key] = size;
            return material;
        }
    }
}
