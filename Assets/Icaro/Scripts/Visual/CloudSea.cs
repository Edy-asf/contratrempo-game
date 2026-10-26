using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Icaro
{
    // Mar de nuvens de volume (Picos acima das Nuvens): uma caixa enorme vista por dentro onde o shader Icaro/CloudVolume
    // percorre o ar da camada de nuvem (de Bottom a Top) e acumula fumaça de ruído 3D iluminada pelo eclipse, parando no
    // cenário pela profundidade da cena (os picos atravessam a camada e somem dentro dela). Os fiapos (Icaro/CloudWisp)
    // usam o mesmo relógio. O relógio das nuvens segue o tempo de Kairós: a 1,0x fica parado e anda (multiplicador - 1)
    // segundos por segundo (Speed); no futuro as zonas sem inimigos devem congelar de vez. Liga a textura de
    // profundidade na câmera principal (os dois shaders precisam dela) e publica as texturas de ruído (CloudNoise) e o
    // relógio como globais (_IcaroCloudNoise, _IcaroCloudPuff, _IcaroCloudTime).
    // Roda também fora do Play, para o volume aparecer na Scene (parado).
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CloudSea : MonoBehaviour
    {
        [Tooltip("Base da camada de nuvem (m, mundo)")]
        public float Bottom = -260f;

        [Tooltip("Topo dos montes de nuvem mais altos (m, mundo)")]
        public float Top = -72f;

        [Tooltip("Meia largura da caixa em volta do objeto (m). Fica abaixo do corte da câmera mesmo na diagonal.")]
        public float Extent = 2000f;

        [Tooltip("Segundos do relógio das nuvens por segundo, por 1,0x acima de 1,0x")]
        public float Speed = 1f;

        static readonly int s_Noise = Shader.PropertyToID("_IcaroCloudNoise");
        static readonly int s_Puff = Shader.PropertyToID("_IcaroCloudPuff");
        static readonly int s_Time = Shader.PropertyToID("_IcaroCloudTime");
        static readonly int s_BottomId = Shader.PropertyToID("_CloudBottom");
        static readonly int s_TopId = Shader.PropertyToID("_CloudTop");

        float m_Clock;
        Camera m_DepthCamera;

        void OnEnable()
        {
            Shader.SetGlobalTexture(s_Noise, CloudNoise.Volume);
            Shader.SetGlobalTexture(s_Puff, CloudNoise.Puff);
            Shader.SetGlobalFloat(s_Time, m_Clock);

            var filter = GetComponent<MeshFilter>();
            filter.sharedMesh = BuildBox();
            var block = new MaterialPropertyBlock();
            block.SetFloat(s_BottomId, Bottom);
            block.SetFloat(s_TopId, Top);
            GetComponent<MeshRenderer>().SetPropertyBlock(block);
        }

        void OnDisable()
        {
            var filter = GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh)
                return;
            if (Application.isPlaying)
                Destroy(filter.sharedMesh);
            else
                DestroyImmediate(filter.sharedMesh);
            filter.sharedMesh = null;
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;
            float rate = Mathf.Max(0f, TimeManager.CurrentMultiplier - 1f) * Speed;
            m_Clock += Time.deltaTime * rate;
            Shader.SetGlobalFloat(s_Time, m_Clock);
            EnsureDepth();
        }

        // Os shaders param a fumaça no cenário e suavizam os fiapos pela profundidade da cena
        void EnsureDepth()
        {
            Camera camera = Camera.main;
            if (!camera || camera == m_DepthCamera)
                return;
            m_DepthCamera = camera;
            camera.depthTextureMode |= DepthTextureMode.Depth;
            if (camera.TryGetComponent(out UniversalAdditionalCameraData data))
                data.requiresDepthOption = CameraOverrideOption.On;
        }

        // Caixa vista por dentro (o shader decide onde há nuvem), da base da camada até bem acima da câmera, para os
        // raios rasantes do horizonte também passarem por ela
        Mesh BuildBox()
        {
            Vector3 c = transform.position;
            var min = new Vector3(-Extent, Bottom - 10f - c.y, -Extent);
            var max = new Vector3(Extent, Top + 800f - c.y, Extent);
            var vertices = new Vector3[8];
            for (int i = 0; i < 8; i++)
                vertices[i] = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
            int[] triangles =
            {
                0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5,
            };
            var mesh = new Mesh { name = "Nuvens_Caixa", vertices = vertices, triangles = triangles, hideFlags = HideFlags.DontSave };
            mesh.bounds = new Bounds((min + max) * 0.5f, max - min);
            return mesh;
        }
    }
}
