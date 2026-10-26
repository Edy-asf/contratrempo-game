using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Folha de sprites de um inimigo (gerada por "Ícaro > Sprites > Importar sprites dos inimigos" a partir de
    // <Nome>.png e <Nome>.json em Art/Sprites/Inimigos/<Nome>): uma grade de células iguais, uma linha por clipe e
    // direção (frente, direita, costas, esquerda; a linha 0 é a de cima) e uma coluna por quadro. Formato no
    // LEIAME.md da pasta.
    [CreateAssetMenu(menuName = "Ícaro/Folha de sprites de inimigo")]
    public class EnemySpriteSet : ScriptableObject
    {
        [System.Serializable]
        public class Clip
        {
            public string Name;
            [Tooltip("Primeira linha do clipe (a da frente); as outras três direções vêm logo abaixo")]
            public int Row;
            public int Frames = 1;
            public float Fps = 8f;
            public bool Loop = true;

            public float Duration => Frames / Mathf.Max(0.01f, Fps);
        }

        public Texture2D Atlas;

        [Tooltip("Mapa de brilho (opcional): as partes que brilham na cor cheia e o resto do corpo mais escuro; sem ele, " +
                 "a própria folha")]
        public Texture2D Emission;

        [Tooltip("Quanto do corpo aparece no mapa de brilho (1 sem mapa próprio): as cores de emissão do sprite são " +
                 "divididas por isto, para o brilho mínimo e o piscar valerem igual com ou sem o mapa")]
        public float BodyGlow = 1f;

        [Tooltip("Material da folha (URP Lit com recorte e brilho mínimo), criado pelo importador")]
        public Material Material;

        public int CellWidth = 64, CellHeight = 64;
        public int Columns = 1, Rows = 1;
        public float PixelsPerMeter = 32f;

        [Tooltip("Ponto do chão dentro da célula (pixels a partir do canto de baixo à esquerda)")]
        public Vector2 Pivot = new Vector2(32f, 0f);

        public List<Clip> Clips = new List<Clip>();

        public Clip Find(string name)
        {
            foreach (Clip clip in Clips)
            {
                if (clip.Name == name)
                    return clip;
            }

            return null;
        }

        // Tiling e deslocamento da célula (linha, coluna) para o _BaseMap_ST
        public Vector4 CellST(int row, int column)
        {
            float w = 1f / Columns, h = 1f / Rows;
            return new Vector4(w, h, column * w, (Rows - 1 - row) * h);
        }

        public float WorldWidth => CellWidth / PixelsPerMeter;
        public float WorldHeight => CellHeight / PixelsPerMeter;
    }
}
