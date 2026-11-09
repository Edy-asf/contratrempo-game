using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Icaro
{
    // Fundo das telas que abrem por cima do jogo (escolha de cartas, pausa): um Volume global que desfoca a cena
    // (profundidade de campo gaussiana a partir da câmera), tira parte da cor e escurece um pouco. Quem abre a tela
    // pede o peso (Target, de 0 a 1) e o volume chega lá suave, em tempo sem escala (as telas pausam o jogo); com o
    // peso em 0 o volume fica desligado e não custa nada.
    public class MenuBackdropFx : MonoBehaviour
    {
        static MenuBackdropFx s_Instance;

        const float k_Speed = 7f;

        Volume m_Volume;
        float m_Weight;

        public static float Target
        {
            get => s_Instance ? s_Instance.m_Target : 0f;
            set
            {
                if (!s_Instance && value <= 0f)
                    return;
                Get().m_Target = Mathf.Clamp01(value);
            }
        }

        float m_Target;

        static MenuBackdropFx Get()
        {
            if (s_Instance)
                return s_Instance;
            var go = new GameObject("Volume_FundoDosMenus");
            s_Instance = go.AddComponent<MenuBackdropFx>();
            return s_Instance;
        }

        void Awake()
        {
            m_Volume = gameObject.AddComponent<Volume>();
            m_Volume.isGlobal = true;
            m_Volume.priority = 90f;
            m_Volume.weight = 0f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "FundoDosMenus";

            DepthOfField dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(0f);
            dof.gaussianEnd.Override(1.5f);
            dof.gaussianMaxRadius.Override(1.5f);
            dof.highQualitySampling.Override(true);

            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-45f);
            color.postExposure.Override(-0.35f);

            m_Volume.profile = profile;
            m_Volume.enabled = false;
        }

        void OnDestroy()
        {
            if (m_Volume && m_Volume.profile)
                Destroy(m_Volume.profile);
            if (s_Instance == this)
                s_Instance = null;
        }

        void Update()
        {
            m_Weight = Mathf.MoveTowards(m_Weight, m_Target, Time.unscaledDeltaTime * k_Speed * Mathf.Max(0.15f, Mathf.Abs(m_Target - m_Weight)));
            if (Mathf.Abs(m_Weight - m_Target) < 0.002f)
                m_Weight = m_Target;
            m_Volume.weight = m_Weight;
            m_Volume.enabled = m_Weight > 0.001f;
        }
    }
}
