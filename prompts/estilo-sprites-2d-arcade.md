# Prompt: Estilo arcade retrô com sprites 2D (inimigos e itens em pixel art no mundo 3D)

Leia o CLAUDE.md. Quero mudar o visual do jogo para um estilo **arcade retrô com pixel art**, no formato clássico de FPS "2.5D" (como Doom/Wolfenstein): o **cenário continua 3D** (paredes, chão, portas, colisões, NavMesh), mas **inimigos, projéteis, pickups e itens são sprites 2D em pixel art** que sempre olham para o jogador (billboard). A arma é um sprite 2D fixo na parte de baixo da tela, animado. Cores neon sobre fundo escuro, com bloom.

Este estilo **substitui** o plano cartoon/Meshy para inimigos. Os modelos 3D só continuam para o cenário (estruturas simples, em baixa resolução). Atualize a seção "Estilo visual" do CLAUDE.md (reescreva, não acrescente por cima).

## Antes de implementar
1. Veja como o Microgame monta inimigos (Animator, NavMesh, colliders, `EnemyController`, `Health`, `Damageable`) e a câmera da arma. Me diga o que encontrou.
2. Verifique os prefabs atuais em `Assets/Icaro/Prefabs/` (Espinhento, Predador, Golem, Cascudo, Vagante, Zangão, Broto, chefes). A lógica de IA, vida, dano, hitbox, `AimPoint` e o multiplicador de tempo **não mudam**: só a parte visual é trocada.
3. Evite editar scripts originais do Microgame; novos componentes em `Assets/Icaro/Scripts/Visual/`. Nunca edite `.meta` à mão.

## Parte 1: Base visual (baixa resolução + neon)
Se `prompts/estilo-3d-pixelado-neon.md` já foi aplicado, reutilize `RetroVisualSettings`, o `NeonPalette` e a renderização em baixa resolução, e pule esta parte. Senão, implemente o mínimo:
- Câmera do jogador (e da arma, se houver) renderiza para uma RenderTexture de baixa resolução (altura interna 270 px, configurável) com filtro Point, exibida em tela cheia por um RawImage num Canvas atrás do HUD. O HUD fica nítido e em resolução nativa.
- HDR no URP Asset + Volume global com Bloom forte, Tonemapping, Vignette leve. Fundo escuro com fog colorido por bioma.
- Cenário: materiais escuros com texturas Point sem compressão e linhas/detalhes emissivos (neon) por bioma. Chão com grade neon.
- Paleta por bioma em `NeonPalette`: Bosque verde-ácido/ciano, Charco ciano/azul, Ruínas magenta/roxo, Cratera laranja/vermelho. Cores de função fixas: vermelho = perigo/telegraph, âmbar = mira, ciano = metrônomo/fragmento, verde = cura.

## Parte 2: Sprites no mundo
### 2.1 Importação
- Pasta de arte: `Assets/Icaro/Art/Sprites/<Entidade>/`. Configuração de importação via `AssetPostprocessor` para tudo nessa pasta: Sprite Mode Multiple, **Filter Mode Point**, Compression None, Pixels Per Unit **32**, Mesh Type Full Rect, Generate Mip Maps desligado.
- Sprite Atlas para cada grupo, quando fizer sentido.

### 2.2 Shader do sprite
- Shader Graph (ou HLSL URP) **Unlit com alpha clip** (sem semitransparência para evitar ordenação errada), com: textura, cor de tint, **cor de flash** (para dano), **mapa/cor de emissão** (partes neon brilham com bloom), suporte a **fog** da cena, e uma opção de "luz mínima" para não sumir no escuro. Compatível com SRP Batcher e com instâncias via MaterialPropertyBlock (sem criar materiais por inimigo).
- Sombra falsa: um disco escuro/neon achatado no chão sob o inimigo (blob), sem luz em tempo real.

### 2.3 Componentes
- `SpriteBillboard`: o sprite gira só no eixo Y para olhar a câmera (nunca inclina), custo baixo; para muitos Zangões use uma única atualização central (um gerenciador que atualiza todos), não um Update por objeto.
- `SpriteAnimator`: reproduz animações a partir de uma `SpriteAnimationSet` (ScriptableObject com: clipes `Idle`, `Move`, `Telegraph`, `Attack`, `Hit`, `Death`; frames, FPS e loop). Expõe `Speed`. **Os inimigos multiplicam o FPS pelo `TimeManager.Multiplier`** (como antes faziam com `Animator.speed`), respeitando o teto de efeito por inimigo. O jogador nunca é afetado.
- Modo de direção: começar com **1 direção** (frente). Deixe o `SpriteAnimationSet` preparado para 4 direções (frente/costas/lados) no futuro, escolhendo o frame pelo ângulo entre o inimigo e a câmera, mas sem exigir isso agora.
- `EnemySpriteView`: liga a máquina de estados do inimigo (parado, andando, carregando/telegraph, atacando, levando dano, morrendo) à animação e ao flash de dano branco de 0,08 s. O telegraph de ataque também acende a emissão em vermelho, pulsando. Morte: animação curta + desaparece, sem bloquear o pooling.
- O **collider 3D continua sendo a hitbox** (cápsula ou caixa) e o `AimPoint` continua no centro do corpo. O sprite é só um filho visual. Ajuste o tamanho do collider ao tamanho aparente do sprite. Zangões podem ter hurtbox ~1,5× maior que o sprite.

