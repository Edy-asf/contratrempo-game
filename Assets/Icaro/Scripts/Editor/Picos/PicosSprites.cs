using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;
using Paint = Icaro.EditorTools.GridTextureKit.Paint;

namespace Icaro.EditorTools
{
    // Detalhes de ambiente dos Picos (pedido do usuário em 2026-11-02; em 2026-11-06 ele recusou a neve miúda, os cristais
    // de gelo e de runa, os escombros, a neve soprada e as bandeirolas das Vilas do Alto, e ficaram só estes), em pixel
    // art desenhada em código a 16 px/m, virados para a câmera na placa de vídeo pelo lote do Bosque
    // (GridSceneKit.BillboardBatch, shader Icaro/BillboardSprite, que anima tiras de quadros):
    // 1. chamas de runa (ciano e violeta, animadas) em braseiros de pedra nos portais, no templo, na sela, no bastião, no
    //    Portal do Reino e na rotatória, com poucas luzes; parte das colunas de lampiões acesa;
    // 2. velas votivas (chama âmbar, animadas) nos altares, nos monólitos, nos santuários e no portal quebrado;
    // 3. bandeirolas de pano rasgado e duro de gelo (malha com o shader Icaro/FlagCloth, que tremula mais rápido com o
    //    multiplicador): em postes nos marcos de pedra das trilhas e penduradas nas correntes das pontes.
    // Tudo é colocado por raio de cima para baixo no chão de verdade (visível e plano), sem encostar em nada, e depois de
    // todo o cenário ter colisão (fim do BuildContent). Sorteio próprio, para não mudar o resto do mapa.
    public static partial class PicosBuilder
    {
        // Registros feitos durante a geração (marcos, correntes das pontes, portais, santuários)
        static readonly List<(Vector3 top, Vector3 along)> s_Cairns = new List<(Vector3, Vector3)>();
        static readonly List<Vector3[]> s_ChainRails = new List<Vector3[]>();
        static readonly List<(Vector3 floor, Vector3 facing)> s_SpritePortals = new List<(Vector3, Vector3)>();
        static readonly List<(Vector3 front, Vector3 facing)> s_SpriteShrines = new List<(Vector3, Vector3)>();
        // Oferendas dos peregrinos na praça da basílica: velas acesas, sorteadas depois de tudo (não mudam o resto)
        static readonly List<Vector3> s_SpriteVotives = new List<Vector3>();

        // Sprites recusados (2026-11-06): as malhas, materiais e texturas que a geração anterior deixou são apagados
        static readonly string[] k_RemovedSprites =
        {
            "Monte_1", "Monte_2", "Monte_3", "CristaVento", "Seixos_1", "Seixos_2", "Crosta", "Lascas", "CristaisGelo_1",
            "CristaisGelo_2", "CristalRuna_1", "CristalRuna_2", "Cacos", "Corrente", "Tabuas", "LanternaCaida", "NeveSoprada",
        };

        static void ResetSprites()
        {
            s_Cairns.Clear();
            s_ChainRails.Clear();
            s_SpritePortals.Clear();
            s_SpriteShrines.Clear();
            s_SpriteVotives.Clear();
        }

        struct SpriteDef
        {
            public Material Material;
            public Vector2 Size;
        }

        class SpriteSet
        {
            public SpriteDef Flame, Candles;
            public Material Flags, LitLantern;
        }

        static System.Random s_SpriteRandom;
        static GridSceneKit.BillboardBatch s_SpriteBatch;
        static readonly RaycastHit[] s_SpriteHits = new RaycastHit[32];
        static int s_SpriteLights;
        const int k_MaxSpriteLights = 24;

        static float SR(float min, float max) => min + (float)s_SpriteRandom.NextDouble() * (max - min);
        static bool SChance(double p) => s_SpriteRandom.NextDouble() < p;

