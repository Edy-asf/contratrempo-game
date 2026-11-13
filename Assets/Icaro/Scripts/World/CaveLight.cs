using UnityEngine;

namespace Icaro
{
    // Luz dentro da caverna do portal das Vilas do Alto (montado pelo gerador dos Picos com a planta do
    // caverna_portal.py). No salão a luz é a do portal: a luz ambiente vira um tom ciano escuro (o brilho rebatendo na
    // rocha) e o sol, os reflexos e a névoa escurecem. No túnel, onde a câmera enxerga a boca (raios até ela), entra a
    // luz do dia: o ambiente volta para perto do de fora com um tom laranja do eclipse, mais forte perto da boca (as
    // luzes laranja da subida são do gerador). Cada valor é medido por cima do que os outros sistemas escreveram
    // (BiomeZone, ajustes do céu): se outro script mudou, aquele vira a nova base. A névoa é escurecida pelo
    // TimeAtmosphere, que lê Inside, Fog e FogStrength.
    public class CaveLight : MonoBehaviour
    {
        [Header("Planta (coordenadas do mundo)")]
        public Vector3 HallCenter;
        public Vector2 HallAxis = Vector2.right;    // ao longo do salão, no plano XZ
        public float HallA = 70f, HallB = 52f;
        public float HallFloor = 140f, HallCeiling = 300f;
        public Vector4[] Tunnel = new Vector4[0];    // xyz = ponto do eixo do caminho, w = metros ao longo dele
        public float MouthS = 155f;
        public float TunnelHalf = 7f;

        [Header("Escuro do salão")]
        [Tooltip("Luz ambiente lá dentro (o brilho do portal rebatendo na rocha): alto, meio e baixo")]
        public Color CaveSky = new Color(0.16f, 0.27f, 0.32f);
        public Color CaveEquator = new Color(0.13f, 0.22f, 0.26f);
        public Color CaveGround = new Color(0.09f, 0.14f, 0.17f);
        [Range(0f, 1f)] public float SunInside = 0.05f;
        [Range(0f, 1f)] public float ReflectionInside = 0.35f;
        [Tooltip("Quanto a névoa puxa para FogColor lá dentro")] [Range(0f, 1f)] public float FogAmount = 0.5f;
        public Color FogColor = new Color(0.04f, 0.07f, 0.09f);

        [Header("Luz do dia pela boca")]
        [Tooltip("Metros de túnel, a partir da boca, em que a luz do dia chega inteira")] public float DaylightFull = 4f;
        [Tooltip("Metros de túnel, a partir da boca, em que ela cai até DaylightFar")] public float DaylightReach = 14f;
        [Range(0f, 1f)] public float DaylightFar = 0f;
        [Tooltip("Tom somado ao ambiente com a luz do dia (laranja do eclipse)")]
        public Color DaylightTint = new Color(0.32f, 0.17f, 0.07f);
        [Tooltip("Para conferir a vista da boca: raios por segundo")] public float SightChecks = 10f;

        [Tooltip("Velocidade da adaptação (por segundo)")] public float Response = 2.5f;

        // 0 fora, 1 no escuro do fundo da caverna (lido pelo TimeAtmosphere para a névoa)
        public static float Inside { get; private set; }
        public static Color Fog { get; private set; }
        public static float FogStrength { get; private set; }

        float m_Dark, m_Day, m_AppliedDark = -1f, m_AppliedDay = -1f, m_NextSight;
        bool m_SeesMouth;
        Vector3 m_Mouth;
        Color m_BaseSky, m_BaseEquator, m_BaseGround, m_LastSky, m_LastEquator, m_LastGround;
        float m_BaseSun, m_LastSun = -1f, m_BaseReflection, m_LastReflection = -1f;
        bool m_HasBase;

        void OnEnable()
        {
            m_Mouth = HallCenter;
            float best = float.MaxValue;
            foreach (Vector4 t in Tunnel)
                if (Mathf.Abs(t.w - (MouthS - 1f)) < best)
                {
                    best = Mathf.Abs(t.w - (MouthS - 1f));
                    m_Mouth = t;
                }
        }

        void OnDisable()
        {
            if (m_HasBase)
                Write(0f, 0f, true);
            Inside = 0f;
            m_Dark = m_Day = 0f;
            m_AppliedDark = m_AppliedDay = -1f;
        }

        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (!cam)
                return;
            Vector3 p = cam.transform.position;
            float inside = Target(p, out float tunnelS);
            if (Time.unscaledTime >= m_NextSight)
            {
                m_NextSight = Time.unscaledTime + 1f / Mathf.Max(1f, SightChecks);
                m_SeesMouth = inside > 0f && SeesMouth(p);
            }

