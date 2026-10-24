# Prompt: Estilo visual 3D pixelado + neon (arcade retrô)

Leia o CLAUDE.md. Quero mudar o visual do jogo para um estilo **arcade retrô pixelado com efeitos neon**, mantendo o jogo em 3D. A cena é renderizada em baixa resolução e esticada na tela com filtro Point (pixels grandes e nítidos), com cores neon brilhantes sobre fundos escuros e bloom forte. Referências de atitude: ULTRAKILL, Superhot, Tron, synthwave. Nada de copiar arte de outros jogos.

Este estilo **substitui** o estilo cartoon/toon anterior: não implemente o shader toon com contorno. Atualize a seção "Estilo visual" do CLAUDE.md (reescreva, não acrescente por cima).

## Antes de implementar
1. Verifique a versão do URP instalada, o URP Asset em uso e como o Microgame organiza as câmeras (câmera principal, câmera da arma/overlay, câmera do menu). Me diga o que encontrou.
2. Verifique os materiais, o Volume global e a skybox atuais do Microgame e das cenas em `Assets/Icaro/Scenes/`.
3. Evite editar scripts originais do Microgame; prefira componentes novos em `Assets/Icaro/Scripts/Visual/` (assembly `Icaro.asmdef`, se já existir). Nunca edite `.meta` à mão.

## Parte 1: Renderização em baixa resolução
Abordagem preferida (robusta entre versões do URP): a câmera do jogador (e a câmera da arma, se existir) renderiza para uma `RenderTexture` de baixa resolução com `FilterMode.Point`, sem mipmaps, e essa textura é exibida em tela cheia por um `RawImage` num Canvas **separado e atrás** do HUD. O HUD continua em resolução nativa e nítido (ou, se eu pedir depois, pixelado também).

- Resolução controlada por **altura interna** (padrão 270 px, com opções 180/216/270/360/480); a largura é calculada pela proporção da janela. Recrie a RenderTexture quando a janela mudar de tamanho, sem alocar por frame.
- Se houver uma câmera separada para a arma, ela também deve cair na baixa resolução e ficar por cima da principal.
- Mira, aim assist e cálculo de projéteis usam a câmera real e continuam corretos; confirme que o centro da tela, o raycast do tiro e a crosshair não deslocam com a mudança. Mouse e UI não podem ser afetados.
- Se a abordagem acima não funcionar bem com o Microgame, alternativa: um `ScriptableRendererFeature` com Render Graph (confira a API correta para a versão do URP instalada). Explique a escolha.
- A pixelização não pode afetar o gameplay nem a performance negativamente: renderizar menos pixels deve até melhorar o FPS (importante para os enxames de Zangões).

## Parte 2: Neon, bloom e pós-processamento
- Ative HDR no URP Asset. Crie um Volume global com: **Bloom** forte (intensidade e threshold ajustáveis), Tonemapping, Color Adjustments (contraste alto), Vignette leve e Chromatic Aberration muito sutil.
- Fundo escuro: skybox substituída por gradiente escuro (ou cor sólida) por bioma, com fog colorido por bioma.
- Paleta neon por bioma, definida em um ScriptableObject `NeonPalette` (cores de luz ambiente, fog, grade do chão, detalhes brilhantes e cor de destaque):
  - Bosque: verde-ácido e ciano
  - Charco: ciano e azul elétrico
  - Ruínas: magenta e roxo
  - Cratera: laranja e vermelho
- Cores de função (iguais em todos os biomas, para o jogador aprender): **vermelho** = telegraph/perigo, **âmbar** = alvo travado/mira, **ciano** = pickups de metrônomo/fragmento, **verde** = cura, **branco** = acertos.
- Chão com **grade neon** (shader no Shader Graph ou HLSL URP, linhas finas emissivas sobre base escura, com cor vinda da NeonPalette). Linhas e bordas de salas, portas e paredes com emissão para guiar a leitura do espaço.
- Material base compartilhado (`M_Neon_Lit` e `M_Neon_Unlit`) com cor base escura/chapada + cor de emissão HDR configurável por instância via MaterialPropertyBlock (sem criar materiais por objeto).
- Texturas: filtro Point, sem compressão, baixa resolução. Não use texturas detalhadas.

