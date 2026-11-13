# História e sistemas novos (checkpoints, modos, mercante, habilidades, tradutor)

Documento de ideias da conversa de 2026-11-12. Nada disto está implementado ainda. **Decidido** marca o que o usuário já fechou; o resto são propostas a discutir.

## História

**Decidido:**
- O ÍCARO-9 chega ao planeta com uma missão: destruir o item que causa o descompasso temporal, que está afetando todo o universo. O item fica na catedral, a basílica de Archen Kaedra.
- A raça que vivia lá é ancestral da humanidade atual. Os sobreviventes fugiram para a Terra quando o mundo entrou em colapso.
- Os NPCs pelo caminho vão contando, aos poucos, como o mundo chegou a esse estado.

**Propostas para amarrar com o que já existe:**
- O item pode ser o **Coração** das três escolhas do final: levar um fragmento, desligá-lo ou ficar como âncora.
- A raça pode ser a dos **Aeônidas**, os construtores de anéis de `Picos_Acima_das_Nuvens.md`.
- Os relógios parados às **11h58** em Archen Kaedra marcam o instante do colapso.
- O **cerco** à cidade é um acontecimento da história; quem atacou ainda não foi decidido.
- A tecnologia de cristal lilás é o que eles usavam para medir e controlar o tempo.
- Gancho possível: quem fabricou o ÍCARO-9 são descendentes dos que fugiram, e ele volta à terra natal dos próprios criadores sem saber.
- A abertura e o objetivo de hoje (seguir até a nave Dédalo) precisam ser reescritos.

## Estrutura do jogo

**Decidido:**
- **Modo normal com checkpoints.** O jogo deixa de ser roguelike.
- **Onde ficam os checkpoints:** no começo de cada bioma. Os Picos têm dois: um no começo do bioma e outro na caverna do portal, antes da primeira vila das Vilas do Alto.
- **Ao morrer:**
  - Volta ao último checkpoint, com o estado que tinha ao passar por ele.
  - Perde as cartas, as habilidades e o tradutor conseguidos depois dele.
  - O que veio dos biomas concluídos fica.
  - A vida volta cheia.
  - As Cronitas voltam ao valor de quando passou pelo checkpoint.
- **Modo extremo:** roguelike como hoje. Morreu, volta ao começo do jogo.
- **Progresso permanente** (salvo na hora, vale para todas as runs):
  - a chave do portão da caverna;
  - o modo endless destravado;
  - as armas dropadas pelos chefes. O jogador escolhe uma delas antes de começar a run.

**Em aberto:** quantos espaços de save haverá (um só ou vários).

## Mercante onipresente

**Decidido:**
- É sempre o mesmo personagem, em vários pontos do jogo.
- Não é afetado pelo tempo.
- É carismático e tem visual único (a detalhar).
- Aparece duas vezes no Bosque e duas no Arquipélago, em transições diferentes.
- Substitui a loja que surge depois de cada chefe.
- Vende habilidades, armas (haverá armas novas) e o tradutor.

**Propostas:**
- Com o mundo congelado fora de combate, só ele se mexe. Perto dele toca a música dele em velocidade normal, enquanto o resto está desacelerado como fita.
- Visual em sprite pixel art, como os inimigos.
- A loja funciona sem o tradutor, só com ícones e preços. As falas é que ficam em hieróglifos.

## Habilidades ativas

**Decidido:**
- O jogador carrega no máximo 2.
- Comprar uma terceira pede para escolher qual trocar. A trocada vai para o inventário e só pode voltar na transição de biomas.
- Haverá mais habilidades para compor a loja (a pensar).

**As duas primeiras:**
- **Voltar no tempo.** Volta alguns segundos: vida e multiplicador do jogador, inimigos, projéteis e efeitos. Quem morreu no intervalo volta à vida, e o multiplicador que o abate tinha tirado volta junto.
  - Proposta técnica: gravar o que se vê (posição, vida, animação, projéteis, Cronitas no chão).
  - Cada IA retoma do começo da ação em que estava.
- **Mundo em câmera lenta.** Por um momento curto o mundo fica bem lento e o jogador não. Serve para matar alguns inimigos e baixar o multiplicador numa enrascada.
  - Proposta: o multiplicador não sobe enquanto ela dura.

## Tradutor alienígena e NPCs

**Decidido:**
- Sem o tradutor, a caixa de diálogo de qualquer NPC (inclusive a do mercante) mostra hieróglifos, e a única opção é ignorar.
- Com o tradutor, o jogador entende as falas.

**Proposta:** uma escrita própria em pixel art desenhada em código. Com o tradutor, os glifos se embaralham e viram letras na caixa (efeito de decodificação).

**Ideias anteriores (2026-11-09):**
- um NPC na primeira vila;
- outro na ponte, preso atrás das grades;
- becos de Archen Kaedra com lojinhas ou vendedores.

## Boss secreto do Bairro Voador (sala 20)

**Decidido:**
- Um NPC fica no Bairro Voador. Sem o tradutor, só hieróglifos e a opção de ignorar.
- Com o tradutor, ele conversa e depois se transforma, na frente do jogador, num boss secreto.
- Derrotado, ele dá os **Ion akimbos** e a **chave do portão da caverna** (salva na hora).

**Proposta:** tirar os Ion akimbos da loja, porque viram recompensa do boss.

## Caminhos mais longos (Bosque e Arquipélago)

**Decidido:**
- As salas continuam as mesmas, mas os caminhos entre elas ficam bem mais longos e orgânicos, com vários elementos.
- Principalmente no Bosque, que deixa de ser plano: subidas, trilhas curvas, ponte sobre um rio, paredão com ruínas etc.
- O Bosque ganha uma ramificação até a caverna endless, com um portão gigante de madeira e ferro aberto pela chave.

**Propostas:**
- **Trechos de caminho:**
  - ponte sobre um rio;
  - subida em zigue-zague por um barranco;
  - paredão com ruínas;
  - vão para pular;
  - passagem estreita entre rochas;
  - clareira de descanso;
  - no Arquipélago: cais, ponte que cai, calçada submersa.
- **Recompensas pequenas pelo caminho**, para não virar só andar: esconderijos de Cronitas, atalhos, o mercante.
- **Cuidado técnico:** esticar o Bosque empurra o desfiladeiro e o Arquipélago na Via, então a Via inteira é gerada de novo.

## Modo endless (depois de todo o resto)

**Decidido:**
- Fica numa caverna ligada ao Bosque e aproveita a Câmara de Cristal, com várias áreas diferentes.
- É um modo separado: depois de destravado uma vez, aparece no menu.
- Só começa quando todo o resto do jogo estiver pronto. O portão e o caminho podem ser feitos antes.
- Ideia anotada, sem mexer por enquanto: a sucata pode ficar para o endless, para o jogador melhorar a arma e ir mais longe.

## Ordem sugerida

1. Este documento.
2. Sistema de diálogo com hieróglifos e o tradutor.
3. Mercante: visual, loja e aparições.
4. Save e checkpoints, e o modo extremo.
5. Habilidades: câmera lenta primeiro, voltar no tempo depois.
6. Caminhos novos do Bosque, depois os do Arquipélago, por etapas e com aprovação.
7. NPC e boss secreto, com a chave salva.
8. Portão e caminho da caverna. O modo endless vem por último.
