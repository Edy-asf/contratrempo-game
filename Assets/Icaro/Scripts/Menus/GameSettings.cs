using System;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Icaro
{
    // Configurações do jogador, salvas entre sessões (PlayerPrefs, prefixo "Icaro.Config."). Apply aplica o que é
    // global (volume, qualidade, tela, VSync, limite de FPS, escala de renderização, sombras) e avisa Changed; o que é
    // da cena (sensibilidade, FOV, brilho, música, mira assistida, efeitos de tempo) o GameMenus aplica ao receber o
    // aviso, e os sistemas leem os valores direto (números de dano, marcador de acerto, setas de ameaça, teclas).
    // As escalas de renderização e as sombras mexem numa cópia do pipeline URP feita em jogo, nunca no asset.
    public static class GameSettings
    {
        const string k_Prefix = "Icaro.Config.";
        const string k_Bindings = k_Prefix + "Teclas";

        // ---------- Jogo ----------
        public static bool AimAssist = true;
        public static float AimAssistIntensity = 1f;
        public static bool DamageNumbers = true;
        public static bool HitMarker = true;
        public static bool ThreatIndicators = true;
        public static bool ShowFps;

        public static bool CameraShakeOn
        {
            get => CameraShake.Enabled;
            set => CameraShake.Enabled = value;
        }

        // ---------- Vídeo ----------
        public static int ScreenMode;          // 0 tela cheia, 1 janela sem borda, 2 janela
        public static int ResolutionIndex = -1; // -1 = a atual
        public static int Quality = -1;         // -1 = a do projeto
        public static bool VSync = true;
        public static int FpsLimit = 1;         // índice em FpsLimits
        public static float RenderScale = 1f;
        public static int Shadows = 3;          // 0 desligadas, 1 baixa, 2 média, 3 alta
        public static float Brightness;         // exposição, -1 a +1
        public static float FieldOfView = 70f;
        public static float ScreenEffects = 1f; // vinheta e aberração do multiplicador, 0 a 1

        public static readonly int[] FpsLimits = { 30, 60, 120, 144, 240, -1 };
        public static readonly string[] ScreenModes = { "Tela cheia", "Janela sem borda", "Janela" };
        public static readonly string[] ShadowLevels = { "Desligadas", "Baixas", "Médias", "Altas" };
        static readonly float[] k_ShadowDistances = { 0f, 35f, 70f, 120f };

        // ---------- Áudio ----------
        public static float MasterVolume = 0.8f;
        public static float MusicVolume = 1f;

        // ---------- Controles ----------
        public static float MouseSensitivity = 1f;
        public static bool InvertY;
        public static Key DashKey = Key.F;
        public static Key ShopKey = Key.G;

        // Ações do Microgame que podem ter a tecla trocada (nome mostrado, caminho da ação)
        public static readonly (string Label, string Action)[] RebindableActions =
        {
            ("Atirar", "Player/Fire"),
            ("Mirar", "Player/Aim"),
            ("Pular", "Player/Jump"),
            ("Correr", "Player/Sprint"),
            ("Agachar", "Player/Crouch"),
            ("Recarregar", "Player/Reload"),
        };

        public static event Action Changed;

        static bool s_Loaded;
        static RenderPipelineAsset s_OriginalPipeline;
        static UniversalRenderPipelineAsset s_RuntimePipeline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            // Play sem recarregar o domínio: estes valores sobrevivem entre um Play e outro
            s_Loaded = false;
            s_RuntimePipeline = null;
            Application.quitting -= RestorePipeline;
            Application.quitting += RestorePipeline;
            Load();
            Apply();
        }

        public static void Load()
        {
            s_Loaded = true;
            AimAssist = GetBool("MiraAssistida", AimAssist);
            AimAssistIntensity = PlayerPrefs.GetFloat(k_Prefix + "MiraIntensidade", AimAssistIntensity);
            DamageNumbers = GetBool("NumerosDano", DamageNumbers);
            HitMarker = GetBool("MarcadorAcerto", HitMarker);
            ThreatIndicators = GetBool("SetasAmeaca", ThreatIndicators);
            ShowFps = GetBool("MostrarFps", ShowFps);

            ScreenMode = PlayerPrefs.GetInt(k_Prefix + "ModoTela", ScreenMode);
            ResolutionIndex = PlayerPrefs.GetInt(k_Prefix + "Resolucao", ResolutionIndex);
            Quality = PlayerPrefs.GetInt(k_Prefix + "Qualidade", Quality);
            VSync = GetBool("VSync", VSync);
            FpsLimit = PlayerPrefs.GetInt(k_Prefix + "LimiteFps", FpsLimit);
            RenderScale = PlayerPrefs.GetFloat(k_Prefix + "EscalaRender", RenderScale);
            Shadows = PlayerPrefs.GetInt(k_Prefix + "Sombras", Shadows);
            Brightness = PlayerPrefs.GetFloat(k_Prefix + "Brilho", Brightness);
            FieldOfView = PlayerPrefs.GetFloat(k_Prefix + "Fov", FieldOfView);
            ScreenEffects = PlayerPrefs.GetFloat(k_Prefix + "EfeitosTela", ScreenEffects);

            MasterVolume = PlayerPrefs.GetFloat(k_Prefix + "VolumeGeral", MasterVolume);
            MusicVolume = PlayerPrefs.GetFloat(k_Prefix + "VolumeMusica", MusicVolume);

            MouseSensitivity = PlayerPrefs.GetFloat(k_Prefix + "Sensibilidade", MouseSensitivity);
            InvertY = GetBool("InverterY", InvertY);
            DashKey = (Key)PlayerPrefs.GetInt(k_Prefix + "TeclaDash", (int)DashKey);
            ShopKey = (Key)PlayerPrefs.GetInt(k_Prefix + "TeclaLoja", (int)ShopKey);

            string bindings = PlayerPrefs.GetString(k_Bindings, "");
            if (!string.IsNullOrEmpty(bindings) && InputSystem.actions)
                InputSystem.actions.LoadBindingOverridesFromJson(bindings);
        }

        public static void Save()
        {
            SetBool("MiraAssistida", AimAssist);
            PlayerPrefs.SetFloat(k_Prefix + "MiraIntensidade", AimAssistIntensity);
            SetBool("NumerosDano", DamageNumbers);
            SetBool("MarcadorAcerto", HitMarker);
            SetBool("SetasAmeaca", ThreatIndicators);
            SetBool("MostrarFps", ShowFps);

            PlayerPrefs.SetInt(k_Prefix + "ModoTela", ScreenMode);
            PlayerPrefs.SetInt(k_Prefix + "Resolucao", ResolutionIndex);
            PlayerPrefs.SetInt(k_Prefix + "Qualidade", Quality);
            SetBool("VSync", VSync);
            PlayerPrefs.SetInt(k_Prefix + "LimiteFps", FpsLimit);
            PlayerPrefs.SetFloat(k_Prefix + "EscalaRender", RenderScale);
            PlayerPrefs.SetInt(k_Prefix + "Sombras", Shadows);
            PlayerPrefs.SetFloat(k_Prefix + "Brilho", Brightness);
            PlayerPrefs.SetFloat(k_Prefix + "Fov", FieldOfView);
            PlayerPrefs.SetFloat(k_Prefix + "EfeitosTela", ScreenEffects);

            PlayerPrefs.SetFloat(k_Prefix + "VolumeGeral", MasterVolume);
            PlayerPrefs.SetFloat(k_Prefix + "VolumeMusica", MusicVolume);

            PlayerPrefs.SetFloat(k_Prefix + "Sensibilidade", MouseSensitivity);
            SetBool("InverterY", InvertY);
            PlayerPrefs.SetInt(k_Prefix + "TeclaDash", (int)DashKey);
            PlayerPrefs.SetInt(k_Prefix + "TeclaLoja", (int)ShopKey);
            if (InputSystem.actions)
                PlayerPrefs.SetString(k_Bindings, InputSystem.actions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        // Aplica o que é global e avisa quem cuida do resto
        public static void Apply()
        {
            if (!s_Loaded)
                Load();

            // O AudioUtility do Microgame precisa do AudioManager da cena (antes da cena carregar, ainda não existe)
            if (UnityEngine.Object.FindAnyObjectByType<AudioManager>())
                AudioUtility.SetMasterVolume(MasterVolume);

            if (Quality >= 0 && Quality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != Quality)
                QualitySettings.SetQualityLevel(Quality, true);

            QualitySettings.vSyncCount = VSync ? 1 : 0;
            int limit = FpsLimits[Mathf.Clamp(FpsLimit, 0, FpsLimits.Length - 1)];
            Application.targetFrameRate = VSync ? -1 : limit;

#if !UNITY_EDITOR
            ApplyScreen();
#endif
            ApplyPipeline();
            Changed?.Invoke();
        }

        // Muda e salva: atalho para os controles do menu
        public static void Commit()
        {
            Save();
            Apply();
        }

        public static void ResetBindings()
        {
            if (InputSystem.actions)
                InputSystem.actions.RemoveAllBindingOverrides();
            DashKey = Key.F;
            ShopKey = Key.G;
            Commit();
        }

        public static Resolution[] Resolutions => Screen.resolutions;

        static void ApplyScreen()
        {
            FullScreenMode mode = ScreenMode switch
            {
                0 => FullScreenMode.ExclusiveFullScreen,
                1 => FullScreenMode.FullScreenWindow,
                _ => FullScreenMode.Windowed,
            };
            Resolution[] resolutions = Screen.resolutions;
            if (ResolutionIndex >= 0 && ResolutionIndex < resolutions.Length)
            {
                Resolution r = resolutions[ResolutionIndex];
                Screen.SetResolution(r.width, r.height, mode, r.refreshRateRatio);
            }
            else
            {
                Screen.fullScreenMode = mode;
            }
        }

        // Escala de renderização e sombras numa cópia do pipeline (o asset do projeto não muda)
        static void ApplyPipeline()
        {
            RenderPipelineAsset current = QualitySettings.renderPipeline ? QualitySettings.renderPipeline
                : GraphicsSettings.defaultRenderPipeline;
            if (current != s_RuntimePipeline)
            {
                s_OriginalPipeline = current;
                s_RuntimePipeline = current is UniversalRenderPipelineAsset urp
                    ? UnityEngine.Object.Instantiate(urp)
                    : null;
                if (!s_RuntimePipeline)
                    return;
                s_RuntimePipeline.name = current.name + " (Ícaro)";
                QualitySettings.renderPipeline = s_RuntimePipeline;
            }

            if (!s_RuntimePipeline)
                return;
            s_RuntimePipeline.renderScale = Mathf.Clamp(RenderScale, 0.5f, 1f);
            s_RuntimePipeline.shadowDistance = k_ShadowDistances[Mathf.Clamp(Shadows, 0, k_ShadowDistances.Length - 1)];
        }

        // Volta o pipeline original (no Editor, ao sair do Play, a cópia não pode ficar nas configurações do projeto)
        static void RestorePipeline()
        {
            if (s_RuntimePipeline && QualitySettings.renderPipeline == s_RuntimePipeline)
                QualitySettings.renderPipeline = s_OriginalPipeline == GraphicsSettings.defaultRenderPipeline
                    ? null
                    : s_OriginalPipeline;
            s_RuntimePipeline = null;
        }

        static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(k_Prefix + key, fallback ? 1 : 0) == 1;
        static void SetBool(string key, bool value) => PlayerPrefs.SetInt(k_Prefix + key, value ? 1 : 0);

        // Nome legível de uma tecla ou botão
        public static string KeyName(Key key) => key.ToString().ToUpperInvariant();
    }
}
