using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Conversas que o jogador já ouviu nesta run (pelo nome do asset): da segunda vez em diante, o NPC toca a conversa
    // de repetição (DialogueDefinition.Repeat), também num NPC novo com a mesma conversa (o Méton aparece várias vezes).
    // Vai junto pelos portais (RunCarry); recomeçar a run esquece. Adicionado pelo PlayerStats.
    public class DialogueMemory : MonoBehaviour
    {
        public static DialogueMemory Instance { get; private set; }

        readonly HashSet<string> m_Heard = new HashSet<string>();

        public IEnumerable<string> Heard => m_Heard;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        static string Key(DialogueDefinition dialogue) =>
            string.IsNullOrEmpty(dialogue.name) ? dialogue.Speaker : dialogue.name;

        public static bool HasHeard(DialogueDefinition dialogue) =>
            dialogue && Instance && Instance.m_Heard.Contains(Key(dialogue));

        public static void Mark(DialogueDefinition dialogue)
        {
            if (dialogue && Instance)
                Instance.m_Heard.Add(Key(dialogue));
        }

        public void Restore(IEnumerable<string> keys)
        {
            foreach (string key in keys)
                m_Heard.Add(key);
        }
    }
}
