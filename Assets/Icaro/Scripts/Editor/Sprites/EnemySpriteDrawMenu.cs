using System.IO;
using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // "Ícaro > Sprites > Desenhar inimigos": desenha em código as folhas de sprites dos inimigos do Bosque
    // (EnemySpritePainter), grava em Art/Sprites/Inimigos/<Nome> a folha (<Nome>.png), o mapa de brilho
    // (<Nome>_Brilho.png) e a descrição (<Nome>.json), importa (EnemySpriteImporter) e refaz os prefabs dos
    // inimigos com o visual em sprite. Uma folha trocada à mão (mesmo formato) é sobrescrita por este menu.
    public static class EnemySpriteDrawMenu
    {
        [MenuItem("Ícaro/Sprites/Desenhar inimigos")]
        public static void DrawAll()
        {
            try
            {
                EnemyArt[] arts = EnemySpritePainter.All();
                for (int i = 0; i < arts.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Sprites dos inimigos", arts[i].Key, i / (float)arts.Length);
                    Write(arts[i]);
                }

                AssetDatabase.Refresh();
                EnemySpriteImporter.ImportAll();
                EditorUtility.DisplayProgressBar("Sprites dos inimigos", "Prefabs", 0.95f);
                EnemyPrefabBuilder.BuildAll();
                AssetDatabase.SaveAssets();
                Debug.Log($"[Ícaro] {arts.Length} inimigos desenhados e prefabs refeitos com o sprite.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void Write(EnemyArt art)
        {
            string folder = $"{EnemySpriteImporter.Root}/{art.Key}";
            Directory.CreateDirectory(folder);
            var sheet = new EnemySpriteSheet(art);
            File.WriteAllBytes($"{folder}/{art.Key}.png", ToPng(sheet.Color));
            string emission = art.Key + "_Brilho.png";
            File.WriteAllBytes($"{folder}/{emission}", ToPng(sheet.Emission));
            File.WriteAllText($"{folder}/{art.Key}.json", sheet.Json(emission));
        }

        // A tela de pixels tem (0, 0) em cima; a textura do Unity, embaixo
        static byte[] ToPng(PixelCanvas canvas)
        {
            var texture = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false);
            var pixels = new Color32[canvas.Width * canvas.Height];
            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    Px p = canvas.Get(x, y);
                    pixels[(canvas.Height - 1 - y) * canvas.Width + x] = new Color32(p.R, p.G, p.B, p.A);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }
    }
}