        static void BuildSprites()
        {
            DeleteRemovedSprites();
            s_SpriteRandom = new System.Random(1777);
            s_SpriteLights = 0;
            SpriteSet set = PaintSprites();
            Physics.SyncTransforms();
            bool backfaces = Physics.queriesHitBackfaces;
            s_SpriteBatch = new GridSceneKit.BillboardBatch(s_World);
            var stones = new RuinMesh(1791);
            var poles = new MeshData();
            var ropes = new MeshData();
            var flags = new MeshData();
            try
            {
                PlaceBraziers(set, stones);
                PlaceCandles(set);
                LightLanterns(set);
                PlaceFlags(poles, ropes, flags);
                foreach (Vector3 p in s_SpriteVotives)
                    Candles(set, p, 3, 0.7f);
            }
            finally
            {
                Physics.queriesHitBackfaces = backfaces;
            }

            s_SpriteBatch.Build(k_Prefix + "_Sprites");
            int count = s_SpriteBatch.Count;
            s_SpriteBatch = null;
            CreateRuin("Braseiros", stones, true, false);
            GridSceneKit.CreateObject(k_Prefix + "_Bandeirolas_Mastros", "Bandeirolas_Mastros", poles, s_Materials.Wood, s_World, true);
            GridSceneKit.CreateObject(k_Prefix + "_Bandeirolas_Cordas", "Bandeirolas_Cordas", ropes, s_Materials.Wood, s_World, false, false);
            GridSceneKit.CreateObject(k_Prefix + "_Bandeirolas", "Bandeirolas", flags, set.Flags, s_World, false, false);
            Debug.Log($"[Ícaro] Picos: {count} sprites de chamas e velas, {s_SpriteLights} luzes de chama.");
        }

        static void DeleteRemovedSprites()
        {
            var paths = new List<string>();
            foreach (string name in k_RemovedSprites)
            {
                string material = "Picos_Sprite_" + name, chunks = k_Prefix + "_Sprites_" + material + "_";
                foreach (string guid in AssetDatabase.FindAssets(chunks.TrimEnd('_'), new[] { GridSceneKit.MeshFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileName(path).StartsWith(chunks))
                        paths.Add(path);
                }

                paths.Add($"{GridTextureKit.MaterialsFolder}/{material}.mat");
                paths.Add($"{GridTextureKit.MaterialsFolder}/{material}_GPU.mat");
                paths.Add($"{GridTextureKit.Folder}/{material}.png");
                paths.Add($"{GridTextureKit.Folder}/{material}_Brilho.png");
            }

            int deleted = 0;
            foreach (string path in paths)
                if (AssetDatabase.LoadMainAssetAtPath(path) && AssetDatabase.DeleteAsset(path))
                    deleted++;
            if (deleted > 0)
                Debug.Log($"[Ícaro] Picos: {deleted} arquivos dos sprites recusados apagados.");
        }

        // ---------- chão ----------

        // Chão visível e plano logo abaixo de 'near' (procura de 'above' acima até 'below' abaixo). Recusa quando um
        // colisor invisível (rampa de escada, passarela) fica bem acima do chão visível.
        static bool SpriteGround(Vector3 near, float above, float below, out Vector3 point)
        {
            point = default;
            Physics.queriesHitBackfaces = false;
            int count = Physics.RaycastNonAlloc(near + Vector3.up * above, Vector3.down, s_SpriteHits, above + below, ~0,
                QueryTriggerInteraction.Ignore);
            float visible = float.MaxValue, invisible = float.MaxValue;
            RaycastHit best = default;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_SpriteHits[i];
                bool seen = hit.collider.TryGetComponent(out MeshRenderer renderer) && renderer.enabled;
                if (seen && hit.distance < visible)
                {
                    visible = hit.distance;
                    best = hit;
                }
                else if (!seen)
                    invisible = Mathf.Min(invisible, hit.distance);
            }

            if (visible == float.MaxValue || best.normal.y < 0.9f || invisible < visible - 0.35f)
                return false;
            point = best.point;
            return true;
        }

        // Espaço livre em volta do sprite (nada de parede, coluna ou casa encostando) e, se 'open', céu aberto em cima
        static bool SpriteRoom(Vector3 foot, float radius, float height, bool open)
        {
            Vector3 bottom = foot + Vector3.up * (radius + 0.15f);
            Vector3 top = foot + Vector3.up * Mathf.Max(radius + 0.15f, height - radius);
            if (Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore))
                return false;
            if (!open)
                return true;
            Physics.queriesHitBackfaces = true;
            bool covered = Physics.Raycast(foot + Vector3.up * 0.3f, Vector3.up, 10f, ~0, QueryTriggerInteraction.Ignore);
            Physics.queriesHitBackfaces = false;
            return !covered;
        }

        static void AddSprite(SpriteDef def, Vector3 foot) => s_SpriteBatch.Add(def.Material, foot, def.Size, SChance(0.5));

        // ---------- braseiros, velas e lampiões ----------

