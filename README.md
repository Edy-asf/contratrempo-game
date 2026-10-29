# CONTRATEMPO

FPS roguelike em primeira pessoa feito em Unity. O jogador é **ÍCARO-9**, um robô explorador que cai no planeta
**Kairós** e precisa seguir a **Via Aeônida** até a nave **Dédalo**. O problema: o tempo do planeta corre cada vez
mais rápido em relação ao Ícaro, e tudo que vive nele (inimigos, projéteis, música e até o céu) acelera junto.

> O código e as pastas ainda usam o nome de trabalho **Ícaro** (`Assets/Icaro`).

## A ideia central: o tempo acelera

- Um **multiplicador de tempo** sobe sem parar: +1,0x a cada 120 segundos, até o teto de 6x.
- Os inimigos multiplicam por ele a velocidade, a cadência de ataque, a velocidade dos projéteis e a animação.
  O jogador **nunca** é afetado. O jogo não usa `Time.timeScale` para isso.
- Os avisos de ataque encurtam com o multiplicador, mas nunca ficam abaixo de 60% da duração original. Assim o
  jogo continua justo.
- Há duas formas de baixar o multiplicador:
  - **Metrônomos**, que surgem depois de cada chefe e tiram 3,0x.
  - **Orbes do tempo**, soltos por inimigos marcados com um relógio âmbar. É preciso acertar o tiro no ritmo do
    anel que se fecha: Perfeito, Bom ou fora do tempo.
- A música, a névoa, as partículas e as estrelas do céu seguem o mesmo relógio. A trilha ganha camadas
  (chimbais, linha acid, quebrada de drum'n'bass, lead) e acelera conforme o multiplicador sobe.

## Como é uma run

A Via é um mapa linear com **25 arenas** em quatro biomas. Cada bioma termina num chefe.

| Bioma | Salas | Clima |
|---|---|---|
| **Bosque** | 1 a 8 | Floresta alienígena escura e úmida, com bioluminescência ciano e âmbar, riachos, lagos e templos engolidos pela mata. Chefe: **Rei Fungo**. |
| **Arquipélago** | 9 a 16 | Ilhas e ruínas colossais sobre o mar, com anéis de pedra gigantes, um farol e um estaleiro. Termina na **Ponte Colossal**, diante de um portal de nove anéis girando. |
| **Picos acima das Nuvens** | 17 em diante | Torres esculpidas no alto de montanhas nevadas, acima de um mar de nuvens sob um eclipse congelado. Tem trilhas de crista, pontes de corrente, um bastião arrasado, o Bairro Voador e as Vilas do Alto. Chefe: **Oráculo**. *(Em construção.)* |
| **Cratera** | últimas | Rocha queimada e tons quentes. Chefe: **Dragão de Magma**. *(Planejado.)* |

- **Arenas**: ao entrar numa sala, um **domo de energia** hexagonal sela a arena. Os inimigos surgem de forma
  contínua até a cota da sala acabar. Quando ela é limpa, o domo se estilhaça.
- **Cartas de item**: ao limpar uma sala, o jogo mostra 4 cartas em pixel art e o jogador escolhe 2. Os itens
  são inspirados em Risk of Rain 2, se acumulam e valem a run inteira. Há raridades Comum, Incomum e Rara, com
  críticos, ricochete elétrico, fogo, mísseis, escudo, pulo duplo, revivida, itens de dash e outros.
- **Cronitas e loja**: os inimigos deixam cristais (Cronitas). Depois de cada chefe aparece uma loja que vende
  itens, armas, reparo e melhorias. Os preços sobem com o multiplicador, então comprar cedo sai mais barato.
- **Armas** (o Ícaro carrega até 2):
  - Agulha: fuzil inicial.
  - Titan: metralhadora pesada.
  - Ion akimbos: revólveres duplos.
  - Viper: submetralhadora silenciada.
  - Liberdade: escopeta automática.

  As armas aparecem em sprite 2D no estilo Doom, com o braço robótico do Ícaro.
- **Morte**: os itens, as armas e as Cronitas se perdem, e a run recomeça do início da Via. A tela de morte
  mostra as estatísticas da run.

### Inimigos

Os inimigos são sprites animados em pixel art no estilo Doom, desenhados em código e iluminados pela cena, com 4
direções:

| Nome | Comportamento |
|---|---|
| **Tatu-lanterna** | Corpo a corpo, lento e resistente, com uma cabeçada depois de um aviso. |
| **Flor-canhão** | Fica parada em poleiros altos e atira em qualquer ponto da arena. |
| **Galhada** | Para, avisa e investe em linha reta até onde o jogador estava. |
| **Explorador Tomado** | Robô atirador tomado pela mata que anda pela arena disparando rajadas. |
| **Mariposa-lanterna** | Voadora pequena que aparece em enxames e atira de longe. |
| **Golem** | Grande e lento. Mira um laser e golpeia o chão em área. |

