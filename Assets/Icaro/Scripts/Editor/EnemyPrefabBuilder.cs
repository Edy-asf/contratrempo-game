using System.Collections.Generic;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro.EditorTools
{
    // Monta Broto, Espinhento, Predador, Cascudo e Vagante com primitivas sobre o sistema de inimigos do Microgame:
    // Health + Damageable (a arma do jogador funciona neles), Actor (afiliação dos inimigos),
    // EnemyController (registro no EnemyManager, flash ao levar dano, explosão e loot), DetectionModule,
    // NavMeshAgent e EnemyTimeScaler. O Zangão (voador, com PrefabPool) usa só Health, Damageable e Actor.
    // Chamado pelo menu "Ícaro > Gerar prefabs".
    public static class EnemyPrefabBuilder
    {
        public const string BrotoPath = IcaroAssetUtility.PrefabsFolder + "/Broto.prefab";
        public const string EspinhentoPath = IcaroAssetUtility.PrefabsFolder + "/Espinhento.prefab";
        public const string PredadorPath = IcaroAssetUtility.PrefabsFolder + "/Predador.prefab";
        public const string CascudoPath = IcaroAssetUtility.PrefabsFolder + "/Cascudo.prefab";
        public const string VagantePath = IcaroAssetUtility.PrefabsFolder + "/Vagante.prefab";
        public const string VaganteProjectilePath = IcaroAssetUtility.PrefabsFolder + "/Projectile_Vagante.prefab";
        public const string ZangaoPath = IcaroAssetUtility.PrefabsFolder + "/Zangao.prefab";
        public const string ZangaoProjectilePath = IcaroAssetUtility.PrefabsFolder + "/Projectile_Zangao.prefab";
        public const string SpikeProjectilePath = IcaroAssetUtility.PrefabsFolder + "/Projectile_Espinho.prefab";

        const string k_HoverBotSource = "Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab";
        const string k_TurretProjectileSource = "Assets/FPS/Prefabs/Projectiles/Projectile_Turret.prefab";
        const string k_HoverBotProjectileSource = "Assets/FPS/Prefabs/Projectiles/Projectile_Hoverbot.prefab";
        const string k_VaganteShotSound = "Assets/FPS/Audio/SFX/Enemies/Hoverbot_Attack.wav";
        // Alcance de detecção do Espinhento: na prática, a arena inteira (o limite é a linha de visão)
        const float k_EspinhentoRange = 300f;

        // Vida do espinho: a 18 m/s cruza 180 m, mais que o diâmetro da maior arena (124 m)
        const float k_SpikeLifetime = 10f;

        const string k_WarningSound = "Assets/FPS/Audio/SFX/Enemies/Turret_Alert.mp3";

        // Afiliação dos inimigos no Microgame (o jogador é 1)
        const int k_EnemyAffiliation = 0;

        public static void BuildAll()
        {
            var hoverBot = AssetDatabase.LoadAssetAtPath<GameObject>(k_HoverBotSource);
            EnemyController reference = hoverBot ? hoverBot.GetComponent<EnemyController>() : null;
            if (!reference)
            {
                Debug.LogError("[Ícaro] HoverBot não encontrado; os inimigos novos precisam dele como referência.");
                return;
            }

            CombatAssetsBuilder.EnsureAll();

            BuildBroto(reference);
            BuildEspinhento(reference, BuildSpikeProjectile());
            BuildPredador(reference);
            BuildCascudo(reference);
            BuildVagante(reference);
            BuildZangao();
            GolemPrefabBuilder.Build();

            // O RoomManager instancia o prefab ligado a cada ficha
            CombatAssetsBuilder.Link(CombatAssetsBuilder.Broto, BrotoPath);
            CombatAssetsBuilder.Link(CombatAssetsBuilder.Espinhento, EspinhentoPath);
            CombatAssetsBuilder.Link(CombatAssetsBuilder.Predador, PredadorPath);
            CombatAssetsBuilder.Link(CombatAssetsBuilder.Cascudo, CascudoPath);
            CombatAssetsBuilder.Link(CombatAssetsBuilder.Vagante, VagantePath);
            CombatAssetsBuilder.Link(CombatAssetsBuilder.Zangao, ZangaoPath);
            CombatAssetsBuilder.Link(CombatAssetsBuilder.Golem, GolemPrefabBuilder.GolemPath);
            AssetDatabase.SaveAssets();
        }

        static void BuildBroto(EnemyController reference)
        {
            Material body = IcaroMaterials.GetOrCreate("Broto_Corpo", new Color(0.55f, 0.85f, 0.35f));
            Material leaf = IcaroMaterials.GetOrCreate("Broto_Folha", new Color(0.2f, 0.55f, 0.18f));
            Material eye = IcaroMaterials.GetOrCreate("Inimigo_Olho", new Color(0.05f, 0.05f, 0.05f),
                new Color(0.6f, 1.2f, 0.2f));

            GameObject root = CreateEnemyRoot("Broto", reference, body, CombatAssetsBuilder.Broto.MaxHealth, height: 1f,
                detectionRange: 25f, attackRange: 1.5f);
            AddCapsule(root, radius: 0.45f, height: 1f);
            ConfigureAgent(root, radius: 0.45f, height: 1f, speed: 7f, acceleration: 25f, stoppingDistance: 0.3f);

            var visual = new GameObject("Corpo").transform;
            visual.SetParent(root.transform, false);
            Part(PrimitiveType.Sphere, "Bulbo", visual, new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 0.85f, 0.9f), body);
            GameObject leafPart = Part(PrimitiveType.Cube, "Folha", visual, new Vector3(0.1f, 1.05f, 0f),
                new Vector3(0.12f, 0.45f, 0.35f), leaf);
            leafPart.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
            Part(PrimitiveType.Cube, "Olho_E", visual, new Vector3(-0.17f, 0.6f, 0.4f), new Vector3(0.12f, 0.16f, 0.06f), eye);
            Part(PrimitiveType.Cube, "Olho_D", visual, new Vector3(0.17f, 0.6f, 0.4f), new Vector3(0.12f, 0.16f, 0.06f), eye);

            AddEmptyWeapon(root, visual, new Vector3(0f, 0.5f, 0.45f));

            var ai = root.AddComponent<BrotoAI>();
            ai.Body = visual;
            AddStats(root, CombatAssetsBuilder.Broto);
            AddHitReaction(root, recoil: 0.4f, centerHeight: 0.5f, burstScale: 0.8f);

            Save(root, BrotoPath);
        }

        static void BuildEspinhento(EnemyController reference, ProjectileBase spike)
        {
            Material body = IcaroMaterials.GetOrCreate("Espinhento_Corpo", new Color(0.42f, 0.32f, 0.52f));
            Material baseMaterial = IcaroMaterials.GetOrCreate("Espinhento_Base", new Color(0.25f, 0.21f, 0.18f));
            Material spikeMaterial = IcaroMaterials.GetOrCreate("Espinhento_Espinho", new Color(0.88f, 0.86f, 0.7f),
                new Color(0.3f, 0.25f, 0.1f));

            GameObject root = CreateEnemyRoot("Espinhento", reference, body, CombatAssetsBuilder.Espinhento.MaxHealth,
                height: 2.4f, detectionRange: k_EspinhentoRange, attackRange: k_EspinhentoRange);
            AddCapsule(root, radius: 0.7f, height: 2.4f);
            ConfigureAgent(root, radius: 0.7f, height: 2.4f, speed: 0f, acceleration: 0f, stoppingDistance: 0f);

            // Com a folha de sprites, o visual é a Flor-canhão e a cabeça vira um ponto vazio que gira para mirar (a
            // arma é filha dela); sem ela, as primitivas
            EnemySpriteSet sprites = EnemySpriteImporter.SetFor("Espinhento");
            Transform head, ring = null;
            if (sprites)
            {
                AddSpriteVisual(root, sprites);
                head = new GameObject("Cabeca").transform;
                head.SetParent(root.transform, false);
                head.localPosition = new Vector3(0f, 1.95f, 0f);
            }
            else
            {
                var visual = new GameObject("Corpo").transform;
                visual.SetParent(root.transform, false);
                Part(PrimitiveType.Cylinder, "Raiz", visual, new Vector3(0f, 0.15f, 0f), new Vector3(1.5f, 0.15f, 1.5f),
                    baseMaterial);
                Part(PrimitiveType.Cylinder, "Caule", visual, new Vector3(0f, 0.95f, 0f), new Vector3(1f, 0.8f, 1f), body);

                // Anel de espinhos decorativos que gira em volta do caule
                ring = new GameObject("AnelDeEspinhos").transform;
                ring.SetParent(visual, false);
                ring.localPosition = new Vector3(0f, 1f, 0f);
                for (int i = 0; i < 8; i++)
                {
                    GameObject s = Part(PrimitiveType.Cube, "Espinho", ring, Vector3.zero, new Vector3(0.1f, 0.1f, 1.7f),
                        spikeMaterial);
                    s.transform.localRotation = Quaternion.Euler(-15f, i * 45f, 0f);
                }

                // Cabeça que gira para mirar; a arma é filha dela
                head = new GameObject("Cabeca").transform;
                head.SetParent(visual, false);
                head.localPosition = new Vector3(0f, 1.95f, 0f);
                Part(PrimitiveType.Sphere, "Bulbo", head, Vector3.zero, new Vector3(1.1f, 0.9f, 1.1f), body);
                for (int i = 0; i < 5; i++)
                {
                    GameObject s = Part(PrimitiveType.Cube, "Espinho", head, new Vector3(0f, 0.35f, 0f),
                        new Vector3(0.08f, 0.08f, 0.8f), spikeMaterial);
                    s.transform.localRotation = Quaternion.Euler(-50f, i * 72f, 0f);
                }
                Part(PrimitiveType.Cube, "Boca", head, new Vector3(0f, 0f, 0.55f), new Vector3(0.3f, 0.3f, 0.2f), spikeMaterial);
            }

            WeaponController weapon = AddWeapon(root, head, new Vector3(0f, 0f, 0.75f), spike);
            weapon.ShootType = WeaponShootType.Automatic;
            weapon.DelayBetweenShots = 0.05f; // a cadência é do EspinhentoAI
            weapon.BulletsPerShot = 3;
            weapon.BulletSpreadAngle = 6f;
            weapon.MaxAmmo = 30;
            weapon.AmmoReloadRate = 10f;
            weapon.AmmoReloadDelay = 0.5f;

            var ai = root.AddComponent<EspinhentoAI>();
            ai.Head = head;
            ai.SpikeRing = ring;
            AddStats(root, CombatAssetsBuilder.Espinhento);
            AddHitReaction(root, recoil: 0.06f, centerHeight: 1.3f, burstScale: 1.2f);

            Save(root, EspinhentoPath);
        }

        // Cascudo: corpo largo e baixo com uma casca em cima. O visual fica no filho "Visual" (trocável por modelo);
        // o CascudoAI anima o Visual (recua e avança no golpe) e a "Casca" (ergue na preparação). Com a folha de
        // sprites importada (Art/Sprites/Inimigos/Cascudo), o Visual vira o sprite animado (EnemySpriteView).
        static void BuildCascudo(EnemyController reference)
        {
            CascudoDefinition definition = CombatAssetsBuilder.Cascudo;
            Material body = IcaroMaterials.GetOrCreate("Cascudo_Corpo", new Color(0.24f, 0.22f, 0.16f));
            Material shellMaterial = IcaroMaterials.GetOrCreate("Cascudo_Casca", new Color(0.18f, 0.32f, 0.3f),
                new Color(0.05f, 0.25f, 0.22f), 0.55f);
            Material eye = IcaroMaterials.GetOrCreate("Inimigo_Olho", new Color(0.05f, 0.05f, 0.05f),
                new Color(0.6f, 1.2f, 0.2f));

            GameObject root = CreateEnemyRoot("Cascudo", reference, body, definition.MaxHealth, height: 1.4f,
                detectionRange: 40f, attackRange: definition.AttackRange);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.75f, 0.05f);
            box.size = new Vector3(1.9f, 1.5f, 2f);
            root.AddComponent<Damageable>();
            ConfigureAgent(root, radius: 0.9f, height: 1.4f, speed: definition.FallbackSpeed, acceleration: 20f,
                stoppingDistance: 1.5f);

            EnemySpriteSet sprites = EnemySpriteImporter.SetFor("Cascudo");
            var ai = root.AddComponent<CascudoAI>();
            if (sprites)
            {
                ai.Visual = AddSpriteVisual(root, sprites);
                AddEmptyWeapon(root, root.transform, new Vector3(0f, 0.6f, 1.2f));
                AddStats(root, definition);
                AddHitReaction(root, recoil: 0.12f, centerHeight: 0.7f, burstScale: 1.3f);
                Save(root, CascudoPath);
                return;
            }

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            ai.Visual = visual;
            Part(PrimitiveType.Cube, "Corpo", visual, new Vector3(0f, 0.55f, 0f), new Vector3(1.8f, 0.75f, 1.7f), body);
            Part(PrimitiveType.Cube, "Cabeca", visual, new Vector3(0f, 0.55f, 1.0f), new Vector3(0.8f, 0.5f, 0.5f), body);
            Part(PrimitiveType.Cube, "Olho_E", visual, new Vector3(-0.2f, 0.65f, 1.26f), new Vector3(0.14f, 0.1f, 0.05f), eye);
            Part(PrimitiveType.Cube, "Olho_D", visual, new Vector3(0.2f, 0.65f, 1.26f), new Vector3(0.14f, 0.1f, 0.05f), eye);
            foreach (Vector3 p in new[]
                     {
                         new Vector3(-0.75f, 0.2f, 0.6f), new Vector3(0.75f, 0.2f, 0.6f),
                         new Vector3(-0.75f, 0.2f, -0.6f), new Vector3(0.75f, 0.2f, -0.6f),
                     })
                Part(PrimitiveType.Cylinder, "Pata", visual, p, new Vector3(0.3f, 0.2f, 0.3f), body);

            var shell = new GameObject("Casca").transform;
            shell.SetParent(visual, false);
            shell.localPosition = new Vector3(0f, 0.9f, -0.1f);
            Part(PrimitiveType.Sphere, "Cupula", shell, Vector3.zero, new Vector3(2.1f, 0.9f, 2f), shellMaterial);
            for (int i = -1; i <= 1; i++)
                Part(PrimitiveType.Cube, "Crista", shell, new Vector3(i * 0.5f, 0.42f, 0f), new Vector3(0.12f, 0.18f, 1.6f),
                    shellMaterial);

            AddEmptyWeapon(root, root.transform, new Vector3(0f, 0.6f, 1.2f));

            ai.Shell = shell;
            AddStats(root, definition);
            AddHitReaction(root, recoil: 0.12f, centerHeight: 0.7f, burstScale: 1.3f);

            Save(root, CascudoPath);
        }

        // Vagante: humanoide (cápsula com cabeça) segurando uma arma de primitivas. O visual fica no filho "Visual";
        // a arma gira num pivô ("Mira") dentro dele, e o brilho de aviso fica na ponta do cano.
        static void BuildVagante(EnemyController reference)
        {
            VaganteDefinition definition = CombatAssetsBuilder.Vagante;
            Material body = IcaroMaterials.GetOrCreate("Vagante_Corpo", new Color(0.3f, 0.27f, 0.36f));
            Material gunMaterial = IcaroMaterials.GetOrCreate("Vagante_Arma", new Color(0.12f, 0.12f, 0.14f), default, 0.6f);
            Material glow = IcaroMaterials.GetOrCreate("Vagante_Brilho", new Color(1f, 0.5f, 0.2f),
                new Color(4f, 1.4f, 0.3f), 0.6f);
            Material eye = IcaroMaterials.GetOrCreate("Inimigo_Olho", new Color(0.05f, 0.05f, 0.05f),
                new Color(0.6f, 1.2f, 0.2f));

            GameObject root = CreateEnemyRoot("Vagante", reference, body, definition.MaxHealth, height: 1.9f,
                detectionRange: 40f, attackRange: definition.AttackRange);
            AddCapsule(root, radius: 0.4f, height: 1.9f);
            ConfigureAgent(root, radius: 0.45f, height: 1.9f, speed: definition.MoveSpeed, acceleration: 10f,
                stoppingDistance: 0.5f);

            // Com a folha de sprites, o visual é o Explorador Tomado; o pivô da mira e o cano ficam como pontos vazios
            EnemySpriteSet sprites = EnemySpriteImporter.SetFor("Vagante");
            if (sprites)
            {
                AddSpriteVisual(root, sprites);
                var aimPoint = new GameObject("Mira").transform;
                aimPoint.SetParent(root.transform, false);
                aimPoint.localPosition = new Vector3(0.3f, 1.3f, 0.1f);
                WeaponController gun = AddWeapon(root, aimPoint, new Vector3(0f, 0.04f, 0.88f), BuildVaganteProjectile());
                gun.ShootType = WeaponShootType.Manual;
                var spriteAi = root.AddComponent<VaganteAI>();
                spriteAi.AimPivot = aimPoint;
                spriteAi.Muzzle = gun.WeaponMuzzle;
                spriteAi.ShotSound = AssetDatabase.LoadAssetAtPath<AudioClip>(k_VaganteShotSound);
                AddStats(root, definition);
                AddHitReaction(root, recoil: 0.3f, centerHeight: 1f, burstScale: 1f);
                Save(root, VagantePath);
                return;
            }

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Part(PrimitiveType.Capsule, "Corpo", visual, new Vector3(0f, 0.8f, 0f), new Vector3(0.7f, 0.75f, 0.5f), body);
            Part(PrimitiveType.Sphere, "Cabeca", visual, new Vector3(0f, 1.72f, 0f), Vector3.one * 0.42f, body);
            Part(PrimitiveType.Cube, "Visor", visual, new Vector3(0f, 1.74f, 0.19f), new Vector3(0.3f, 0.08f, 0.06f), eye);

            var aim = new GameObject("Mira").transform;
            aim.SetParent(visual, false);
            aim.localPosition = new Vector3(0.3f, 1.3f, 0.1f);
            Part(PrimitiveType.Cylinder, "Braco", aim, new Vector3(0f, -0.05f, 0.15f), new Vector3(0.12f, 0.18f, 0.12f), body)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Cube, "Arma", aim, new Vector3(0f, 0.02f, 0.35f), new Vector3(0.12f, 0.16f, 0.5f), gunMaterial);
            Part(PrimitiveType.Cylinder, "Cano", aim, new Vector3(0f, 0.04f, 0.7f), new Vector3(0.06f, 0.16f, 0.06f), gunMaterial)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            WeaponController weapon = AddWeapon(root, aim, new Vector3(0f, 0.04f, 0.88f), BuildVaganteProjectile());
            weapon.ShootType = WeaponShootType.Manual;

            GameObject flash = Part(PrimitiveType.Sphere, "BrilhoCano", weapon.WeaponMuzzle, Vector3.zero,
                Vector3.one * 0.35f, glow);

            var ai = root.AddComponent<VaganteAI>();
            ai.AimPivot = aim;
            ai.Muzzle = weapon.WeaponMuzzle;
            ai.MuzzleGlow = flash.transform;
            ai.ShotSound = AssetDatabase.LoadAssetAtPath<AudioClip>(k_VaganteShotSound);
            AddStats(root, definition);
            AddHitReaction(root, recoil: 0.3f, centerHeight: 1f, burstScale: 1f);

            Save(root, VagantePath);
        }

        // Projétil do Vagante: variante do projétil do HoverBot. Velocidade e dano vêm da ficha (EnemyTimeScaler).
        static ProjectileBase BuildVaganteProjectile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ProjectileBase>(VaganteProjectilePath);
            if (existing)
                return existing;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(k_HoverBotProjectileSource);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.GetComponent<ProjectileStandard>().MaxLifeTime = 5f;
            instance.name = "Projectile_Vagante";
            GameObject saved = IcaroPrefabBuilder.SavePrefab(instance, VaganteProjectilePath);
            return saved ? saved.GetComponent<ProjectileBase>() : null;
        }

        // Zangão: inseto voador pequeno. Sem NavMeshAgent nem EnemyController (voo próprio e PrefabPool): Health,
        // Damageable e Actor do Microgame, um colisor de esfera com Rigidbody cinemático (colisor que se move) e o
        // visual no filho "Visual" (corpo, asas, ferrão). Materiais com GPU instancing.
        static void BuildZangao()
        {
            ZangaoDefinition definition = CombatAssetsBuilder.Zangao;
            Material body = IcaroMaterials.GetOrCreate("Zangao_Corpo", new Color(0.22f, 0.2f, 0.12f), default, 0.55f);
            Material stripe = IcaroMaterials.GetOrCreate("Zangao_Faixa", new Color(0.75f, 0.55f, 0.12f),
                new Color(0.25f, 0.15f, 0f), 0.5f);
            Material wing = IcaroMaterials.GetOrCreate("Zangao_Asa", new Color(0.7f, 0.85f, 0.9f),
                new Color(0.05f, 0.15f, 0.18f), 0.8f);
            Material glow = IcaroMaterials.GetOrCreate("Zangao_Brilho", new Color(1f, 0.85f, 0.3f),
                new Color(4f, 2.6f, 0.4f), 0.6f);
            Material eye = IcaroMaterials.GetOrCreate("Inimigo_Olho", new Color(0.05f, 0.05f, 0.05f),
                new Color(0.6f, 1.2f, 0.2f));

            var root = new GameObject("Zangao");
            var health = root.AddComponent<Health>();
            health.MaxHealth = definition.MaxHealth;

            var aimPoint = new GameObject("AimPoint").transform;
            aimPoint.SetParent(root.transform, false);
            var actor = root.AddComponent<Actor>();
            actor.Affiliation = k_EnemyAffiliation;
            actor.AimPoint = aimPoint;

            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = 0.56f;
            root.AddComponent<Damageable>();
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            // Com a folha de sprites, o visual é a Mariposa-lanterna (pivô no centro do corpo); o cano fica vazio
            EnemySpriteSet sprites = EnemySpriteImporter.SetFor("Zangao");
            if (sprites)
            {
                Transform spriteVisual = AddSpriteVisual(root, sprites);
                var spriteMuzzle = new GameObject("Cano").transform;
                spriteMuzzle.SetParent(root.transform, false);
                spriteMuzzle.localPosition = new Vector3(0f, -0.1f, 0.4f);
                var spriteAi = root.AddComponent<ZangaoAI>();
                spriteAi.Visual = spriteVisual;
                spriteAi.Wings = new Transform[0];
                spriteAi.Muzzle = spriteMuzzle;
                spriteAi.ProjectilePrefab = BuildZangaoProjectile(glow);
                spriteAi.ShotSound = AssetDatabase.LoadAssetAtPath<AudioClip>(k_VaganteShotSound);
                spriteAi.FlashRenderers = new Renderer[0];
                AddStats(root, definition);
                AddHitReaction(root, recoil: 0f, centerHeight: 0f, burstScale: 0.45f);
                Save(root, ZangaoPath);
                return;
            }

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            // Um pouco maior que as medidas das peças (o colisor acompanha)
            visual.localScale = Vector3.one * 1.25f;
            var parts = new List<Renderer>
            {
                Part(PrimitiveType.Sphere, "Torax", visual, new Vector3(0f, 0f, 0.05f), new Vector3(0.32f, 0.3f, 0.36f), body)
                    .GetComponent<Renderer>(),
                Part(PrimitiveType.Sphere, "Abdomen", visual, new Vector3(0f, -0.02f, -0.3f), new Vector3(0.3f, 0.28f, 0.45f), stripe)
                    .GetComponent<Renderer>(),
                Part(PrimitiveType.Sphere, "Cabeca", visual, new Vector3(0f, 0.03f, 0.3f), Vector3.one * 0.22f, body)
                    .GetComponent<Renderer>(),
            };
            Part(PrimitiveType.Cube, "Olhos", visual, new Vector3(0f, 0.07f, 0.39f), new Vector3(0.2f, 0.06f, 0.04f), eye);

            var wings = new List<Transform>();
            foreach (float side in new[] { -1f, 1f })
            {
                var pivot = new GameObject(side < 0f ? "Asa_E" : "Asa_D").transform;
                pivot.SetParent(visual, false);
                pivot.localPosition = new Vector3(0.1f * side, 0.14f, 0.05f);
                Part(PrimitiveType.Cube, "Membrana", pivot, new Vector3(0.28f * side, 0f, -0.05f),
                    new Vector3(0.5f, 0.015f, 0.2f), wing);
                wings.Add(pivot);
            }

            // Ferrão na frente, com o cano na ponta
            GameObject stinger = Part(PrimitiveType.Cylinder, "Ferrao", visual, new Vector3(0f, -0.05f, 0.52f),
                new Vector3(0.05f, 0.12f, 0.05f), stripe);
            stinger.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var muzzle = new GameObject("Cano").transform;
            muzzle.SetParent(visual, false);
            muzzle.localPosition = new Vector3(0f, -0.05f, 0.66f);
            GameObject flash = Part(PrimitiveType.Sphere, "BrilhoFerrao", muzzle, Vector3.zero, Vector3.one * 0.18f, glow);

            var ai = root.AddComponent<ZangaoAI>();
            ai.Visual = visual;
            ai.Wings = wings.ToArray();
            ai.Muzzle = muzzle;
            ai.MuzzleGlow = flash.transform;
            ai.ProjectilePrefab = BuildZangaoProjectile(glow);
            ai.ShotSound = AssetDatabase.LoadAssetAtPath<AudioClip>(k_VaganteShotSound);
            ai.FlashRenderers = parts.ToArray();
            AddStats(root, definition);
            AddHitReaction(root, recoil: 0f, centerHeight: 0f, burstScale: 0.45f);

            Save(root, ZangaoPath);
        }

        // Projétil do Zangão: esfera alongada brilhante, sem colisor (o ZangaoProjectile testa com raycast)
        static ZangaoProjectile BuildZangaoProjectile(Material glow)
        {
            var root = new GameObject("Projectile_Zangao");
            GameObject shape = Part(PrimitiveType.Sphere, "Forma", root.transform, Vector3.zero,
                new Vector3(0.09f, 0.09f, 0.4f), glow);
            shape.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.AddComponent<ZangaoProjectile>();
            GameObject saved = IcaroPrefabBuilder.SavePrefab(root, ZangaoProjectilePath);
            return saved ? saved.GetComponent<ZangaoProjectile>() : null;
        }

        static void BuildPredador(EnemyController reference)
        {
            Material body = IcaroMaterials.GetOrCreate("Predador_Corpo", new Color(0.42f, 0.08f, 0.08f));
            Material leg = IcaroMaterials.GetOrCreate("Predador_Pata", new Color(0.18f, 0.06f, 0.05f));
            Material warning = IcaroMaterials.GetOrCreate("Predador_Alerta", new Color(1f, 0.8f, 0.35f),
                new Color(1.2f, 0.6f, 0.1f));

            GameObject root = CreateEnemyRoot("Predador", reference, body, CombatAssetsBuilder.Predador.MaxHealth,
                height: 1.6f, detectionRange: 30f, attackRange: 25f);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.85f, 0.1f);
            box.size = new Vector3(1.1f, 1.5f, 2f);
            root.AddComponent<Damageable>();
            ConfigureAgent(root, radius: 0.7f, height: 1.6f, speed: 4.5f, acceleration: 12f, stoppingDistance: 1f);

            // Com a folha de sprites, o visual é a Galhada (o sprite já se encolhe e acende no aviso)
            EnemySpriteSet sprites = EnemySpriteImporter.SetFor("Predador");
            if (sprites)
            {
                AddSpriteVisual(root, sprites);
                AddEmptyWeapon(root, root.transform, new Vector3(0f, 0.9f, 1f));
                var spriteAi = root.AddComponent<PredadorAI>();
                spriteAi.WarningRenderers = new Renderer[0];
                spriteAi.LineMaterial = CombatAssetsBuilder.LineMaterial;
                spriteAi.WarningSound = AssetDatabase.LoadAssetAtPath<AudioClip>(k_WarningSound);
                AddStats(root, CombatAssetsBuilder.Predador);
                AddHitReaction(root, recoil: 0.25f, centerHeight: 0.9f, burstScale: 1.1f);
                Save(root, PredadorPath);
                return;
            }

            var visual = new GameObject("Corpo").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(0f, 0.9f, 0f);
            Part(PrimitiveType.Cube, "Tronco", visual, Vector3.zero, new Vector3(1f, 0.9f, 1.8f), body);
            Part(PrimitiveType.Cube, "Cabeca", visual, new Vector3(0f, 0.2f, 1.05f), new Vector3(0.7f, 0.6f, 0.6f), body);
            GameObject horn = Part(PrimitiveType.Cube, "Chifre", visual, new Vector3(0f, 0.55f, 1.35f),
                new Vector3(0.14f, 0.14f, 0.8f), warning);
            horn.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            GameObject eyeLeft = Part(PrimitiveType.Cube, "Olho_E", visual, new Vector3(-0.2f, 0.3f, 1.36f),
                new Vector3(0.14f, 0.1f, 0.05f), warning);
            GameObject eyeRight = Part(PrimitiveType.Cube, "Olho_D", visual, new Vector3(0.2f, 0.3f, 1.36f),
                new Vector3(0.14f, 0.1f, 0.05f), warning);
            GameObject spine = Part(PrimitiveType.Cube, "Dorso", visual, new Vector3(0f, 0.5f, -0.1f),
                new Vector3(0.2f, 0.25f, 1.4f), warning);

            foreach (Vector3 p in new[]
                     {
                         new Vector3(-0.4f, -0.55f, 0.6f), new Vector3(0.4f, -0.55f, 0.6f),
                         new Vector3(-0.4f, -0.55f, -0.6f), new Vector3(0.4f, -0.55f, -0.6f),
                     })
                Part(PrimitiveType.Cylinder, "Pata", visual, p, new Vector3(0.22f, 0.35f, 0.22f), leg);

            AddEmptyWeapon(root, visual, new Vector3(0f, 0f, 1f));

            var ai = root.AddComponent<PredadorAI>();
            ai.Body = visual;
            ai.WarningRenderers = new[]
            {
                horn.GetComponent<Renderer>(), eyeLeft.GetComponent<Renderer>(), eyeRight.GetComponent<Renderer>(),
                spine.GetComponent<Renderer>(),
            };
            ai.LineMaterial = CombatAssetsBuilder.LineMaterial;
            ai.WarningSound = AssetDatabase.LoadAssetAtPath<AudioClip>(k_WarningSound);
            AddStats(root, CombatAssetsBuilder.Predador);
            AddHitReaction(root, recoil: 0.25f, centerHeight: 0.9f, burstScale: 1.1f);

            Save(root, PredadorPath);
        }

        // Espinho: variante do projétil da torreta, lento o bastante para desviar.
        // Só é criado se não existir, para preservar ajustes feitos no Inspector.
        static ProjectileBase BuildSpikeProjectile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ProjectileStandard>(SpikeProjectilePath);
            if (existing)
            {
                // O Espinhento atira de qualquer ponto da arena: o espinho precisa durar para cruzá-la
                existing.MaxLifeTime = k_SpikeLifetime;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(k_TurretProjectileSource);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            var projectile = instance.GetComponent<ProjectileStandard>();
            projectile.Speed = 18f;
            projectile.Damage = 8f;
            projectile.MaxLifeTime = k_SpikeLifetime;
            instance.name = "Projectile_Espinho";

            GameObject saved = IcaroPrefabBuilder.SavePrefab(instance, SpikeProjectilePath);
            return saved ? saved.GetComponent<ProjectileBase>() : null;
        }

        // Componentes comuns: Health, Actor, NavMeshAgent, EnemyController, DetectionModule, pontos de mira e
        // de explosão. Flash de dano, som e explosão vêm do HoverBot para manter o mesmo feedback.
        internal static GameObject CreateEnemyRoot(string name, EnemyController reference, Material bodyMaterial,
            float maxHealth, float height, float detectionRange, float attackRange)
        {
            var root = new GameObject(name);

            var health = root.AddComponent<Health>();
            health.MaxHealth = maxHealth;

            var aimPoint = new GameObject("AimPoint").transform;
            aimPoint.SetParent(root.transform, false);
            aimPoint.localPosition = new Vector3(0f, height * 0.6f, 0f);

            var actor = root.AddComponent<Actor>();
            actor.Affiliation = k_EnemyAffiliation;
            actor.AimPoint = aimPoint;

            root.AddComponent<NavMeshAgent>();

            var deathPoint = new GameObject("DeathVfxPoint").transform;
            deathPoint.SetParent(root.transform, false);
            deathPoint.localPosition = new Vector3(0f, height * 0.5f, 0f);

            var controller = root.AddComponent<EnemyController>();
            controller.BodyMaterial = bodyMaterial;
            // Piscar branco curto ao levar dano (emissão branca forte que some em 0,12 s)
            controller.OnHitBodyGradient = WhiteFlash();
            controller.FlashOnHitDuration = 0.12f;
            controller.DamageTick = reference.DamageTick;
            controller.DeathVfx = reference.DeathVfx;
            controller.DeathVfxSpawnPoint = deathPoint;
            // Sem cura no chão: o Ícaro regenera sozinho (CombatBalance.PlayerBaseRegen)
            controller.LootPrefab = null;
            controller.DropRate = 0f;

            var detectionObject = new GameObject("DetectionModule");
            detectionObject.transform.SetParent(root.transform, false);
            detectionObject.transform.localPosition = new Vector3(0f, height * 0.8f, 0f);
            var detection = detectionObject.AddComponent<DetectionModule>();
            detection.DetectionSourcePoint = detectionObject.transform;
            detection.DetectionRange = detectionRange;
            detection.AttackRange = attackRange;

            root.AddComponent<EnemyTimeScaler>();
            return root;
        }

        // Visual em sprite animado (folha desenhada em código ou trocada à mão, Art/Sprites/Inimigos)
        static Transform AddSpriteVisual(GameObject root, EnemySpriteSet sprites)
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.gameObject.AddComponent<EnemySpriteView>().Set = sprites;
            return visual;
        }

        // Recuo ao levar dano e explosão de partículas ao morrer (feedback de combate)
        internal static void AddHitReaction(GameObject root, float recoil, float centerHeight, float burstScale)
        {
            var reaction = root.AddComponent<HitReaction>();
            reaction.RecoilDistance = recoil;
            reaction.CenterHeight = centerHeight;
            reaction.DeathBurst = CombatFeedbackBuilder.DeathBurst;
            reaction.DeathBurstScale = burstScale;
        }

        static Gradient WhiteFlash()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white * 3f, 0f), new GradientColorKey(Color.black, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        // Ficha do inimigo (vida, dano, custo para o Diretor); o RoomManager aplica a escala do bioma
        internal static void AddStats(GameObject root, EnemyDefinition definition)
        {
            root.AddComponent<EnemyStats>().Definition = definition;
        }

        internal static void AddCapsule(GameObject root, float radius, float height)
        {
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);

            // O projétil do jogador procura o Damageable no próprio collider atingido
            root.AddComponent<Damageable>();
        }

        internal static void ConfigureAgent(GameObject root, float radius, float height, float speed, float acceleration,
            float stoppingDistance)
        {
            var agent = root.GetComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.speed = speed;
            agent.acceleration = acceleration;
            agent.angularSpeed = speed > 0f ? 540f : 0f;
            agent.stoppingDistance = stoppingDistance;
            agent.autoBraking = false;
        }

        internal static WeaponController AddWeapon(GameObject root, Transform parent, Vector3 muzzlePosition,
            ProjectileBase projectile)
        {
            var weaponObject = new GameObject("Arma", typeof(AudioSource));
            weaponObject.transform.SetParent(parent, false);
            weaponObject.GetComponent<AudioSource>().playOnAwake = false;

            var muzzle = new GameObject("Boca").transform;
            muzzle.SetParent(weaponObject.transform, false);
            muzzle.localPosition = muzzlePosition;

            var weapon = weaponObject.AddComponent<WeaponController>();
            weapon.WeaponName = root.name;
            weapon.WeaponRoot = weaponObject;
            weapon.WeaponMuzzle = muzzle;
            weapon.ProjectilePrefab = projectile;
            weapon.RecoilForce = 0f;
            return weapon;
        }

        // O EnemyController exige ao menos uma arma; Broto e Predador recebem uma que nunca dispara
        internal static void AddEmptyWeapon(GameObject root, Transform parent, Vector3 muzzlePosition)
        {
            WeaponController weapon = AddWeapon(root, parent, muzzlePosition, null);
            weapon.ShootType = WeaponShootType.Manual;
            weapon.MaxAmmo = 1;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale,
            Material material)
        {
            // Sem collider: quem recebe os tiros é o collider da raiz, que tem o Damageable
            return IcaroPrefabBuilder.CreatePart(type, name, parent, position, scale, material, keepCollider: false);
        }

        static void Save(GameObject root, string path)
        {
            IcaroPrefabBuilder.SavePrefab(root, path);
        }
    }
}
