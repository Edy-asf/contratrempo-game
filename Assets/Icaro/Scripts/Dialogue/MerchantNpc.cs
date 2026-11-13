using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Méton, o mercador de instantes: um NPC (DialogueNpc, sprite "Meton") com uma loja (ShopTerminal sem aviso próprio).
    // Sem o tradutor, fala em hieróglifos e não diz quem é ("???", também no título da loja); apontar para a mochila
    // (resposta sem palavras) abre a loja, onde o tradutor está à venda. A loja abre quando a conversa dispara
    // "abrir_loja". O
    // ShopDirector faz ele aparecer depois de cada chefe e das salas de ShopSettings.MerchantRooms: surge com um relógio
    // ciano e um sopro, na frente do jogador; some quando a próxima luta começa ou o jogador se afasta muito. Um só por
    // vez. Não sente o tempo de Kairós (tempo sem escala).
    [RequireComponent(typeof(DialogueNpc))]
    public class MerchantNpc : MonoBehaviour
    {
        public const string OpenShopEvent = "abrir_loja";
        const float k_AppearTime = 0.5f;
        const float k_LeaveTime = 0.35f;
        const float k_LeaveDistance = 150f;

        public static MerchantNpc Current { get; private set; }

        ShopTerminal m_Shop;
        bool m_OpenShop;
        float m_Age;
        float m_Leaving = -1f;

        public static MerchantNpc SpawnAt(Vector3 position)
        {
            if (Current)
                Current.Leave();

            var go = new GameObject("Meton");
            go.transform.position = position;
            Vector3 to = BossTarget.Exists ? BossTarget.Feet - position : Vector3.forward;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                go.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            go.transform.localScale = Vector3.zero;

            var npc = go.AddComponent<DialogueNpc>();
            npc.Art = "Meton";
            npc.LookHeight = 2.1f;
            npc.Dialogue = ShopDirector.Settings.MerchantDialogue
                ? ShopDirector.Settings.MerchantDialogue
                : FallbackDialogue();

            var shop = go.AddComponent<ShopTerminal>();
            shop.Interactive = false;

            MerchantNpc merchant = go.AddComponent<MerchantNpc>();
            merchant.m_Shop = shop;
            Current = merchant;
            return merchant;
        }

        // Na frente do jogador, num ponto do NavMesh que ele enxerga (tenta para os lados e para trás)
        public static MerchantNpc SpawnNearPlayer()
        {
            if (!BossTarget.Exists)
                return null;
            Vector3 feet = BossTarget.Feet;
            Camera cam = Camera.main;
            Vector3 forward = cam ? cam.transform.forward : BossTarget.Player.transform.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;

            float distance = ShopDirector.Settings.MerchantDistance;
            float[] angles = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 120f, -120f, 180f };
            foreach (float scale in new[] { 1f, 0.6f })
            {
                foreach (float angle in angles)
                {
                    Vector3 guess = feet + Quaternion.Euler(0f, angle, 0f) * forward * (distance * scale);
                    if (!NavMesh.SamplePosition(guess, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                        continue;
                    if (Mathf.Abs(hit.position.y - feet.y) > 2.5f)
                        continue;
                    if (Physics.Linecast(feet + Vector3.up * 1.6f, hit.position + Vector3.up * 1.6f, ~0,
                            QueryTriggerInteraction.Ignore))
                        continue;
                    return SpawnAt(hit.position);
                }
            }

            return SpawnAt(GroundTelegraph.GroundPoint(feet + forward * 3f));
        }

        void Start()
        {
            if (!m_Shop)
                m_Shop = GetComponent<ShopTerminal>();
            TimeRewindBurst.Spawn(transform.position + Vector3.up * 1.4f, 0.6f);
            UiSounds.Play(UiSounds.Whoosh, 0.45f, 0.8f);
            UiSounds.Play(UiSounds.Reveal, 0.3f, 1.15f);
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        // Avisado pela conversa (DialogueEvents.Raise)
        void OnDialogueEvent(string name)
        {
            if (name == OpenShopEvent)
                m_OpenShop = true;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            m_Age += dt;

            if (m_Leaving >= 0f)
            {
                m_Leaving += dt;
                float k = 1f - Mathf.Clamp01(m_Leaving / k_LeaveTime);
                transform.localScale = new Vector3(Mathf.Lerp(0.2f, 1f, k), k * k, 1f);
                if (m_Leaving >= k_LeaveTime)
                    Destroy(gameObject);
                return;
            }

            // Surge esticando, com um pulo no fim
            float e = Mathf.Clamp01(m_Age / k_AppearTime);
            float back = 1f + 2.2f * Mathf.Pow(e - 1f, 3f) + 1.2f * Mathf.Pow(e - 1f, 2f);
            transform.localScale = new Vector3(Mathf.Lerp(0.3f, 1f, e), back, 1f);

            // A loja abre quando a conversa terminou de fechar
            if (m_OpenShop && !DialogueUI.Talking)
            {
                m_OpenShop = false;
                if (m_Shop)
                {
                    // Sem o tradutor, o jogador não sabe quem ele é
                    m_Shop.Title = PlayerTranslator.Owned ? "Méton, mercador de instantes" : "???";
                    m_Shop.OpenShop();
                }
            }

            bool busy = DialogueUI.Talking || (ShopUI.Instance && ShopUI.Instance.IsOpen) || m_OpenShop;
            if (!busy && m_Age > 1f &&
                (TimeManager.CurrentlyRunning || (BossTarget.Exists && BossTarget.FlatDistance(transform.position) > k_LeaveDistance)))
                Leave();
        }

        public void Leave()
        {
            if (m_Leaving >= 0f)
                return;
            m_Leaving = 0f;
            if (Current == this)
                Current = null;
            TimeRewindBurst.Spawn(transform.position + Vector3.up * 1.4f, 0.4f);
        }

        // Sem a conversa ligada no ShopSettings: só o essencial
        static DialogueDefinition s_Fallback;

        static DialogueDefinition FallbackDialogue()
        {
            if (s_Fallback)
                return s_Fallback;
            s_Fallback = ScriptableObject.CreateInstance<DialogueDefinition>();
            s_Fallback.name = "Dialogo_Meton_Reserva";
            s_Fallback.Speaker = "Méton";
            s_Fallback.SpeakerColor = new Color(1f, 0.74f, 0.35f);
            s_Fallback.IgnoreWithoutTranslator = true;
            s_Fallback.WithoutTranslator = new List<DialogueChoice>
            {
                new DialogueChoice { Label = "(Apontar para a mochila)", Next = DialogueDefinition.End, Event = OpenShopEvent },
            };
            s_Fallback.Nodes = new List<DialogueNode>
            {
                new DialogueNode
                {
                    Text = "Méton, mercador de instantes. Vamos fazer negócio?",
                    Choices = new List<DialogueChoice>
                    {
                        new DialogueChoice { Label = "Mostre o que tem.", Next = DialogueDefinition.End, Event = OpenShopEvent },
                        new DialogueChoice { Label = "Depois.", Next = DialogueDefinition.End },
                    },
                },
            };
            return s_Fallback;
        }
    }
}
