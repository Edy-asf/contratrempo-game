using System.Collections;
using UnityEngine;

namespace Icaro
{
    // Portal de anéis no fim da Ponte Colossal: quando a sala do chefe é limpa, os anéis aceleram aos poucos e o centro
    // do portal se enche de luz (o núcleo cresce e a luz acende). Aberto, atravessar o disco de luz (a passarela do
    // alto do templo passa por ele) leva à cena da chegada dos Picos com a run inteira (RunCarry). O gatilho é montado
    // em jogo no plano do núcleo, então não é preciso gerar o Arquipélago de novo.
    public class PortalActivation : MonoBehaviour
    {
        public RoomManager Room;

        [Header("Destino")]
        [Tooltip("Cena para onde o portal aberto leva (caminho a partir de Assets/)")]
        public string DestinationScene = "Assets/Icaro/Scenes/Picos_Teste.unity";

        public string ArrivalTitle = "Picos acima das Nuvens";
        public string ArrivalSubtitle = "Chegada";

        [Tooltip("Profundidade do gatilho no plano do portal (m)")]
        public float GateDepth = 3f;

        static readonly System.Collections.Generic.List<PortalActivation> s_All =
            new System.Collections.Generic.List<PortalActivation>();

        public bool IsOpen => m_Active;

        // Teste: abre todos os portais da cena na hora ("Ícaro > Arquipélago > Abrir portal (teste)")
        public static void OpenAll()
        {
            foreach (PortalActivation portal in s_All)
            {
                if (portal && !portal.m_Active)
                    portal.StartCoroutine(portal.Activate());
            }
        }

        float m_Radius = 24f;

        void OnEnable() => s_All.Add(this);
        void OnDisable() => s_All.Remove(this);
        public SlowRotation[] Rings;
        public Transform Core;
        public Light Glow;

        [Tooltip("Quanto os anéis aceleram")]
        public float SpeedUp = 3f;

        [Tooltip("Duração da ativação (s)")]
        public float Duration = 4f;

        public float GlowIntensity = 6f;

        bool m_Active;

        void Start()
        {
            // Raio do disco de luz (antes de o núcleo ser encolhido)
            var filter = Core ? Core.GetComponentInChildren<MeshFilter>() : null;
            if (filter && filter.sharedMesh)
                m_Radius = Mathf.Max(filter.sharedMesh.bounds.extents.x, filter.sharedMesh.bounds.extents.y);

            if (Core)
            {
                Core.localScale = Vector3.zero;
                Core.gameObject.SetActive(false);
            }

            if (Glow)
                Glow.intensity = 0f;
            if (Room)
                Room.OnRoomCleared += OnCleared;
        }

        void OnDestroy()
        {
            if (Room)
                Room.OnRoomCleared -= OnCleared;
        }

        void OnCleared(RoomManager room)
        {
            if (!m_Active)
                StartCoroutine(Activate());
        }

        IEnumerator Activate()
        {
            m_Active = true;
            var speeds = new float[Rings.Length];
            for (int i = 0; i < Rings.Length; i++)
            {
                speeds[i] = Rings[i] ? Rings[i].DegreesPerSecond : 0f;
                // O portal aberto gira mesmo com o mundo congelado depois da luta
                if (Rings[i])
                    Rings[i].IgnoreFreeze = true;
            }
            if (Core)
                Core.gameObject.SetActive(true);
            CreateGate();
            for (float t = 0f; t < Duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / Duration);
                for (int i = 0; i < Rings.Length; i++)
                {
                    if (Rings[i])
                        Rings[i].DegreesPerSecond = speeds[i] * Mathf.Lerp(1f, SpeedUp, k);
                }

                if (Core)
                    Core.localScale = Vector3.one * k;
                if (Glow)
                    Glow.intensity = GlowIntensity * k;
                yield return null;
            }
        }

        // Gatilho na metade de cima do disco (a de baixo fica dentro do templo), no plano do portal
        void CreateGate()
        {
            if (!Core || string.IsNullOrEmpty(DestinationScene))
                return;
            var gate = new GameObject("Portal_Travessia");
            gate.transform.SetParent(Core.parent, false);
            gate.transform.SetPositionAndRotation(Core.position, Core.rotation);
            var box = gate.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(m_Radius * 2f, m_Radius, GateDepth);
            box.center = new Vector3(0f, m_Radius * 0.5f, 0f);
            gate.AddComponent<PortalGate>().Portal = this;
        }

        public void Enter(Collider other)
        {
            if (!m_Active || RunCarry.Traveling || !other.GetComponentInParent<Unity.FPS.Gameplay.PlayerCharacterController>())
                return;
            RunCarry.Travel(DestinationScene, ArrivalTitle, ArrivalSubtitle);
        }
    }
}
