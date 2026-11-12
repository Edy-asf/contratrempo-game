using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Icaro
{
    // Leva a run de uma cena para outra (portal do Arquipélago → chegada dos Picos): guarda o que vive no jogador
    // (itens, armas e a da mão, vida, Cronitas, fichas, melhorias da loja e o relatório da run), carrega a cena com um
    // clarão que atravessa a troca e, na chegada, devolve tudo ao jogador novo. A cena onde a run começou fica guardada
    // (HomeScenePath): recomeçar ou voltar ao menu volta para ela, como a morte sempre volta ao início da Via.
    public class RunCarry : MonoBehaviour
    {
        class Snapshot
        {
            public readonly List<(ItemDefinition Item, int Count)> Items = new List<(ItemDefinition, int)>();
            public readonly List<WeaponDefinition> Weapons = new List<WeaponDefinition>();
            public WeaponDefinition ActiveWeapon;
            public float HealthRatio = 1f;
            public int Cronitas, RerollTokens, DamageUpgrades, FireRateUpgrades;
            public RunStats.Totals Stats;
        }

        // Chegando de um portal: o menu de título e a abertura não aparecem
        public static bool Arriving { get; private set; }

        // Cena onde a run começou (vazio = a run nunca trocou de cena)
        public static string HomeScenePath { get; private set; } = "";

        static RunCarry s_Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Arriving = false;
            HomeScenePath = "";
            s_Instance = null;
        }

        Snapshot m_Snapshot;
        string m_ScenePath;
        string m_Title, m_Subtitle;
        Image m_Flash;
        Volume m_Volume;
        VolumeProfile m_Profile;
        Color m_Tint = new Color(0.85f, 1f, 0.98f, 0f);

        public static bool Traveling => s_Instance;

        // Guarda a run e troca para a cena (caminho a partir de Assets/), com o título da chegada na faixa do visor
        public static void Travel(string scenePath, string arrivalTitle, string arrivalSubtitle)
        {
            if (s_Instance || string.IsNullOrEmpty(scenePath))
                return;
            var go = new GameObject("Travessia_Cena");
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<RunCarry>();
            s_Instance.m_ScenePath = scenePath;
            s_Instance.m_Title = arrivalTitle;
            s_Instance.m_Subtitle = arrivalSubtitle;
            s_Instance.StartCoroutine(s_Instance.Run());
        }

        // A cena para onde recomeçar a run (a de início, se a run trocou de cena) e esquece a viagem
        public static string TakeHomeScene(string current)
        {
            string home = string.IsNullOrEmpty(HomeScenePath) ? current : HomeScenePath;
            HomeScenePath = "";
            return home;
        }

        // Carrega uma cena pelo caminho; no Editor, também as que não estão nas Build Settings
        public static void LoadScene(string path)
        {
            int index = SceneUtility.GetBuildIndexByScenePath(path);
#if UNITY_EDITOR
            if (index < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,
                    new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            if (index < 0)
            {
                Debug.LogError($"[Ícaro] A cena {path} não está nas Build Settings (Ícaro > Usar a Via como cena inicial).");
                return;
            }

            SceneManager.LoadScene(index);
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
            if (m_Profile)
                Destroy(m_Profile);
        }

        IEnumerator Run()
        {
            CreateOverlay();

            // Clarão e distorção do tempo subindo (como o portal quebrado do Bastião)
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                SetFade(t / 0.6f);
                yield return null;
            }

            SetFade(1f);
            m_Snapshot = Capture();
            if (string.IsNullOrEmpty(HomeScenePath))
                HomeScenePath = SceneManager.GetActiveScene().path;

            Arriving = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            bool loaded = false;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onLoaded = (s, m) => loaded = true;
            SceneManager.sceneLoaded += onLoaded;
            LoadScene(m_ScenePath);
            for (float t = 0f; !loaded && t < 30f; t += Time.unscaledDeltaTime)
                yield return null;
            SceneManager.sceneLoaded -= onLoaded;

            // O jogador novo monta as armas iniciais no Start: espera ele ficar pronto antes de devolver a run
            for (int i = 0; i < 300 && (!PlayerStats.Instance || !PlayerArsenal.Instance); i++)
                yield return null;
            yield return null;
            yield return null;
            Restore(m_Snapshot);
            Arriving = false;

            yield return new WaitForSecondsRealtime(0.25f);
            if (!string.IsNullOrEmpty(m_Title))
                VisorBanner.Show(m_Title, m_Subtitle, BannerTone.Normal);

            for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
            {
                SetFade(1f - t / 1.2f);
                yield return null;
            }

            Destroy(gameObject);
        }

        void CreateOverlay()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            m_Flash = new GameObject("Clarao").AddComponent<Image>();
            m_Flash.transform.SetParent(transform, false);
            m_Flash.rectTransform.anchorMin = Vector2.zero;
            m_Flash.rectTransform.anchorMax = Vector2.one;
            m_Flash.rectTransform.offsetMin = m_Flash.rectTransform.offsetMax = Vector2.zero;
            m_Flash.raycastTarget = false;
            m_Flash.color = m_Tint;

            m_Volume = gameObject.AddComponent<Volume>();
            m_Volume.isGlobal = true;
            m_Volume.priority = 200f;
            m_Volume.weight = 0f;
            m_Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            m_Profile.Add<LensDistortion>(true).intensity.Override(-0.75f);
            m_Profile.Add<ChromaticAberration>(true).intensity.Override(1f);
            m_Volume.profile = m_Profile;
        }

        void SetFade(float k)
        {
            k = Mathf.Clamp01(k);
            m_Volume.weight = k;
            m_Tint.a = k * k;
            m_Flash.color = m_Tint;
        }

        // ---------- guardar e devolver ----------

        static Snapshot Capture()
        {
            var snapshot = new Snapshot();
            PlayerStats stats = PlayerStats.Instance;
            if (stats)
            {
                foreach (PlayerStats.ItemStack stack in stats.Items)
                {
                    if (stack.Item && stack.Count > 0)
                        snapshot.Items.Add((stack.Item, stack.Count));
                }

                var health = stats.GetComponent<Health>();
                if (health && health.MaxHealth > 0f)
                    snapshot.HealthRatio = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);

                var weapons = stats.GetComponent<PlayerWeaponsManager>();
                for (int i = 0; weapons && i < 9; i++)
                {
                    WeaponController weapon = weapons.GetWeaponAtSlotIndex(i);
                    var icaro = weapon ? weapon.GetComponent<IcaroWeapon>() : null;
                    if (icaro && icaro.Definition)
                        snapshot.Weapons.Add(icaro.Definition);
                }

                WeaponController active = weapons ? weapons.GetActiveWeapon() : null;
                var activeIcaro = active ? active.GetComponent<IcaroWeapon>() : null;
                snapshot.ActiveWeapon = activeIcaro ? activeIcaro.Definition : null;
            }

            PlayerWallet wallet = PlayerWallet.Instance;
            if (wallet)
            {
                snapshot.Cronitas = wallet.Cronitas;
                snapshot.RerollTokens = wallet.RerollTokens;
                snapshot.DamageUpgrades = wallet.DamageUpgrades;
                snapshot.FireRateUpgrades = wallet.FireRateUpgrades;
            }

            if (RunStats.Instance)
                snapshot.Stats = RunStats.Instance.Save();
            return snapshot;
        }

        static void Restore(Snapshot snapshot)
        {
            if (snapshot == null)
                return;

            // A carteira primeiro: as melhorias da loja entram no cálculo dos itens
            PlayerWallet wallet = PlayerWallet.Instance;
            if (wallet)
                wallet.Restore(snapshot.Cronitas, snapshot.RerollTokens, snapshot.DamageUpgrades,
                    snapshot.FireRateUpgrades);

            PlayerStats stats = PlayerStats.Instance;
            if (stats)
            {
                foreach ((ItemDefinition item, int count) in snapshot.Items)
                {
                    for (int i = 0; i < count; i++)
                        stats.AddItem(item);
                }

                stats.Recalculate();
                RestoreWeapons(stats.GetComponent<PlayerWeaponsManager>(), snapshot);

                var health = stats.GetComponent<Health>();
                if (health)
                    health.CurrentHealth = Mathf.Max(1f, health.MaxHealth * snapshot.HealthRatio);
            }

            if (RunStats.Instance && snapshot.Stats != null)
                RunStats.Instance.Load(snapshot.Stats);
        }

        static void RestoreWeapons(PlayerWeaponsManager weapons, Snapshot snapshot)
        {
            PlayerArsenal arsenal = PlayerArsenal.Instance;
            if (!weapons || !arsenal || snapshot.Weapons.Count == 0)
                return;

            // Tira as iniciais que o jogador não carregava mais (trocadas na loja) e dá as compradas
            for (int i = 0; i < 9; i++)
            {
                WeaponController weapon = weapons.GetWeaponAtSlotIndex(i);
                var icaro = weapon ? weapon.GetComponent<IcaroWeapon>() : null;
                if (weapon && (!icaro || !snapshot.Weapons.Contains(icaro.Definition)))
                    weapons.RemoveWeapon(weapon);
            }

            foreach (WeaponDefinition definition in snapshot.Weapons)
            {
                if (!arsenal.Owns(definition))
                    arsenal.Give(definition);
            }

            for (int i = 0; snapshot.ActiveWeapon && i < 9; i++)
            {
                WeaponController weapon = weapons.GetWeaponAtSlotIndex(i);
                var icaro = weapon ? weapon.GetComponent<IcaroWeapon>() : null;
                if (icaro && icaro.Definition == snapshot.ActiveWeapon)
                {
                    weapons.SwitchToWeaponIndex(i);
                    break;
                }
            }
        }
    }
}
