using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Sala 20, Bairro Voador: um bairro gótico abandonado numa ilha de rocha que flutua solta a oeste do bastião, copiando
    // a imagem de referência do usuário (ArteFonte/Picos/EN_BairroVoador/ref/concept.png): praça rebaixada em degraus
    // com uma rosácea em relevo no piso, a fileira de trás 1,2 m acima com a torre gótica, o salão em ruína, a torre
    // sineira, a catedral e a torre espiral, casinhas redondas de cúpula espalhadas dos dois lados da praça (duas em
    // ressaltos de rocha), a torrezinha de cogumelo, e na frente-esquerda dois patamares mais baixos com escadarias, a
    // ruína e o santuário; carroça, obeliscos, lápides e postes. Tudo modelado no Blender
    // (ArteFonte/Picos/BairroVoador_Kit, FBX em Models/Picos/BairroVoador): a rocha esculpida da ilha
    // (EN_BairroVoador_Rocha: os níveis, os ressaltos e as muretas das escadas saem dela), o calçamento por cima
    // (EN_BairroVoador_Chao) e as peças do kit gótico (PicosGothicKit), postas pela planta EN_BairroVoador_Layout.json
    // (EN_Chao/build/layout.py, que também guia a rocha e o chão).
    // Coordenadas do bairro em metros do Blender, V(x, y, z): x para a direita de quem olha a frente, y para o fundo,
    // z para cima, topo da ilha em z = 0. A frente do bairro (o -Y do Blender) fica para leste, de onde vem a ponte de
    // corrente, reta, que chega no largo do patamar de baixo; a saída futura (sala 21) fica livre ao sul.
    public static partial class PicosBuilder
    {
        // O 0, 0, 0 do Blender no mundo (meio da praça na largura, topo da ilha) e o meio da ilha (y = 14 no Blender)
        static readonly Vector3 k_IsleOrigin = new Vector3(-705f, 10f, -1014f);
        static readonly Vector3 k_Isle = new Vector3(-719f, 10f, -1014f);
        const float k_IsleP1 = -4f, k_IsleP2 = -8f, k_IsleLedge = 1.6f;

        static RuinMesh s_Cobble;
        static IsleLayout s_IsleLayout;

        // A planta gravada pelo layout.py
        [System.Serializable]
        class IsleLayout
        {
            public float[] entrance;
            public IslePiece[] pieces;
            public IsleStair[] stairs;
            public IslePlatform[] platforms;
        }

        [System.Serializable]
        class IslePiece
        {
            public string model;
            public float x, y, z, dx, dy;
        }

        [System.Serializable]
        class IsleStair
        {
            public string name;
            public float[] top, bottom;
            public float width;
        }

        [System.Serializable]
        class IslePlatform
        {
            public float x, y, z, radius;
        }

        // Onde a ponte de corrente chega: o meio da beira reta do largo do patamar de baixo
        static Vector3 IsleEntrance => s_IsleLayout?.entrance is { Length: 3 } e ? V(e[0], e[1], e[2]) : V(-28f, -66f, k_IsleP2);

        // Do Blender (frente -Y, z para cima) para o mundo (frente para leste)
        static Vector3 V(float x, float y, float z = 0f) => k_IsleOrigin + new Vector3(-y, z, x);

        // Giro no mundo de uma peça do kit cuja frente (o -Y dela no Blender) aponta para (dx, dy) no Blender
        static float IsleYaw(float dx, float dy) => Mathf.Atan2(-dy, dx) * Mathf.Rad2Deg;

        static void InitIsle()
        {
            s_Cobble = new RuinMesh(1705, 1);
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>($"{PicosGothicKit.Folder}/EN_BairroVoador_Layout.json");
            s_IsleLayout = json ? JsonUtility.FromJson<IsleLayout>(json.text) : null;
            if (s_IsleLayout == null)
                Debug.LogWarning($"[Ícaro] Bairro Voador: falta {PicosGothicKit.Folder}/EN_BairroVoador_Layout.json (rode ArteFonte/Picos/BairroVoador_Kit/EN_Chao/build/03_rocha.py).");
        }

        // Rua de pedra miúda (vai para o corte com neve das ruas, como o Pave das lajes)
        static void PaveCobble(MeshData slab) => AppendMesh(s_Cobble.Tones[0], slab);

        static void AppendMesh(MeshData target, MeshData source)
        {
            int start = target.Vertices.Count;
            target.Vertices.AddRange(source.Vertices);
            target.Normals.AddRange(source.Normals);
            target.Uvs.AddRange(source.Uvs);
            foreach (int index in source.Triangles)
                target.Triangles.Add(start + index);
        }

        static void BuildIsle()
        {
            BuildIsleApproach();
            BuildIsleRock();
            BuildIsleGround();
            BuildIsleTown();
            Clear(k_Isle, 115f);
        }

        // Carrega um FBX do bairro legível por script (a malha é lida para os pisos com neve e para a colisão)
        static GameObject IsleModel(string name)
        {
            string path = $"{PicosGothicKit.Folder}/{name}.fbx";
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab)
                Debug.LogWarning($"[Ícaro] Bairro Voador: falta {path} (rode os scripts de ArteFonte/Picos/BairroVoador_Kit/EN_Chao/build).");
            return prefab;
        }

        // A rotação que leva um FBX do bairro (frente -Y do Blender) para a frente do bairro no mundo (leste)
        static Quaternion IsleRotation(GameObject prefab) => Quaternion.Euler(0f, 90f, 0f) * prefab.transform.rotation;

        // ---------- caminho ----------

        // Caminho de ~470 m pela direita (oeste) do bastião, como os de antes: trilha de crista, ponte de pedra até uma
        // torrezinha, ponte de corrente, trilha em zigue-zague, arco natural de rocha, ponte de pedra até outra
        // torrezinha e uma ponte de corrente reta (de leste para oeste) até o largo do patamar de baixo da ilha voadora
        static void BuildIsleApproach()
        {
            Vector3 start = B(48f, k_TerraceMid, -2f);
            Vector3 ledgeA = B(100f, k_TerraceMid, -2f);      // em linha reta com a saída oeste do terraço do meio
            var towerB = new Vector3(-452f, -3f, -956f);
            var ledgeC = new Vector3(-498f, -9f, -926f);
            var ledgeD = new Vector3(-548f, -4f, -952f);
            var ledgeE = new Vector3(-582f, 0f, -996f);
            Vector3 entrance = IsleEntrance;
            var towerF = new Vector3(-600f, 3f, entrance.z);
            var ahead = new Vector3(towerF.x - 30f, towerF.y, towerF.z);     // a ponte sai pelo lado oeste, reta

            // Ponte de pedra reta saindo da montanha do forte (pedido do usuário: no lugar da trilha de crista)
            RockLedge(ledgeA, 9f, 951);
            Bridge(start, Edge(ledgeA, 9f, start), 5f);
            ClearLine(start, ledgeA);

            float innerB = SmallTower(towerB, 8f, 952, ledgeA, ledgeC);
            Bridge(Edge(ledgeA, 9f, towerB), TowerEdge(towerB, innerB, ledgeA), 5f);
            ClearLine(ledgeA, towerB);

            RockLedge(ledgeC, 9f, 953);
            ChainBridge(TowerEdge(towerB, innerB, ledgeC), Edge(ledgeC, 9f, towerB), 3.4f, 2.5f);
            ClearLine(towerB, ledgeC);

            RockLedge(ledgeD, 10f, 954);
            Trail(new[]
            {
                Edge(ledgeC, 9f, ledgeD), new Vector3(-515f, -7.5f, -944f), new Vector3(-524f, -6f, -930f), new Vector3(-538f, -4.5f, -936f),
                Edge(ledgeD, 10f, ledgeC),
            }, 14);
            ClearLine(ledgeC, ledgeD);

            RockLedge(ledgeE, 9f, 955);
            NaturalArch(Edge(ledgeD, 10f, ledgeE), Edge(ledgeE, 9f, ledgeD), 5f);
            ClearLine(ledgeD, ledgeE);

            float innerF = SmallTower(towerF, 8f, 956, ledgeE, ahead);
            Bridge(Edge(ledgeE, 9f, towerF), TowerEdge(towerF, innerF, ledgeE), 5f);
            ClearLine(ledgeE, towerF);

            ChainBridge(TowerEdge(towerF, innerF, ahead), entrance, 3.4f, 3.5f);
            ClearLine(towerF, entrance);
        }

        // ---------- a ilha ----------

        // A rocha esculpida no Blender (03_rocha.py): paredões de blocos, as pontas penduradas por baixo, os níveis e as
        // muretas das escadas; material das montanhas (neve nos topos pela cor do relevo) e colisão pela malha. Em volta,
        // longe da ilha, quatro picos
        static void BuildIsleRock()
        {
            GameObject prefab = IsleModel("EN_BairroVoador_Rocha");
            if (prefab)
            {
                var rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab, s_World);
                rock.name = "BairroVoador_Rocha";
                rock.transform.SetPositionAndRotation(k_IsleOrigin, IsleRotation(prefab));
                foreach (MeshRenderer renderer in rock.GetComponentsInChildren<MeshRenderer>())
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                        materials[i] = s_Materials.Mountain;
                    renderer.sharedMaterials = materials;
                    renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                }
            }

            int seed = 970;
            foreach ((float deg, float r, float top) in new[] { (140f, 22f, 40f), (215f, 18f, 30f), (300f, 20f, 34f), (60f, 16f, 26f) })
            {
                float distance = 70f + 30f + r * 1.35f;
                PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
                {
                    Top = k_Isle + new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad) * distance, top - k_Isle.y, Mathf.Sin(deg * Mathf.Deg2Rad) * distance),
                    TopRadius = r, BaseRadius = r * 1.6f, Taper = 3f, Cap = PicosMountainKit.Cap.Crag, CapHeight = r * 0.3f, Sides = 40,
                    RingStep = 3f, Corners = 6, Flutes = 6, FluteDepth = 0.08f, BedHeight = R(12f, 18f), BedShift = 0.08f, Waist = 0.12f,
                    Roughness = 0.07f, Seed = seed++, Bundle = false,
                });
            }
        }

        // O calçamento modelado no Blender (03_forms.py): os topos de rua e as lajes da praça vão para o corte com neve
        // dos pisos (Pave e PaveCobble: quadrados de 4 m de pedra, de transição ou de neve pela máscara, os lados de
        // cantaria); o resto (rosácea, degraus das escadarias, pilares da chegada, muretas, entulho e neve) vira objetos
        // com colisão pelos materiais dos Picos
        static void BuildIsleGround()
        {
            if (s_IsleLayout?.platforms != null)
                foreach (IslePlatform platform in s_IsleLayout.platforms)
                    s_Platforms.Add((V(platform.x, platform.y, platform.z), platform.radius));

            GameObject prefab = IsleModel("EN_BairroVoador_Chao");
            if (!prefab)
                return;

            var instance = Object.Instantiate(prefab);
            instance.transform.SetPositionAndRotation(k_IsleOrigin, IsleRotation(prefab));
            var others = new Dictionary<Material, MeshData>();
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                Material[] materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                Vector2[] uvs = mesh.uv;
                for (int s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
                {
                    var data = new MeshData();
                    foreach (int index in mesh.GetTriangles(s))
                    {
                        data.Triangles.Add(data.Vertices.Count);
                        data.Vertices.Add(matrix.MultiplyPoint3x4(vertices[index]));
                        data.Normals.Add(matrix.MultiplyVector(normals[index]).normalized);
                        data.Uvs.Add(uvs.Length > 0 ? uvs[index] : Vector2.zero);
                    }

                    string name = materials[s] ? materials[s].name : "";
                    if (name.StartsWith("MAT_Rua"))
                        PaveCobble(data);
                    else if (name.StartsWith("MAT_Laje"))
                        Pave(data);
                    else
                    {
                        Material material = PicosGothicKit.MaterialFor(name, s_Materials) ?? s_Materials.Stones[0];
                        if (!others.TryGetValue(material, out MeshData target))
                            others[material] = target = new MeshData();
                        AppendMesh(target, data);
                    }
                }
            }
            Object.DestroyImmediate(instance);

            var root = new GameObject("BairroVoador_Chao").transform;
            root.SetParent(s_World, false);
            int part = 0;
            foreach (KeyValuePair<Material, MeshData> pair in others)
                GridSceneKit.CreateObject($"{k_Prefix}_BairroVoador_Chao_{++part}", pair.Key.name, pair.Value, pair.Key, root, true);

            // Rampas invisíveis rente às quinas dos degraus (como as outras escadas dos Picos)
            if (s_IsleLayout?.stairs == null)
                return;
            foreach (IsleStair stair in s_IsleLayout.stairs)
            {
                Vector3 top = V(stair.top[0], stair.top[1], stair.top[2]), bottom = V(stair.bottom[0], stair.bottom[1], stair.bottom[2]);
                Quaternion rotation = Quaternion.LookRotation(bottom - top);
                var ramp = new GameObject($"Rampa_{stair.name}");
                ramp.transform.SetParent(root, false);
                ramp.transform.SetPositionAndRotation((top + bottom) * 0.5f + rotation * Vector3.up * -0.12f, rotation);
                ramp.AddComponent<BoxCollider>().size = new Vector3(stair.width, 0.2f, Vector3.Distance(top, bottom));
            }
        }

        // ---------- o bairro ----------

        // As peças do kit gótico na planta (layout.py): a fileira de trás desencontrada e girada de leve, as casinhas
        // espalhadas fora de qualquer grade em volta da praça (duas em ressaltos de rocha), a ruína e o santuário nos
        // patamares, a carroça, os obeliscos, as lápides, o pedestal e os postes; e os escombros (AR_Escombros): o portal da
        // catedral entupido, cada casinha bloqueada (monte na porta ou porta pregada, na mesma posição e giro dela), montes
        // encostados nas paredes e montes soltos, todos com colisão pela malha; e os detalhes (AR_Detalhes): o sino caído
        // ao pé da sineira, o andaime na fachada da catedral (com a posição e o giro dela), cargas, carrinhos de mão,
        // estandartes e lanternas
        static void BuildIsleTown()
        {
            if (s_IsleLayout?.pieces == null)
                return;
            var root = new GameObject("BairroVoador").transform;
            root.SetParent(s_World, false);
            foreach (IslePiece piece in s_IsleLayout.pieces)
            {
                // Estandartes e lanternas ficam sem colisão (finos e pendurados, só enroscariam o jogador e os tiros)
                bool hanging = piece.model.StartsWith("PR_Estandarte") || piece.model.StartsWith("PR_Lanterna");
                PicosGothicKit.Place(s_Materials, piece.model, V(piece.x, piece.y, piece.z), IsleYaw(piece.dx, piece.dy), root, !hanging);
            }
        }

        // ---------- sala ----------

        // Sala 20: gatilho cobrindo a ilha e o patamar do meio, com o fundo acima do patamar de baixo (o largo onde a
        // ponte chega fica fora dele); spawns na praça, nas ruas, na fileira de trás, nos ressaltos e no patamar do meio
        static void BuildIsleRoom()
        {
            float bottom = k_IsleOrigin.y + k_IsleP1 - 1.5f, top = k_IsleOrigin.y + 45f;
            var trigger = new Bounds(new Vector3(-710f, (bottom + top) * 0.5f, -1015f), new Vector3(132f, top - bottom, 126f));
            BuildRoom("Arena_BairroVoador", "Bairro Voador", 20, k_Isle, trigger, IsleEntrance, k_IsleOrigin.y + k_IsleP1 - 0.5f,
                k_IsleOrigin.y + k_IsleLedge + 0.5f);
        }
    }
}
