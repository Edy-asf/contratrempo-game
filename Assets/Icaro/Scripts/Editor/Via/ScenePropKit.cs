using System.IO;
using UnityEditor;
using UnityEngine;
using BiomeId = Icaro.EditorTools.ViaLayout.BiomeId;

namespace Icaro.EditorTools
{
    // Biblioteca de peças de cenário da Via, um prefab por bioma e tipo, em Prefabs/Cenario/{Bioma}/.
    // O gerador só cria o placeholder (PlaceholderFactory) quando o prefab não existe: para trocar uma peça
    // por um modelo de verdade, basta substituir o conteúdo do prefab mantendo o nome e as convenções do
    // CENARIO_LEIAME.md. A cena usa instâncias de prefab, então a troca vale na hora.
    public static class ScenePropKit
    {
        public enum PropKind
        {
            Arvore,
            RochaGrande,
            RochaMedia,
            TroncoCaido,
            RuinaMuro,
            RuinaColuna,
            RuinaPlataforma,
            CogumeloBio,
            CogumeloAmbar,
            FendaLava,
            Arbusto,
            BrotoLuminoso,
            Portal,
            Barreira,
            RuinaArco,
            RuinaBloco,
            Poleiro,
        }

        public const string Folder = IcaroAssetUtility.PrefabsFolder + "/Cenario";
        public const string ShipPath = Folder + "/Comum/Nave_Dedalo.prefab";
        const string k_ReadmePath = Folder + "/CENARIO_LEIAME.md";

        static readonly BiomeId[] k_AllBiomes = { BiomeId.Bosque, BiomeId.Charco, BiomeId.Ruinas, BiomeId.Cratera };

        public static string PathOf(BiomeId biome, PropKind kind) => $"{Folder}/{biome}/{FileName(kind)}.prefab";

        public static string FileName(PropKind kind)
        {
            switch (kind)
            {
                case PropKind.RochaGrande: return "Rocha_Grande";
                case PropKind.RochaMedia: return "Rocha_Media";
                case PropKind.TroncoCaido: return "Tronco_Caido";
                case PropKind.RuinaMuro: return "Ruina_Muro";
                case PropKind.RuinaColuna: return "Ruina_Coluna";
                case PropKind.RuinaPlataforma: return "Ruina_Plataforma";
                case PropKind.CogumeloBio: return "Cogumelo_Bio";
                case PropKind.CogumeloAmbar: return "Cogumelo_Ambar";
                case PropKind.FendaLava: return "Fenda_Lava";
                case PropKind.BrotoLuminoso: return "Broto_Luminoso";
                case PropKind.RuinaArco: return "Ruina_Arco";
                case PropKind.RuinaBloco: return "Ruina_Bloco";
                case PropKind.Poleiro: return "Poleiro";
                default: return kind.ToString();
            }
        }

        // Raio aproximado ocupado pela peça, usado para espaçar a cobertura
        public static float Footprint(PropKind kind)
        {
            switch (kind)
            {
                case PropKind.RochaGrande: return 3.5f;
                case PropKind.RuinaMuro: return 3.5f;
                case PropKind.RuinaArco: return 3.5f;
                case PropKind.Poleiro: return 3f;
                case PropKind.TroncoCaido: return 3f;
                case PropKind.RuinaPlataforma: return 7f;
                case PropKind.FendaLava: return 2.5f;
                case PropKind.Arvore: return 2f;
                case PropKind.RochaMedia: return 2f;
                case PropKind.RuinaColuna: return 1.5f;
                case PropKind.RuinaBloco: return 1.3f;
                default: return 1f;
            }
        }

        // Peças pequenas/decorativas: ficam fora do static batching para o GPU instancing valer
        public static bool IsSmallDecor(PropKind kind) =>
            kind == PropKind.CogumeloBio || kind == PropKind.CogumeloAmbar || kind == PropKind.FendaLava ||
            kind == PropKind.Arbusto || kind == PropKind.BrotoLuminoso;

        public static GameObject Get(BiomeId biome, PropKind kind)
        {
            string path = PathOf(biome, kind);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab)
                return prefab;

            IcaroAssetUtility.EnsureFolder($"{Folder}/{biome}");
            GameObject root = PlaceholderFactory.Build(biome, kind, ViaLayout.PaletteOf(biome));
            root.name = FileName(kind);
            return IcaroPrefabBuilder.SavePrefab(root, path);
        }