        // Braseiro de pedra (base, fuste e bacia) com a chama de runa por cima e, enquanto houver, uma luz
        static void Brazier(SpriteSet set, RuinMesh stones, Vector3 near, float yaw)
        {
            if (!SpriteGround(near, 4f, 8f, out Vector3 foot) || !SpriteRoom(foot, 0.6f, 2.2f, false))
                return;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            BlockMesh.ChamferBox(stones.Pick(), foot + Vector3.up * 0.18f, new Vector3(1.1f, 0.36f, 1.1f), rotation, 0.06f);
            BlockMesh.ChamferBox(stones.Pick(), foot + Vector3.up * 0.66f, new Vector3(0.55f, 0.6f, 0.55f), rotation, 0.05f);
            BlockMesh.ChamferBox(stones.Pick(), foot + Vector3.up * 1.0f, new Vector3(0.8f, 0.1f, 0.8f), rotation, 0.03f);
            Prism(stones.Pick(), foot, 0.62f, 0.42f, 8, foot.y + 1.42f, foot.y + 1.05f, false);
            Prism(stones.Pick(), foot, 0.66f, 0.62f, 8, foot.y + 1.5f, foot.y + 1.42f, true);
            AddSprite(set.Flame, foot + Vector3.up * 1.3f);
            if (s_SpriteLights < k_MaxSpriteLights)
            {
                Light(foot + Vector3.up * 2.2f, new Color(0.55f, 0.72f, 1f), 2.2f, 9f);
                s_SpriteLights++;
            }
        }

        static void PlaceBraziers(SpriteSet set, RuinMesh stones)
        {
            // Chegada: dos dois lados do estrado do portal
            foreach (float s in new[] { -1f, 1f })
                Brazier(set, stones, new Vector3(s * 12.5f, 0f, 12.5f), 0f);
            // Arena 17: dos dois lados do altar do templo
            foreach (float d in new[] { 306f, 324f })
                Brazier(set, stones, Polar(d, 24.5f, 5.5f), -(315f + 90f));
            // Sela: a leste e a oeste do santuário
            foreach (float s in new[] { -1f, 1f })
                Brazier(set, stones, k_Saddle + Vector3.right * s * 4.4f, 0f);
            // Bastião: na frente do portal quebrado
            foreach (float s in new[] { -1f, 1f })
                Brazier(set, stones, B(4f + s * 8f, k_TerraceHigh, 38f), 0f);
            // Vilas do Alto: dos lados do portal inteiro
            foreach ((Vector3 floor, Vector3 facing) in s_SpritePortals)
            {
                Vector3 right = Vector3.Cross(Vector3.up, facing).normalized;
                foreach (float s in new[] { -1f, 1f })
                    Brazier(set, stones, floor + right * s * 11.5f + facing * 2.5f, YawOf(facing));
            }

            // Portal do Reino: na frente, fora da passagem; rotatória: quatro em volta do monumento, fora da estrada
            float yaw = YawOf(ReinoDir);
            foreach (float s in new[] { -1f, 1f })
                Brazier(set, stones, ReinoBastion + ReinoDir * 8f + ReinoRight * s * 10.5f, yaw);
            foreach (float deg in new[] { 45f, 135f, 225f, 315f })
            {
                Vector3 dir = Quaternion.Euler(0f, deg, 0f) * ReinoDir;
                Brazier(set, stones, ReinoRotunda + dir * 17.5f, yaw + deg);
            }
        }

        // Grupos de velas: no alto do pedestal/altar perto de 'near' (procura a superfície plana mais alta)
        static void Candles(SpriteSet set, Vector3 near, int groups, float spread)
        {
            for (int i = 0, tries = 0; i < groups && tries < groups * 6; tries++)
            {
                Vector3 p = near + new Vector3(SR(-spread, spread), 0f, SR(-spread, spread));
                if (SpriteGround(p, 2f, 3f, out Vector3 foot) && SpriteRoom(foot, 0.25f, 0.6f, false))
                {
                    AddSprite(set.Candles, foot);
                    i++;
                }
            }
        }

