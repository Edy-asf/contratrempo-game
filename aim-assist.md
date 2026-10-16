# Prompt: Assistência de mira (aim assist)

Leia o CLAUDE.md. Quero implementar assistência de mira (aim assist) no tiro do jogador.

## Ideia
A mira tem uma zona na tela que cobre toda a crosshair, definida pela área interna dos colchetes. Se houver um inimigo dentro dessa zona quando o jogador atira, o projétil sai em direção a esse inimigo e o persegue até acertá-lo. Se não houver inimigo na zona, o tiro sai reto, no ponto central, como hoje.

## Antes de implementar
Verifique como o WeaponController do Microgame instancia projéteis (ProjectileStandard etc.) e como a crosshair do HUD é desenhada. Estenda com componentes novos (por exemplo AimAssist e ProjectileHoming) editando o mínimo possível os scripts originais, e me diga o que precisou alterar e por quê.

## Zona de assistência
- Defina a zona em coordenadas normalizadas de viewport, para funcionar em qualquer resolução. Padrão: elipse centralizada com raio de cerca de 8% da altura da tela.
- Um único campo (tamanho da zona) controla tanto o tamanho dos colchetes da crosshair quanto a área real de detecção, para os dois nunca ficarem diferentes. Ajuste o visual dos colchetes para casar com a zona.
- Modo de depuração (tecla ou checkbox): desenha a zona e destaca o alvo escolhido.

## Escolha do alvo (só no instante do disparo)
- Considere os inimigos ativos da sala atual, usando a lista mantida pelo sistema de salas (não use FindObjectsOfType). Cada inimigo tem um ponto de mira (AimPoint, um transform filho no centro do corpo; se não existir, use o centro do collider).
- Um inimigo é candidato se o ponto de mira projetado na tela estiver dentro da zona, estiver na frente da câmera, dentro do alcance máximo (padrão 80 m, editável, suficiente para cruzar a arena) e tiver linha de visão livre: raycast do cano até o ponto de mira sem paredes no caminho. Se estiver bloqueado, não é candidato.
- Entre os candidatos, escolha o mais próximo do centro da zona (distância normalizada na tela), com um pequeno desempate pela distância no mundo.
- Se o raio do centro da mira já atinge um inimigo diretamente, esse inimigo tem prioridade.

## Comportamento do projétil
- O projétil sai do cano já inclinado na direção do alvo e gira de forma suave em direção a ele a cada frame (taxa de giro máxima editável, padrão 720°/s), mantendo a velocidade normal. A curva deve parecer natural, sem teletransporte.
- Garantia de acerto: enquanto o alvo estiver vivo e visível, o projétil continua corrigindo a rota. Nos últimos 2 a 3 m, trave a direção no alvo para não passar de raspão em alvos pequenos e rápidos (como o Zangão).
- Perda de linha de visão: a cada 0,05 s verifique a linha de visão entre o projétil e o alvo. Se for bloqueada por uma parede, o projétil para de perseguir e segue reto. Não deve contornar cantos.
- Se o alvo morrer durante o voo, o projétil segue reto. O projétil continua colidindo normalmente com paredes e com outros inimigos no caminho.
- O projétil do jogador não é afetado pelo multiplicador de tempo.
- Use o evento central de dano existente, para que hit marker, números de dano, crítico e itens funcionem sem mudanças. Se houver itens que duplicam ou disparam projéteis extras (como Eco Temporal), os projéteis extras também devem usar a assistência.
- Use pooling para os projéteis e evite alocações por frame.

## Feedback visual
- Quando houver um alvo válido dentro da zona, os colchetes da crosshair reagem (por exemplo, se aproximam levemente e mudam de cor para âmbar), e o inimigo escolhido ganha um destaque discreto. Tudo deve voltar ao normal quando não houver alvo.
- Rastro curto nos projéteis para a curva ficar legível.

## Configuração
- Crie um ScriptableObject AimAssistSettings com: ligado/desligado, tamanho da zona, alcance máximo, taxa de giro, distância de trava final, intensidade de 0 a 100% (em 0 o tiro sai reto, em valores intermediários a taxa de giro e a zona diminuem proporcionalmente) e o intervalo de checagem de linha de visão. Deixe pronto para ligar a uma opção no futuro menu de configurações.
- Atualize o CLAUDE.md. Faça um commit no fim e me diga o que testar no Editor, incluindo casos de teste: alvo parado, alvo se movendo rápido, Zangão pequeno e distante, inimigo atrás de uma parede, dois inimigos na zona, alvo que morre durante o voo e tiro sem nenhum alvo.