        // Variações de árvore do bioma: Arvore.prefab e, se existirem, Arvore_B.prefab, Arvore_C.prefab...
        public static GameObject[] TreeVariants(BiomeId biome)
        {
            var variants = new System.Collections.Generic.List<GameObject> { Get(biome, PropKind.Arvore) };
            for (char suffix = 'B'; suffix <= 'Z'; suffix++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{biome}/Arvore_{suffix}.prefab");
                if (!prefab)
                    break;
                variants.Add(prefab);
            }

            return variants.ToArray();
        }

        public static GameObject Ship()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShipPath);
            if (prefab)
                return prefab;

            IcaroAssetUtility.EnsureFolder(Folder + "/Comum");
            GameObject root = PlaceholderFactory.BuildShip(BiomePalettes.Cratera);
            root.name = "Nave_Dedalo";
            return IcaroPrefabBuilder.SavePrefab(root, ShipPath);
        }

        // Garante todos os prefabs (chamado por "Gerar prefabs" e pelo gerador da Via)
        public static void EnsureAll()
        {
            foreach (BiomeId biome in k_AllBiomes)
            foreach (PropKind kind in System.Enum.GetValues(typeof(PropKind)))
                Get(biome, kind);

            Ship();
            WriteReadme();
        }

        static void WriteReadme()
        {
            if (File.Exists(k_ReadmePath))
                return;

            IcaroAssetUtility.EnsureFolder(Folder);
            File.WriteAllText(k_ReadmePath,
@"# Peças de cenário da Via Aeônida

Cada pasta (`Bosque`, `Charco`, `Ruinas`, `Cratera`, `Comum`) tem um prefab por tipo de peça. Os que vieram do
gerador são **placeholders** feitos de primitivas. Para trocar por um modelo de verdade:

1. Abra o prefab (duplo clique) e substitua o conteúdo pelo modelo novo. **Não renomeie nem mova o prefab.**
2. Pivô na **base** da peça, frente apontando para **+Z**.
3. Escala real: o Ícaro tem cerca de **1,8 m**. As árvores placeholder têm ~10 m; rochas grandes, ~3 m de altura.
4. Collider: peças de cobertura precisam de collider (de preferência Box/Capsule simples). Decoração pequena
   (cogumelos, arbustos, brotos, fendas de lava) fica sem collider.
5. Materiais em **URP/Lit**, com **Enable GPU Instancing** ligado.
6. Registre o asset em `Assets/Icaro/CREDITOS.md` (só CC0 ou CC-BY).

Casos especiais:

- `Arvore`: é protótipo de árvore do **Terrain**. Precisa de um **LODGroup** (ou MeshRenderer) na **raiz** e,
  para bloquear a passagem, um **CapsuleCollider na raiz**.
- `Arbusto` e `Broto_Luminoso`: são **detalhes** do Terrain. O mesh e o MeshRenderer ficam na **raiz** do prefab.
- `Barreira`: mantenha o componente **RoomDoor** e o **NavMeshObstacle** na raiz (ela afunda no chão ao abrir).
- `Cogumelo_*` e `Fenda_Lava` têm uma luz **Baked**: mantenha-a se quiser as poças de luz no bake.
- `Poleiro`: rocha alta de topo plano (cerca de 5 m) onde o Espinhento nasce. Mantenha o filho **Topo** no centro do
  topo (é o ponto de spawn), colliders sólidos e um topo plano de pelo menos 3 x 3 m (para o NavMesh).

Depois de trocar, rode **Ícaro > Gerar Via Aeônida** para o Terrain recarregar árvores e detalhes. As peças
avulsas (rochas, ruínas, portais...) já aparecem trocadas na cena, por serem instâncias de prefab.

Modelos reais (FBX exportados do Blender; fontes em `ArteFonte/*.blend`, fora do git) ficam em `Assets/Icaro/Models/{Grupo}`.
O menu **Ícaro > Montar modelos importados** cria os materiais URP/Lit a partir de `Textures/{Grupo}/{Material}_BaseMap`,
`_Normal`, `_MetallicSmoothness` e `_Emission` e regrava estes prefabs com os modelos (scripts em `Scripts/Editor/Modelos`):

- `Arvore`, `Arvore_B`, `Arvore_C` (árvores alienígenas A, B e C, com LODs): o Terrain sorteia entre as variações que existirem.
- `Ruina_Muro`, `Ruina_Coluna` (pilar), `Ruina_Plataforma` (escadaria), `Ruina_Arco` e `Ruina_Bloco` (monólito de runas).
- `Comum/Nave_Dedalo` (nave caída; o filho `Embarque` marca onde fica o objetivo final).

Depois de montar, rode **Ícaro > Gerar Via Aeônida** para o Terrain e as arenas usarem as peças novas.
");
            AssetDatabase.ImportAsset(k_ReadmePath);
        }
    }
}