        static void PlaceCandles(SpriteSet set)
        {
            // Santuário da sela: no pedestal, dos dois lados do anel
            foreach (float s in new[] { -1f, 1f })
                Candles(set, k_Saddle + new Vector3(0f, 1.1f, s * 1.2f), 1, 0.5f);
            // Altar do templo da arena 17
            Candles(set, Polar(315f, 26f, 6.7f), 2, 0.7f);
            // Monólito de runas do bastião (na base) e o do nicho
            Candles(set, B(-34f, k_TerraceHigh + 1f, 34f) + BRot(15f) * Vector3.back * 1.12f, 2, 0.2f);
            if (s_NicheRune)
                Candles(set, s_NicheRune.bounds.center + Vector3.down * 2.4f + BDir(0f, 0f, -1f) * 0.45f, 1, 0.3f);
            // Portal quebrado: no plinto, dos dois lados dos degraus
            foreach (float s in new[] { -1f, 1f })
                Candles(set, B(4f + s * 6f, k_TerraceHigh + 1f, 45f), 2, 1f);
            // Monólitos dos portais das Vilas do Alto
            foreach ((Vector3 floor, Vector3 facing) in s_SpritePortals)
            {
                Vector3 right = Vector3.Cross(Vector3.up, facing).normalized;
                foreach (float s in new[] { -1f, 1f })
                    Candles(set, floor + right * s * 8.5f + facing * 1f, 1, 0.4f);
            }

            // Na frente das capelas e do santuário do Bairro Voador
            foreach ((Vector3 front, Vector3 facing) in s_SpriteShrines)
                Candles(set, front, 3, 2f);
            if (s_IsleLayout?.pieces != null)
                foreach (IslePiece piece in s_IsleLayout.pieces)
                    if (piece.model == "AR_Santuario")
                    {
                        Vector3 facing = Quaternion.Euler(0f, IsleYaw(piece.dx, piece.dy), 0f) * Vector3.forward;
                        Candles(set, V(piece.x, piece.y, piece.z) + facing * 3f, 3, 1.2f);
                    }
        }

        // Metade das colunas de lampiões fica acesa: o vidro escuro das lanternas vira luz fria; as perto do bastião e da
        // rotatória ganham uma luz de verdade (enquanto houver). Na Ponte do Reino (bastião e rotatória incluídos) todas
        // as que estão em pé acendem em lilás, a luz da tecnologia dos Picos (pedido do usuário em 2026-11-07); as
        // tombadas ficam apagadas
        static void LightLanterns(SpriteSet set)
        {
            Transform bridge = s_World.Find("Ponte_do_Reino");
            foreach (Transform t in s_World.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "PR_Candelabro")
                    continue;
                bool onBridge = bridge && t.IsChildOf(bridge);
                bool lit = SChance(0.55);
                if (onBridge ? t.up.y < 0.7f : !lit)
                    continue;
                Material glass = onBridge ? s_Materials.Glow : set.LitLantern;
                foreach (MeshRenderer renderer in t.GetComponentsInChildren<MeshRenderer>())
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                        if (materials[i] == s_Materials.Dark)
                            materials[i] = glass;
                    renderer.sharedMaterials = materials;
                }

