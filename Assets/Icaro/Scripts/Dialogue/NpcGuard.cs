using System.Collections.Generic;
using System.Reflection;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Guarda baixa perto dos NPCs: fora da luta, quando um NPC apareceu na vista (dentro da tela e sem nada na frente)
    // e está a menos de DialogueSettings.GuardRadius, o ÍCARO-9 abaixa a arma e não atira. Ao se afastar (raio +
    // GuardRelease), a arma levanta e o tiro só volta quando ela termina de subir. O tiro é barrado pela arma do
    // Microgame achar que acabou de atirar (m_LastTimeShot, por reflexão), sem editar o Microgame. Adicionado pelo
    // PlayerStats.
    [DefaultExecutionOrder(-50)]
    public class NpcGuard : MonoBehaviour
    {
        // O jogador está de guarda baixa (ou a arma ainda está subindo)
        public static bool Lowered { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Lowered = false;

        static readonly FieldInfo s_LastShot = typeof(WeaponController).GetField("m_LastTimeShot",
            BindingFlags.Instance | BindingFlags.NonPublic);

        const float k_CheckEvery = 0.12f;

        readonly HashSet<DialogueNpc> m_Seen = new HashSet<DialogueNpc>();
        PlayerWeaponsManager m_Weapons;
        bool m_Guarding;
        float m_BlockUntil;
        float m_NextCheck;

        void Awake() => m_Weapons = GetComponent<PlayerWeaponsManager>();

        void OnDisable() => SetGuard(false, true);

        void OnDestroy() => SetGuard(false, true);

        void Update()
        {
            if (Time.unscaledTime >= m_NextCheck)
            {
                m_NextCheck = Time.unscaledTime + k_CheckEvery;
                SetGuard(ShouldGuard(), false);
            }

            Lowered = m_Guarding || Time.unscaledTime < m_BlockUntil;
            if (Lowered)
                BlockShot();
        }

        bool ShouldGuard()
        {
            if (TimeManager.CurrentlyRunning || DialogueNpc.All.Count == 0)
            {
                m_Seen.Clear();
                return false;
            }

            DialogueSettings settings = DialogueSettings.Current;
            Camera cam = Camera.main;
            Vector3 feet = transform.position;
            bool any = false;
            m_Seen.RemoveWhere(npc => !npc);

            foreach (DialogueNpc npc in DialogueNpc.All)
            {
                Vector3 d = npc.transform.position - feet;
                float flat = new Vector2(d.x, d.z).magnitude;
                bool near = flat <= settings.GuardRadius && Mathf.Abs(d.y) < 6f;

                if (m_Seen.Contains(npc))
                {
                    // Já visto: fica de guarda até sair do raio com a folga
                    if (flat > settings.GuardRadius + settings.GuardRelease)
                        m_Seen.Remove(npc);
                    else
                        any = true;
                    continue;
                }

                if (near && cam && InView(cam, npc))
                {
                    m_Seen.Add(npc);
                    any = true;
                }
            }

            return any;
        }

        // O NPC está na tela e a câmera enxerga o rosto dele (nada no caminho além dele)
        static bool InView(Camera cam, DialogueNpc npc)
        {
            Vector3 point = npc.LookPoint;
            Vector3 vp = cam.WorldToViewportPoint(point);
            if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
                return false;
            Vector3 from = cam.transform.position;
            if (Physics.Linecast(from, point, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.transform.IsChildOf(npc.transform);
            return true;
        }

        void SetGuard(bool on, bool silent)
        {
            if (on == m_Guarding)
                return;
            m_Guarding = on;
            if (on)
            {
                WeaponSpriteHUD.Lower(this);
                return;
            }

            WeaponSpriteHUD.Raise(this);
            // O tiro volta só com a arma de pé
            m_BlockUntil = silent ? 0f : Time.unscaledTime + WeaponMotionSettings.Current.LiftTime;
        }

        // A arma ativa "acabou de atirar": o TryShoot do Microgame espera DelayBetweenShots e não dispara
        void BlockShot()
        {
            if (!m_Weapons)
                m_Weapons = GetComponent<PlayerWeaponsManager>();
            WeaponController weapon = m_Weapons ? m_Weapons.GetActiveWeapon() : null;
            if (weapon && s_LastShot != null)
                s_LastShot.SetValue(weapon, Time.time);
        }
    }
}
