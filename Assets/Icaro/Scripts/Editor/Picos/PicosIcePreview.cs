using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Prévia do gelo dos Picos (menu "Ícaro > Picos > Prévia do gelo"): refaz os dois materiais da água congelada
    // (PicosTextureKit.IceMaterials), monta numa cena vazia, sem salvar, uma parede de 140 m com tubos e um ressalto (gelo
    // da cascata) e um chão de 220 m (gelo do rio), a 5 km da origem como as Vilas do Alto, renderiza cinco vistas em
    // Prints/Gelo_*.png e escreve no Console os erros do shader Icaro/FrozenIce. Também roda sem o Editor aberto:
    // Unity.exe -batchmode -projectPath <projeto> -executeMethod Icaro.EditorTools.PicosIcePreview.Run -quit
    public static class PicosIcePreview
    {
        static readonly Vector3 k_Origin = new Vector3(0f, 0f, -5000f);

        [MenuItem("Ícaro/Picos/Prévia do gelo")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Run();
        }

        public static void Run()
        {
            ShaderUtil.allowAsyncCompilation = false;
            PicosTextureKit.IceMaterials(out Material fall, out Material sheet);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.27f, 0.4f);
            RenderSettings.fog = false;

            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.62f, 0.38f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(22f, 28f, 0f);

            Block(fall, new Vector3(0f, 70f, 31f), new Vector3(70f, 140f, 2f));
            Block(fall, new Vector3(0f, 50f, 28.5f), new Vector3(60f, 1f, 3f));
            foreach ((float x, float r) in new[] { (-22f, 3.5f), (-12f, 2.2f), (-4f, 4.2f), (7f, 2.8f), (16f, 3.6f), (26f, 2f) })
            {
                var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pipe.transform.position = k_Origin + new Vector3(x, 70f, 30f - r * 0.4f);
                pipe.transform.localScale = new Vector3(r * 2f, 70f, r * 2f);
                pipe.GetComponent<Renderer>().sharedMaterial = fall;
            }

            Block(sheet, new Vector3(0f, -0.5f, -60f), new Vector3(220f, 1f, 180f));

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.1f, 0.2f);
            camera.fieldOfView = 70f;
            camera.farClipPlane = 2000f;

            // Um quadro antes, para as texturas e o shader já estarem prontos na primeira vista
            Shot(camera, null, new Vector3(0f, 60f, -85f), new Vector3(0f, 70f, 30f));
            Shot(camera, "Gelo_Cascata_Longe", new Vector3(0f, 60f, -85f), new Vector3(0f, 70f, 30f));
            Shot(camera, "Gelo_Cascata_Perto", new Vector3(6f, 22f, 14f), new Vector3(0f, 24f, 28f));
            Shot(camera, "Gelo_Rio_Longe", new Vector3(0f, 12f, -140f), new Vector3(0f, 0f, -60f));
            Shot(camera, "Gelo_Rio_Perto", new Vector3(0f, 2.2f, -100f), new Vector3(0f, 0f, -92f));
            Shot(camera, "Gelo_Rio_Alto", new Vector3(0f, 90f, -61f), new Vector3(0f, 0f, -60f));

            int problems = 0;
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(fall.shader))
            {
                problems++;
                Debug.LogWarning($"[Ícaro] Icaro/FrozenIce ({message.platform}, linha {message.line}): {message.message}");
            }

            Debug.Log($"[Ícaro] Prévia do gelo em Prints/Gelo_*.png; shader com {problems} mensagem(ns)" +
                      (ShaderUtil.ShaderHasError(fall.shader) ? ", COM ERRO." : "."));
        }

        static void Block(Material material, Vector3 center, Vector3 size)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.position = k_Origin + center;
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void Shot(Camera camera, string name, Vector3 eye, Vector3 target)
        {
            camera.transform.position = k_Origin + eye;
            camera.transform.LookAt(k_Origin + target);
            var texture = new RenderTexture(1280, 720, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;

            if (name == null)
            {
                Object.DestroyImmediate(image);
                texture.Release();
                Object.DestroyImmediate(texture);
                return;
            }

            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Prints");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            Object.DestroyImmediate(image);
            texture.Release();
            Object.DestroyImmediate(texture);
        }
    }
}
