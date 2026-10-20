# ÍCARO: contexto para a próxima sessão (2026-10-19)

Este arquivo resume o estado do projeto, o que foi feito na última conversa longa, o que falta testar e o que está
planejado. Ele complementa o `CLAUDE.md`, que é a referência completa das regras do jogo e dos sistemas. Leia os dois
no começo da conversa nova.

---

## 1. Como trabalhar com o usuário

### Regras do projeto (também no CLAUDE.md)
- Responder sempre em português.
- Nunca editar nem apagar `.meta` à mão.
- Evitar editar scripts do FPS Microgame. A única exceção já feita é `WeaponController.ProjectileFactory`.
- Usar só assets CC0 ou CC-BY e registrar cada um em `Assets/Icaro/CREDITOS.md`.
- Depois de cada mudança, dizer o que fazer no Editor (menus a rodar, componentes, campos).

### Preferências do usuário observadas nesta sessão
- **Entrevista antes de projetar.** Quando o usuário pede algo de design (HUD, mapas, conexões), ele quase sempre diz
  "me entreviste". Usar `AskUserQuestion` com opções concretas, prévias em ASCII e uma recomendada. Ele responde rápido
  e gosta de escolher.
- **Nível de detalhe aprovado.** O padrão das ruínas do Arquipélago agradou muito ("o cenário está ótimo, quero nesse
  nível de detalhismo para as fases"). Ele inclui:
  - blocos chanfrados, aros, canaletas e quebras em dente;
  - vários materiais por peça, com o tom sorteado por bloco;
  - musgo no topo, base molhada com cracas e trepadeiras.

  Peças lisas ou de uma textura só foram rejeitadas.
- **Sem brilho em texturas de bloco ou parede.** Ele pediu para tirar runas ciano, cristais e fungos brilhantes. O
  estilo "neon tingindo tudo" já foi rejeitado antes.
- **Trabalhar por etapas.** Ele aprova cada uma vendo prints e pede ajustes pontuais.
- **Commit e push só quando ele pedir.** Fazer push só na branch `github-main`.
- **Otimizações não podem mudar o visual.** Ele pediu isso explicitamente e escolhe numa lista.
- **Ele não lê código.** Explicar o resultado do ponto de vista do jogo.

---

## 2. Repositório e ferramentas

- **Repositório:** https://github.com/Edy-asf/contratrempo-game.
- **Branch:** trabalha-se na `github-main`, que acompanha `origin/main`.
- **Branches antigas:** `master` e `estilo-grade` não têm ancestral comum com a `github-main`. Nunca fazer push nem
  merge delas. O histórico antigo ficou de fora porque tinha arquivos acima de 100 MB.
- **Último commit:** `119a3ca5` ("ÍCARO: estado do projeto"). Tudo o que foi feito nesta sessão está **sem commit**,
  cerca de 600 arquivos modificados, a maioria malhas, materiais e texturas regeradas. Oferecer commit e push quando o
  usuário confirmar que as coisas funcionam.
- **Compilar sem o Editor:** rodar da raiz do projeto
  `powershell -NoProfile -File "C:\Users\Edi Filho\.claude\icaro-tools\compile.ps1"`. A saída "N files" significa sem
  erros. Shaders não são verificados por esse script, só dentro do Unity.
- **Python:** usar `"/c/Users/Edi Filho/AppData/Local/Programs/Python/Python313/python.exe" -I`. O `python` do PATH é o
  atalho da Store e não funciona. Tem numpy e matplotlib.
  - Funcionou bem reproduzir em Python a grade das ilhas para conferir layouts (vista de cima em ASCII).
  - Também funcionou desenhar prévias 3D de peças (anéis) com matplotlib antes de mandar para o usuário.
- **Heredoc no Bash:** quebra com aspas simples dentro do texto. Para patches grandes, escrever um `.py` no
  scratchpad e rodar.
- **Log do Editor:** `E:\unity\projetos\My project\Logs\Editor.log`.
- **Editor aberto:** normalmente o Editor está aberto, então não dá para rodar batchmode. Não é possível dar Play pelo
  terminal: o usuário testa e manda prints.
- **Outras memórias:** ficam em `C:\Users\Edi Filho\.claude\projects\E--unity-projetos-My-project\memory\`
  (`MEMORY.md` é o índice). As relevantes:
  - `arquipelago-mapas.md`: todas as decisões do bioma.
  - `nivel-detalhe-cenarios.md`
  - `estilo-grade-plano.md`
  - `github-remoto.md`

---

## 3. O que foi feito nesta sessão (sem commit)

### 3.1 Armas em sprite (`Weapons/Sprites`)
- **Tamanho e lente:** o tamanho da arma na tela não muda com o FOV. Uma lente estica a arma perto do canto acima de
  70° (`WeaponSpriteHUD.ReferenceFov`, `FovStretch`).
- **Dados na ficha:** tamanho, posição, giro e ponta do cano ficam na ficha da arma (`SpriteHeight`, `SpriteOffset`,
  `SpriteTilt`, `SpriteMuzzle`, `OverrideSpriteMuzzle`).
- **Ajuste em jogo:** F10 (`WeaponSpriteTuner`).
- **Coice da arte importada:** por lado (akimbo).
- **Arte do revólver (Ion akimbos):** o usuário perguntou como trocar. É só colocar os PNGs em
  `Assets/Icaro/Resources/Armas/IonAkimbos/` (`parada.png`, `tiro_1.png`... e `recarga_1.png`...). Ele pode trazer a
  arte.

### 3.2 Visor do capacete, o HUD novo (`UI/Visor`)
- **`HelmetVisor`:** Canvas em World Space na câmera de armas, a 0,25 m, com curvatura de 9° nos grupos laterais.
  - Grupos: TopLeft, TopRight, BottomLeft, BottomRight, BottomCenter, TopCenter e Center.
  - Esconde a vida, o painel de armas e o aviso de pausa do Microgame.
  - **Bússola:** o componente fica desligado e todo o desenho apagado, mas o objeto continua ativo, porque os
    `CompassElement` dos inimigos procuram a bússola no Awake.
- **`VisorInertia`:** atraso e amortecimento do HUD. Os números ficam no asset `Resources/VisorSettings` (menu
  "Ícaro > Ajustes do visor"). Mexer no asset durante o Play muda na hora e o valor fica salvo.
- **`VisorHealthPanel`:** vida com rastro (estilo Risk of Rain 2), escudo e barreira.
- **`WeaponAmmoHUD`:** bloco único com munição, recarga e dash.
  - O `DashHUD` foi apagado.
  - **Pendência:** foi oferecido corrigir o "DASH [F]" que sobrepõe a barra do pente. O usuário não respondeu.
- **`TimeMultiplierHUD`:** mostra "KAIRÓS".
- **`CronitaHUD`:** no canto superior direito.
- **`ItemHUD`:** grade de ícones.
- **`CardPickupToast`:** aviso de carta, pelo `ItemDraft.OnCardPicked`.
- **`ObjectivePanel`:** aba de objetivo estilo Risk of Rain 2, com o tempo da run.
  - Objetivos: "Avance pelo <bioma> até a próxima arena", "Elimine os inimigos N", "Derrote <chefe>" e "Elimine os
    reforços".
  - Ao cumprir, o texto é riscado e o objetivo desliza para fora. Texto longo quebra em duas linhas.
- **`BossHealthBar`:** barra do chefe no topo do visor, com fases e rastro.
- **`VisorBanner`:** faixa central de avisos com tons (Normal, Warning, Success, Danger).
  - Substitui o `DisplayMessageManager` do Microgame, que é destruído em jogo.
  - O código do Ícaro chama `VisorBanner.Show(título, subtítulo, tom)`.
  - Qualquer `DisplayMessageEvent` vira faixa ("Título: subtítulo").
  - O `RoomManager` e o `BossAI` já usam.
- **`EnemiesRemainingHUD`:** ficou vazio. As cenas ainda referenciam o componente.
- **`EnemyHealthBars`** (fora do visor, em tela):
  - barra acima dos inimigos acertados, com rastro;
  - marca de elite e ícones de em chamas e lento;
  - some 3 s depois do último acerto;
  - chefes não têm.
- **`DamageNumbers`:** usam a fonte Kenney Future.
- **Fontes do visor:** Kenney Future e Future Narrow (CC0, registradas), em `ItemFonts.Visor`/`VisorWide`.

### 3.3 Bioma Arquipélago (substitui o Charco) e a Via unida
Os detalhes completos estão no CLAUDE.md (seção Mapa) e na memória `arquipelago-mapas.md`. Resumo:

- **Geradores:**
  - `Editor/Arquipelago/ArquipelagoBuilder.cs`: menu "Ícaro > Arquipélago > Gerar Arquipélago", cena
    `Scenes/Arquipelago.unity`.
  - `ArquipelagoRuinKit.cs`: peças trabalhadas.
  - `RuinMesh.cs`: materiais por parte (tons, musgo, base molhada, vãos).
  - `ArquipelagoTextureKit.cs`
  - Peças com perfil em `Editor/Grade/BlockMesh.cs`.
- **Grade:** 270 x 270 células de 2 m. O mapa foi deslocado +50 células em z para caber a costa.
- **Costa** (continente na borda sul):
  - penhasco de 20 m;
  - praça no alto (onde a Via chega), com arco de entrada e escadaria em zigue-zague até a praia;
  - da praia, ponte, ponte quebrada, coluna tombada e ponte, passando por três ilhotas, até a ilha de chegada (52, 75).
- **Arenas** (números da sala global; `Island.Arena` 1, 2, 3):
  1. **Anel Partido (sala 7):** ilha partida por um canal, anel colossal em pé, uma ponte fixa no meio e dois mirantes
     de runas.
     - Os mirantes não têm rampas; sobe-se pulando degraus de 1,5 m.
     - Saída pelo portão da metade leste.
  2. **Enseada (sala 8):** arena "genérica" pedida pelo usuário.
     - Baía recortada, colunata, arco partido e paredão de 5 m com escadaria.
     - A saída fica no alto do paredão.
  3. **Farol (sala 9):** três terraços (2, 5 e 8 m) com quatro escadarias.
     - Farol com escada em espiral até a varanda a 14 m, com feixe giratório (`LighthouseBeam`, só visual).
     - Destroços flutuantes na praia norte.
- **Travessias:**
  - Anel → Enseada: ponte quebrada (vão de 3 m), ilhota e ponte.
  - Enseada → Farol: pedras descendo, ilhota com cais e dois destroços flutuantes que vão e voltam (`FloatingPlatform`)
    até o cais do Farol.
- **Mecânicas criadas:**
  - `FloatingPlatform`: carrega o jogador e fica fora do NavMesh.
  - `UnsafeGround`: o `SeaHazard` não guarda ponto seguro em cima dela.
  - `SeaHazard`: quem cai no mar volta à terra perdendo 15% da vida; inimigos que caem morrem.
  - `CollapsingBridge`: pronta, mas nenhuma arena usa. O usuário tirou essa mecânica do Anel Partido.
- **Numeração:** Anel 7, Enseada 8, Farol 9, Cemitério 10, chefe 11. É contínua, então as Ruínas e a Cratera mantêm os
  números atuais.
- **Via unida** (menu "Ícaro > Via > Gerar Bosque e Arquipélago", `Editor/Via/ViaCoastBuilder.cs`, cena
  `Scenes/Via_Grade.unity`):
  - o Bosque com `GridTheme.OpenEnd`: a sala do chefe ganha porta de saída e uma trilha até a borda norte;
  - um desfiladeiro de ~220 m em S, descendo 6 m;
  - o Arquipélago montado por `ArquipelagoBuilder.BuildInto`, deslocado para o penhasco encostar no fim do
    desfiladeiro.
  - **Transição de atmosfera:** névoa, ambiente, luar e céu fazem a transição pelo `AtmosphereCorridor`, com a névoa
    mais densa no meio.
  - **Bug corrigido:** um NaN deixava a tela preta ao chegar no Arquipélago.
  - **Navmesh separado:** o Arquipélago dentro da Via usa a pasta `Via_Grade_Arquipelago_NavMesh`.

### 3.4 Desempenho (nada pode mudar na tela)
- **Malhas em blocos:** o `GridSceneKit.CreateObject` divide malhas com mais de 4000 vértices em blocos de 64 m
  (parâmetro `split`). Água, borda invisível e mar ficam inteiros.
- **`DistanceCulling`:** desliga o desenho do cenário a mais de 300 m. Ficam de fora mar, silhuetas do fundo, feixe e
  destroços.
- **Sprites girando na placa de vídeo:**
  - O lote `GridSceneKit.BillboardBatch` junta os sprites do cenário (tufos, flores, cogumelos, árvores próximas) numa
    malha por material e bloco.
  - O shader novo `Assets/Icaro/Shaders/BillboardSprite.shader` (`Icaro/BillboardSprite`) gira os sprites no vértice,
    com materiais `<sprite>_GPU`.
  - **Risco:** o shader não pôde ser compilado fora do Unity.
- **`SpriteBillboardSystem`:** só gira sprites a menos de 300 m e só quando a câmera muda de lugar.
- **Oclusão:** calculada no fim de cada geração (`GridSceneKit.BakeOcclusion` + `OcclusionArea`). Só peças opacas são
  oclusoras. A geração ficou alguns minutos mais lenta.
- **HUD:** os textos só são reescritos quando mudam e as animações paradas não mexem no canvas. Também saíram as
  alocações por quadro (barras dos inimigos, `HelmetVisor`, `VisorBanner`, `MenuKit`, `ObjectivePanel`).
- **Luz ambiente:** recalculada no máximo 5 vezes por segundo nas transições (`BiomeZone.RefreshAmbient`).
- **Recusados ou não escolhidos por enquanto:** GPU Resident Drawer e o medidor de desempenho em F8. Próximo passo
  sugerido, se ainda pesar: medir com o Profiler. Suspeitos: o `WeaponSpriteHUD` recalcula a malha da arma a cada
  quadro, as sombras, as luzes do farol.

---

## 4. O que precisa ser testado no Editor (nada disso foi visto rodando)

Tudo abaixo compila, mas o usuário ainda não confirmou no jogo:

1. **Gerar a cena unida:** rodar "Ícaro > Via > Gerar Bosque e Arquipélago". A oclusão demora minutos.
2. **Shader dos sprites:** se tufos e árvores aparecerem rosa ou sumirem, é erro de compilação do
   `BillboardSprite.shader` (ver o Console). Conferir também que estão iguais a antes e girando para o jogador.
3. **Oclusão:** algum objeto pode sumir onde deveria aparecer.
4. **Junções:**
   - entre a trilha do Bosque e o desfiladeiro: foi posta parede na última fileira da grade;
   - entre o desfiladeiro e a praça do penhasco.
5. **Escadarias:**
   - zigue-zague (degraus de ~0,26 m);
   - espiral do farol (0,25 m; o limite de subida do jogador é 0,3 m);
   - terraços (0,27 m).
6. **Destroços flutuantes:** ficar em cima, parado e andando, inclusive na hora em que param e invertem.
7. **Navegação dos inimigos e spawns:** nas três arenas do Arquipélago dentro da Via, com o mundo deslocado.
8. **Nevoeiro do desfiladeiro:** se a névoa do meio estiver densa demais ou de menos, ajustar `MidFogDensity` em
   `Transicao_Atmosfera`.
9. **Ganho de FPS** das otimizações.
10. **Cenas próprias:** também precisam ser regeradas (menus de cada uma) se forem usadas.

---

## 5. Planejado (próximos passos)

### 5.1 Arquipélago: falta construir
- **Cemitério de Anéis (sala 10, Arena = 4):** a próxima arena.
  - Conceito escolhido pelo usuário: "vários anéis gigantes em pé e caídos, uns dentro dos outros; anda-se por cima
    deles em vários níveis", com destroços flutuantes passando entre os anéis.
  - Ainda falta entrevistar sobre o layout: tamanho, quantos anéis, como se sobe neles, onde ficam os destroços e a
    travessia do Farol até lá.
  - **Saída do Farol:** hoje o Farol não tem porta de saída. Ela precisa ser criada (`BuildRoom` aceita `exitPoint`).
- **Ponte Colossal (chefe, sala 11):**
  - Conceito escolhido: ponte enorme e larga entre duas torres, com o mar dos dois lados.
  - Fica inteira; não desaba nem inclina.
  - A sala precisa de `IsBoss`, ponto `ChefeSpawn`, Metrônomo e loja depois do chefe (`ShopDirector`).
  - **Chefe do bioma:** hoje é o Sapo Rei do Brejo (o do Charco, `BiomeIndex` 1), mantido provisoriamente. Perguntar se
    ele fica ou se cria um chefe novo do Arquipélago.
- **Espinhento:** o Arquipélago ainda não tem `HighSpawnPoint`, então ele não aparece nessas arenas. Sugeridos:
  varanda do farol, mirantes, paredão da Enseada.
- **Teleporte de depuração:** o `ViaDebugTeleport` (F6/F7) só inclui as arenas do Bosque. Falta incluir as do
  Arquipélago na Via.
- **Mais salas:** o usuário avisou que 4 por bioma é pouco e que haverá mais salas no futuro.
- **Ligação para a frente:** depois do chefe do Arquipélago, o próximo bioma é Ruínas. Provavelmente vai querer uma
  ligação no mesmo estilo do desfiladeiro.

### 5.2 Limpeza pendente (baixa prioridade)
- Texturas `_Brilho.png` antigas que nada usa (rochas e tocos do Bosque, pedra do Arquipélago). Foi oferecido apagar.
- A cena `Arquipelago_Teste.unity` (sala de teste antiga) e malhas `Arquipelago_Teste_*` órfãs.
- Malhas de pontes antigas (`Arquipelago_Ponte1_*`) que não são mais geradas.
- O componente `EnemiesRemainingHUD` vazio nas cenas.
- `CollapsingBridge` e `CollapsibleBridge()`, sem uso.

### 5.3 Jogo em geral (ordem de implementação do CLAUDE.md)
- Sucata (salva entre runs) e upgrades permanentes: ainda não existem.
- GameManager: run, morte (voltar ao início da Via mantendo a sucata) e os três finais.
- Direção de arte ainda aberta, com tendência cartoon. A Via em grade com sprites estilo Doom é o caminho atual.
- O ÍCARO será um módulo de um jogo maior no futuro. Não preparar integração por conta própria.

---

## 6. Detalhes técnicos úteis para continuar o Arquipélago

- **Ilhas:** `k_Islands` em `ArquipelagoBuilder`. Os índices importam:
  - 0 chegada; 1 e 2 Anel Partido; 3 a 5 ilhotas de cenário;
  - 6 ilhota (`k_CrossingA`); 7 ilhota do cais (`k_CrossingB`);
  - 8 Farol (`k_FarolIsland`); 9 Enseada (`k_Enseada`); 10 continente (`k_Mainland`, forma por `CoastBeach` e
    `CoastCliff`); 11 a 13 ilhotas da costa (`k_CoastIslets`).
  - Ilhas novas entram no fim da lista.
- **Campos de `Island`:**
  - `Terraces`: níveis internos. Um terraço com altura `k_SeaBed` vira água (baía).
  - `Arena`: 0 = fora de combate.
  - `PlateauHeight`: terraço com rampas de 1 célula.
- **Coordenadas:** `CellToWorld(cell, altura)` = célula × 2 m (sem +0,5). A geração é feita na origem e o mundo é
  deslocado depois (`s_Offset`). Rotas de destroços e o nível do mar são corrigidos no deslocamento.
- **Salas:** `BuildRoom(world, nome do objeto, nome da sala, arena, número da sala, entrada, direção, saída?, direção
  da saída, out surface)`.
  - O gatilho é cortado 4 m depois do portão.
  - O NavMesh de cada arena é assado separado (`Arena_N`).
  - `PlaceSpawns(sala, porta, arena)`.
- **Peças disponíveis:**
  - `ArquipelagoRuinKit`: `RichRing`, `AshlarWall`, `Column`, `FallenDrums`, `Tower`, `Arch`, `Pillar`, `HangVine`.
  - Builder: `Bridge` (com vão opcional), `TerraceStairs`, `Dock`, `Platform` (destroço), `RuneLookout`, `Block`,
    `RockPile`, `FallenColumn`.
- **Materiais:** `CreateRuin(nome, RuinMesh, tons, pai, colisor, linha d'água)` separa pedra, musgo, molhada e vãos.
- **Conferir layout antes de mandar:** reproduzir em Python a grade e as `EdgePoints` (pontes, praias, pés de escada).
  Isso pegou vários erros nesta sessão: ilhas encostando, praia sumindo, pontes de 1 m.
- **Faixas de largura de linha:** o arquivo do builder já tem várias linhas acima de 120 colunas. O estilo do projeto
  tolera isso.
