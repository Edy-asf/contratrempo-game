using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Assets de combate: o balanceamento (CombatBalance), as fichas dos inimigos (EnemyDefinition), o material das
    // linhas de aviso e o prefab do aviso de spawn. As fichas e o balanceamento só são criados se faltarem, para
    // preservar o que foi ajustado no Inspector; o prefab de cada inimigo é religado à sua ficha a cada geração.
    public static class CombatAssetsBuilder
    {
        public const string Folder = IcaroAssetUtility.Root + "/Balance";
        public const string BalancePath = Folder + "/CombatBalance.asset";
        public const string BrotoPath = Folder + "/Inimigo_Broto.asset";
        public const string EspinhentoPath = Folder + "/Inimigo_Espinhento.asset";
        public const string PredadorPath = Folder + "/Inimigo_Predador.asset";
        public const string GolemPath = Folder + "/Inimigo_Golem.asset";
        public const string CascudoPath = Folder + "/Inimigo_Cascudo.asset";
        public const string VagantePath = Folder + "/Inimigo_Vagante.asset";
        public const string ZangaoPath = Folder + "/Inimigo_Zangao.asset";
        public const string ReiFungoPath = Folder + "/Chefe_ReiFungo.asset";
        public const string SapoPath = Folder + "/Chefe_Sapo.asset";
        public const string TribalPath = Folder + "/Chefe_Tribal.asset";
        public const string DragaoPath = Folder + "/Chefe_Dragao.asset";
        public const string AimAssistPath = Folder + "/AimAssistSettings.asset";
        public const string MusicPath = Folder + "/MusicSettings.asset";
        public const string SpawnWarningPath = IcaroAssetUtility.PrefabsFolder + "/Aviso_Spawn.prefab";

        const string k_LineMaterialName = "Aviso_Linha";
        const string k_SpawnSound = "Assets/FPS/Audio/SFX/Enemies/Hoverbot_Alert.wav";

        public static CombatBalance Balance => AssetDatabase.LoadAssetAtPath<CombatBalance>(BalancePath);
        public static EnemyDefinition Broto => AssetDatabase.LoadAssetAtPath<EnemyDefinition>(BrotoPath);
        public static EnemyDefinition Espinhento => AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EspinhentoPath);
        public static PredadorDefinition Predador => AssetDatabase.LoadAssetAtPath<PredadorDefinition>(PredadorPath);
        public static GolemDefinition Golem => AssetDatabase.LoadAssetAtPath<GolemDefinition>(GolemPath);
        public static CascudoDefinition Cascudo => AssetDatabase.LoadAssetAtPath<CascudoDefinition>(CascudoPath);
        public static VaganteDefinition Vagante => AssetDatabase.LoadAssetAtPath<VaganteDefinition>(VagantePath);
        public static ZangaoDefinition Zangao => AssetDatabase.LoadAssetAtPath<ZangaoDefinition>(ZangaoPath);

        // Material aditivo das linhas de aviso (trajetória, mira, anéis), visível dos dois lados
        public static Material LineMaterial
        {
            get
            {
                Material material = IcaroMaterials.GetOrCreateAdditiveParticle(k_LineMaterialName, null);
                material.SetFloat("_Cull", 0f);
                // Ganho HDR: as cores das linhas (0 a 1) passam do limiar do bloom
                material.SetColor("_BaseColor", new Color(3f, 3f, 3f, 1f));
                EditorUtility.SetDirty(material);
                return material;
            }
        }

        public static void EnsureAll()
        {
            IcaroAssetUtility.EnsureFolder(Folder);

            // Vida e dano dos três tipos originais: +40% sobre os valores anteriores (40/150/180 de vida,
            // 10/8/25 de dano)
            CreateIfMissing<EnemyDefinition>(BrotoPath, d =>
            {
                d.MinArena = 1; d.MaxAlive = 10; d.CronitaValue = 1;
                d.MaxHealth = 56f; d.Damage = 14f; d.AttackCooldown = 0.8f;
            });
            CreateIfMissing<EspinhentoDefinition>(EspinhentoPath, d =>
            {
                d.DisplayName = "Flor-canhão";
                d.MinArena = 3; d.MaxAlive = 4; d.CronitaValue = 3;
                d.MaxHealth = 210f; d.Damage = 11f; d.AttackCooldown = 2.2f;
            });

            // Estrutural (não é balanceamento): o Espinhento só nasce em pontos altos
            Espinhento.RequiresHighSpawn = true;
            EditorUtility.SetDirty(Espinhento);
            CreateIfMissing<PredadorDefinition>(PredadorPath, d =>
            {
                d.DisplayName = "Galhada";
                d.MinArena = 5; d.MaxAlive = 3; d.CronitaValue = 5;
                d.MaxHealth = 250f; d.Damage = 35f; d.AttackCooldown = 4f;
                d.MaxHitFraction = 1f; // sem limite de dano por acerto: escala normalmente
            });
            CreateIfMissing<GolemDefinition>(GolemPath, d =>
            {
                d.MinArena = 7; d.MaxAlive = 3; d.CronitaValue = 9;
                d.MaxHealth = 800f; d.Damage = 45f; d.AttackCooldown = 5f;
                d.MaxHitFraction = 1f; // sem limite de dano por acerto (laser, pancada, chefe)
            });

            // Cascudo: o dobro da vida de um inimigo comum; substitui o Broto nas salas
            CreateIfMissing<CascudoDefinition>(CascudoPath, d =>
            {
                d.DisplayName = "Tatu-lanterna";
                d.MinArena = 1; d.MaxAlive = 6; d.CronitaValue = 3;
                d.MaxHealth = 420f; d.Damage = 22f; d.AttackCooldown = 1.6f;
                // Base = 1/3 do jogador: com teto de movimento 6, chega no máximo ao dobro da velocidade do jogador
                d.MovementMultiplierCap = 6f;
            });
            // Vagante: vida média, dano baixo por projétil
            CreateIfMissing<VaganteDefinition>(VagantePath, d =>
            {
                d.DisplayName = "Explorador Tomado";
                d.MinArena = 4; d.MaxAlive = 3; d.CronitaValue = 4;
                d.MaxHealth = 180f; d.Damage = 6f; d.AttackCooldown = 2.5f; d.ProjectileSpeed = 24f;
            });

            // Zangão: voador pequeno em grupos de 3 a 5, cerca de 1/4 da vida de um inimigo comum (morre com 1 ou 2
            // tiros normais), dano baixo por projétil rápido; reaproveitado pelo PrefabPool
            CreateIfMissing<ZangaoDefinition>(ZangaoPath, d =>
            {
                d.DisplayName = "Mariposa-lanterna";
                d.MinArena = 2; d.MaxAlive = 14; d.GroupMin = 3; d.GroupMax = 5; d.Pooled = true;
                d.CronitaValue = 1;
                d.MaxHealth = 52f; d.Damage = 4f; d.AttackCooldown = 2.5f; d.ProjectileSpeed = 45f;
            });

            BossKit kit = BossPrefabBuilder.BuildKit();
            EnsureBosses(kit);

            GameObject spawnWarning = BuildSpawnWarning();
            CreateIfMissing<CombatBalance>(BalancePath, b => { });

            CombatBalance balance = Balance;
            if (balance.SpawnTables == null || balance.SpawnTables.Length == 0)
                balance.SpawnTables = DefaultSpawnTables();
            AddToTables(balance, Zangao, k_ZangaoWeights);
            balance.SpawnWarningPrefab = spawnWarning;
            balance.TimeOrbs.Prefab = TimeOrbBuilder.Build();
            ArenaDomeBuilder.Build();
            balance.Items = ItemAssetsBuilder.EnsureAll();
            CreateIfMissing<AimAssistSettings>(AimAssistPath, s => { });
            balance.AimAssist = AssetDatabase.LoadAssetAtPath<AimAssistSettings>(AimAssistPath);
            CreateIfMissing<MusicSettings>(MusicPath, s => { });
            balance.Music = AssetDatabase.LoadAssetAtPath<MusicSettings>(MusicPath);
            balance.Kit = kit;
            balance.Shop = ShopPrefabBuilder.Build();
            WeaponAssetsBuilder.EnsureAll();
            balance.EliteMaterial = BossPrefabBuilder.Additive("Elite_Brilho");
            balance.Bosses = new EnemyDefinition[]
            {
                AssetDatabase.LoadAssetAtPath<ReiFungoDefinition>(ReiFungoPath),
                AssetDatabase.LoadAssetAtPath<SapoDefinition>(SapoPath),
                AssetDatabase.LoadAssetAtPath<TribalDefinition>(TribalPath),
                AssetDatabase.LoadAssetAtPath<DragaoDefinition>(DragaoPath),
            };
            EditorUtility.SetDirty(balance);
            AssetDatabase.SaveAssets();
        }

        // Chefes dos quatro biomas, cada vez mais difíceis: vida da ficha (antes da escala por sala, x1,25 de bônus de
        // chefe), dano de contato e cor dos ataques. Os prefabs vêm do BossPrefabBuilder (modelos cartoon).
        static void EnsureBosses(BossKit kit)
        {
            CreateIfMissing<ReiFungoDefinition>(ReiFungoPath, d =>
            {
                d.DisplayName = "Rei Fungo"; d.MaxHealth = 5000f; d.Damage = 20f;
                d.MoveSpeed = 3f; d.PreferredDistance = 12f; d.AttackGap = 1.8f;
                d.AttackColor = new Color(0.45f, 1f, 0.7f, 0.9f);
            });
            CreateIfMissing<SapoDefinition>(SapoPath, d =>
            {
                d.DisplayName = "Sapo Rei do Brejo"; d.MaxHealth = 6500f; d.Damage = 22f;
                d.PreferredDistance = 14f; d.AttackGap = 1.6f;
                d.AttackColor = new Color(1f, 0.85f, 0.2f, 0.9f);
            });
            CreateIfMissing<TribalDefinition>(TribalPath, d =>
            {
                d.DisplayName = "Oráculo"; d.MaxHealth = 8000f; d.Damage = 24f;
                d.MoveSpeed = 4f; d.PreferredDistance = 15f; d.AttackGap = 1.5f; d.PhaseTwoAt = 0.55f;
                d.AttackColor = new Color(0.3f, 0.95f, 1f, 0.9f);
            });
            CreateIfMissing<DragaoDefinition>(DragaoPath, d =>
            {
                d.DisplayName = "Dragão de Magma"; d.MaxHealth = 10000f; d.Damage = 28f;
                d.MoveSpeed = 3.5f; d.PreferredDistance = 18f; d.AttackGap = 1.4f;
                d.PhaseTwoAt = 0.6f; d.PhaseThreeAt = 0.25f;
                d.AttackColor = new Color(1f, 0.45f, 0.1f, 0.95f);
            });

            // Estrutural: todos usam o kit compartilhado
            foreach (string path in new[] { ReiFungoPath, SapoPath, TribalPath, DragaoPath })
            {
                var definition = AssetDatabase.LoadAssetAtPath<BossDefinition>(path);
                definition.Kit = kit;
                EditorUtility.SetDirty(definition);
            }

            BossPrefabBuilder.BuildBoss<ReiFungoAI>(AssetDatabase.LoadAssetAtPath<ReiFungoDefinition>(ReiFungoPath),
                "Chefe_ReiFungo", 0.55f, new Color(0.4f, 0.7f, 0.9f));
            BossPrefabBuilder.BuildBoss<SapoAI>(AssetDatabase.LoadAssetAtPath<SapoDefinition>(SapoPath),
                "Chefe_Sapo", 0.7f, new Color(0.9f, 0.8f, 0.2f));
            BossPrefabBuilder.BuildBoss<TribalAI>(AssetDatabase.LoadAssetAtPath<TribalDefinition>(TribalPath),
                "Chefe_Tribal", 0.75f, new Color(0.7f, 0.25f, 0.2f));
            BossPrefabBuilder.BuildBoss<DragaoAI>(AssetDatabase.LoadAssetAtPath<DragaoDefinition>(DragaoPath),
                "Chefe_Dragao", 0.8f, new Color(0.9f, 0.45f, 0.15f));
        }

        // Pesos iniciais por bioma e faixa de salas: os fortes (Predador, Golem, Vagante) pesam mais conforme o índice
        // sobe. O MinArena de cada ficha continua valendo (ex.: Golem só a partir da sala 7).
        static CombatBalance.SpawnTable[] DefaultSpawnTables()
        {
            return new[]
            {
                Table("Bosque 1-3", 1, 3, (Cascudo, 4f), (Espinhento, 2f)),
                Table("Bosque 4-6", 4, 6, (Cascudo, 3f), (Espinhento, 2f), (Vagante, 1.5f), (Predador, 1f)),
                Table("Charco 7-9", 7, 9, (Cascudo, 2.5f), (Espinhento, 2f), (Vagante, 2f), (Predador, 1.6f),
                    (Golem, 1f)),
                Table("Charco 10-11", 10, 11, (Cascudo, 2f), (Espinhento, 2f), (Vagante, 2f), (Predador, 2f),
                    (Golem, 1.5f)),
                Table("Ruínas 12-16", 12, 16, (Cascudo, 1.8f), (Espinhento, 2f), (Vagante, 2.2f), (Predador, 2.2f),
                    (Golem, 2f)),
                Table("Cratera 17-20", 17, 20, (Cascudo, 1.5f), (Espinhento, 2f), (Vagante, 2.4f), (Predador, 2.5f),
                    (Golem, 2.6f)),
            };
        }

        // Peso do Zangão por faixa (na ordem das tabelas iniciais); só a partir da sala 2 (MinArena)
        static readonly float[] k_ZangaoWeights = { 1.5f, 2f, 2.5f, 2.5f, 3f, 3f };

        // Acrescenta um tipo novo às tabelas que ainda não o têm (preserva os pesos já ajustados)
        static void AddToTables(CombatBalance balance, EnemyDefinition enemy, float[] weights)
        {
            if (!enemy || balance.SpawnTables == null)
                return;
            for (int i = 0; i < balance.SpawnTables.Length; i++)
            {
                CombatBalance.SpawnTable table = balance.SpawnTables[i];
                if (table == null)
                    continue;
                var list = new System.Collections.Generic.List<CombatBalance.SpawnWeight>(
                    table.Weights ?? new CombatBalance.SpawnWeight[0]);
                if (list.Exists(w => w != null && w.Enemy == enemy))
                    continue;
                list.Add(new CombatBalance.SpawnWeight { Enemy = enemy, Weight = weights[Mathf.Min(i, weights.Length - 1)] });
                table.Weights = list.ToArray();
            }
        }

        static CombatBalance.SpawnTable Table(string name, int from, int to,
            params (EnemyDefinition enemy, float weight)[] weights)
        {
            var table = new CombatBalance.SpawnTable { Name = name, FromRoom = from, ToRoom = to };
            table.Weights = new CombatBalance.SpawnWeight[weights.Length];
            for (int i = 0; i < weights.Length; i++)
                table.Weights[i] = new CombatBalance.SpawnWeight { Enemy = weights[i].enemy, Weight = weights[i].weight };
            return table;
        }

        // Religa a ficha ao prefab gerado (o RoomManager instancia Definition.Prefab)
        public static void Link(EnemyDefinition definition, string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!definition || !prefab)
                return;
            definition.Prefab = prefab;
            EditorUtility.SetDirty(definition);
        }

        static GameObject BuildSpawnWarning()
        {
            var root = new GameObject("Aviso_Spawn");
            var warning = root.AddComponent<SpawnWarning>();
            warning.LineMaterial = LineMaterial;
            warning.Sound = AssetDatabase.LoadAssetAtPath<AudioClip>(k_SpawnSound);
            return IcaroPrefabBuilder.SavePrefab(root, SpawnWarningPath);
        }

        static void CreateIfMissing<T>(string path, System.Action<T> configure) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path))
                return;

            T asset = ScriptableObject.CreateInstance<T>();
            configure(asset);
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
