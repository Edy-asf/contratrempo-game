using System.Collections.Generic;
using UnityEngine;

namespace Icaro.EditorTools
{
    // A praça da basílica de Archen Kaedra: muro do pódio com o parapeito, colunatas, fachada e a guarda na entrada
    public static partial class PicosBuilder
    {
        static void BuildBasilicaSquare(Vector3 city, Vector3 toBridge)
        {
            Vector3 across = Vector3.Cross(Vector3.up, toBridge);
            var root = new GameObject("Basilica_Praca").transform;
            root.SetParent(s_World, false);
            Vector3 plaza = city + Vector3.up * (k_CityPodium + 0.1f);

            Vector3 At(float x, float z) => city + across * x + toBridge * z;

            // ---------- muro do pódio: o mesmo contorno do BuildCityPodium ----------
            var line = new List<Vector2> { new Vector2(11.5f, 104f), new Vector2(60f, 100f) };
            const float R = 113f;
            float from = Mathf.Atan2(88f, 70f), to = Mathf.Atan2(66f, -92f);
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.Lerp(from, to, i / 10f);
                line.Add(new Vector2(Mathf.Sin(a) * R, Mathf.Cos(a) * R));
            }

            line.Add(new Vector2(66f, -270f));
            line.Add(new Vector2(40f, -332f));

            void WallRun(Vector2 a, Vector2 b)
            {
                Vector3 pa = At(a.x, a.y), pb = At(b.x, b.y), d = pb - pa;
                int n = Mathf.Max(1, Mathf.RoundToInt(d.magnitude / 18.4f));
                Vector3 mid2 = (pa + pb) * 0.5f;
                Vector3 outward = Vector3.Cross(Vector3.up, d).normalized;
                if (Vector3.Dot(outward, new Vector3(mid2.x - city.x, 0f, mid2.z - city.z)) < 0f)
                    outward = -outward;

                float yaw = YawOf(outward);
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = Vector3.Lerp(pa, pb, (i + 0.5f) / n) + Vector3.down * 0.12f;
                    GameObject go = PicosGothicKit.Place(s_Materials, "AR_Podio_Modulo", p, yaw, root, false, PicosGothicKit.VillagesFolder);
                    if (go)
                        FitWidth(go, yaw, d.magnitude / n / 18.4f);
                }

