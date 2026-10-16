using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Fichas dos itens (Balance/Itens/Item_*.asset) e o catálogo (Balance/Itens/ItemCatalog.asset). Cada ficha só é
    // criada se faltar, para preservar o que foi ajustado no Inspector; a lista do catálogo e o material dos
    // efeitos são refeitos a cada geração. Chamado pelo CombatAssetsBuilder (Ícaro > Gerar prefabs).
    public static class ItemAssetsBuilder
    {
        public const string Folder = CombatAssetsBuilder.Folder + "/Itens";
        public const string CatalogPath = Folder + "/ItemCatalog.asset";

        const string k_EffectMaterialName = "Item_Efeito";

        // Itens removidos: a ficha é apagada na próxima geração
        static readonly string[] k_Removed = { "AncoraDoNucleo" };

        public static ItemCatalog Catalog => AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);

        static ItemEffect E(PlayerStat stat, float first, float perExtra) =>
            new ItemEffect { Stat = stat, FirstStack = first, PerExtraStack = perExtra };

        [MenuItem("Ícaro/Gerar itens", priority = 2)]
        public static ItemCatalog EnsureAll()
        {
            IcaroAssetUtility.EnsureFolder(Folder);
            var items = new List<ItemDefinition>
            {
                // ---------- Comuns ----------
                Item("LenteDeCalibracao", "Lente de Calibração", "L", ItemRarity.Comum, ItemTag.Dano,
                    "+6% de chance de crítico.",
                    "+6% de chance por pilha.",
                    null,
                    E(PlayerStat.CritChance, 0.06f, 0.06f)),
                Item("ServoGatilho", "Servo-Gatilho", "S", ItemRarity.Comum, ItemTag.Arma,
                    "+10% de cadência de tiro.",
                    "+10% por pilha.",
                    null,
                    E(PlayerStat.FireRate, 0.1f, 0.1f)),
                Item("PlacaDeCasco", "Placa de Casco", "P", ItemRarity.Comum, ItemTag.Defesa | ItemTag.Vida,
                    "+15 de vida máxima.",
                    "+15 por pilha.",
                    null,
                    E(PlayerStat.MaxHealth, 15f, 15f)),
                Item("AtuadoresLeves", "Atuadores Leves", "A", ItemRarity.Comum, ItemTag.Mobilidade,
                    "+9% de velocidade de movimento.",
                    "+9% por pilha.",
                    null,
                    E(PlayerStat.MoveSpeed, 0.09f, 0.09f)),
                Item("PolvoraSintetica", "Pólvora Sintética", "V", ItemRarity.Comum, ItemTag.Dano,
                    "+8% de dano em todos os tiros.",
                    "+8% por pilha.",
                    null,
                    E(PlayerStat.DamageBonus, 0.08f, 0.08f)),
                Item("TamborAmpliado", "Tambor Ampliado", "O", ItemRarity.Comum, ItemTag.Arma,
                    "+20% de munição no pente.",
                    "+20% por pilha.",
                    null,
                    E(PlayerStat.AmmoCapacity, 0.2f, 0.2f)),
                Item("ResfriadorAvancado", "Resfriador Avançado", "W", ItemRarity.Comum, ItemTag.Arma,
                    "A munição recarrega 15% mais rápido.",
                    "-15% de tempo por pilha (no mínimo 25% do original).",
                    null,
                    E(PlayerStat.ReloadTimeReduction, 0.15f, 0.15f)),
                Item("CanoCurto", "Cano Curto", "Q", ItemRarity.Comum, ItemTag.Dano,
                    "+12% de dano em inimigos a menos de 6 m.",
                    "+12% por pilha.",
                    null,
                    E(PlayerStat.CloseRangeDamage, 0.12f, 0.12f)),
                Item("SoroDeBiomassa", "Soro de Biomassa", "Y", ItemRarity.Comum, ItemTag.Vida,
                    "Regenera 0,5 de vida por segundo.",
                    "+0,5 por segundo por pilha.",
                    null,
                    E(PlayerStat.HealthRegen, 0.5f, 0.5f)),
                Item("NucleoImantado", "Núcleo Imantado", "U", ItemRarity.Comum, ItemTag.Recurso,
                    "Atrai Cronitas de 40% mais longe.",
                    "+30% por pilha.",
                    null,
                    E(PlayerStat.PickupRange, 0.4f, 0.3f)),

                // ---------- Incomuns ----------
                Item("BobinaRessonante", "Bobina Ressonante", "B", ItemRarity.Incomum, ItemTag.AoAcertar | ItemTag.Eletrico,
                    "15% ao acertar: um raio salta para 2 inimigos (70% do dano).",
                    "+1 inimigo alcançado por pilha.",
                    "E a sua música era elétrica.",
                    E(PlayerStat.LightningChance, 0.15f, 0f),
                    E(PlayerStat.LightningTargets, 2f, 1f),
                    E(PlayerStat.LightningDamage, 0.7f, 0f)),
                Item("ResinaVolatil", "Resina Volátil", "R", ItemRarity.Incomum, ItemTag.Dano | ItemTag.Fogo,
                    "Inimigos mortos explodem: 150% do dano em 4 m.",
                    "+75% de dano e +1,5 m de raio por pilha.",
                    "Kairós guarda rancor em cada gota.",
                    E(PlayerStat.ExplosionDamage, 1.5f, 0.75f),
                    E(PlayerStat.ExplosionRadius, 4f, 1.5f)),
                Item("PropulsorAuxiliar", "Propulsor Auxiliar", "J", ItemRarity.Incomum, ItemTag.Mobilidade,
                    "+1 pulo no ar.",
                    "+1 pulo por pilha.",
                    null,
                    E(PlayerStat.AirJumps, 1f, 1f)),
                Item("EscudoDeSincronia", "Escudo de Sincronia", "E", ItemRarity.Incomum, ItemTag.Defesa,
                    "Escudo de 8% da vida. Recarrega após 3 s sem dano.",
                    "+8% da vida máxima por pilha.",
                    null,
                    E(PlayerStat.ShieldFraction, 0.08f, 0.08f)),
                Item("EcoTemporal", "Eco Temporal", "T", ItemRarity.Incomum, ItemTag.Arma | ItemTag.Tempo,
                    "15% de chance de o tiro se repetir.",
                    "+15% de chance por pilha (acima de 100%, repete mais de uma vez).",
                    "O passado também sabe atirar.",
                    E(PlayerStat.EchoChance, 0.15f, 0.15f)),
                Item("CristalCeifador", "Cristal Ceifador", "C", ItemRarity.Incomum, ItemTag.Vida,
                    "Críticos curam 4 de vida.",
                    "+4 de cura por pilha.",
                    null,
                    E(PlayerStat.CritHeal, 4f, 4f)),
                Item("InjetorDeAdrenalina", "Injetor de Adrenalina", "!", ItemRarity.Incomum, ItemTag.Mobilidade | ItemTag.Arma,
                    "Ao levar dano: +15% de velocidade e cadência por 4 s (recarga de 10 s).",
                    "+4 s de duração por pilha (a recarga não muda).",
                    null,
                    E(PlayerStat.AdrenalineDuration, 4f, 4f)),
                Item("MunicaoAtordoante", "Munição Atordoante", "Z", ItemRarity.Incomum, ItemTag.AoAcertar | ItemTag.Tempo,
                    "15% ao acertar: o inimigo fica 30% mais lento por 2 s.",
                    "+10% de lentidão por pilha (até 70%).",
                    "Por dois segundos, o tempo esquece deles.",
                    E(PlayerStat.SlowChance, 0.15f, 0f),
                    E(PlayerStat.SlowAmount, 0.3f, 0.1f)),
                Item("DisparoPerfurante", "Disparo Perfurante", ">", ItemRarity.Incomum, ItemTag.Dano,
                    "25% de o tiro atravessar o 1º inimigo (60% do dano nos seguintes).",
                    "+15% de chance por pilha (acima de 100%, atravessa mais de um).",
                    null,
                    E(PlayerStat.PierceChance, 0.25f, 0.15f)),
                Item("CartuchoIncendiario", "Cartucho Incendiário", "*", ItemRarity.Incomum, ItemTag.AoAcertar | ItemTag.Fogo,
                    "15% ao acertar: incendeia o alvo (60% do dano em 3 s). Acumula até 3 vezes.",
                    "+30% do dano do tiro por queimadura, por pilha.",
                    "O planeta queima, e nós também.",
                    E(PlayerStat.BurnChance, 0.15f, 0f),
                    E(PlayerStat.BurnDamage, 0.6f, 0.3f)),
                Item("AceleradorTermico", "Acelerador Térmico", "%", ItemRarity.Incomum, ItemTag.Dano | ItemTag.Fogo,
                    "Inimigos em chamas recebem +20% de dano.",
                    "+15% de dano por pilha.",
                    "Tudo derrete mais rápido quando já está queimando.",
                    E(PlayerStat.BurningVulnerability, 0.2f, 0.15f)),

                // ---------- Raros ----------
                Item("CoracaoAeonida", "Coração Aeônida", "H", ItemRarity.Raro, ItemTag.Dano | ItemTag.Tempo,
                    "+1% de dano por 0,1x de multiplicador de tempo (máx. +250%).",
                    "+1% por 0,1x por pilha (o limite de +250% não muda).",
                    "O tempo não perdoa ninguém. Mas com você, ele abre uma exceção.",
                    E(PlayerStat.DamagePerTenthMultiplier, 0.01f, 0.01f)),
                Item("AmplificadorFocal", "Amplificador Focal", "3", ItemRarity.Raro, ItemTag.Dano,
                    "Críticos causam 3x em vez de 2x.",
                    "+100% de dano crítico por pilha.",
                    null,
                    E(PlayerStat.CritDamage, 1f, 1f)),
                Item("MisseisDeSondagem", "Mísseis de Sondagem", "M", ItemRarity.Raro, ItemTag.AoAcertar | ItemTag.Dano,
                    "10% ao acertar: míssil teleguiado com 300% do dano.",
                    "+300% de dano do míssil por pilha.",
                    null,
                    E(PlayerStat.MissileChance, 0.1f, 0f),
                    E(PlayerStat.MissileDamage, 3f, 3f)),
                Item("SegundaIgnicao", "Segunda Ignição", "I", ItemRarity.Raro, ItemTag.Vida,
                    "Ao morrer, revive com metade da vida. Gasta o item.",
                    "+1 revivida por pilha (cada uma consome uma pilha).",
                    null,
                    E(PlayerStat.Revives, 1f, 1f)),

                // ---------- Dash ----------
                Item("CapacitorDeImpulso", "Capacitor de Impulso", "K", ItemRarity.Comum, ItemTag.Mobilidade,
                    "O dash recarrega 15% mais rápido.",
                    "+15% de velocidade de recarga por pilha.",
                    null,
                    E(PlayerStat.DashCooldownReduction, 0.15f, 0.15f)),
                Item("BocalEstendido", "Bocal Estendido", "X", ItemRarity.Comum, ItemTag.Mobilidade,
                    "+20% de distância do dash.",
                    "+20% por pilha.",
                    null,
                    E(PlayerStat.DashDistance, 0.2f, 0.2f)),
                Item("LaminaDeFase", "Lâmina de Fase", "F", ItemRarity.Comum, ItemTag.Dano | ItemTag.Mobilidade,
                    "O dash corta quem atravessa: 120% do dano do tiro.",
                    "+60% de dano por pilha.",
                    null,
                    E(PlayerStat.DashDamage, 1.2f, 0.6f)),
                Item("EgideDeImpulso", "Égide de Impulso", "G", ItemRarity.Comum, ItemTag.Defesa | ItemTag.Mobilidade,
                    "Cada dash dá 10 de barreira por 3 s.",
                    "+6 de barreira por pilha.",
                    null,
                    E(PlayerStat.DashBarrier, 10f, 6f)),
                Item("CamaraDupla", "Câmara Dupla", "D", ItemRarity.Raro, ItemTag.Mobilidade,
                    "+1 carga de dash.",
                    "+1 carga por pilha.",
                    null,
                    E(PlayerStat.DashCharges, 1f, 1f)),
            };
            DeleteRemoved();
            ItemDefinition revive = items.Find(i => i.name == "Item_SegundaIgnicao");
            if (revive && !revive.Consumable)
            {
                revive.Consumable = true;
                EditorUtility.SetDirty(revive);
            }

            ItemCatalog catalog = Catalog;
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Items = items.ToArray();
            catalog.EffectMaterial = EffectMaterial();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        // Fichas de itens que saíram do jogo (o AssetDatabase apaga junto o .meta)
        static void DeleteRemoved()
        {
            foreach (string file in k_Removed)
            {
                string path = $"{Folder}/Item_{file}.asset";
                if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        static ItemDefinition Item(string file, string name, string letter, ItemRarity rarity, ItemTag tags,
            string description, string stackRule, string flavor, params ItemEffect[] effects)
        {
            string path = $"{Folder}/Item_{file}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item)
                return item;

            item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.DisplayName = name;
            item.IconLetter = letter;
            item.Rarity = rarity;
            item.Description = description;
            item.StackRule = stackRule;
            item.Effects = effects;
            item.Tags = tags;
            item.Flavor = flavor;
            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        // Aditivo e com ganho HDR (passa do limiar do bloom); a cor vem do vértice (linhas) ou do _BaseColor por
        // objeto (esferas)
        static Material EffectMaterial()
        {
            Material material = IcaroMaterials.GetOrCreateAdditiveParticle(k_EffectMaterialName, null);
            material.SetFloat("_Cull", 0f);
            material.SetColor("_BaseColor", new Color(2f, 2f, 2f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