                bool near = Vector3.Distance(t.position, ReinoBastion) < 70f || Vector3.Distance(t.position, ReinoRotunda) < 45f;
                if (near && s_SpriteLights < k_MaxSpriteLights)
                {
                    Light(t.position + Vector3.up * 5.6f, onBridge ? new Color(0.8f, 0.62f, 1f) : new Color(0.55f, 0.72f, 1f), 1.6f, 10f);
                    s_SpriteLights++;
                }
            }
        }

        // ---------- bandeirolas ----------

        const float k_FlagWidth = 10f / 16f, k_FlagHeight = 14f / 16f;

        static void PlaceFlags(MeshData poles, MeshData ropes, MeshData flags)
        {
            // Marcos de pedra das trilhas: um poste no alto do marco e a corda descendo até uma estaca ao lado da trilha
            foreach ((Vector3 top, Vector3 along) in s_Cairns)
            {
                Vector3 dir = new Vector3(along.x, 0f, along.z).normalized;
                Vector3 pole = top + Vector3.up * 2.4f;
                Vector3 stakeNear = top + dir * SR(4.5f, 6f) * (SChance(0.5) ? 1f : -1f);
                if (!SpriteGround(stakeNear, 2f, 4f, out Vector3 stake))
                    continue;
                Pole(poles, top - Vector3.up * 0.3f, 2.7f, 0.09f);
                Pole(poles, stake - Vector3.up * 0.2f, 0.9f, 0.06f);
                Vector3 end = stake + Vector3.up * 0.65f;
                if (Physics.Linecast(pole, end, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                FlagLine(ropes, flags, pole, end, 0.35f, 0.75f);
            }

            // Correntes das pontes: bandeirolas penduradas no corrimão, de espaço em espaço
            foreach (Vector3[] rail in s_ChainRails)
                for (int i = 2; i + 2 < rail.Length; i += 2)
                {
                    Vector3 along = rail[i + 1] - rail[i - 1];
                    if (SChance(0.25))
                        continue;
                    Pennant(flags, rail[i] + Vector3.down * 0.08f, along);
                }

            // Archen Kaedra: as linhas que o gerador da cidade deixou (no fim, para não mudar o sorteio do resto)
            CityFlagLines(ropes, flags);
        }

        static void Pole(MeshData mesh, Vector3 foot, float height, float thickness)
        {
            BlockMesh.Box(mesh, foot + Vector3.up * height * 0.5f, new Vector3(thickness, height, thickness),
                Quaternion.Euler(0f, SR(0f, 90f), 0f));
        }

        // Corda cedendo de 'a' até 'b' com bandeirolas a cada 'spacing' metros
        static void FlagLine(MeshData ropes, MeshData flags, Vector3 a, Vector3 b, float sag, float spacing)
        {
            Vector3 Rope(float t) => Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            float length = Vector3.Distance(a, b);
            int segments = Mathf.Max(4, Mathf.CeilToInt(length / 1.2f));
            for (int i = 0; i < segments; i++)
            {
                Vector3 p = Rope(i / (float)segments), q = Rope((i + 1) / (float)segments);
                BlockMesh.Box(ropes, (p + q) * 0.5f, new Vector3(0.035f, 0.035f, Vector3.Distance(p, q) + 0.02f), Quaternion.LookRotation(q - p));
            }

            int count = Mathf.FloorToInt(length / spacing);
            for (int i = 1; i < count; i++)
            {
                float t = i / (float)count;
                Pennant(flags, Rope(t), Rope(t + 0.01f) - Rope(t - 0.01f));
            }
        }

        // Uma bandeirola triangular pendurada em 'top', no plano da corda; a cor do vértice diz quanto balança (G) e a
        // fase (R) para o shader Icaro/FlagCloth
        static void Pennant(MeshData mesh, Vector3 top, Vector3 along)
        {
            along = new Vector3(along.x, 0f, along.z);
            if (along.sqrMagnitude < 1e-6f)
                along = Vector3.right;
            along.Normalize();
            Vector3 normal = Vector3.Cross(along, Vector3.up);
            int cell = s_SpriteRandom.Next(4);
            float u0 = cell / 4f, u1 = (cell + 1) / 4f, phase = SR(0f, 1f);
            const int rows = 3;
            int start = mesh.Count;
            for (int k = 0; k <= rows; k++)
            {
                float v = k / (float)rows;
                Vector3 row = top + Vector3.down * k_FlagHeight * v;
                foreach (float s in new[] { -1f, 1f })
                {
                    mesh.Vertices.Add(row + along * s * k_FlagWidth * 0.5f);
                    mesh.Normals.Add(normal);
                    mesh.Uvs.Add(new Vector2(s < 0f ? u0 : u1, 1f - v));
                    mesh.Colors.Add(new Color(phase, v, 0f, 1f));
                }
            }

            for (int k = 0; k < rows; k++)
            {
                int i = start + k * 2;
                mesh.Triangles.AddRange(new[] { i, i + 2, i + 3, i, i + 3, i + 1 });
            }
        }

        // ---------- desenhos ----------

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        static readonly Color k_SnowHi = Hex(0xF2F5FC);

        static SpriteSet PaintSprites()
        {
            var set = new SpriteSet
            {
                Flame = Def("Picos_Sprite_ChamaRuna", PaintRuneFlame(181), 8, 10f, 1f, Color.white * 2.2f),
                Candles = Def("Picos_Sprite_Velas", PaintCandles(191), 4, 6f, 1f, Color.white * 2f),
            };
            set.Flags = FlagMaterial();
            set.LitLantern = GridTextureKit.Surface("Picos_Lanterna_Acesa", PaintLanternGlass(), Color.white * 2.6f);
            return set;
        }

        static SpriteDef Def(string name, Paint paint, int frames, float fps, float follow, Color emission)
        {
            Material material = GridTextureKit.Sprite(name, paint, emission);
            if (frames > 1)
                GridTextureKit.SpriteAnimation[material] = new Vector3(frames, fps, follow);
            else
                GridTextureKit.SpriteAnimation.Remove(material);
            return new SpriteDef
            {
                Material = material,
                Size = new Vector2(paint.W / (float)frames, paint.H) / GridTextureKit.PixelsPerMeter,
            };
        }

        // Pixel com brilho na própria cor (o mapa de brilho guarda a cor; o brilho do material é branco)
        static void GlowPx(Paint p, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= p.W || y >= p.H)
                return;
            p.Set(x, y, c, true);
            p.Glow[y * p.W + x] = c;
        }

        // Pixel só dentro do quadro 'frame' (largura 'fw') de uma tira
        static void FramePx(Paint p, int frame, int fw, int x, int y, Color c, bool glow = false)
        {
            if (x < 0 || x >= fw || y < 0 || y >= p.H)
                return;
            if (glow)
                GlowPx(p, frame * fw + x, y, c);
            else
                p.Set(frame * fw + x, y, c);
        }

        // Chama de runa (8 quadros, 16 x 26): fogo frio, miolo quase branco, ciano e borda violeta; fagulhas
        static Paint PaintRuneFlame(int seed)
        {
            const int frames = 8, fw = 16, fh = 26;
            Paint p = GridTextureKit.Transparent(fw * frames, fh, seed);
            Color core = Hex(0xEAFFFF), bright = Hex(0x72F0FF), middle = Hex(0x8C74FF), dark = Hex(0x4B2FB0);
            const float scale = 0.24f, climb = 3.2f;
            for (int f = 0; f < frames; f++)
            {
                float blend = (float)f / frames, phase = blend * Mathf.PI * 2f;
                for (int y = 0; y < fh; y++)
                for (int x = 0; x < fw; x++)
                {
                    float v = (y + 0.5f) / fh;
                    float sway = (Mathf.Sin(phase - v * 5f) * 0.16f + Mathf.Sin(phase * 2f - v * 9f + 1.3f) * 0.07f) * v;
                    float u = (x + 0.5f) / fw * 2f - 1f - sway;
                    float a = Mathf.PerlinNoise(x * scale + seed, y * scale - f * climb);
                    float b = Mathf.PerlinNoise(x * scale + seed, y * scale - (f - frames) * climb);
                    float noise = Mathf.Lerp(a, b, blend);
                    float width = 0.9f * (1f - v * 0.7f);
                    float heat = (1f - Mathf.Abs(u) / Mathf.Max(width, 0.05f)) * (1.15f - v) + (noise - 0.5f) * 1.3f * v + (1f - v) * 0.15f;
                    if (heat > 0.16f)
                        FramePx(p, f, fw, x, y, heat > 0.72f ? core : heat > 0.5f ? bright : heat > 0.3f ? middle : dark, true);
                }

                var sparks = new System.Random(seed);
                for (int k = 0; k < 4; k++)
                {
                    int sx = sparks.Next(fw / 4, fw * 3 / 4), start = sparks.Next(0, frames);
                    int sy = fh / 2 + (f - start + frames) % frames * (fh / 2) / frames;
                    FramePx(p, f, fw, sx + (int)(Mathf.Sin(phase * 2f + k) * 1.5f), sy, k % 2 == 0 ? bright : core, true);
                }
            }
            return p;
        }

        // Velas votivas (4 quadros, 12 x 10): cinco velas de alturas diferentes numa poça de cera, chamas âmbar
        static Paint PaintCandles(int seed)
        {
            const int frames = 4, fw = 12, fh = 10;
            Paint p = GridTextureKit.Transparent(fw * frames, fh, seed);
            Color wax = Hex(0xE8DDC2), waxShade = Hex(0xB3A68A), wick = Hex(0x2A2420);
            Color flameCore = Hex(0xFFF6D2), amber = Hex(0xFFB347), deep = Hex(0xD9681E);
            (int x, int width, int height)[] candles = { (1, 2, 4), (3, 2, 6), (6, 1, 3), (7, 2, 5), (9, 2, 3) };
            for (int f = 0; f < frames; f++)
            {
                for (int x = 0; x < fw; x++)
                    FramePx(p, f, fw, x, 0, x == 0 || x == fw - 1 ? waxShade : wax);
                int k = 0;
                foreach ((int x0, int width, int height) in candles)
                {
                    for (int y = 1; y <= height; y++)
                    for (int i = 0; i < width; i++)
                        FramePx(p, f, fw, x0 + i, y, i == width - 1 && width > 1 ? waxShade : wax);
                    if (height > 3)
                        FramePx(p, f, fw, x0 - 1, height - 1, wax);   // escorrido
                    int top = height + 1;
                    FramePx(p, f, fw, x0, top, wick);
                    // Chama: 2 ou 3 pixels, balançando um para o lado no alto
                    int tall = (f + k) % 3 == 0 ? 3 : 2;
                    int lean = (f + k * 2) % 4 == 1 ? 1 : (f + k) % 4 == 3 ? -1 : 0;
                    FramePx(p, f, fw, x0, top + 1, deep, true);
                    FramePx(p, f, fw, x0, top + 1, amber, true);
                    if (top + 2 < fh)
                        FramePx(p, f, fw, x0 + (tall == 3 ? 0 : lean), top + 2, flameCore, true);
                    if (tall == 3 && top + 3 < fh)
                        FramePx(p, f, fw, x0 + lean, top + 3, amber, true);
                    k++;
                }
            }
            return p;
        }

        // Vidro da lanterna acesa: luz fria com o caixilho em cruz
        static Paint PaintLanternGlass()
        {
            var p = new Paint(8, 8, 211, Color.black, true);
            Color light = Hex(0xBFEFFF), mid = Hex(0x7FB8FF);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                bool bar = x == 0 || y == 0 || x == 4 || y == 4;
                Color c = bar ? Hex(0x2A2C34) : (x + y) % 3 == 0 ? mid : light;
                p.Set(x, y, c, !bar);
            }
            return p;
        }

        // Bandeirolas (4 cores, 10 x 14 cada): triângulo de pano rasgado, duro de gelo, com bainha e um emblema
        static Material FlagMaterial()
        {
            const int cw = 10, ch = 14;
            Paint p = GridTextureKit.Transparent(cw * 4, ch, 221);
            (Color main, Color dark, Color mark)[] colors =
            {
                (Hex(0x6A3F8F), Hex(0x452763), Hex(0xD8CDB8)),   // violeta com o relógio claro
                (Hex(0xD8CDB8), Hex(0xA99C86), Hex(0x6A3F8F)),   // creme com divisa violeta
                (Hex(0x2C6470), Hex(0x1B3F48), Hex(0xD8CDB8)),   // azul-petróleo
                (Hex(0x8C4632), Hex(0x5C2A1E), Hex(0x2A2420)),   // ferrugem com faixa escura
            };
            for (int c = 0; c < 4; c++)
            {
                (Color main, Color dark, Color mark) = colors[c];
                for (int y = 0; y < ch; y++)
                {
                    float half = (cw * 0.5f) * (y + 1f) / ch;
                    for (int x = 0; x < cw; x++)
                    {
                        float d = Mathf.Abs(x + 0.5f - cw * 0.5f);
                        if (d > half)
                            continue;
                        if (y < 4 && p.Next < 0.18f)
                            continue;   // rasgado na ponta
                        Color col = y >= ch - 1 ? dark : d > half - 1f ? dark : main;
                        float ring = new Vector2(x + 0.5f - cw * 0.5f, y - 9.5f).magnitude;
                        bool emblem = c == 0 ? ring > 1f && ring < 2.2f
                            : c == 1 ? Mathf.Abs(Mathf.Abs(x + 0.5f - cw * 0.5f) - (y - 6f) * 0.6f) < 0.7f && y > 6 && y < 12
                            : c == 2 ? y == 9 && Mathf.Abs(x + 0.5f - cw * 0.5f) < 1.5f
                            : y == 8;
                        if (emblem && y < ch - 1)
                            col = mark;
                        if (y >= ch - 2 && p.Next < 0.3f)
                            col = k_SnowHi;   // geada na bainha
                        p.Set(c * cw + x, y, col);
                    }
                }
            }

            Texture2D texture = GridTextureKit.Save("Picos_Bandeirolas", p.W, p.Pixels, true);
            string path = $"{GridTextureKit.MaterialsFolder}/Picos_Bandeirolas.mat";
            Shader shader = Shader.Find("Icaro/FlagCloth");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader) { name = "Picos_Bandeirolas" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_Amplitude", 0.09f);
            material.SetFloat("_Speed", 5f);
            material.SetFloat("_TimeFollow", 1f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