                // Barreira invisível atrás do parapeito (o resto da cidade fica 28 m abaixo)
                var wall = new GameObject("Podio_Barreira");
                wall.transform.SetParent(root, false);
                wall.transform.SetPositionAndRotation(mid2 + Vector3.up * (k_CityPodium + 20f), Quaternion.LookRotation(d));
                wall.AddComponent<BoxCollider>().size = new Vector3(0.5f, 40f, d.magnitude);
            }

            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i + 1 < line.Count; i++)
                    WallRun(new Vector2(side * line[i].x, line[i].y), new Vector2(side * line[i + 1].x, line[i + 1].y));
            WallRun(new Vector2(40f, -332f), new Vector2(-40f, -332f));

            // ---------- colunata: uma construção só, como a do Vaticano (semicírculo de raio 104 m e, numa quina, os braços
            // retos até as torres sineiras da fachada); a origem da peça é o meio da praça, com a ponte para a frente ----------
            PicosGothicKit.Place(s_Materials, "AR_Colunata", plaza, YawOf(toBridge), root, true, PicosGothicKit.VillagesFolder);

            // ---------- fachada da basílica, com os degraus até a praça ----------
            Vector3 facadeAt = plaza - toBridge * 138f;
            PicosGothicKit.Place(s_Materials, "AR_Basilica_Fachada", facadeAt, YawOf(toBridge), root, true, PicosGothicKit.VillagesFolder);
            Vector3 domeAt = plaza - toBridge * 194f;
            PicosGothicKit.Place(s_Materials, "AR_Basilica_Corpo", domeAt, YawOf(toBridge), root, true, PicosGothicKit.VillagesFolder);
            PicosGothicKit.Place(s_Materials, "AR_Basilica_Cupula", domeAt, YawOf(toBridge), root, false, PicosGothicKit.VillagesFolder);
            Vector3 low = facadeAt + toBridge * 19.3f, high = facadeAt + toBridge * 8.8f + Vector3.up * 2.4f;
            var ramp = new GameObject("Rampa_Fachada");
            ramp.transform.SetParent(root, false);
            Quaternion slope = Quaternion.LookRotation(high - low);
            ramp.transform.SetPositionAndRotation((low + high) * 0.5f + slope * Vector3.up * -0.12f, slope);
            ramp.AddComponent<BoxCollider>().size = new Vector3(74f, 0.2f, (high - low).magnitude);

            // ---------- a guarda de pedra dos dois lados da chegada da escadaria ----------
            foreach (float side in new[] { -1f, 1f })
                PicosGothicKit.Place(s_Materials, "PR_Guardiao", At(side * 13.5f, 100.5f) + Vector3.up * (k_CityPodium + 0.1f), YawOf(toBridge), root, true, ReinoFolder);

            BuildBasilicaDetail(root, plaza, facadeAt, toBridge);
        }

        // Detalhe da praça (pedido do usuário em 2026-11-11: continua aberta, com muito mais detalhe; a peça central fica
        // para depois; kit AR_PracaBasilica): o desenho do piso, estandartes na arquitrave da colunata, braseiros dos dois
        // lados que acendem em cascata quando o jogador chega ao pódio (das pontas perto da basílica até as mais longe), a
        // última defesa diante da escadaria e as oferendas e placas no degrau de cima. Coordenadas do Blender: 'Col' na
        // origem da colunata (y para a basílica), 'Fac' na da fachada
        static void BuildBasilicaDetail(Transform root, Vector3 plaza, Vector3 facadeAt, Vector3 toBridge)
        {
            Vector3 across = Vector3.Cross(Vector3.up, toBridge);
            Vector3 Col(float x, float y, float h = 0f) => plaza + across * x - toBridge * y + Vector3.up * h;
            Vector3 Fac(float x, float y, float h = 0f) => facadeAt + across * x - toBridge * y + Vector3.up * h;
            float Inward(Vector3 p) => YawOf(Flat(plaza - p));
            float facing = YawOf(toBridge);
            string folder = PicosGothicKit.VillagesFolder;
            int banners = 0, braziers = 0, offerings = 0;

            // ---------- piso: raios, anéis e a faixa da procissão (sem colisão) ----------
            PicosGothicKit.Place(s_Materials, "PR_Piso_Praca_Catedral", plaza, facing, root, false, folder);

            // ---------- estandartes na face de dentro da arquitrave, entre as colunas da fileira de dentro ----------
            // (o arco em 35 partes iguais dos 35° aos 155°; um vão a cada três; e dois vãos em cada braço reto)
            const string kinds = "ABACBA";
            const float face = 104f - 6.75f, rod = 16.6f;
            Vector2 joint = new Vector2(Mathf.Sin(155f * Mathf.Deg2Rad), -Mathf.Cos(155f * Mathf.Deg2Rad)) * 104f;
            Vector2 armEnd = new Vector2(47f, 129.9f);
            foreach (float side in new[] { -1f, 1f })
            {
                int n = side > 0 ? 0 : 3;
                for (int i = 1; i < 34; i += 3)
                {
                    float a = (35f + 120f * (i + 0.5f) / 35f) * Mathf.Deg2Rad;
                    Vector3 p = Col(side * Mathf.Sin(a) * face, -Mathf.Cos(a) * face, rod);
                    if (PicosGothicKit.Place(s_Materials, "PR_Estandarte_Colunata_" + kinds[n++ % kinds.Length], p, Inward(p), root, false, folder))
                        banners++;
                }

                foreach (float t in new[] { 1.5f / 6f, 3.5f / 6f })
                {
                    Vector2 l = Vector2.Lerp(joint, armEnd, t);
                    Vector3 p = Col(side * (l.x - 6.78f), l.y, rod);
                    if (PicosGothicKit.Place(s_Materials, "PR_Estandarte_Colunata_" + kinds[n++ % kinds.Length], p, YawOf(-across * side), root, false, folder))
                        banners++;
                }
            }

            // ---------- braseiros: um de cada lado em cada passo, do braço reto (perto da basílica) até 45° ----------
            var cascade = new GameObject("Braseiros_Cascata");
            cascade.transform.SetParent(root, false);
            var steps = new List<Vector2> { new Vector2(33.8f, 110f) };
            for (float a = 150f; a >= 44f; a -= 15f)
                steps.Add(new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), -Mathf.Cos(a * Mathf.Deg2Rad)) * 90.5f);
            var cascadeSteps = new List<BrazierCascade.Step>();
            var reserved = new List<(Vector3, float)>();
            foreach (Vector2 b in steps)
            {
                var fires = new List<GameObject>();
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 foot = Col(side * b.x, b.y);
                    if (!PicosGothicKit.Place(s_Materials, "PR_Braseiro_Catedral", foot, Inward(foot), root, true, folder))
                        continue;
                    braziers++;
                    reserved.Add((foot, 3.2f));
                    var fire = new GameObject("Fogo_Braseiro");
                    fire.transform.SetParent(cascade.transform, false);
                    fire.transform.position = foot + Vector3.up * 5.15f;
                    var flame = new GameObject("Chama").AddComponent<SpriteFlame>();
                    flame.transform.SetParent(fire.transform, false);
                    flame.Height = 2.4f;
                    flame.Lilac = true;
                    var light = new GameObject("Luz").AddComponent<Light>();
                    light.transform.SetParent(fire.transform, false);
                    light.transform.localPosition = Vector3.up * 1.3f;
                    light.type = LightType.Point;
                    light.color = new Color(0.78f, 0.55f, 1f);
                    light.range = 16f;
                    light.intensity = 3f;
                    light.shadows = LightShadows.None;
                    fire.SetActive(false);
                    fires.Add(fire);
                }

                cascadeSteps.Add(new BrazierCascade.Step { Fires = fires.ToArray() });
            }

            // O gatilho: a entrada do pódio, onde chega a escadaria do Mercado Alto
            cascade.transform.SetPositionAndRotation(Col(0f, -97f, 3f), Quaternion.LookRotation(toBridge));
            cascade.AddComponent<BoxCollider>().size = new Vector3(26f, 6f, 6f);
            cascade.AddComponent<BrazierCascade>().Steps = cascadeSteps;

            // ---------- a última defesa diante da escadaria (o ataque veio da ponte) ----------
            Physics.SyncTransforms();
            int before = s_SiegePlaced, skippedBefore = s_SiegeSkipped;
            s_SiegeReserved.AddRange(reserved);
            System.Func<Vector3, bool> floor = w =>
            {
                Vector3 d = Flat(w - plaza);
                float x = Vector3.Dot(d, across), y = -Vector3.Dot(d, toBridge), r = d.magnitude;
                return y < 118.3f && r > 15f && (r < 93.5f || (y > 90f && Mathf.Abs(x) < 35f));
            };
            var rnd = new System.Random(5113);
            float Range(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Vector3 Polar(float deg, float r) => Col(Mathf.Sin(deg * Mathf.Deg2Rad) * r, -Mathf.Cos(deg * Mathf.Deg2Rad) * r);

            // Barricadas num arco na frente dos degraus, o meio livre, viradas para a ponte; armas largadas atrás
            Vector3 arc = Col(0f, 122f);
            foreach (float side in new[] { -1f, 1f })
                foreach (float a in new[] { 22f, 42f, 62f })
                {
                    Vector3 p = Col(side * Mathf.Sin(a * Mathf.Deg2Rad) * 22f, 122f - Mathf.Cos(a * Mathf.Deg2Rad) * 22f);
                    Vector3 outward = Flat(p - arc).normalized;
                    GameObject b = SiegeScatter(rnd, "PR_Barricada_A", root, r => (p + outward * Range(-0.8f, 0.8f), YawOf(outward) + Range(-8f, 8f)), floor);
                    if (b && rnd.NextDouble() < 0.7)
                        SiegePiece("PR_Armas_Largadas", p - outward * Range(2.8f, 3.6f), Range(0f, 360f), root, floor, false);
                }

            // Crateras (pequenas, no meio das casas do desenho do piso) com corpos na beira
            foreach ((float deg, float r) in new[] { (168.75f, 61f), (213.75f, 84.5f), (146.25f, 84.5f) })
            {
                GameObject crater = SiegeScatter(rnd, "PR_Cratera_A", root, q => (Polar(deg + Range(-2f, 2f), r + Range(-2f, 2f)), Range(0f, 360f)), floor);
                if (!crater)
                    continue;
                Vector3 at = crater.transform.position;
                SiegeScatter(rnd, "PR_Corpos_Carbonizados", root, q => (at + Around(Range(0f, 6.3f)) * Range(4.5f, 5.5f), Range(0f, 360f)), floor, 20, false);
            }

            // Guardião tombado, cápsula destruída, autômatos, cristais e escombros queimados no lado da basílica
            bool fallen = false;
            for (int k = 0; k < 20 && !fallen; k++)
                fallen = FallenGuardian(root, Polar(Range(196f, 206f), Range(68f, 74f)), facing + Range(150f, 210f), floor);
            if (!fallen)
                s_SiegeSkipped++;
            (Vector3, float) Near(System.Random q) => (Polar(Range(140f, 220f), Range(50f, 90f)), Range(0f, 360f));
            SiegeScatter(rnd, "PR_Capsula_Destruida", root, q => (Polar(Range(155f, 165f), Range(76f, 88f)), Range(0f, 360f)), floor);
            for (int k = 0; k < 3; k++)
                SiegeScatter(rnd, "PR_Sentinela_Destruida", root, Near, floor);
            SiegeScatter(rnd, "PR_Cristais_Estilhacados", root, Near, floor);
            SiegeScatter(rnd, "PR_Cristais_Estilhacados", root, Near, floor);
            SiegeScatter(rnd, "PR_Escombros_Queimados_A", root, Near, floor);
            SiegeScatter(rnd, "PR_Escombros_Queimados_B", root, Near, floor);
            SiegeScatter(rnd, "PR_Armas_Largadas", root, Near, floor, 30, false);
            // Escombros encostados no espelho do primeiro degrau, dos dois lados da faixa da procissão
            foreach (float side in new[] { -1f, 1f })
                SiegeScatter(rnd, "PR_Escombros_Queimados_C", root, q => (Col(side * Range(18f, 34f), 118.9f), facing), w => floor(w) || -Vector3.Dot(Flat(w - plaza), toBridge) < 119.2f);
            foreach ((Vector3, float) spot in reserved)
                s_SiegeReserved.Remove(spot);
            int siegePlaced = s_SiegePlaced - before, siegeSkipped = s_SiegeSkipped - skippedBefore;

            // ---------- oferendas dos peregrinos e placas no degrau de cima (2,4 m, o patamar dos portais) ----------
            foreach (float side in new[] { -1f, 1f })
            {
                foreach ((string model, float x, float y) in new[] { ("PR_Oferendas_A", 3.6f, -7.5f), ("PR_Oferendas_B", 19.125f, -6.3f) })
                {
                    Vector3 p = Fac(side * x, y, 2.4f);
                    if (PicosGothicKit.Place(s_Materials, model, p, facing, root, false, folder))
                    {
                        offerings++;
                        s_SpriteVotives.Add(p);
                    }
                }

                foreach ((float x, float y) in new[] { (8.0f, -8.3f), (24.75f, -7.3f) })
                    if (PicosGothicKit.Place(s_Materials, "PR_Placa_Inscricao", Fac(side * x, y, 2.4f), facing, root, true, folder))
                        offerings++;
            }

            Debug.Log($"[Ícaro] Archen Kaedra, praça da basílica: {banners} estandartes, {braziers} braseiros em {cascadeSteps.Count} passos, " +
                      $"{siegePlaced} peças da última defesa ({siegeSkipped} ficaram de fora) e {offerings} oferendas e placas.");
        }

        // Estica só a largura da peça, seja qual for o giro da raiz do FBX
        static void FitWidth(GameObject go, float yaw, float factor)
        {
            Vector3 width = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 scale = go.transform.localScale;
            Vector3[] axes = { go.transform.right, go.transform.up, go.transform.forward };
            for (int i = 0; i < 3; i++)
                if (Mathf.Abs(Vector3.Dot(axes[i], width)) > 0.7f)
                    scale[i] *= factor;
            go.transform.localScale = scale;
        }
    }
}
