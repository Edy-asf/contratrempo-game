using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace Icaro
{
    // Arma em sprite 2D no canto inferior direito (estilo Doom, com a mão robótica), no lugar do modelo 3D: para cada
    // arma do Ícaro com desenho no WeaponSpritePainter (chave = nome da ficha sem "Arma_"), o modelo 3D e o clarão
    // 3D do Microgame são escondidos e o sprite toca as animações: parada (com balanço ao andar e atraso ao virar a
    // câmera), tiro (2 quadros de coice e clarão a cada disparo; a Liberdade alterna os canos e a Titan esquenta as
    // aletas atirando sem parar), recarga (8 quadros pelo progresso do IcaroWeapon) e troca de arma (sobe da borda).
    // O sprite é uma malha em grade presa à câmera de armas do Microgame (camada das armas, sem atravessar paredes).
    // Tamanho, posição e giro vêm da ficha da arma (WeaponDefinition.Sprite*; ajuste em jogo no WeaponSpriteTuner,
    // F10) e valem na tela no campo de visão de referência; o tamanho não muda com o campo de visão, mas cada vértice
    // passa por uma lente: com mais campo de visão a parte da arma perto do canto estica (como a borda de uma
    // grande-angular), com menos ela encolhe um pouco (FovStretch). O canto da tela fica sempre no lugar. O Ion akimbos tem um segundo quad espelhado no canto esquerdo, e cada lado anima no
    // próprio tiro. Os quadros são desenhados numa thread separada (o desenho não usa o Unity) e viram textura aos
    // poucos na principal; até o quadro parado ficar pronto, o modelo 3D continua aparecendo. Criado pelo PlayerStats.
    // Luz do ambiente (o sprite continua sem iluminação de verdade, para a pixel art não escurecer nem lavar): a cor é
    // tingida pela luz ambiente do momento (o BiomeZone troca com transição: roxo no Bosque, verde-escuro no
    // Arquipélago), com um piso de brilho; as luzes pontuais por perto somam a cor delas (braseiros, cristais, a
    // lanterna da cápsula) e cada disparo dá um clarão rápido (menos nas armas silenciadas). Valores no asset
    // Resources/WeaponLightSettings (menu "Ícaro > Ajustes da luz da arma").
    public class WeaponSpriteHUD : MonoBehaviour
    {
        [Tooltip("Campo de visão (graus) em que o sprite aparece exatamente como na ficha, sem distorção")]
        [Range(30f, 120f)] public float ReferenceFov = 70f;

        [Tooltip("Quanto a lente estica o sprite com campo de visão acima da referência (e encolhe abaixo): 0 = nunca " +
                 "muda, 1 = como uma lente de verdade")]
        [Range(0f, 1.5f)] public float FovStretch = 1f;

        [Tooltip("Distância da malha à câmera de armas (m); não muda o tamanho na tela")]
        public float SpriteDistance = 0.3f;

        [Tooltip("Sprite iluminado pelas luzes da cena (URP/Lit) em vez de cores puras (URP/Unlit). Vale ao criar")]
        public bool LitByScene;

        [Tooltip("Distância da câmera (m) em que o tiro nasce, na direção da ponta do cano desenhada")]
        public float MuzzleDistance = 0.6f;

        [Tooltip("Segundos de cada quadro de tiro")]
        public float FireFrameTime = 0.045f;

        [Tooltip("Segundos para a arma subir da borda ao trocar")]
        public float RaiseTime = 0.25f;

        [Tooltip("Balanço ao andar (pixels do desenho em código, 160 de altura)")]
        public float BobAmount = 4f;

        [Tooltip("Quanto o sprite atrasa ao virar a câmera (pixels do desenho em código por grau)")]
        public float SwayPerDegree = 0.6f;

        const float k_HotHeat = 0.55f;
        const int k_GridX = 24;
        const int k_GridY = 16;

        public static WeaponSpriteHUD Instance { get; private set; }

        // Para o WeaponSpriteTuner: a ficha da arma na mão e a ponta do cano usada (tela, em pixels)
        public WeaponDefinition ActiveDefinition { get; private set; }
        public bool ActiveImported { get; private set; }
        public bool Showing { get; private set; }
        public Vector2 MuzzleScreen { get; private set; }

        class Frames
        {
            // Arte importada (Resources/Armas/<chave>/): sem quadros de tiro ou recarga, o coice e a recarga são
            // movimentos da imagem parada
            public bool Imported;
            public Texture2D Idle, HotIdle;
            public Texture2D[] Fire = new Texture2D[2];
            public readonly Texture2D[] FireAlt = new Texture2D[2];
            public readonly Texture2D[] HotFire = new Texture2D[2];
            public Texture2D[] Reload = new Texture2D[WeaponSpritePainter.ReloadFrames];
        }

        class Side
        {
            public Transform Quad;
            public Mesh Mesh;
            public Vector3[] Vertices;
            public MeshRenderer Renderer;
            public Material Material;
            public bool Left;
            public float FireTime = -1f;
            public bool Alt;
        }

        struct Painted
        {
            public PixelCanvas Canvas;
            public string Name;
            public Action<Texture2D> Assign;
        }

        readonly Dictionary<string, Frames> m_Frames = new Dictionary<string, Frames>();
        readonly ConcurrentQueue<Painted> m_Done = new ConcurrentQueue<Painted>();
        readonly HashSet<WeaponController> m_Prepared = new HashSet<WeaponController>();
        readonly HashSet<WeaponController> m_Hidden = new HashSet<WeaponController>();

        PlayerWeaponsManager m_Weapons;
        PlayerCharacterController m_Controller;
        Health m_Health;
        Side m_Right;
        Side m_Left;
        WeaponController m_Active;
        float m_RaiseStart = -10f;
        float m_Heat;
        int m_Shots;
        float m_BobPhase;
        Vector2 m_Sway;
        float m_LastYaw;
        float m_LastPitch;
        float m_FlashTime = -10f;
        Color m_Glow = Color.black;
        readonly List<Light> m_Lights = new List<Light>();
        float m_LightsRefresh;

        // Sprite da direita como um quad num espaço de câmera virtual com o campo de visão de referência (canto
        // inferior direito no pivô, x de -1 a 0 e y de 0 a 1), e as tangentes da metade do campo de visão
        Matrix4x4 m_Layout = Matrix4x4.identity;
        float m_TanRef = 1f;
        float m_TanCurrent = 1f;
        float m_Distance = 0.3f;
        float m_EdgeRadius = 1f;

        Camera WeaponCamera => m_Weapons && m_Weapons.WeaponCamera ? m_Weapons.WeaponCamera
            : m_Controller ? m_Controller.PlayerCamera : Camera.main;

        Camera PlayerCamera => m_Controller && m_Controller.PlayerCamera ? m_Controller.PlayerCamera : Camera.main;

        void Awake()
        {
            Instance = this;
            if (Application.isEditor || Debug.isDebugBuild)
                gameObject.AddComponent<WeaponSpriteTuner>();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            foreach (Side side in new[] { m_Right, m_Left })
            {
                if (side == null)
                    continue;
                if (side.Quad)
                    Destroy(side.Quad.gameObject);
                Destroy(side.Material);
                Destroy(side.Mesh);
            }
        }

        // Os quads ficam presos à câmera de armas, na camada das armas do Microgame
        bool EnsureQuads()
        {
            if (m_Right != null)
                return true;
            Camera camera = WeaponCamera;
            if (!camera)
                return false;
            int layer = m_Weapons && m_Weapons.FpsWeaponLayer.value != 0
                ? Mathf.RoundToInt(Mathf.Log(m_Weapons.FpsWeaponLayer.value, 2))
                : camera.gameObject.layer;
            m_Right = NewSide("ArmaSprite_Direita", false, camera.transform, layer);
            m_Left = NewSide("ArmaSprite_Esquerda", true, camera.transform, layer);
            return true;
        }

        Side NewSide(string name, bool left, Transform parent, int layer)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)) { layer = layer };
            go.transform.SetParent(parent, false);
            Mesh mesh = GridMesh(name);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LitByScene ? LightProbeUsage.BlendProbes : LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var material = NewMaterial(LitByScene);
            renderer.sharedMaterial = material;
            renderer.enabled = false;
            return new Side
            {
                Quad = go.transform, Mesh = mesh, Vertices = new Vector3[mesh.vertexCount], Renderer = renderer,
                Material = material, Left = left,
            };
        }

        // Grade de vértices (a lente deforma cada um); as posições são refeitas a cada frame, já no espaço da câmera
        static Mesh GridMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            int count = (k_GridX + 1) * (k_GridY + 1);
            var uv = new Vector2[count];
            for (int j = 0; j <= k_GridY; j++)
                for (int i = 0; i <= k_GridX; i++)
                    uv[j * (k_GridX + 1) + i] = new Vector2((float)i / k_GridX, (float)j / k_GridY);
            var triangles = new int[k_GridX * k_GridY * 6];
            int t = 0;
            for (int j = 0; j < k_GridY; j++)
            {
                for (int i = 0; i < k_GridX; i++)
                {
                    int a = j * (k_GridX + 1) + i, b = a + 1, c = a + k_GridX + 1, d = c + 1;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = d;
                    triangles[t++] = a; triangles[t++] = d; triangles[t++] = b;
                }
            }

            mesh.vertices = new Vector3[count];
            mesh.uv = uv;
            mesh.normals = Enumerable.Repeat(Vector3.back, count).ToArray();
            mesh.triangles = triangles;
            return mesh;
        }

        // Material transparente (a fumaça e os brilhos do desenho têm meia transparência), sem sombras nem brilho
        static Material NewMaterial(bool lit)
        {
            Shader shader = Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit");
            if (!shader)
                shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "ArmaSprite" };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (lit)
            {
                material.SetFloat("_Smoothness", 0f);
                material.SetFloat("_SpecularHighlights", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.SetFloat("_EnvironmentReflections", 0f);
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                material.SetFloat("_ReceiveShadows", 0f);
                material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            }

            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        void Update()
        {
            if (!m_Weapons && PlayerStats.Instance)
            {
                m_Weapons = PlayerStats.Instance.GetComponent<PlayerWeaponsManager>();
                m_Controller = PlayerStats.Instance.GetComponent<PlayerCharacterController>();
                m_Health = PlayerStats.Instance.GetComponent<Health>();
            }

            // Quadros já desenhados na outra thread viram textura (alguns por frame)
            for (int i = 0; i < 4 && m_Done.TryDequeue(out Painted done); i++)
                done.Assign(ItemArt.ToTexture(done.Canvas, done.Name));

            m_Heat = Mathf.Max(0f, m_Heat - Time.deltaTime * 0.45f);

            WeaponController weapon = m_Weapons ? m_Weapons.GetActiveWeapon() : null;
            string key = KeyOf(weapon);
            if (weapon != m_Active)
            {
                m_Active = weapon;
                m_RaiseStart = Time.time;
                m_Heat = 0f;
            }

            if (key != null)
                Prepare(weapon, key);
            bool show = key != null && m_Frames[key].Idle && (!m_Health || m_Health.CurrentHealth > 0f) &&
                        EnsureQuads();
            if (show)
                Hide3D(weapon);
            var icaro = show ? weapon.GetComponent<IcaroWeapon>() : null;
            bool akimbo = icaro && icaro.Definition.Akimbo;
            Showing = show;
            ActiveDefinition = show ? icaro.Definition : null;
            if (m_Right != null)
            {
                m_Right.Renderer.enabled = show;
                m_Left.Renderer.enabled = show && akimbo;
            }

            if (!show)
                return;

            Frames frames = m_Frames[key];
            ActiveImported = frames.Imported;
            WeaponDefinition definition = icaro.Definition;
            Camera camera = WeaponCamera;

            // Câmera virtual a 1 m com o campo de visão de referência: a ficha mede tamanho e deslocamento em fração
            // da altura da tela ("unit") e o quad é montado nela; depois cada ponto passa pela lente (Lens)
            m_Distance = Mathf.Max(camera.nearClipPlane + 0.01f, SpriteDistance);
            m_TanRef = Mathf.Tan(ReferenceFov * 0.5f * Mathf.Deg2Rad);
            m_TanCurrent = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            m_EdgeRadius = Mathf.Sqrt(camera.aspect * camera.aspect + 1f);
            float unit = 2f * m_TanRef;
            float height = definition.SpriteHeight * unit;
            float width = height * frames.Idle.width / Mathf.Max(1f, frames.Idle.height);

            var corner = new Vector2(m_TanRef * camera.aspect, -m_TanRef);
            Vector2 anchor = corner + definition.SpriteOffset * unit;
            Vector2 motion = Motion() * height;
            Color tint = AmbientColor(camera.transform.position);
            foreach (Side side in akimbo ? new[] { m_Right, m_Left } : new[] { m_Right })
            {
                side.Material.mainTexture = FrameFor(frames, icaro, side);
                side.Material.SetColor("_BaseColor", tint);
                Vector2 position = anchor + motion + ImportedMotion(frames, icaro, side) * height;
                // O balanço nunca puxa o canto para dentro da tela além de onde a ficha o pôs
                position.x = Mathf.Max(position.x, Mathf.Min(anchor.x, corner.x));
                position.y = Mathf.Min(position.y, Mathf.Max(anchor.y, corner.y));
                Matrix4x4 layout = Matrix4x4.TRS(new Vector3(position.x, position.y, 1f),
                    Quaternion.Euler(0f, definition.SpriteTilt, 0f), new Vector3(width, height, 1f));
                if (side == m_Right)
                    m_Layout = layout;
                side.Quad.localPosition = Vector3.zero;
                side.Quad.localRotation = Quaternion.identity;
                side.Quad.localScale = Vector3.one;
                for (int j = 0; j <= k_GridY; j++)
                {
                    for (int i = 0; i <= k_GridX; i++)
                    {
                        var local = new Vector2((float)i / k_GridX - 1f, (float)j / k_GridY);
                        side.Vertices[j * (k_GridX + 1) + i] = ToCamera(layout, local, side.Left);
                    }
                }

                side.Mesh.vertices = side.Vertices;
                side.Mesh.RecalculateBounds();
            }

            Vector2 uv = MuzzleUv(key, frames, definition);
            MuzzleScreen = AlignMuzzle(uv, m_Right, icaro.FirstMuzzle);
            if (akimbo)
                AlignMuzzle(uv, m_Left, icaro.SecondMuzzle);
        }

        // Ponto do quad (x de -1 a 0, y de 0 a 1) para o espaço da câmera de armas, passando pela lente; o lado
        // esquerdo é o espelho do direito
        Vector3 ToCamera(Matrix4x4 layout, Vector2 local, bool left)
        {
            Vector3 p = layout.MultiplyPoint3x4(local);
            Vector2 q = Lens(new Vector2(p.x, p.y) / (Mathf.Max(0.01f, p.z) * m_TanRef));
            float x = q.x * m_TanCurrent * m_Distance;
            return new Vector3(left ? -x : x, q.y * m_TanCurrent * m_Distance, m_Distance);
        }

        // Posição na tela (em metades da altura, a partir do centro) com o campo de visão de referência -> posição com
        // o campo de visão atual. Cada ponto mantém a fração do ângulo até o canto da tela que tinha na referência e
        // volta para a tela pela projeção da câmera: o canto fica no lugar, e com mais campo de visão a região perto
        // dele estica como a borda de uma grande-angular
        Vector2 Lens(Vector2 q)
        {
            float r = q.magnitude;
            return r < 1e-5f ? q : q * (LensRadius(r) / r);
        }

        float LensRadius(float r)
        {
            float edgeRef = Mathf.Atan(m_EdgeRadius * m_TanRef);
            float edgeCurrent = Mathf.Atan(m_EdgeRadius * m_TanCurrent);
            float angle = Mathf.Atan(r * m_TanRef) * edgeCurrent / edgeRef;
            float lens = Mathf.Tan(Mathf.Min(angle, 89f * Mathf.Deg2Rad)) / m_TanCurrent;
            return Mathf.LerpUnclamped(r, lens, FovStretch);
        }

        // Inverso da lente (busca binária; LensRadius cresce com r)
        float UnlensRadius(float r)
        {
            float low = 0f, high = 50f;
            for (int i = 0; i < 40; i++)
            {
                float mid = (low + high) * 0.5f;
                if (LensRadius(mid) < r)
                    low = mid;
                else
                    high = mid;
            }

            return (low + high) * 0.5f;
        }

        // Ponta do cano na imagem, em fração a partir do canto superior esquerdo
        public Vector2 ActiveMuzzleUv { get; private set; }

        Vector2 MuzzleUv(string key, Frames frames, WeaponDefinition definition)
        {
            Vector2 uv = definition.SpriteMuzzle;
            if (!frames.Imported && !definition.OverrideSpriteMuzzle &&
                WeaponSpritePainter.MuzzlePoint(key, WeaponSpritePainter.IdlePose(), out float mx, out float my))
                uv = new Vector2(mx / WeaponSpritePainter.Width, my / WeaponSpritePainter.Height);
            ActiveMuzzleUv = uv;
            return uv;
        }

        // Põe o cano 3D (invisível) no ponto do mundo atrás da ponta do cano desenhada, apontado para o que está no
        // centro da tela: o tiro sai de onde a arma aparece. Devolve a ponta na tela (pixels)
        Vector2 AlignMuzzle(Vector2 uv, Side side, Transform muzzle)
        {
            Vector3 local = ToCamera(m_Layout, new Vector2(uv.x - 1f, 1f - uv.y), side.Left);
            Vector3 screen = WeaponCamera.WorldToScreenPoint(side.Quad.TransformPoint(local));
            Camera camera = PlayerCamera;
            if (!muzzle || !camera)
                return screen;

            Ray ray = camera.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            Vector3 origin = ray.GetPoint(MuzzleDistance);
            Ray center = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 target = Physics.Raycast(center, out RaycastHit hit, 300f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore)
                ? hit.point
                : center.GetPoint(300f);
            muzzle.SetPositionAndRotation(origin, Quaternion.LookRotation(target - origin));
            return screen;
        }

        // Para o ajuste: quanto o deslocamento da ficha muda por pixel de tela arrastado (a lente não entra na conta)
        public float OffsetPerPixel => 1f / Mathf.Max(1f, Screen.height);

        // Para o ajuste: o ponto da tela (pixels) cai em que lugar da imagem da direita? (fração a partir do canto
        // superior esquerdo; falso fora dela)
        public bool ScreenToUv(Vector2 screen, out Vector2 uv)
        {
            uv = default;
            if (m_Right == null || !m_Right.Renderer.enabled)
                return false;
            float half = Mathf.Max(1f, Screen.height * 0.5f);
            var q = new Vector2((screen.x - Screen.width * 0.5f) / half, (screen.y - half) / half);
            float r = q.magnitude;
            if (r > 1e-5f)
                q *= UnlensRadius(r) / r;

            // Raio da câmera virtual até o plano do quad
            Matrix4x4 inverse = m_Layout.inverse;
            Vector3 origin = inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 direction = inverse.MultiplyVector(new Vector3(q.x * m_TanRef, q.y * m_TanRef, 1f));
            if (Mathf.Abs(direction.z) < 1e-6f)
                return false;
            float t = -origin.z / direction.z;
            if (t <= 0f)
                return false;
            Vector3 local = origin + direction * t;
            uv = new Vector2(local.x + 1f, 1f - local.y);
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        // Arte importada sem quadros próprios: coice (recua para o canto) e recarga (abaixa e volta), em fração da
        // altura do sprite
        Vector2 ImportedMotion(Frames frames, IcaroWeapon icaro, Side side)
        {
            if (!frames.Imported)
                return Vector2.zero;
            if (icaro && icaro.IsReloading && frames.Reload.Length == 0)
                return new Vector2(0f, -Mathf.Sin(icaro.ReloadProgress * Mathf.PI) * 0.35f);
            // Coice de cada lado no próprio tiro (recua para o canto)
            float sinceShot = Time.time - side.FireTime;
            float kickTime = FireFrameTime * 2f;
            if (frames.Fire.Length > 0 || side.FireTime < 0f || sinceShot >= kickTime)
                return Vector2.zero;
            float kick = (1f - sinceShot / kickTime) * 0.02f;
            return new Vector2(kick, -kick * 0.8f);
        }

        static string KeyOf(WeaponController weapon)
        {
            var icaro = weapon ? weapon.GetComponent<IcaroWeapon>() : null;
            if (!icaro || !icaro.Definition)
                return null;
            string name = icaro.Definition.name;
            string key = name.StartsWith("Arma_") ? name.Substring(5) : name;
            return Array.IndexOf(WeaponSpritePainter.Keys, key) >= 0 ? key : null;
        }

        // Esconde o modelo e o clarão 3D do Microgame (o sprite já está pronto)
        void Hide3D(WeaponController weapon)
        {
            if (!m_Hidden.Add(weapon))
                return;
            if (weapon.WeaponRoot)
            {
                foreach (Renderer renderer in weapon.WeaponRoot.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = false;
            }

            weapon.MuzzleFlashPrefab = null;
        }

        // Primeira vez que a arma aparece: escuta os tiros e manda desenhar os quadros
        void Prepare(WeaponController weapon, string key)
        {
            if (!m_Prepared.Add(weapon))
                return;
            var icaro = weapon.GetComponent<IcaroWeapon>();
            weapon.OnShootProcessed += () => OnShot(weapon, icaro);

            if (m_Frames.ContainsKey(key))
                return;
            var frames = new Frames();
            m_Frames[key] = frames;

            // Arte pronta na pasta da arma tem prioridade sobre o desenho em código
            var imported = Resources.Load<Texture2D>($"Armas/{key}/parada");
            if (imported)
            {
                frames.Imported = true;
                frames.Idle = imported;
                frames.Fire = LoadSequence(key, "tiro");
                frames.Reload = LoadSequence(key, "recarga");
                foreach (Texture2D texture in new[] { frames.Idle }.Concat(frames.Fire).Concat(frames.Reload))
                    texture.filterMode = FilterMode.Point;
                return;
            }

            Queue(key, WeaponSpritePainter.IdlePose(), "parada", t => frames.Idle = t);
            for (int i = 0; i < 2; i++)
            {
                int frame = i;
                Queue(key, WeaponSpritePainter.FirePose(frame), "tiro", t => frames.Fire[frame] = t);
            }

            for (int i = 0; i < WeaponSpritePainter.ReloadFrames; i++)
            {
                int frame = i;
                WeaponPose pose = WeaponSpritePainter.ReloadPose(key, (i + 0.5f) / WeaponSpritePainter.ReloadFrames);
                Queue(key, pose, "recarga", t => frames.Reload[frame] = t);
            }

            if (key == "Liberdade")
            {
                for (int i = 0; i < 2; i++)
                {
                    int frame = i;
                    Queue(key, WeaponSpritePainter.FirePose(frame, true), "tiro2", t => frames.FireAlt[frame] = t);
                }
            }

            if (key == "Titan")
            {
                Queue(key, new WeaponPose { Heat = 1f }, "quente", t => frames.HotIdle = t);
                for (int i = 0; i < 2; i++)
                {
                    int frame = i;
                    Queue(key, WeaponSpritePainter.FirePose(frame, false, 1f), "quente", t => frames.HotFire[frame] = t);
                }
            }
        }

        // Quadros importados em sequência: <nome>_1, <nome>_2... até faltar um
        static Texture2D[] LoadSequence(string key, string name)
        {
            var frames = new List<Texture2D>();
            for (int i = 1; i <= 64; i++)
            {
                var frame = Resources.Load<Texture2D>($"Armas/{key}/{name}_{i}");
                if (!frame)
                    break;
                frames.Add(frame);
            }

            return frames.ToArray();
        }

        // Desenha numa thread separada; a textura é criada no Update
        void Queue(string key, WeaponPose pose, string label, Action<Texture2D> assign) =>
            Task.Run(() => m_Done.Enqueue(new Painted
            {
                Canvas = WeaponSpritePainter.Paint(key, pose), Name = $"Arma_{key}_{label}", Assign = assign,
            }));

        void OnShot(WeaponController weapon, IcaroWeapon icaro)
        {
            if (weapon != m_Active || m_Right == null)
                return;
            bool left = icaro && icaro.Definition && icaro.Definition.Akimbo && icaro.LastShotSecond;
            Side side = left ? m_Left : m_Right;
            side.FireTime = Time.time;
            side.Alt = m_Shots++ % 2 == 1;
            if (!(icaro && icaro.Definition && icaro.Definition.Suppressed))
                m_FlashTime = Time.time;
            m_Heat = Mathf.Min(1f, m_Heat + 0.08f);
        }

        // Cor da arma: tom da luz ambiente (só a matiz, com o piso de brilho), mais a cor das luzes pontuais por perto
        // (suavizada, sem saltos ao passar por elas) e o clarão do disparo
        Color AmbientColor(Vector3 eye)
        {
            WeaponLightSettings settings = WeaponLightSettings.Current;
            Color ambient = RenderSettings.ambientMode == AmbientMode.Trilight
                ? (RenderSettings.ambientSkyColor + RenderSettings.ambientEquatorColor) * 0.5f
                : RenderSettings.ambientLight;
            float peak = Mathf.Max(ambient.maxColorComponent, 0.001f);
            var hue = new Color(ambient.r / peak, ambient.g / peak, ambient.b / peak);
            float bright = Mathf.Clamp(ambient.grayscale * 2.5f, settings.MinBrightness, 1f);
            Color color = Color.Lerp(Color.white, hue, settings.AmbientTint) * Mathf.Lerp(1f, bright, settings.AmbientTint);

            Color glow = Color.black;
            if (settings.NearbyLights > 0f)
            {
                if (Time.time >= m_LightsRefresh)
                {
                    m_LightsRefresh = Time.time + 1.5f;
                    m_Lights.Clear();
                    foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    {
                        if (light.type == LightType.Point || light.type == LightType.Spot)
                            m_Lights.Add(light);
                    }
                }

                foreach (Light light in m_Lights)
                {
                    if (!light || !light.isActiveAndEnabled || light.intensity <= 0f)
                        continue;
                    float distance = Vector3.Distance(eye, light.transform.position);
                    if (distance >= light.range)
                        continue;
                    float falloff = 1f - distance / light.range;
                    glow += light.color * (falloff * falloff * Mathf.Clamp01(light.intensity));
                }
            }

            m_Glow = Color.Lerp(m_Glow, glow * settings.NearbyLights, Time.deltaTime * settings.NearbySmoothing);
            color += m_Glow;
            float flash = settings.ShotFlash *
                          Mathf.Clamp01(1f - (Time.time - m_FlashTime) / Mathf.Max(0.01f, settings.ShotFlashTime));
            color = Color.Lerp(color, settings.ShotFlashColor * 1.3f, flash);
            color.r = Mathf.Min(color.r, 1.6f);
            color.g = Mathf.Min(color.g, 1.6f);
            color.b = Mathf.Min(color.b, 1.6f);
            color.a = 1f;
            return color;
        }

        Texture2D FrameFor(Frames frames, IcaroWeapon icaro, Side side)
        {
            int reloadCount = frames.Reload.Length;
            if (icaro && icaro.IsReloading && reloadCount > 0)
            {
                int index = Mathf.Clamp(Mathf.FloorToInt(icaro.ReloadProgress * reloadCount), 0, reloadCount - 1);
                return frames.Reload[index] ? frames.Reload[index] : frames.Idle;
            }

            bool hot = m_Heat > k_HotHeat;
            float sinceShot = Time.time - side.FireTime;
            int fireCount = frames.Fire.Length;
            if (fireCount > 0 && side.FireTime >= 0f && sinceShot < FireFrameTime * fireCount)
            {
                int frame = Mathf.Min(fireCount - 1, Mathf.FloorToInt(sinceShot / FireFrameTime));
                Texture2D fire = frame < 2 && hot && frames.HotFire[frame] ? frames.HotFire[frame]
                    : frame < 2 && side.Alt && frames.FireAlt[frame] ? frames.FireAlt[frame]
                    : frames.Fire[frame];
                if (fire)
                    return fire;
            }

            return hot && frames.HotIdle ? frames.HotIdle : frames.Idle;
        }

        // Balanço ao andar, atraso ao virar a câmera e subida ao trocar de arma, em fração da altura do sprite
        Vector2 Motion()
        {
            float dt = Time.deltaTime;
            float speed = 0f;
            if (m_Controller && m_Controller.IsGrounded)
            {
                Vector3 velocity = m_Controller.CharacterVelocity;
                velocity.y = 0f;
                speed = Mathf.Clamp(velocity.magnitude / Mathf.Max(1f, m_Controller.MaxSpeedOnGround), 0f, 1.5f);
            }

            m_BobPhase += dt * 9f * speed;
            float bobX = Mathf.Sin(m_BobPhase) * BobAmount * speed;
            float bobY = -Mathf.Abs(Mathf.Cos(m_BobPhase)) * BobAmount * 0.75f * speed;

            if (m_Controller && dt > 0f)
            {
                float yaw = m_Controller.transform.eulerAngles.y;
                float pitch = m_Controller.PlayerCamera ? m_Controller.PlayerCamera.transform.localEulerAngles.x : 0f;
                float dyaw = Mathf.DeltaAngle(m_LastYaw, yaw), dpitch = Mathf.DeltaAngle(m_LastPitch, pitch);
                m_LastYaw = yaw;
                m_LastPitch = pitch;
                var target = new Vector2(Mathf.Clamp(-dyaw * SwayPerDegree, -10f, 10f),
                    Mathf.Clamp(dpitch * SwayPerDegree, -8f, 8f));
                m_Sway = Vector2.Lerp(m_Sway, target, 1f - Mathf.Exp(-dt * 10f));
            }

            var motion = new Vector2(bobX + m_Sway.x, bobY + m_Sway.y) / WeaponSpritePainter.Height;

            float raise = Mathf.Clamp01((Time.time - m_RaiseStart) / Mathf.Max(0.01f, RaiseTime));
            motion.y -= (1f - raise) * (1f - raise);
            return motion;
        }
    }
}
