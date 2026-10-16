using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Icaro
{
    // Fontes em pixel das cartas (Pixel Operator, CC0, em Resources/Fonts): o asset do TextMesh Pro é criado em jogo
    // na primeira vez, com atlas dinâmico (os acentos entram sob demanda). Sem a fonte, fica a padrão do TMP.
    public static class ItemFonts
    {
        static TMP_FontAsset s_Bold;
        static TMP_FontAsset s_Regular;
        static bool s_Loaded;

        public static TMP_FontAsset Bold
        {
            get
            {
                Load();
                return s_Bold;
            }
        }

        public static TMP_FontAsset Regular
        {
            get
            {
                Load();
                return s_Regular;
            }
        }

        static void Load()
        {
            if (s_Loaded && s_Bold && s_Regular)
                return;
            s_Loaded = true;
            s_Bold = Create("Fonts/PixelOperator-Bold");
            s_Regular = Create("Fonts/PixelOperator");
            if (!s_Bold)
                s_Bold = s_Regular;
            if (!s_Regular)
                s_Regular = s_Bold;
        }

        static TMP_FontAsset Create(string path)
        {
            var font = Resources.Load<Font>(path);
            if (!font)
                return null;
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 48, 5, GlyphRenderMode.SDFAA, 512, 512,
                AtlasPopulationMode.Dynamic);
            if (asset)
                asset.name = font.name + " (cartas)";
            return asset;
        }
    }
}