### 2.4 Substituir o visual dos inimigos
Tamanho de referência (altura do sprite em metros no mundo): Zangão 0,6; Vagante 1,8; Cascudo 2,2; Predador 1,6; Espinhento 2,0; Golem 3,5; Broto 0,9; chefes 1,5× o tamanho normal. Os sprites podem ter quadros de 64×64 px (Zangão 32×32, Golem e chefes 128×128).

Como a arte final ainda não existe, **gere sprites provisórios por script** (Python com PIL ou editor C#), um por inimigo, com formas simples e coloridas (cada tipo em uma cor neon), com os quadros necessários para cada animação, só para o pipeline funcionar de ponta a ponta. Documente em `Assets/Icaro/Art/Sprites/LEIAME.md` o **formato exato** que as folhas de sprite finais devem ter, para eu substituir depois sem mexer em código: nome do arquivo, tamanho de quadro, ordem das linhas (Idle, Move, Telegraph, Attack, Hit, Death), número de quadros por linha, pivô (centro-baixo), fundo transparente.

### 2.5 Projéteis, itens e efeitos
- Projéteis dos inimigos e do jogador: sprites ou quads pequenos com emissão neon (Billboard) + trail curto. Mantenha pooling e a lógica de velocidade/homing existentes.
- Pickups (fragmento de metrônomo, sucata, cura, cartas) como sprites emissivos que giram/flutuam. Metrônomo e estruturas continuam 3D, com anéis neon e onda de luz ciano ao ativar.
- Partículas quadradas (pixel), poucas por efeito, com pooling. Hit marker, números de dano e crit continuam via evento central de dano, em fonte pixel.

### 2.6 Arma do jogador (HUD)
- A Agulha vira um **sprite 2D animado** na parte inferior central da tela (estilo Doom), no Canvas de HUD, com animações: idle (leve balanço ao andar), disparo (flash neon), recarga/ciclo, e movimento de dash. Esconda/desative o modelo 3D da arma e ajuste a origem do projétil (cano) para um ponto fixo na câmera que case com a ponta do sprite. Gere um sprite provisório por script e documente o formato, como nos inimigos.

## Parte 3: UI e acabamento
- Fonte pixel (*Press Start 2P* ou *VT323*, OFL): campo no Inspector e pasta `Assets/Icaro/Fonts/`; eu coloco o arquivo. Gere o TMP Font Asset a partir dela.
- HUD retrô: vida, munição, `TEMPO x2.5` (ciano → vermelho conforme sobe), dash, "Inimigos restantes", cartas de upgrade com moldura neon.
- `RetroVisualSettings` (se ainda não existir): ligado/desligado, altura interna, bloom, scanlines (0–100%), aberração cromática, flash de tela. Tudo pronto para um futuro menu de opções.
- Acessibilidade: sem flashes rápidos repetidos (máx. 3/s) e uma opção para reduzir.

## Performance
- Enxame de 8 Zangões + 30 inimigos: sem alocação por frame, SRP Batcher/instancing, atlas, Update central para billboard e animação. Me diga o FPS antes e depois.

## Critérios de aceite
- Todos os inimigos aparecem como sprites billboard neon, animam, piscam ao levar dano, telegrafiam ataques com vermelho pulsante e respeitam o multiplicador de tempo.
- Hitbox, tiro, aim assist, hit marker e números de dano funcionam como antes.
- Arma em sprite no HUD, com disparo animado e projétil saindo do ponto certo.
- Trocar uma folha de sprite pelo arquivo final (mesmo formato) funciona sem alterar código.

## No fim
Atualize o CLAUDE.md, faça um commit e me diga exatamente o que preciso fazer no Editor (componentes, campos, arquivos a colocar, o que testar).

## Reversibilidade (obrigatório)
Quero poder voltar atrás se eu não gostar do resultado.
- Antes de qualquer mudança, confirme que estamos numa branch própria (por exemplo `estilo-retro`) criada a partir do estado atual e que não há alterações pendentes. Se estivermos na `main`/`master` ou houver mudanças não commitadas, pare e me avise antes de continuar.
- Trabalhe em commits pequenos, um por parte (renderização, neon/bloom, materiais, UI etc.), com mensagens claras, para eu poder desfazer só uma parte.
- Não apague nem sobrescreva os prefabs, materiais e modelos atuais: crie versões novas ao lado (ex.: pasta `Assets/Icaro/Prefabs/Retro/` ou sufixo `_Retro`) e deixe o jogo apontando para elas por meio do `RetroVisualSettings` (campo "estilo ativo"). O menu "Ícaro > Aplicar estilo neon" deve ter também "Ícaro > Voltar ao estilo anterior".
- Com o efeito desligado em `RetroVisualSettings`, o jogo deve voltar a renderizar normalmente (resolução nativa, sem bloom extra, visual anterior).
- No fim, me diga em 3 linhas como desfazer tudo (voltar para a branch anterior) e como aproveitar só uma parte.
