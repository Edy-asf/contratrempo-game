# Prompt: Buscar e integrar sprites animados sozinho (sem me perguntar)

Leia o CLAUDE.md e o `Assets/Icaro/Art/Sprites/LEIAME.md` (pipeline de sprites já implementado). Quero que você resolva tudo sozinho, de ponta a ponta, **sem parar para me perguntar**: decida, execute e me entregue um relatório no fim.

## Antes de começar
Confirme que estamos na branch `estilo-retro` e sem mudanças pendentes; se não, pare e me avise. Faça commits pequenos (um por etapa).

## 1. Busca
Procure na web pacotes de sprites pixel art **com animação** para: Zangão (voador pequeno), Vagante (humanoide atirador), Cascudo (brutamontes lento), Predador (criatura de investida), Espinhento (torre/estátua que atira), Golem (grande), Broto (corredor pequeno), chefes (versões maiores), mais sprites da arma (Agulha), projéteis, pickups e efeitos (explosão, faísca).
- Fontes preferidas, nesta ordem: OpenGameArt.org, Kenney.nl, itch.io (apenas pacotes com link de download direto, sem login), outros repositórios abertos.
- **Licença:** só CC0 ou CC-BY (ou equivalente que permita uso comercial com atribuição). Leia a página da licença de cada pacote. Se houver dúvida, descarte. Nunca use material "só para uso pessoal", "sem comercial", ripado de outros jogos ou de origem duvidosa.

## 2. Escolha automática
Escolha **um pacote principal** (ou, no máximo, 2 de estilo compatível) que cubra o maior número de inimigos com a melhor consistência de tamanho de pixel e paleta. Critérios: licença segura, animações completas, quadros de tamanho parecido, estética que combine com neon/arcade. Registre a decisão e os motivos.

## 3. Download seguro
- Baixe cada pacote para uma pasta nova e vazia fora do projeto (por exemplo, a pasta temporária do sistema), extraia lá e inspecione antes de copiar para `Assets/`. Não execute nada que venha dentro dos pacotes (scripts, executáveis); copie só imagens, e licença/leia-me.
- Verifique se os arquivos são realmente imagens (PNG/GIF/aseprite exportado) e abra algumas para olhar.
- Se um download exigir login, clique manual ou falhar, **não fique tentando**: passe ao próximo pacote da lista.

## 4. Integração
- Corte as folhas de sprite (detecte o tamanho dos quadros olhando as imagens), organize em `Assets/Icaro/Art/Sprites/<Entidade>/`, aplique as configurações de importação do pipeline (Point, sem compressão, PPU 32).
- Gere os `SpriteAnimationSet` de cada inimigo mapeando para Idle, Move, Telegraph, Attack, Hit, Death. Onde faltar uma animação, **adapte** (ex.: Telegraph = quadro de pré-ataque com tint vermelho pulsante; Hit = flash branco sobre o Idle; Death = os quadros existentes ou encolher e desaparecer). Onde um inimigo não tiver nenhum sprite adequado, mantenha o sprite provisório atual.
- Ajuste tamanhos (em metros), colliders, `AimPoint` e a origem do projétil para casar com os sprites novos. Padronize o tamanho de pixel entre os pacotes (escala) para não parecerem de jogos diferentes.
- Se um recurso estiver quebrado ou estranho, tire um print/inspecione a imagem e corrija ou descarte.

## 5. Créditos e relatório
- `Assets/Icaro/CREDITOS.md`: para cada pacote, nome, autor, link, licença e o que foi usado. Copie o arquivo de licença para junto dos assets.
- Relatório final curto: quais pacotes foram escolhidos e por quê, o que cobriu cada inimigo, o que foi adaptado, o que ficou provisório, o que eu poderia melhorar manualmente e o que testar no Editor.
- Faça o commit e me diga como desfazer.
