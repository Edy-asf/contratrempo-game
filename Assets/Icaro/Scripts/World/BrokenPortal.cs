using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Icaro
{
    // Portal quebrado no fim de uma sala (a máquina de anéis do Bastião, sala 19): até a sala ser concluída fica parado,
    // escuro e em pedaços. Ao concluir, tudo se reconstrói: cada pedaço caído (blocos do montante partido, a metade da
    // viga, o trecho do anel) se ergue e voa de volta ao lugar, um depois do outro; as runas acendem em ordem, as
    // engrenagens e o anel começam a girar e o centro se enche de luz. Atravessar o portal leva ao Destino com uma
    // transição curta (clarão e distorção do tempo na tela).
    // "Concluída" é o fim do combate (RoomManager.OnRoomCleared); numa sala sem inimigos (sem CombatBalance), abre quando
    // o jogador entra na OpenZone. Para testar: "Ícaro > Picos > Abrir portais quebrados (teste)" (OpenAll).
    // Precisa de um colisor de gatilho no mesmo objeto (a boca do portal), desligado até abrir.
    public class BrokenPortal : MonoBehaviour
    {
        [System.Serializable]
        public class Piece
        {
            public Transform Target;
            [Tooltip("Pose montada (no mundo); a pose da cena é a quebrada")]
            public Vector3 FixedPosition;
            public Quaternion FixedRotation = Quaternion.identity;
            [Tooltip("Atraso desde o começo da reconstrução (s)")]
            public float Delay;
            [Tooltip("Ao encaixar, vira filho deste objeto (o trecho do anel gira junto com o anel)")]
            public Transform AttachTo;
        }

        public RoomManager Room;
        public Piece[] Pieces = new Piece[0];

        [Tooltip("Anel (gira em volta do eixo depois de montado)")]
        public Transform Ring;
        public Vector3 RingAxis = Vector3.forward;
        public float RingSpeed = 7f;

        public Transform[] Gears = new Transform[0];
        public float[] GearSpeeds = new float[0];
        public Vector3 GearAxis = Vector3.right;

        [Tooltip("Placas de runa, na ordem em que acendem")]
        public Renderer[] Runes = new Renderer[0];

        [Tooltip("Superfície de luz no centro (cresce de 0 a 1)")]
        public Transform Surface;
        public Light Glow;
        public float GlowIntensity = 6f;

        [Tooltip("Para onde o portal leva (posição e direção do olhar)")]
        public Transform Destination;
        [Tooltip("Faixa do visor ao chegar no destino (vazio = nenhuma)")]
        public string ArrivalTitle = "";
        public string ArrivalSubtitle = "";

        [Tooltip("Sala sem inimigos: abre quando o jogador entra nesta área (no mundo)")]
        public Bounds OpenZone;

        [Tooltip("Duração do voo de cada pedaço (s)")]
        public float PieceTime = 1.6f;
        public float ArcHeight = 3f;

        static readonly List<BrokenPortal> s_All = new List<BrokenPortal>();
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        Collider m_Gate;
        bool m_Opening, m_Open, m_Traveling;
        Transform m_Player;
        MaterialPropertyBlock m_Block;

        public bool IsOpen => m_Open;

        // Abre todos os portais quebrados da cena (teste)
        public static void OpenAll()
        {
            foreach (BrokenPortal portal in s_All)
                portal.Open();
        }

        void OnEnable() => s_All.Add(this);

        void OnDisable() => s_All.Remove(this);

        void Start()
        {
            m_Gate = GetComponent<Collider>();
            if (m_Gate)
            {
                m_Gate.isTrigger = true;
                m_Gate.enabled = false;
            }

            if (Surface)
            {
                Surface.localScale = Vector3.zero;
                Surface.gameObject.SetActive(false);
            }

            if (Glow)
                Glow.intensity = 0f;
            m_Block = new MaterialPropertyBlock();
            foreach (Renderer rune in Runes)
                SetRune(rune, 0f);
            if (Room)
                Room.OnRoomCleared += OnCleared;
        }

        void OnDestroy()
        {
            if (Room)
                Room.OnRoomCleared -= OnCleared;
        }

        void OnCleared(RoomManager room) => Open();

        void Update()
        {
            if (m_Opening || (Room && Room.Balance))
                return;
            if (!m_Player)
            {
                var player = FindFirstObjectByType<PlayerCharacterController>();
                if (!player)
                    return;
                m_Player = player.transform;
            }

            if (OpenZone.size != Vector3.zero && OpenZone.Contains(m_Player.position))
                Open();
        }

        public void Open()
        {
            if (m_Opening)
                return;
            m_Opening = true;
            StartCoroutine(Rebuild());
        }

        IEnumerator Rebuild()
        {
            VisorBanner.Show("Portal", "Os anéis se reconstroem", BannerTone.Normal);

            // Os pedaços voam de volta, cada um na sua vez
            float end = 0f;
            foreach (Piece piece in Pieces)
            {
                if (!piece.Target)
                    continue;
                StartCoroutine(Fly(piece));
                end = Mathf.Max(end, piece.Delay + PieceTime);
            }

            yield return new WaitForSeconds(end + 0.2f);

            // Runas em ordem
            foreach (Renderer rune in Runes)
                StartCoroutine(LightRune(rune));
            yield return new WaitForSeconds(Mathf.Min(1.6f, Runes.Length * 0.08f));

            // Engrenagens e anel começam a girar; o centro se enche de luz
            if (Surface)
                Surface.gameObject.SetActive(true);
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 2f);
                Spin(k);
                if (Surface)
                    Surface.localScale = Vector3.one * k;
                if (Glow)
                    Glow.intensity = GlowIntensity * k;
                yield return null;
            }

            if (Surface)
                Surface.localScale = Vector3.one;
            if (m_Gate)
                m_Gate.enabled = true;
            m_Open = true;
            VisorBanner.Show("Portal aberto", "Atravesse para seguir", BannerTone.Success);
            while (true)
            {
                Spin(1f);
                yield return null;
            }
        }

        void Spin(float k)
        {
            if (Ring)
                Ring.Rotate(RingAxis, RingSpeed * k * Time.deltaTime, Space.World);
            for (int i = 0; i < Gears.Length; i++)
            {
                if (Gears[i])
                    Gears[i].Rotate(GearAxis, (i < GearSpeeds.Length ? GearSpeeds[i] : 30f) * k * Time.deltaTime, Space.World);
            }
        }

        // Ergue o pedaço com um tremor, voa em arco até a pose montada e encaixa
        IEnumerator Fly(Piece piece)
        {
            yield return new WaitForSeconds(piece.Delay);
            Transform target = piece.Target;
            Vector3 from = target.position;
            Quaternion fromRotation = target.rotation;
            const float lift = 0.3f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / PieceTime)
            {
                if (t < lift)
                {
                    float k = t / lift;
                    Vector3 shake = Random.insideUnitSphere * 0.06f * (1f - k);
                    target.position = from + Vector3.up * (1.2f * Mathf.SmoothStep(0f, 1f, k)) + shake;
                }
                else
                {
                    float k = Mathf.SmoothStep(0f, 1f, (t - lift) / (1f - lift));
                    Vector3 start = from + Vector3.up * 1.2f;
                    target.position = Vector3.Lerp(start, piece.FixedPosition, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * ArcHeight);
                    target.rotation = Quaternion.Slerp(fromRotation, piece.FixedRotation, k);
                }

                yield return null;
            }

            target.SetPositionAndRotation(piece.FixedPosition, piece.FixedRotation);
            if (piece.AttachTo)
                target.SetParent(piece.AttachTo, true);
        }

        IEnumerator LightRune(Renderer rune)
        {
            int index = System.Array.IndexOf(Runes, rune);
            yield return new WaitForSeconds(index * 0.08f);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                SetRune(rune, t / 0.35f);
                yield return null;
            }

            SetRune(rune, 1f);
        }

        // Runa de apagada (pedra escura, sem brilho) a acesa (a cor e o brilho do material)
        void SetRune(Renderer rune, float k)
        {
            if (!rune || !rune.sharedMaterial)
                return;
            Material material = rune.sharedMaterial;
            Color baseColor = material.HasProperty(k_BaseColor) ? material.GetColor(k_BaseColor) : Color.white;
            Color emission = material.HasProperty(k_EmissionColor) ? material.GetColor(k_EmissionColor) : Color.black;
            rune.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_BaseColor, Color.Lerp(baseColor * 0.3f, baseColor, k));
            m_Block.SetColor(k_EmissionColor, emission * (k * k));
            rune.SetPropertyBlock(m_Block);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!m_Open || m_Traveling || !Destination)
                return;
            var player = other.GetComponentInParent<PlayerCharacterController>();
            if (player)
                StartCoroutine(Travel(player));
        }

        // Clarão e distorção do tempo na tela, troca de lugar no auge e volta
        IEnumerator Travel(PlayerCharacterController player)
        {
            m_Traveling = true;
            var root = new GameObject("Travessia_Portal");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var flash = new GameObject("Clarao").AddComponent<Image>();
            flash.transform.SetParent(root.transform, false);
            flash.rectTransform.anchorMin = Vector2.zero;
            flash.rectTransform.anchorMax = Vector2.one;
            flash.rectTransform.offsetMin = flash.rectTransform.offsetMax = Vector2.zero;
            flash.raycastTarget = false;
            var tint = new Color(0.85f, 1f, 0.98f, 0f);
            flash.color = tint;

            var volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 200f;
            volume.weight = 0f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<LensDistortion>(true).intensity.Override(-0.75f);
            profile.Add<ChromaticAberration>(true).intensity.Override(1f);
            volume.profile = profile;

            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.6f;
                volume.weight = k;
                tint.a = k * k;
                flash.color = tint;
                yield return null;
            }

            tint.a = 1f;
            flash.color = tint;
            Teleport(player);
            yield return new WaitForSecondsRealtime(0.25f);
            if (!string.IsNullOrEmpty(ArrivalTitle))
                VisorBanner.Show(ArrivalTitle, ArrivalSubtitle, BannerTone.Normal);

            for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / 0.9f;
                volume.weight = k;
                tint.a = k * k;
                flash.color = tint;
                yield return null;
            }

            Destroy(profile);
            Destroy(root);
            m_Traveling = false;
        }

        void Teleport(PlayerCharacterController player)
        {
            var controller = player.GetComponent<CharacterController>();
            bool enabled = controller && controller.enabled;
            if (controller)
                controller.enabled = false;
            player.transform.SetPositionAndRotation(Destination.position,
                Quaternion.Euler(0f, Destination.eulerAngles.y, 0f));
            player.CharacterVelocity = Vector3.zero;
            if (controller)
                controller.enabled = enabled;
            Physics.SyncTransforms();
        }
    }
}
