using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Icaro
{
    // Menus dos diálogos: ajustes, criação das conversas de rascunho (Design/Historia_Rascunho.md) e atalhos de teste
    // em Play (um NPC na frente do jogador, dar ou tirar o tradutor).
    public static class DialogueMenu
    {
        const string Folder = "Assets/Icaro/Balance/Dialogos";

        [MenuItem("Ícaro/Ajustes dos diálogos", priority = 63)]
        static void Select()
        {
            DialogueSettings settings = DialogueSettings.CreateAsset();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        // Cria só as que faltam: um asset que já existe (talvez editado à mão) nunca é sobrescrito
        [MenuItem("Ícaro/Diálogos/Criar diálogos de rascunho", priority = 64)]
        static void CreateDrafts()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            int created = 0;
            foreach (KeyValuePair<string, DialogueDefinition> draft in Drafts())
            {
                string path = $"{Folder}/{draft.Key}.asset";
                if (AssetDatabase.LoadAssetAtPath<DialogueDefinition>(path))
                {
                    Object.DestroyImmediate(draft.Value);
                    continue;
                }
                AssetDatabase.CreateAsset(draft.Value, path);
                created++;
            }

            // As conversas de repetição apontam para os assets
            Link("Dialogo_Peregrino", "Dialogo_Peregrino_Repete");
            Link("Dialogo_Meton", "Dialogo_Meton_Repete");

            // A conversa do Méton vai para as fichas de loja que ainda não têm uma
            var meton = AssetDatabase.LoadAssetAtPath<DialogueDefinition>($"{Folder}/Dialogo_Meton.asset");
            foreach (string guid in AssetDatabase.FindAssets("t:ShopSettings"))
            {
                var shop = AssetDatabase.LoadAssetAtPath<ShopSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (shop && meton && !shop.MerchantDialogue)
                {
                    shop.MerchantDialogue = meton;
                    EditorUtility.SetDirty(shop);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Ícaro: {created} diálogo(s) de rascunho criado(s) em {Folder} (os que já existiam ficaram como estavam).");
        }

        static void Link(string main, string repeat)
        {
            var a = AssetDatabase.LoadAssetAtPath<DialogueDefinition>($"{Folder}/{main}.asset");
            var b = AssetDatabase.LoadAssetAtPath<DialogueDefinition>($"{Folder}/{repeat}.asset");
            if (a && b && !a.Repeat)
            {
                a.Repeat = b;
                EditorUtility.SetDirty(a);
            }
        }

        // Em Play: um NPC a 4 m na frente do jogador, com a conversa do Peregrino (o asset, ou o rascunho em memória)
        [MenuItem("Ícaro/Teste: NPC na frente", priority = 65)]
        static void SpawnNpc()
        {
            if (!BossTarget.Exists)
            {
                Debug.LogWarning("Ícaro: entre em Play com o jogador na cena.");
                return;
            }

            Transform player = BossTarget.Player.transform;
            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            Vector3 point = player.position + forward * 4f;
            if (Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                point = hit.point;

            DialogueDefinition dialogue = AssetDatabase.LoadAssetAtPath<DialogueDefinition>($"{Folder}/Dialogo_Peregrino.asset");
            if (!dialogue)
            {
                Dictionary<string, DialogueDefinition> drafts = Drafts();
                dialogue = drafts["Dialogo_Peregrino"];
                dialogue.Repeat = drafts["Dialogo_Peregrino_Repete"];
            }

            var go = new GameObject("NPC_Teste_Peregrino");
            go.transform.position = point;
            go.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            var npc = go.AddComponent<DialogueNpc>();
            npc.Dialogue = dialogue;
            npc.Art = "Peregrino";
            Selection.activeGameObject = go;
            Debug.Log($"Ícaro: NPC de teste criado (G para conversar). Tradutor: {(PlayerTranslator.Owned ? "sim" : "não")}.");
        }

        // Em Play: o Méton aparece na frente do jogador, como depois de uma sala
        [MenuItem("Ícaro/Teste: Méton na frente", priority = 66)]
        static void SpawnMerchant()
        {
            if (!BossTarget.Exists)
            {
                Debug.LogWarning("Ícaro: entre em Play com o jogador na cena.");
                return;
            }
            MerchantNpc merchant = MerchantNpc.SpawnNearPlayer();
            if (merchant)
                Selection.activeGameObject = merchant.gameObject;
        }

        [MenuItem("Ícaro/Teste: dar ou tirar o tradutor", priority = 67)]
        static void ToggleTranslator()
        {
            PlayerTranslator translator = PlayerTranslator.Instance;
            if (!translator)
            {
                Debug.LogWarning("Ícaro: entre em Play com o jogador na cena.");
                return;
            }
            translator.Set(!PlayerTranslator.Owned);
            Debug.Log($"Ícaro: tradutor {(PlayerTranslator.Owned ? "dado" : "tirado")}.");
        }

        [MenuItem("Ícaro/Teste: NPC na frente", true)]
        [MenuItem("Ícaro/Teste: Méton na frente", true)]
        [MenuItem("Ícaro/Teste: dar ou tirar o tradutor", true)]
        static bool InPlay() => Application.isPlaying;

        // ---------- Conversas de rascunho (ver Design/Historia_Rascunho.md) ----------

        static Dictionary<string, DialogueDefinition> Drafts()
        {
            var lilac = new Color(0.72f, 0.55f, 1f);
            var amber = new Color(1f, 0.74f, 0.35f);
            var cyan = new Color(0.5f, 0.92f, 1f);
            var steel = new Color(0.75f, 0.82f, 0.9f);

            return new Dictionary<string, DialogueDefinition>
            {
                ["Dialogo_Peregrino"] = Make("O Peregrino", lilac, true,
                    N("", "Uma máquina que fala a língua dos anéis... Faz tanto tempo que ninguém me responde."),
                    N("", "Eu ia à basílica de Archen Kaedra. Os sinos tocaram às 11h58, e desde então eu ando."),
                    N("pergunta", "Ando em direção à cidade, mas ela nunca fica mais perto. O que você procura, máquina?",
                        C("O que aconteceu às 11h58?", "1158"),
                        C("Onde estão os outros?", "outros"),
                        C("Vim desligar o Coração.", "coracao"),
                        C("Preciso ir.", "adeus")),
                    N("1158", "O Conselho dos Anéis ligou o Coração de Kairós. Era para segurar o fim do mundo."),
                    N("", "Segurou. Nada mais termina aqui. Nem o dia, nem a dor, nem esta caminhada.", next: "pergunta"),
                    N("outros", "Fugiram pelos portais, para um mundo azul. Dizem que lá esqueceram o próprio nome."),
                    N("", "Eu fiquei. Alguém tinha que rezar diante do Coração. Ainda não cheguei.", next: "pergunta"),
                    N("coracao", "Desligar...? Então o instante acabaria. E eu, enfim, chegaria a algum lugar."),
                    N("", "Siga as pontes. A cidade só se afasta de quem tem medo de chegar.", next: "fim"),
                    N("adeus", "Vá. O tempo não espera... ah, espere. Aqui ele espera.", next: "fim")),

                ["Dialogo_Peregrino_Repete"] = Make("O Peregrino", lilac, true,
                    N("", "Ainda aqui? Eu também. Sempre um passo, e a cidade sempre longe.")),

                // O evento "abrir_loja" fica para quando o mercante tiver a loja dele
                // Sem o tradutor, ele também é só hieróglifos: dá para apontar para a mochila e abrir a loja
                ["Dialogo_Meton"] = Wordless(Make("Méton", amber, true,
                    N("", "Ah! Agora sim nos entendemos. Eu sabia que esse tradutor ia servir direitinho em você."),
                    N("", "Méton, mercador de instantes. Eu não acredito em coincidências. Eu as vendo."),
                    N("", "Você passou um bom tempo apontando para a minha mochila. Ótimo cliente, mesmo sem uma palavra."),
                    N("", "E não se preocupe com o preço. Ele sobe com a pressa do mundo, então compre cedo.",
                        C("Mostre o que tem.", "fim", "abrir_loja"),
                        C("De onde você vem?", "origem"),
                        C("Depois.", "fim")),
                    N("origem", "De onde? Que pergunta antiga. Digamos que vendi o primeiro cristal deste planeta.", next: "fim"))),

                ["Dialogo_Meton_Repete"] = Wordless(Make("Méton", amber, true,
                    N("", "De volta! Eu sabia. Bem, eu sempre sei.",
                        C("Mostre o que tem.", "fim", "abrir_loja"),
                        C("Depois.", "fim")))),

                ["Dialogo_Varo"] = Make("Varo, guarda da barbacã", steel, true,
                    N("", "Alto! Ninguém passa pela barbacã sem ordem do Conselho. Nem... seja lá o que você for."),
                    N("", "A grade desceu às 11h58, comigo do lado de dentro. Desde então eu guardo um portão que ninguém atravessa."),
                    N("", "Você viu a ponte? Viu o cerco?",
                        C("Quem atacou a cidade?", "ataque"),
                        C("Posso abrir a grade.", "grade"),
                        C("Adeus, guarda.", "fim")),
                    N("ataque", "Eles não andavam. Eles apareciam. Num instante a ponte estava vazia, no outro, cheia."),
                    N("", "Lutamos o quanto deu. Depois o tempo parou, e eles também.", next: "fim"),
                    N("grade", "Não. A grade sobe quando o Conselho mandar. Ou quando o mundo voltar a andar.", next: "fim")),

                ["Dialogo_Oren"] = Make("Oren, o último sineiro", cyan, true,
                    N("", "Shh... Ouça. Ainda está ecoando. A última badalada."),
                    N("", "Fui eu que toquei o sino às 11h58. O Conselho mandou avisar a cidade. Então o sino caiu."),
                    N("", "Espero a próxima badalada desde então. Ela nunca vem. Por que veio até aqui, máquina?",
                        C("Vim destruir o Coração.", "fim_do_tempo"),
                        C("Só estou de passagem.", "passagem")),
                    N("passagem", "Ninguém está de passagem num instante. Volte quando souber o que veio fazer.", next: "fim"),
                    N("fim_do_tempo", "Então... você é a próxima badalada."),
                    N("", "Mas um sino não toca sozinho. Ele precisa ser batido. Mostre que você é forte o bastante.",
                        next: "fim", evt: "chefe_secreto")),
            };
        }

        // Mercante: sem o tradutor, apontar para a mochila abre a loja
        static DialogueDefinition Wordless(DialogueDefinition d)
        {
            d.WithoutTranslator = new List<DialogueChoice> { C("(Apontar para a mochila)", "fim", "abrir_loja") };
            return d;
        }

        static DialogueDefinition Make(string speaker, Color color, bool needsTranslator, params DialogueNode[] nodes)
        {
            var d = ScriptableObject.CreateInstance<DialogueDefinition>();
            d.Speaker = speaker;
            d.SpeakerColor = color;
            d.IgnoreWithoutTranslator = needsTranslator;
            d.Nodes = new List<DialogueNode>(nodes);
            return d;
        }

        static DialogueNode N(string id, string text, params DialogueChoice[] choices) =>
            new DialogueNode { Id = id, Text = text, Choices = new List<DialogueChoice>(choices) };

        static DialogueNode N(string id, string text, string next, string evt = null) =>
            new DialogueNode { Id = id, Text = text, Next = next, Event = evt };

        static DialogueChoice C(string label, string next, string evt = null) =>
            new DialogueChoice { Label = label, Next = next, Event = evt };
    }
}
