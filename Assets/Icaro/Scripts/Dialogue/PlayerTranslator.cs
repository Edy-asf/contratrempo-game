using System;
using UnityEngine;

namespace Icaro
{
    // O tradutor aeônida do jogador: com ele, as conversas e a escrita dos NPCs ficam legíveis. Vale para a run (vai
    // junto pelos portais no RunCarry; recomeçar a run perde). Vendido pelo mercante (hoje, pela loja depois do chefe).
    // Adicionado pelo PlayerStats.
    public class PlayerTranslator : MonoBehaviour
    {
        public static PlayerTranslator Instance { get; private set; }

        // O jogador tem o tradutor
        public static bool Owned => Instance && Instance.m_Owned;

        public static event Action OnChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => OnChanged = null;

        [SerializeField] bool m_Owned;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Set(bool owned)
        {
            if (m_Owned == owned)
                return;
            m_Owned = owned;
            OnChanged?.Invoke();
        }
    }
}
