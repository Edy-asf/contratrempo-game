using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Icaro
{
    // Modo de teste das armas (menu "Ícaro > Teste: todas as armas"): dá ao jogador todas as armas do catálogo de uma
    // vez (teclas 1 a 5, Q/E ou a rodinha; o limite de 2 armas não vale aqui), cria alvos de treino na frente dele e
    // mostra um painel com os números da arma na mão e um medidor de dano (DPS dos últimos segundos, total, acertos,
    // maior acerto). Os alvos são inimigos de verdade parados (IA desligada) com vida enorme, que voltam cheios
    // alguns segundos depois do último dano; a assistência de mira os enxerga (AimAssist.ExtraTargets). Teclas: F1
    // painel, F2 invulnerável, F4 alvos (tira e põe de volta na frente do jogador), F5 zera o medidor. Rodar o menu de
    // novo desliga o modo (as armas continuam com o jogador).
    public class WeaponTestMode : MonoBehaviour
    {
        [Serializable]
        public class Target
        {
            public GameObject Prefab;
            public EnemyDefinition Definition;

            [Tooltip("Posição em relação ao jogador: x para a direita, y altura, z para a frente (m)")]
            public Vector3 Offset;

            [Tooltip("Inimigo voador: fica na altura do Offset em vez de pousar no chão")]
            public bool Flying;
        }

        public WeaponCatalog Catalog;
        public Target[] Targets;

        [Tooltip("Vida de cada alvo de treino")]
        public float TargetHealth = 100000f;

        [Tooltip("Segundos sem levar dano para o alvo voltar com a vida cheia")]
        public float ResetDelay = 3f;

        [Tooltip("Janela do medidor de DPS (s)")]
        public float DpsWindow = 3f;

        static readonly CultureInfo k_Culture = CultureInfo.GetCultureInfo("pt-BR");

        class Dummy
        {
            public GameObject Root;
            public Health Health;
            public float LastHit;
        }

        readonly List<Dummy> m_Dummies = new List<Dummy>();
        readonly Queue<(float time, float amount)> m_Recent = new Queue<(float, float)>();
        PlayerWeaponsManager m_Weapons;
        Health m_PlayerHealth;
        bool m_Invulnerable = true;
        bool m_ShowPanel = true;
        float m_Total;
        int m_Hits;
        float m_Biggest;
        float m_FirstHit = -1f;
        GameObject m_Canvas;
        TextMeshProUGUI m_Panel;

        IEnumerator Start()
        {
            // Espera o jogador existir (cena carregando ou tela de título)
            while (!PlayerStats.Instance)
                yield return null;
            m_Weapons = PlayerStats.Instance.GetComponent<PlayerWeaponsManager>();
            m_PlayerHealth = PlayerStats.Instance.GetComponent<Health>();
            yield return null;

            GiveAllWeapons();
            SpawnTargets();
            CombatEvents.OnDamage += OnDamage;
            BuildPanel();
            Debug.Log("[Ícaro] Modo de teste de armas: 1 a 5 trocam de arma; F1 painel, F2 invulnerável, F4 alvos, " +
                      "F5 zera o medidor.");
        }

        void OnDestroy()
        {
            CombatEvents.OnDamage -= OnDamage;
            ClearTargets();
            if (m_Canvas)
                Destroy(m_Canvas);
        }

        // ---------- armas ----------

        void GiveAllWeapons()
        {
            if (!m_Weapons || !Catalog)
            {
                Debug.LogWarning("[Ícaro] Teste de armas sem catálogo de armas ou sem PlayerWeaponsManager.");
                return;
            }

            foreach (WeaponDefinition weapon in Catalog.Weapons)
            {
                if (weapon && weapon.WeaponPrefab && !m_Weapons.HasWeapon(weapon.WeaponPrefab))
                    m_Weapons.AddWeapon(weapon.WeaponPrefab);
            }

            m_Weapons.SwitchToWeaponIndex(0);
        }

        // ---------- alvos ----------

        void SpawnTargets()
        {
            ClearTargets();
            Transform player = PlayerStats.Instance.transform;
            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            foreach (Target target in Targets ?? Array.Empty<Target>())
            {
                if (target == null || !target.Prefab)
                    continue;
                Vector3 position = player.position + right * target.Offset.x + forward * target.Offset.z;
                if (target.Flying)
                    position.y = player.position.y + target.Offset.y;
                else if (Physics.Raycast(position + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 80f,
                             Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    position = hit.point;
                SpawnDummy(target, position, Quaternion.LookRotation(-forward));
            }
        }

        void SpawnDummy(Target target, Vector3 position, Quaternion rotation)
        {
            GameObject root = Object.Instantiate(target.Prefab, position, rotation);
            root.name = "AlvoDeTreino_" + target.Prefab.name;

            // Parado: só ficam vida, dano, ficha, ator, animação e luzes
            foreach (NavMeshAgent agent in root.GetComponentsInChildren<NavMeshAgent>(true))
                agent.enabled = false;
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
                body.isKinematic = true;
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(behaviour is Health || behaviour is Damageable || behaviour is EnemyStats ||
                      behaviour is DamageReporter || behaviour is Actor))
                    behaviour.enabled = false;
            }

            var stats = root.GetComponent<EnemyStats>();
            if (stats)
            {
                if (!stats.Definition)
                    stats.Definition = target.Definition;
                stats.Configure(1f, 1f);
            }

            var health = root.GetComponent<Health>();
            if (health)
            {
                health.MaxHealth = TargetHealth;
                health.CurrentHealth = TargetHealth;
                AimAssist.ExtraTargets.Add(health);
            }

            m_Dummies.Add(new Dummy { Root = root, Health = health, LastHit = -999f });
        }

        void ClearTargets()
        {
            foreach (Dummy dummy in m_Dummies)
            {
                if (dummy.Health)
                    AimAssist.ExtraTargets.Remove(dummy.Health);
                if (dummy.Root)
                    Destroy(dummy.Root);
            }

            m_Dummies.Clear();
        }

        // ---------- medidor ----------

        void OnDamage(DamageInfo info)
        {
            if (info.VictimIsPlayer || !info.AttackerIsPlayer)
                return;
            foreach (Dummy dummy in m_Dummies)
            {
                if (dummy.Health == info.VictimHealth)
                    dummy.LastHit = Time.time;
            }

            if (m_FirstHit < 0f)
                m_FirstHit = Time.time;
            m_Recent.Enqueue((Time.time, info.Amount));
            m_Total += info.Amount;
            m_Hits++;
            m_Biggest = Mathf.Max(m_Biggest, info.Amount);
        }

        void ResetMeter()
        {
            m_Recent.Clear();
            m_Total = 0f;
            m_Hits = 0;
            m_Biggest = 0f;
            m_FirstHit = -1f;
        }

        float Dps()
        {
            while (m_Recent.Count > 0 && m_Recent.Peek().time < Time.time - DpsWindow)
                m_Recent.Dequeue();
            float sum = 0f;
            foreach (var hit in m_Recent)
                sum += hit.amount;
            // Antes de completar a janela, divide pelo tempo desde o primeiro acerto
            float window = m_FirstHit < 0f ? DpsWindow : Mathf.Clamp(Time.time - m_FirstHit, 0.5f, DpsWindow);
            return sum / window;
        }

        // ---------- laço ----------

        void Update()
        {
            if (!m_Weapons)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f1Key.wasPressedThisFrame)
                    m_ShowPanel = !m_ShowPanel;
                if (keyboard.f2Key.wasPressedThisFrame)
                    m_Invulnerable = !m_Invulnerable;
                if (keyboard.f4Key.wasPressedThisFrame)
                {
                    if (m_Dummies.Count > 0)
                        ClearTargets();
                    else
                        SpawnTargets();
                }

                if (keyboard.f5Key.wasPressedThisFrame)
                    ResetMeter();
            }

            if (m_Invulnerable && m_PlayerHealth && m_PlayerHealth.CurrentHealth > 0f)
                m_PlayerHealth.CurrentHealth = m_PlayerHealth.MaxHealth;

            foreach (Dummy dummy in m_Dummies)
            {
                if (dummy.Health && Time.time - dummy.LastHit > ResetDelay)
                    dummy.Health.CurrentHealth = dummy.Health.MaxHealth;
            }

            if (m_Canvas)
                m_Canvas.SetActive(m_ShowPanel);
            if (m_Panel && m_ShowPanel)
                m_Panel.text = PanelText();
        }

        // ---------- painel ----------

        void BuildPanel()
        {
            Canvas canvas = ItemUI.CreateCanvas(transform, "TesteDeArmasCanvas", 20);
            m_Canvas = canvas.gameObject;
            Image back = ItemUI.Panel("Painel", canvas.transform, new Vector2(0f, 0.5f), new Vector2(20f, 40f),
                new Vector2(520f, 470f), new Color(0.03f, 0.05f, 0.08f, 0.82f));
            m_Panel = ItemUI.Text("Texto", back.transform, new Vector2(0f, 1f), new Vector2(18f, -14f),
                new Vector2(490f, 450f), 20f, TextAlignmentOptions.TopLeft, new Color(0.88f, 0.93f, 1f));
            m_Panel.rectTransform.pivot = new Vector2(0f, 1f);
            if (ItemFonts.Regular)
                m_Panel.font = ItemFonts.Regular;
        }

        string PanelText()
        {
            WeaponController active = m_Weapons.GetActiveWeapon();
            var text = new System.Text.StringBuilder();
            text.Append("<color=#FFD24A><b>TESTE DE ARMAS</b></color>\n");
            for (int i = 0; i < 9; i++)
            {
                WeaponController weapon = m_Weapons.GetWeaponAtSlotIndex(i);
                if (!weapon)
                    continue;
                bool current = weapon == active;
                text.Append(current ? "<color=#7EE070>> " : "<color=#8A96A4>  ");
                text.Append($"{i + 1}  {weapon.WeaponName}</color>\n");
            }

            var icaro = active ? active.GetComponent<IcaroWeapon>() : null;
            WeaponDefinition d = icaro ? icaro.Definition : null;
            if (d)
            {
                text.Append("\n<color=#FFFFFF>");
                text.Append(d.PelletsPerShot > 1
                    ? $"Dano {Num(d.Damage)} x {d.PelletsPerShot} = {Num(d.DamagePerShot)} por disparo\n"
                    : $"Dano {Num(d.Damage)} por bala\n");
                float rpm = 60f / Mathf.Max(0.001f, active.DelayBetweenShots);
                text.Append($"{Num(rpm)} RPM (ficha {Num(d.RoundsPerMinute)}) · pente {active.MaxAmmo}\n");
                text.Append($"Recarga {Num(d.ReloadTime * (PlayerStats.Instance ? PlayerStats.Instance.ReloadTimeMultiplier : 1f))} s");
                text.Append(d.AimMode == WeaponAimMode.None ? " · sem mira assistida" : "");
                text.Append($"\nDPS teórico: {Num(d.DamagePerShot * rpm / 60f)}</color>\n");
            }

            text.Append($"\n<color=#FFB060>DPS ({Num(DpsWindow)} s): <b>{Num(Dps())}</b></color>\n");
            text.Append($"Total {Num(m_Total)} · {m_Hits} acertos · maior {Num(m_Biggest)}\n");
            text.Append("\n<size=85%><color=#8A96A4>1-5 / Q E: armas · R: recarregar\n");
            text.Append($"F1 painel · F2 invulnerável ({(m_Invulnerable ? "ligado" : "desligado")})\n");
            text.Append($"F4 alvos ({(m_Dummies.Count > 0 ? "na frente" : "fora")}) · F5 zerar medidor · F10 ajustar sprite</color></size>");
            return text.ToString();
        }

        static string Num(float value) => value.ToString("0.#", k_Culture);
    }
}
