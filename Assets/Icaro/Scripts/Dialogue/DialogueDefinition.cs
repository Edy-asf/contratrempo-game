using System;
using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Uma conversa com um NPC: quem fala e as falas em nós. Cada nó tem o texto e, opcionalmente, respostas; sem
    // respostas, "continuar" segue para 'Next' (vazio = o próximo nó da lista, "fim" = acaba). Uma resposta leva ao nó
    // 'Next' dela. 'Event' (no nó, ao aparecer, ou na resposta, ao escolher) é avisado em DialogueEvents.OnEvent, para
    // outros sistemas reagirem (abrir a loja, o NPC virar chefe). Sem o tradutor, a conversa mostra só o primeiro nó em
    // hieróglifos e as respostas são as de WithoutTranslator e 'Ignorar' (a não ser que IgnoreWithoutTranslator esteja
    // desligado).
    [CreateAssetMenu(menuName = "Ícaro/Diálogo", fileName = "Dialogo_")]
    public class DialogueDefinition : ScriptableObject
    {
        public const string End = "fim";

        [Tooltip("Nome mostrado na caixa (com o tradutor; sem ele, em hieróglifos)")]
        public string Speaker = "???";

        [Tooltip("Cor do nome e do traço da caixa")]
        public Color SpeakerColor = new Color(0.72f, 0.55f, 1f);

        [Tooltip("Sem o tradutor: só hieróglifos e 'Ignorar'. Desligado: a conversa é sempre entendida (ex.: tutoriais)")]
        public bool IgnoreWithoutTranslator = true;

        [Tooltip("Sem o tradutor: respostas sem palavras, além de 'Ignorar' (ex.: apontar para a mochila do mercante)")]
        public List<DialogueChoice> WithoutTranslator = new List<DialogueChoice>();

        [Tooltip("Conversa que toca a partir da segunda vez entendida (vazio = esta de novo)")]
        public DialogueDefinition Repeat;

        public List<DialogueNode> Nodes = new List<DialogueNode>();

        public int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id))
                return -1;
            for (int i = 0; i < Nodes.Count; i++)
                if (Nodes[i].Id == id)
                    return i;
            return -1;
        }

        // O nó que vem depois do nó 'index' seguindo 'next' (-1 = acabou)
        public int Follow(int index, string next)
        {
            if (next == End)
                return -1;
            if (string.IsNullOrEmpty(next))
                return index + 1 < Nodes.Count ? index + 1 : -1;
            return IndexOf(next);
        }
    }

    [Serializable]
    public class DialogueNode
    {
        [Tooltip("Nome do nó, para as respostas apontarem para ele (pode ficar vazio)")]
        public string Id;

        [TextArea(2, 6)] public string Text;

        [Tooltip("Vazio = o próximo nó da lista; 'fim' = acaba")]
        public string Next;

        [Tooltip("Avisado quando o nó aparece")]
        public string Event;

        public List<DialogueChoice> Choices = new List<DialogueChoice>();
    }

    [Serializable]
    public class DialogueChoice
    {
        public string Label;

        [Tooltip("Nó para onde vai; vazio = o próximo da lista; 'fim' = acaba")]
        public string Next;

        [Tooltip("Avisado quando a resposta é escolhida")]
        public string Event;
    }

    // Avisos das conversas (nome do evento, o NPC que fala)
    public static class DialogueEvents
    {
        public static event Action<string, DialogueNpc> OnEvent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => OnEvent = null;

        public static void Raise(string name, DialogueNpc npc)
        {
            if (string.IsNullOrEmpty(name))
                return;
            OnEvent?.Invoke(name, npc);
            if (npc)
                npc.SendMessage("OnDialogueEvent", name, SendMessageOptions.DontRequireReceiver);
        }
    }
}