Inimigos **elite** (Blindado, Veloz, Explosivo) aparecem com mais frequência nas salas finais.

## Controles

| Ação | Tecla |
|---|---|
| Andar / olhar | WASD / mouse |
| Atirar / mirar | botão esquerdo / botão direito |
| Pular, correr, agachar | Espaço, Shift, Ctrl |
| Recarregar | R |
| Dash (7 m, invulnerável) | F |
| Trocar de arma | Q / E ou rodinha |
| Abrir a loja (perto do terminal) | G |
| Escolher cartas | clique ou 1 a 4, Enter confirma, R troca as cartas (com a Ficha de Rerrolagem) |
| Pausa | Esc |

As teclas podem ser trocadas em **Configurações > Controles**. Lá também ficam as opções de mira assistida,
vídeo, áudio e acessibilidade (tremor, números de dano, setas de ameaça).

Teclas de teste (só no Editor e em builds de desenvolvimento):

| Tecla | Função |
|---|---|
| F11 | Modo livre para prints: voa, atravessa tudo e fica imortal. H esconde o HUD e P salva um print. |
| F3 | Depuração da mira assistida. |
| F6 a F9 | Teletransporte pela Via. |
| F10 | Ajuste do sprite da arma. |

## Como abrir o projeto

1. Instale o **Unity 6.6 (6000.6.0f1)**. O projeto usa URP e o Input System novo.
2. Abra a pasta do repositório pelo Unity Hub. A primeira importação demora, porque o projeto tem muitas malhas
   geradas.
3. Abra a cena `Assets/Icaro/Scenes/Via_Grade.unity`: é o jogo inteiro, com Bosque e Arquipélago numa cena só.
   Aperte Play.

Outras cenas, todas em `Assets/Icaro/Scenes`:

| Cena | Conteúdo |
|---|---|
| `Bosque` | O Bosque sozinho. |
| `Arquipelago` | O Arquipélago sozinho. |
| `Picos_Teste` | O bioma dos Picos, em construção. |
| `Camara_Teste` | Teste de cenário de uma caverna de cristal. |

Os mapas são gerados por código e regerados pelo menu **Ícaro** do Editor:

- **Ícaro > Via > Gerar Bosque e Arquipélago** gera a Via completa.
- **Ícaro > Picos > Gerar Chegada (teste)** gera a cena dos Picos.
- O mesmo menu tem ferramentas de teste: simular uma sala, todas as armas, 30 Mariposas, tabela de vida e dano
  por sala, ajustes do céu, do visor e da luz da arma.

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `Assets/Icaro/Scripts` | Código do jogo, separado por sistema: `Time`, `Rooms`, `Enemies`, `Bosses`, `Combat`, `Items`, `Shop`, `Weapons`, `UI`, `Menus`, `World`, `Visual` e outros. Os geradores de cenário ficam em `Scripts/Editor`. |
| `Assets/Icaro/Balance` | Balanceamento em ScriptableObjects. `CombatBalance` é o principal; há uma ficha para cada inimigo, chefe, arma e item. Não há números de balanceamento fixos no código das IAs. |
| `Assets/Icaro/Prefabs`, `Models`, `Materials`, `Textures`, `Shaders` | Assets do jogo. Inclui shaders próprios para o céu noturno, o volume de nuvens, a rocha nevada e o domo. |
| `Assets/Icaro/Art` | Malhas e texturas geradas, sprites dos inimigos e das armas. |
| `Assets/FPS` | Base do **FPS Microgame** da Unity (movimento, câmera, vida). O jogo estende essa base com componentes próprios em vez de editá-la. |
| `Ferramentas` | Scripts Python do Blender, rodados em segundo plano: montanhas esculpidas dos Picos e renderização de sprites. |
| `Design` | Documentos de design dos biomas. |
| `Balanceamento` | Planilha das cartas e tabela de vida e dano por sala. |

Os arquivos-fonte do Blender (`ArteFonte/`) e os prints do modo livre (`Prints/`) ficam fora do git, porque são
grandes. Os modelos exportados (FBX) estão no repositório.

## Créditos

- Base: **FPS Microgame** (Unity Technologies).
- Menus e tela de título: Luciano.
- Assets externos: só licenças **CC0** ou **CC BY**, de fontes como Poly Haven, Kenney, Quaternius e Sketchfab.
  Cada um está registrado com fonte, autor e licença em [`Assets/Icaro/CREDITOS.md`](Assets/Icaro/CREDITOS.md).
- A pixel art dos inimigos, das cartas e das armas, a música do jogo e boa parte dos cenários são gerados por
  código no próprio projeto.
