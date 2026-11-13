using System;
using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Icaro
{
    // Habilidades ativas do Ícaro: dois espaços (teclas GameSettings.Ability1Key e Ability2Key, Z e X por padrão), cada
    // um com a sua recarga (tempo do jogador, nunca o multiplicador). Comprar com os dois espaços ocupados guarda a nova
    // no inventário (trocar entre os espaços e o inventário fica para a transição de biomas). As habilidades só
    // funcionam durante a luta:
    // - Voltar no tempo (TimeRewind): os últimos segundos da sala correm de trás para frente;
    // - Câmera lenta (TimeSlow): o mundo dos inimigos fica lento (CombatTiming.WorldSpeed) e o jogador não.
    // Ajustes no AbilitySettings. O PlayerStats adiciona este componente.
    [DefaultExecutionOrder(25)]
    public class PlayerAbilities : MonoBehaviour
    {
        public const int SlotCount = 2;

        public static PlayerAbilities Instance { get; private set; }

        // A câmera lenta da habilidade está ligada
        public static bool SlowActive { get; private set; }

        // Espaços (None = vazio) e o inventário
        readonly AbilityType[] m_Slots = new AbilityType[SlotCount];
        readonly List<AbilityType> m_Stored = new List<AbilityType>();
        readonly Dictionary<AbilityType, float> m_ReadyAt = new Dictionary<AbilityType, float>();

        public IReadOnlyList<AbilityType> Stored => m_Stored;

        // Mudou o que o jogador tem (HUD, loja)
        public event Action OnChanged;

        // Uma habilidade foi usada (espaço) ou recusada (espaço, motivo)
        public event Action<int> OnUsed;
        public event Action<int, string> OnDenied;

        PlayerCharacterController m_Controller;
        PlayerInputHandler m_Input;
        TimeRewind m_Rewind;
        Coroutine m_Slow;

        static AbilitySettings Settings => AbilitySettings.Current;

        public AbilityType SlotAt(int slot) => slot >= 0 && slot < SlotCount ? m_Slots[slot] : AbilityType.None;

        public bool Owns(AbilityType type) =>
            type != AbilityType.None && (Array.IndexOf(m_Slots, type) >= 0 || m_Stored.Contains(type));

        public static Key KeyFor(int slot) => slot == 0 ? GameSettings.Ability1Key : GameSettings.Ability2Key;

        void Awake()
        {
            Instance = this;
            m_Controller = GetComponent<PlayerCharacterController>();
            m_Input = GetComponent<PlayerInputHandler>();
            m_Rewind = GetComponent<TimeRewind>();
            if (!m_Rewind)
                m_Rewind = gameObject.AddComponent<TimeRewind>();
        }

        void Start()
        {
            if (Settings.StartWithAll)
            {
                Give(AbilityType.TimeRewind);
                Give(AbilityType.TimeSlow);
            }

            if (!FindAnyObjectByType<AbilityHUD>())
                new GameObject("HabilidadesHUD").AddComponent<AbilityHUD>();
            if (!FindAnyObjectByType<AbilityFx>())
                new GameObject("EfeitosHabilidades").AddComponent<AbilityFx>();
        }

        void OnDisable()
        {
            // Nunca deixar o mundo lento para trás (morte, troca de cena)
            if (SlowActive)
                EndSlow();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // Dá uma habilidade: no primeiro espaço livre ou, com os dois ocupados, no inventário. Falso se já tem.
        public bool Give(AbilityType type) => Give(type, out _);

        public bool Give(AbilityType type, out bool stored)
        {
            stored = false;
            if (type == AbilityType.None || Owns(type))
                return false;
            int free = Array.IndexOf(m_Slots, AbilityType.None);
            if (free >= 0)
                m_Slots[free] = type;
            else
            {
                m_Stored.Add(type);
                stored = true;
            }

            OnChanged?.Invoke();
            return true;
        }

        // Troca o espaço 'slot' por uma habilidade do inventário (a que sai vai para o inventário)
        public bool Swap(int slot, AbilityType fromInventory)
        {
            if (slot < 0 || slot >= SlotCount || !m_Stored.Remove(fromInventory))
                return false;
            if (m_Slots[slot] != AbilityType.None)
                m_Stored.Add(m_Slots[slot]);
            m_Slots[slot] = fromInventory;
            OnChanged?.Invoke();
            return true;
        }

        // Tira tudo (recomeçar a run) e devolve o que veio de outra cena (RunCarry)
        public void Restore(IList<AbilityType> slots, IList<AbilityType> stored)
        {
            for (int i = 0; i < SlotCount; i++)
                m_Slots[i] = slots != null && i < slots.Count ? slots[i] : AbilityType.None;
            m_Stored.Clear();
            if (stored != null)
                m_Stored.AddRange(stored);
            OnChanged?.Invoke();
        }

        // Segundos até poder usar de novo (0 = pronta) e a fração da recarga que falta (1 = acabou de usar)
        public float Remaining(int slot)
        {
            AbilityType type = SlotAt(slot);
            return type != AbilityType.None && m_ReadyAt.TryGetValue(type, out float at)
                ? Mathf.Max(0f, at - Time.time)
                : 0f;
        }

        public float CooldownFraction(int slot)
        {
            AbilityType type = SlotAt(slot);
            return type == AbilityType.None ? 0f : Mathf.Clamp01(Remaining(slot) / Settings.CooldownOf(type));
        }

        // A habilidade do espaço está agindo agora (para o HUD)
        public bool InUse(int slot) => SlotAt(slot) switch
        {
            AbilityType.TimeRewind => TimeRewind.Rewinding,
            AbilityType.TimeSlow => SlowActive,
            _ => false,
        };

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || (m_Controller && m_Controller.IsDead))
                return;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (keyboard[KeyFor(slot)].wasPressedThisFrame && (!m_Input || m_Input.CanProcessInput()))
                    TryUse(slot);
            }
        }

        public bool TryUse(int slot)
        {
            AbilityType type = SlotAt(slot);
            if (type == AbilityType.None)
                return false;

            string reason = null;
            if (Remaining(slot) > 0f)
                reason = "Recarregando";
            else if (TimeRewind.Rewinding || SlowActive)
                reason = "O tempo já está alterado";
            else if (!TimeManager.CurrentlyRunning)
                reason = "Só durante a luta";
            else if (type == AbilityType.TimeRewind && !m_Rewind.CanRewind)
                reason = "Nada para voltar ainda";

            if (reason != null)
            {
                UiSounds.Play(UiSounds.Deny, 0.6f);
                OnDenied?.Invoke(slot, reason);
                return false;
            }

            switch (type)
            {
                case AbilityType.TimeRewind:
                    if (!m_Rewind.Rewind(Settings.RewindSeconds, Settings.RewindPlayback))
                        return false;
                    break;
                case AbilityType.TimeSlow:
                    m_Slow = StartCoroutine(RunSlow());
                    break;
            }

            m_ReadyAt[type] = Time.time + Settings.CooldownOf(type);
            OnUsed?.Invoke(slot);
            return true;
        }

        // ---------- câmera lenta ----------

        IEnumerator RunSlow()
        {
            AbilitySettings s = Settings;
            SlowActive = true;
            AbilityFx.SlowBegin();

            // Rampa de entrada, mundo lento e rampa de saída, no tempo do jogador (a pausa congela tudo junto)
            for (float t = 0f; t < s.SlowRampIn; t += Time.deltaTime)
            {
                CombatTiming.WorldSpeed = Mathf.Lerp(1f, s.SlowSpeed, Ease(t / s.SlowRampIn));
                yield return null;
            }

            CombatTiming.WorldSpeed = s.SlowSpeed;
            for (float t = 0f; t < s.SlowDuration; t += Time.deltaTime)
            {
                AbilityFx.SlowProgress = t / s.SlowDuration;
                yield return null;
            }

            AbilityFx.SlowEnding();
            for (float t = 0f; t < s.SlowRampOut; t += Time.deltaTime)
            {
                CombatTiming.WorldSpeed = Mathf.Lerp(s.SlowSpeed, 1f, Ease(t / s.SlowRampOut));
                yield return null;
            }

            EndSlow();
        }

        void EndSlow()
        {
            if (m_Slow != null)
                StopCoroutine(m_Slow);
            m_Slow = null;
            SlowActive = false;
            CombatTiming.WorldSpeed = 1f;
            AbilityFx.SlowEnd();
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
