using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Ajuste fino à mão do cenário gerado.
    // Na geração: Begin(escopo) no começo, Register em cada peça do kit (PicosGothicKit.Place) e Apply(raiz) depois de
    // tudo posto e antes das salas (NavMesh): cada peça ganha a marca GeneratedPiece com a chave (modelo + posição
    // gerada em passos de 5 cm, + "#n" quando repete) e recebe o ajuste salvo. Apply não muda nenhum sorteio, então as
    // outras peças nascem iguais.
    // No Editor: mover, girar, escalar, desligar ou apagar as peças e rodar "Salvar ajustes da cena".
    public static class ManualAdjustmentsTool
    {
        static string s_Scope;
        static List<(GameObject Go, string Model)> s_Pending;

        public static void Begin(string scope)
        {
            s_Scope = scope;
            s_Pending = new List<(GameObject, string)>();
        }

        public static void End()
        {
            s_Scope = null;
            s_Pending = null;
        }

        // Só as peças do kit (AR_ e PR_) destas pastas contam como adicionáveis; montanhas e rochas do Blender ficam de fora
        static readonly string[] k_KitFolders =
        {
            IcaroAssetUtility.Root + "/Models/Picos/BairroVoador/",
            IcaroAssetUtility.Root + "/Models/Picos/VilasDoAlto/",
            IcaroAssetUtility.Root + "/Models/Picos/PonteDoReino/",
        };

        static bool s_Adding;

        public static void Register(GameObject go, string model)
        {
            if (s_Scope != null && go && !s_Adding)
                s_Pending.Add((go, model));
        }

        static int Step(float v) => Mathf.RoundToInt(v * 20f);

        // 'place' põe uma peça do kit (modelo, pasta, pai) com os materiais do bioma; a pose vem depois
        public static void Apply(Transform root, System.Func<string, string, Transform, GameObject> place)
        {
            if (s_Scope == null)
                return;

            var counts = new Dictionary<string, int>();
            var pieces = new List<GeneratedPiece>();
            foreach (var (go, model) in s_Pending)
            {
                if (!go)
                    continue;
                Transform t = go.transform;
                Vector3 p = t.position;
                string baseKey = $"{model}@{Step(p.x)},{Step(p.y)},{Step(p.z)}";
                counts.TryGetValue(baseKey, out int n);
                counts[baseKey] = n + 1;
                var piece = go.AddComponent<GeneratedPiece>();
                piece.Key = n == 0 ? baseKey : $"{baseKey}#{n}";
                piece.GeneratedPosition = p;
                piece.GeneratedRotation = t.rotation;
                piece.GeneratedScale = t.localScale;
                pieces.Add(piece);
            }

            var index = root.gameObject.AddComponent<GeneratedPieceIndex>();
            index.Scope = s_Scope;
            index.Keys = pieces.Select(x => x.Key).ToList();

            int moved = 0, hidden = 0, deleted = 0, missing = 0, added = 0;
            ManualAdjustments data = ManualAdjustments.Load(false);
            if (data)
            {
                Dictionary<string, ManualAdjustments.Entry> map = data.ForScope(s_Scope);
                var used = new HashSet<string>();
                var toDelete = new List<GameObject>();
                // Pais antes dos filhos: o filho sem ajuste acompanha o pai, o com ajuste fica onde foi salvo
                foreach (GeneratedPiece piece in pieces.OrderBy(Depth))
                {
                    if (!map.TryGetValue(piece.Key, out ManualAdjustments.Entry e))
                        continue;
                    used.Add(piece.Key);
                    if (e.Deleted)
                    {
                        toDelete.Add(piece.gameObject);
                        continue;
                    }
                    piece.transform.SetPositionAndRotation(e.Position, e.Rotation);
                    piece.transform.localScale = e.Scale;
                    moved++;
                    if (e.Hidden)
                    {
                        piece.gameObject.SetActive(false);
                        hidden++;
                    }
                }

                foreach (GameObject go in toDelete)
                    if (go)
                    {
                        // A chave fica no índice: o salvamento vê que ela falta e a mantém apagada
                        Object.DestroyImmediate(go);
                        deleted++;
                    }

                // Peças adicionadas à mão: recriadas com os materiais do bioma e a colisão, no pai de antes
                s_Adding = true;
                try
                {
                    foreach (ManualAdjustments.Entry e in map.Values)
                    {
                        if (!e.Added)
                            continue;
                        used.Add(e.Key);
                        Transform parent = string.IsNullOrEmpty(e.ParentPath) ? root : root.Find(e.ParentPath) ?? root;
                        GameObject go = place(e.Model, e.Folder, parent);
                        if (!go)
                        {
                            missing++;
                            continue;
                        }
                        go.transform.SetPositionAndRotation(e.Position, e.Rotation);
                        go.transform.localScale = e.Scale;
                        var piece = go.AddComponent<GeneratedPiece>();
                        piece.Key = e.Key;
                        piece.GeneratedPosition = e.Position;
                        piece.GeneratedRotation = e.Rotation;
                        piece.GeneratedScale = e.Scale;
                        if (e.Hidden)
                            go.SetActive(false);
                        added++;
                    }
                }
                finally
                {
                    s_Adding = false;
                }

                foreach (var pair in map)
                    if (!used.Contains(pair.Key))
                    {
                        missing++;
                        Debug.LogWarning($"[Ícaro] Ajuste manual sem peça ({s_Scope}): {pair.Key}. O código mudou e ela nasce em " +
                            "outro lugar ou não nasce mais; ajuste de novo e salve (o ajuste antigo fica guardado até lá).");
                    }
            }

            Physics.SyncTransforms();
            Debug.Log($"[Ícaro] Ajustes manuais ({s_Scope}): {pieces.Count} peças marcadas; {moved} ajustadas, {hidden} desligadas, " +
                $"{deleted} apagadas, {added} adicionadas, {missing} ajustes sem peça.");
        }

        static int Depth(GeneratedPiece p)
        {
            int d = 0;
            for (Transform t = p.transform; t; t = t.parent)
                d++;
            return d;
        }

        // ---------- menus ----------

        [MenuItem("Ícaro/Ajustes manuais/Salvar ajustes da cena", priority = 200)]
        static void Save()
        {
            GeneratedPieceIndex[] indices = Object.FindObjectsByType<GeneratedPieceIndex>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (indices.Length == 0)
            {
                EditorUtility.DisplayDialog("Ajustes manuais", "Esta cena não tem peças marcadas. Gere a cena de novo (a marca " +
                    "entrou agora) e depois ajuste as peças.", "OK");
                return;
            }

            // Cópias (Ctrl+D, Ctrl+C/V) levam a marca da original: a mais perto da pose gerada continua a original, as
            // outras viram peças adicionadas com chave própria
            var present = new Dictionary<string, GeneratedPiece>();
            var addedPieces = new List<GeneratedPiece>();
            foreach (var group in Object.FindObjectsByType<GeneratedPiece>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(p => !string.IsNullOrEmpty(p.Key)).GroupBy(p => p.Key))
            {
                var list = group.OrderBy(p => (p.transform.position - p.GeneratedPosition).sqrMagnitude).ToList();
                if (group.Key.StartsWith("+"))
                    addedPieces.Add(list[0]);
                else
                    present[group.Key] = list[0];
                list.RemoveAt(0);
                foreach (GeneratedPiece copy in list)
                {
                    Undo.RecordObject(copy, "Salvar ajustes manuais");
                    copy.Key = NewKey();
                    addedPieces.Add(copy);
                }
            }

            // Peças do kit arrastadas do Project para a cena (sem marca)
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                GameObject go = t.gameObject;
                if (go.GetComponent<GeneratedPiece>() || !PrefabUtility.IsOutermostPrefabInstanceRoot(go) || KitSource(go) == null)
                    continue;
                var piece = Undo.AddComponent<GeneratedPiece>(go);
                piece.Key = NewKey();
                piece.GeneratedPosition = t.position;
                piece.GeneratedRotation = t.rotation;
                piece.GeneratedScale = t.localScale;
                addedPieces.Add(piece);
            }

            ManualAdjustments data = ManualAdjustments.Load(true);
            Undo.RecordObject(data, "Salvar ajustes manuais");
            int moved = 0, hidden = 0, deleted = 0, restored = 0;
            foreach (GeneratedPieceIndex index in indices)
            {
                Dictionary<string, ManualAdjustments.Entry> map = data.ForScope(index.Scope);
                foreach (string key in index.Keys)
                {
                    map.TryGetValue(key, out ManualAdjustments.Entry entry);
                    if (!present.TryGetValue(key, out GeneratedPiece piece))
                    {
                        entry ??= Add(data, index.Scope, key);
                        entry.Deleted = true;
                        deleted++;
                        continue;
                    }

                    Transform t = piece.transform;
                    bool off = !piece.gameObject.activeSelf;
                    bool changed = (t.position - piece.GeneratedPosition).sqrMagnitude > 1e-6f ||
                        Quaternion.Angle(t.rotation, piece.GeneratedRotation) > 0.05f ||
                        (t.localScale - piece.GeneratedScale).sqrMagnitude > 1e-6f;
                    if (!off && !changed)
                    {
                        if (entry != null)
                        {
                            data.Entries.Remove(entry);
                            restored++;
                        }
                        continue;
                    }

                    entry ??= Add(data, index.Scope, key);
                    entry.Deleted = false;
                    entry.Hidden = off;
                    entry.Position = t.position;
                    entry.Rotation = t.rotation;
                    entry.Scale = t.localScale;
                    if (off)
                        hidden++;
                    else
                        moved++;
                }
            }

            // Adicionadas: grava cada uma; as que sumiram da cena (apagadas) saem do asset
            string scope = indices[0].Scope;
            Transform root = indices[0].transform;
            var addedKeys = new HashSet<string>();
            foreach (GeneratedPiece piece in addedPieces)
            {
                string source = KitSource(piece.gameObject);
                if (source == null)
                {
                    Debug.LogWarning($"[Ícaro] Ajustes manuais: {piece.name} não é uma peça do kit (AR_/PR_ dos Picos); fica de fora.", piece);
                    continue;
                }
                addedKeys.Add(piece.Key);
                ManualAdjustments.Entry entry = data.ForScope(scope).TryGetValue(piece.Key, out var found) ? found : Add(data, scope, piece.Key);
                Transform t = piece.transform;
                entry.Added = true;
                entry.Deleted = false;
                entry.Hidden = !piece.gameObject.activeSelf;
                entry.Model = System.IO.Path.GetFileNameWithoutExtension(source);
                entry.Folder = System.IO.Path.GetDirectoryName(source).Replace('\\', '/');
                entry.ParentPath = t.parent && t.parent.IsChildOf(root) ? PathFrom(root, t.parent) : "";
                entry.Position = t.position;
                entry.Rotation = t.rotation;
                entry.Scale = t.localScale;
            }
            int removedAdds = data.Entries.RemoveAll(e => e.Added && e.Scope == scope && !addedKeys.Contains(e.Key));

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Ícaro] Ajustes manuais salvos em {ManualAdjustments.AssetPath}: {moved} movidas, {hidden} desligadas, " +
                $"{deleted} apagadas, {addedKeys.Count} adicionadas ({removedAdds} adicionadas antes foram apagadas), " +
                $"{restored} voltaram ao lugar gerado. Valem na próxima geração.", data);
        }

        static string NewKey() => "+" + System.Guid.NewGuid().ToString("N");

        // Caminho do FBX de origem, se for uma peça do kit (AR_ ou PR_ das pastas dos kits)
        static string KitSource(GameObject go)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return null;
            string path = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(go));
            if (string.IsNullOrEmpty(path) || !k_KitFolders.Any(path.StartsWith))
                return null;
            string file = System.IO.Path.GetFileName(path);
            return file.StartsWith("AR_") || file.StartsWith("PR_") ? path : null;
        }

        static string PathFrom(Transform root, Transform t)
        {
            var names = new List<string>();
            for (; t && t != root; t = t.parent)
                names.Insert(0, t.name);
            return string.Join("/", names);
        }

        static ManualAdjustments.Entry Add(ManualAdjustments data, string scope, string key)
        {
            var e = new ManualAdjustments.Entry { Scope = scope, Key = key };
            data.Entries.Add(e);
            return e;
        }

        [MenuItem("Ícaro/Ajustes manuais/Voltar a seleção ao lugar gerado", priority = 201)]
        static void Revert()
        {
            var pieces = Selection.gameObjects.Select(g => g.GetComponentInParent<GeneratedPiece>(true)).Where(p => p).Distinct().ToList();
            if (pieces.Count == 0)
            {
                EditorUtility.DisplayDialog("Ajustes manuais", "Selecione uma ou mais peças geradas.", "OK");
                return;
            }

            ManualAdjustments data = ManualAdjustments.Load(false);
            if (data)
                Undo.RecordObject(data, "Voltar peças ao lugar gerado");
            foreach (GeneratedPiece p in pieces)
            {
                Undo.RecordObject(p.transform, "Voltar peças ao lugar gerado");
                p.transform.SetPositionAndRotation(p.GeneratedPosition, p.GeneratedRotation);
                p.transform.localScale = p.GeneratedScale;
                if (data && !p.Key.StartsWith("+"))
                    data.Entries.RemoveAll(e => e.Key == p.Key);
            }

            if (data)
            {
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[Ícaro] {pieces.Count} peça(s) de volta ao lugar gerado, ajuste esquecido.");
        }

        [MenuItem("Ícaro/Ajustes manuais/Selecionar peças ajustadas", priority = 202)]
        static void SelectAdjusted()
        {
            ManualAdjustments data = ManualAdjustments.Load(false);
            var keys = data ? new HashSet<string>(data.Entries.Select(e => e.Key)) : new HashSet<string>();
            GameObject[] found = Object.FindObjectsByType<GeneratedPiece>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(p => keys.Contains(p.Key)).Select(p => p.gameObject).ToArray();
            Selection.objects = found;
            Debug.Log($"[Ícaro] {found.Length} peça(s) com ajuste salvo nesta cena.");
        }

        [MenuItem("Ícaro/Ajustes manuais/Esquecer todos os ajustes", priority = 220)]
        static void ClearAll()
        {
            ManualAdjustments data = ManualAdjustments.Load(false);
            if (!data || data.Entries.Count == 0)
                return;
            if (!EditorUtility.DisplayDialog("Ajustes manuais", $"Esquecer os {data.Entries.Count} ajustes salvos? A próxima " +
                "geração põe tudo no lugar do código.", "Esquecer", "Cancelar"))
                return;
            Undo.RecordObject(data, "Esquecer ajustes manuais");
            data.Entries.Clear();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
    }
}
