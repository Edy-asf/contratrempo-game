using UnityEditor;

namespace Icaro.EditorTools
{
    // Caminhos e utilidades comuns aos geradores do Ícaro
    public static class IcaroAssetUtility
    {
        public const string Root = "Assets/Icaro";
        public const string PrefabsFolder = Root + "/Prefabs";
        public const string MaterialsFolder = Root + "/Materials";
        public const string ScenesFolder = Root + "/Scenes";

        // Cria a pasta (e as intermediárias) pelo AssetDatabase, para o Unity gerar os .meta
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
