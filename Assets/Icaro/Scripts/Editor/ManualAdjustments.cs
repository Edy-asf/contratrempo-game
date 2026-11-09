using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Ajustes manuais do cenário gerado: peças do kit que o usuário moveu, girou, escalou, desligou ou apagou no Editor
    // (menu "Ícaro > Ajustes manuais > Salvar ajustes da cena"). A geração aplica tudo de novo
    // (ManualAdjustmentsTool.Apply), então o ajuste fino sobrevive a gerar a cena outra vez. Só Editor.
    public class ManualAdjustments : ScriptableObject
    {
        public const string AssetPath = IcaroAssetUtility.Root + "/Balance/Editor/AjustesManuais.asset";

        [System.Serializable]
        public class Entry
        {
            public string Scope;        // qual gerador ("Picos")
            public string Key;          // GeneratedPiece.Key
            public bool Deleted;        // apagada: não fica na cena
            public bool Hidden;         // desligada: fica, mas inativa
            public Vector3 Position;    // pose no mundo
            public Quaternion Rotation = Quaternion.identity;
            public Vector3 Scale = Vector3.one;   // escala local
            // Peça adicionada à mão (cópia de uma peça ou FBX arrastado do Project): recriada a cada geração
            public bool Added;
            public string Model;        // nome do FBX, sem extensão
            public string Folder;       // pasta do FBX
            public string ParentPath;   // caminho do pai a partir da raiz do cenário ("" = na raiz)
        }

        public List<Entry> Entries = new List<Entry>();

        public static ManualAdjustments Load(bool create)
        {
            var data = AssetDatabase.LoadAssetAtPath<ManualAdjustments>(AssetPath);
            if (data || !create)
                return data;
            IcaroAssetUtility.EnsureFolder(AssetPath.Substring(0, AssetPath.LastIndexOf('/')));
            data = CreateInstance<ManualAdjustments>();
            AssetDatabase.CreateAsset(data, AssetPath);
            return data;
        }

        public Dictionary<string, Entry> ForScope(string scope)
        {
            var map = new Dictionary<string, Entry>();
            foreach (Entry e in Entries)
                if (e.Scope == scope && !string.IsNullOrEmpty(e.Key))
                    map[e.Key] = e;
            return map;
        }
    }
}
