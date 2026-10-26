# Sprites dos inimigos

Cada inimigo tem uma pasta `<Nome>/` (o nome no código: Cascudo, Espinhento, Predador, Vagante, Zangao) com três arquivos:

- `<Nome>.png`: a folha de sprites.
- `<Nome>_Brilho.png`: o mapa de brilho. É opcional.
- `<Nome>.json`: a descrição da folha.

Hoje as folhas são desenhadas em código. **Ícaro > Sprites > Desenhar inimigos** faz tudo de uma vez:

1. Desenha as folhas e grava os três arquivos de cada inimigo (código em `Assets/Icaro/Scripts/Enemies/Sprites`).
2. Importa as folhas, criando `<Nome>_Sprites.asset` e `<Nome>_Sprite.mat`.
3. Refaz os prefabs dos inimigos.

**Para usar arte própria:** substitua os arquivos e rode só **Ícaro > Sprites > Importar sprites dos inimigos** e depois **Ícaro > Gerar prefabs**. O menu "Desenhar inimigos" sobrescreveria a sua arte.

## A folha (PNG)

- **Grade:** células todas do mesmo tamanho, com fundo transparente. Use alfa cheio ou zero, sem meio-termo, porque o jogo recorta pelo alfa.
- **Colunas:** uma por quadro. A coluna 0 fica à esquerda. Clipes com menos quadros deixam o resto da linha vazio.
- **Linhas:** quatro por clipe, uma por direção, nesta ordem:
  1. Frente: o inimigo de frente para o jogador.
  2. Direita: o lado direito do inimigo, com a cara virada para a direita da imagem.
  3. Costas.
  4. Esquerda: a cara virada para a esquerda da imagem.

  A linha 0 é a de cima.
- **Pixel art:** o Unity importa sem suavização (filtro Point) e sem compressão.
- **Escala:** 16 pixels por metro, a mesma das árvores.

## O mapa de brilho (PNG, opcional)

- **Grade:** a mesma da folha.
- **Partes que brilham** (lanternas, olhos, listras, cogumelos): na cor cheia. Elas acendem no escuro.
- **Resto do corpo:** a cor vezes `bodyGlow` (0,25). É o brilho mínimo que impede o sprite de sumir à noite.
- **Sem o mapa:** o corpo todo brilha por igual, com 25% da cor.

## A descrição (JSON)

```json
{
  "name": "Cascudo",
  "cellWidth": 44, "cellHeight": 36,
  "columns": 5, "rows": 24,
  "pixelsPerMeter": 16,
  "pivotX": 22, "pivotY": 1,
  "emission": "Cascudo_Brilho.png", "bodyGlow": 0.25,
  "clips": [
    {"name": "Idle",   "row": 0,  "frames": 4, "fps": 4,  "loop": true},
    {"name": "Move",   "row": 4,  "frames": 4, "fps": 8,  "loop": true},
    {"name": "Windup", "row": 8,  "frames": 3, "fps": 8,  "loop": false},
    {"name": "Attack", "row": 12, "frames": 3, "fps": 10, "loop": false},
    {"name": "Hit",    "row": 16, "frames": 2, "fps": 10, "loop": false},
    {"name": "Death",  "row": 20, "frames": 5, "fps": 9,  "loop": false}
  ]
}
```

- **`pixelsPerMeter`:** quantos pixels valem 1 m no jogo. Define o tamanho do sprite.
- **`pivotX`, `pivotY`:** o ponto do chão (os pés) dentro da célula, em pixels contados a partir do canto de baixo à esquerda. Nos voadores, é o centro do corpo.
- **`row`:** a linha da frente do clipe. As outras três direções vêm logo abaixo.
- **`fps`:** quadros por segundo a 1,0x. A animação acelera com o multiplicador de tempo.
- **`emission`, `bodyGlow`:** o mapa de brilho e quanto do corpo aparece nele. São opcionais.

## Clipes

| Clipe | Quando toca | Obrigatório |
|---|---|---|
| `Idle` | Parado | Sim |
| `Move` | Andando, ou voando no caso da Mariposa | Não |
| `Windup` | Aviso de ataque. Os quadros se espalham pela duração do aviso e o sprite pulsa em vermelho | Não |
| `Attack` | Golpe, disparo ou investida. Nos clipes sem laço, os quadros se espalham pela duração do golpe | Não |
| `Hit` | Ao levar dano, se estiver parado ou andando | Não |
| `Death` | Ao morrer. O último quadro fica parado e o corpo afunda no chão | Não |

Um clipe que falta é trocado pelo `Idle`.

## Gerar a partir de um modelo 3D

`Ferramentas/Sprites/render_enemy_sprites.py` renderiza um modelo animado (GLB) no Blender, em segundo plano, neste mesmo formato, mas sem o mapa de brilho:

```
blender -b --python Ferramentas/Sprites/render_enemy_sprites.py -- <config.json>
```

O cabeçalho do script descreve a configuração.