## Parte 3: Aplicar o estilo ao que já existe
- Menu de Editor **"Ícaro > Aplicar estilo neon"** que percorre os prefabs e modelos em `Assets/Icaro/` (inimigos, Ícaro, Agulha, Metrônomo, fragmento, ruínas, árvores, rochas) e troca para os materiais neon, preservando o que tiver a mesma função. Deve ser idempotente (rodar duas vezes não quebra nada) e listar o que foi alterado no Console. Faça um backup/commit antes de alterar prefabs em lote e me avise.
- Inimigos: corpo escuro com **contorno/linhas emissivas** da cor do tipo; olhos ou núcleo brilhantes. O flash de dano vira branco puro por 0,08 s. O telegraph de ataque muda a emissão para vermelho e pulsa.
- Projéteis: emissão forte + trail curto (inimigos em vermelho/magenta, jogador em ciano/branco), com luz pontual barata ou apenas bloom (prefira bloom).
- Metrônomo: pilar com anéis neon; ao ser ativado, onda de luz ciano e flash de tela rápido (sem luz estroboscópica).
- Partículas: quadradas (pixel), poucas por efeito, com pooling.
- Luzes: use poucas luzes em tempo real; o brilho vem do emissivo + bloom.

## Parte 4: UI retrô
- Fonte pixel: `Assets/Icaro/Fonts/` (eu vou colocar ali o arquivo *Press Start 2P* ou *VT323*, licença OFL; se o arquivo não existir ainda, deixe um campo no Inspector e uma nota). Gere o TMP Font Asset a partir dela.
- Reestilize o HUD: vida, munição, multiplicador de tempo (ex.: `TEMPO x2.5` com cor que vai de ciano a vermelho), dash, "Inimigos restantes", números de dano e cartas de upgrade (molduras neon, texto pixel). Mantenha tudo legível: contraste alto, tamanho mínimo confortável.
- Tela de título/pausa opcional no mesmo estilo, só se for pouco trabalho.

## Parte 5: Configuração (`RetroVisualSettings`)
ScriptableObject com: efeito ligado/desligado, altura interna, intensidade do bloom, scanlines (0–100%), aberração cromática (0–100%), vinheta, intensidade do flash de tela. Tudo aplicado em runtime e pronto para ligar a um futuro menu de opções. Scanlines como um overlay leve (shader UI ou textura repetida), desligado por padrão se pesar.

Acessibilidade: nada de flashes rápidos repetidos; limite o flash do Metrônomo e do dano a no máximo 3 por segundo e deixe uma opção para reduzir.

## Áudio (opcional, só estrutura)
Não gere músicas. Apenas deixe os pontos de ligação: AudioMixer com grupos Música/Efeitos, e uma lista no `Assets/Icaro/CREDITOS.md` para eu registrar os sons que for baixar (efeitos 8-bit do jsfxr, trilhas chiptune/synthwave CC0 ou CC-BY).

## Critérios de aceite
- O jogo roda em baixa resolução pixelada, com bloom visível nos elementos neon, sem mexer na jogabilidade.
- A resolução interna muda por configuração, e o redimensionamento da janela funciona.
- Crosshair, tiro e aim assist continuam acertando onde mostram.
- HUD nítido e legível.
- Comparação de FPS antes/depois com uns 30 inimigos na tela (me diga os números).

## No fim
Atualize o CLAUDE.md, faça um commit e me diga exatamente o que preciso fazer no Editor (componentes a adicionar, campos a preencher, arquivos de fonte a colocar, o que testar).

## Reversibilidade (obrigatório)
Quero poder voltar atrás se eu não gostar do resultado.
- Antes de qualquer mudança, confirme que estamos numa branch própria (por exemplo `estilo-retro`) criada a partir do estado atual e que não há alterações pendentes. Se estivermos na `main`/`master` ou houver mudanças não commitadas, pare e me avise antes de continuar.
- Trabalhe em commits pequenos, um por parte (renderização, neon/bloom, materiais, UI etc.), com mensagens claras, para eu poder desfazer só uma parte.
- Não apague nem sobrescreva os prefabs, materiais e modelos atuais: crie versões novas ao lado (ex.: pasta `Assets/Icaro/Prefabs/Retro/` ou sufixo `_Retro`) e deixe o jogo apontando para elas por meio do `RetroVisualSettings` (campo "estilo ativo"). O menu "Ícaro > Aplicar estilo neon" deve ter também "Ícaro > Voltar ao estilo anterior".
- Com o efeito desligado em `RetroVisualSettings`, o jogo deve voltar a renderizar normalmente (resolução nativa, sem bloom extra, visual anterior).
- No fim, me diga em 3 linhas como desfazer tudo (voltar para a branch anterior) e como aproveitar só uma parte.