            // Luz do dia: só com a boca à vista, mais forte perto dela
            float day = 0f;
            if (m_SeesMouth)
            {
                float fromMouth = tunnelS >= 0f ? MouthS - tunnelS : Vector3.Distance(p, m_Mouth);
                day = Mathf.Lerp(1f, DaylightFar, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DaylightFull, DaylightReach, fromMouth)));
                // O escuro da caverna vai até perto do fim da escadaria: a luz do dia só entra nos últimos metros
            }

            float step = Response * Time.unscaledDeltaTime;
            m_Dark = Mathf.MoveTowards(m_Dark, inside, step);
            m_Day = Mathf.MoveTowards(m_Day, day, step);
            Inside = m_Dark * (1f - m_Day);
            Fog = FogColor;
            FogStrength = FogAmount;
            if (Mathf.Abs(m_Dark - m_AppliedDark) < 0.002f && Mathf.Abs(m_Day - m_AppliedDay) < 0.002f)
            {
                Rebase();
                return;
            }

            Write(m_Dark, m_Day, m_Dark == inside && m_Day == day);
            m_AppliedDark = m_Dark;
            m_AppliedDay = m_Day;
        }

        // A boca está à vista: raio livre até o meio dela ou até um pouco acima do meio
        bool SeesMouth(Vector3 p)
        {
            foreach (float up in new[] { 2.5f, 5f })
            {
                Vector3 d = m_Mouth + Vector3.up * up - p;
                if (d.sqrMagnitude < 1f || !Physics.Raycast(p, d.normalized, d.magnitude - 0.5f, ~0, QueryTriggerInteraction.Ignore))
                    return true;
            }
            return false;
        }

        // Quanto a posição está dentro (o salão inteiro é escuro; no túnel até a boca); 'tunnelS' é o ponto do túnel
        // em que a posição está (-1 fora dele)
        public float Target(Vector3 p, out float tunnelS)
        {
            tunnelS = -1f;
            float best = 0f;
            Vector2 d = new Vector2(p.x - HallCenter.x, p.z - HallCenter.z);
            Vector2 axis = HallAxis.normalized;
            float along = Vector2.Dot(d, axis), across = axis.x * d.y - axis.y * d.x;
            float q = Mathf.Sqrt(along * along / (HallA * HallA) + across * across / (HallB * HallB));
            if (p.y > HallFloor && p.y < HallCeiling)
                best = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.92f, 1.05f, q));

            float nearest = float.MaxValue;
            Vector4 point = default;
            foreach (Vector4 t in Tunnel)
            {
                float dx = p.x - t.x, dz = p.z - t.z, dist = dx * dx + dz * dz;
                if (dist < nearest)
                {
                    nearest = dist;
                    point = t;
                }
            }
            if (nearest < TunnelHalf * TunnelHalf && Mathf.Abs(p.y - point.y) < 14f)
            {
                tunnelS = point.w;
                best = Mathf.Max(best, Mathf.SmoothStep(0f, 1f, (MouthS - point.w) / 6f));
            }
            return best;
        }

        public float Target(Vector3 p) => Target(p, out _);

        // Se outro script escreveu um valor, ele vira a base
        void Rebase()
        {
            if (!m_HasBase || RenderSettings.ambientSkyColor != m_LastSky || RenderSettings.ambientEquatorColor != m_LastEquator ||
                RenderSettings.ambientGroundColor != m_LastGround)
            {
                m_BaseSky = RenderSettings.ambientSkyColor;
                m_BaseEquator = RenderSettings.ambientEquatorColor;
                m_BaseGround = RenderSettings.ambientGroundColor;
                m_LastSky = m_BaseSky;
                m_LastEquator = m_BaseEquator;
                m_LastGround = m_BaseGround;
            }

            Light sun = RenderSettings.sun;
            if (sun && (!m_HasBase || !Mathf.Approximately(sun.intensity, m_LastSun)))
            {
                m_BaseSun = sun.intensity;
                m_LastSun = sun.intensity;
            }

            if (!m_HasBase || !Mathf.Approximately(RenderSettings.reflectionIntensity, m_LastReflection))
            {
                m_BaseReflection = RenderSettings.reflectionIntensity;
                m_LastReflection = m_BaseReflection;
            }

            m_HasBase = true;
        }

        // 'dark': quanto está dentro; 'day': quanto da luz do dia chega (o ambiente volta para perto do de fora, quente)
        void Write(float dark, float day, bool settled)
        {
            Rebase();
            float k = dark * (1f - day);
            Color warm = DaylightTint * (dark * day);
            m_LastSky = RenderSettings.ambientSkyColor = Color.Lerp(m_BaseSky, CaveSky, k) + warm;
            m_LastEquator = RenderSettings.ambientEquatorColor = Color.Lerp(m_BaseEquator, CaveEquator, k) + warm;
            m_LastGround = RenderSettings.ambientGroundColor = Color.Lerp(m_BaseGround, CaveGround, k) + warm * 0.7f;
            BiomeZone.RefreshAmbient(settled);

            // O sol de verdade continua apagado na caverna toda (a rocha não faz sombra além de 40 m)
            Light sun = RenderSettings.sun;
            if (sun)
                m_LastSun = sun.intensity = m_BaseSun * Mathf.Lerp(1f, SunInside, dark);
            m_LastReflection = RenderSettings.reflectionIntensity = m_BaseReflection * Mathf.Lerp(1f, ReflectionInside, k);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.9f, 0.8f);
            Vector3 a = new Vector3(HallAxis.x, 0f, HallAxis.y).normalized, b = new Vector3(-a.z, 0f, a.x);
            for (int i = 0; i < 48; i++)
            {
                float t0 = i / 48f * Mathf.PI * 2f, t1 = (i + 1) / 48f * Mathf.PI * 2f;
                Gizmos.DrawLine(HallCenter + a * Mathf.Cos(t0) * HallA + b * Mathf.Sin(t0) * HallB,
                    HallCenter + a * Mathf.Cos(t1) * HallA + b * Mathf.Sin(t1) * HallB);
            }
            for (int i = 1; i < Tunnel.Length; i++)
                Gizmos.DrawLine(Tunnel[i - 1], Tunnel[i]);
        }
#endif
    }
}
