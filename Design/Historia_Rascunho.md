# Rascunho da história (para revisar)

Rascunho de 2026-11-12, feito a partir das decisões de `Historia_e_Sistemas.md`. Nada aqui está fechado: é uma proposta para o usuário cortar, trocar e reescrever. Os nomes são sugestões.

## A ideia em uma frase

Os Aeônidas tentaram impedir o fim do próprio mundo segurando o tempo; conseguiram, e agora nada em Kairós termina, e o descompasso vaza para o universo inteiro. O ÍCARO-9, feito pelos descendentes esquecidos deles, vem desligar o que os próprios ancestrais ligaram.

## Antes do jogo

- **Os Aeônidas** viviam em Kairós. Mediam o tempo com anéis colossais (os do Arquipélago, do Cemitério de Anéis, do portal) e construíram a capital, **Archen Kaedra**, a cidade flutuante com a basílica.
- O mundo deles estava acabando (proposta: a estrela de Kairós estava morrendo; o **eclipse parado** no céu dos Picos é esse instante final congelado).
- O **Conselho dos Anéis** mandou construir na basílica o **Coração de Kairós**, uma máquina de cristal lilás que segura o tempo. Ele foi ligado às **11h58**: por isso os relógios de Archen Kaedra estão todos parados nessa hora.
- Funcionou do jeito errado: o tempo de Kairós saiu do compasso. Em alguns lugares parou (os Picos, o eclipse), em outros corre cada vez mais rápido (as lutas, o multiplicador). Os bichos e as plantas que vivem dentro dessa aceleração enlouqueceram.
- A maioria dos Aeônidas fugiu pelos portais para um "mundo azul": **a Terra**. Lá eles esqueceram quem eram e viraram os ancestrais da humanidade.
- Alguns ficaram: presos no instante, sem envelhecer, sem conseguir terminar. São os NPCs.
- Depois das 11h58, **Archen Kaedra foi atacada** (o cerco que aparece na ponte, nas vilas e na cidade). Quem atacou continua em aberto. Opções:
  1. criaturas que nasceram do próprio Coração, como um sistema imunológico do tempo;
  2. um grupo de Aeônidas que queria desligar o Coração à força;
  3. algo que veio de fora, atraído pelo descompasso.

## O ÍCARO-9

- Na Terra, o **descompasso** começou a aparecer: relógios atômicos discordando, estrelas fora do lugar. Uma agência (proposta: o **Consórcio Dédalo**) rastreou a origem até Kairós e mandou o ÍCARO-9.
- A **Dédalo** é a nave-mãe em órbita. A cápsula do ÍCARO-9 cai no Bosque; o objetivo deixa de ser "chegar à Dédalo" e passa a ser **"chegar à basílica e destruir o Coração"**. A Dédalo recolhe ele no fim.
- Gancho (proposta): o núcleo do ÍCARO-9 tem um fragmento do mesmo cristal lilás, achado na Terra. É por isso que o **multiplicador não afeta o jogador**, e é por isso que o tradutor funciona nele. O jogador descobre perto do fim que os criadores dele são descendentes dos Aeônidas e que ele está, sem saber, voltando para casa.
- Isso também amarra os três finais que já existem: **levar o fragmento do Coração** (a Terra ganha o poder de segurar o tempo), **desligar o Coração** (Kairós termina de morrer, o universo volta ao compasso) e **ficar como âncora** (o ÍCARO-9 vira o novo Coração, estável, e fica para sempre).

## Os personagens

### Méton, o mercador de instantes (o mercante)
- O único que anda no tempo certo além do jogador. Não é Aeônida e ninguém sabe o que é: aparece onde quer, sempre do mesmo jeito.
- Carismático, falador, trata tudo como negócio. "Eu não acredito em coincidências. Eu as vendo."
- Mistério (proposta): foi ele quem vendeu o cristal lilás aos Aeônidas, muito tempo atrás. Ele nunca confirma, só brinca com isso.
- Aparece duas vezes no Bosque, duas no Arquipélago e depois de cada chefe. Vende habilidades, armas e o tradutor (já no primeiro encontro).
- Visual (proposta): sprite pixel art, figura alta e magra com um casaco de remendos de várias épocas, uma mochila enorme de gavetas e relógios pendurados, cada um marcando uma hora diferente; o rosto escondido por uma máscara de porcelana sorridente.

### O Peregrino (primeira vila das Vilas do Alto)
- Um Aeônida que ia à basílica rezar diante do Coração quando os sinos tocaram às 11h58. Desde então anda em direção à cidade, mas ela nunca fica mais perto.
- Serve para contar o básico: os anéis, o Coração, as 11h58, a fuga para o "mundo azul".
- É o NPC de teste do sistema de diálogo.

### Varo, o guarda da barbacã (Ponte do Reino, preso atrás da grade)
- Guarda do portão da barbacã. A grade desceu às 11h58 e ele ficou do lado de dentro, guardando um portão que ninguém atravessa.
- Conta do cerco sem dizer quem atacou (a decisão fica em aberto): "eles não andavam, eles apareciam".
- Proposta: se o jogador desligar o Coração no final, a cena final mostra a grade subindo.

### Oren, o último sineiro (Bairro Voador, sala 20, boss secreto)
- Foi ele quem tocou o sino às 11h58, a mando do Conselho, para avisar a cidade que o Coração ia ser ligado. O sino caiu (é o sino caído ao pé da sineira) e ele ficou esperando a próxima badalada, que nunca vem.
- Sem o tradutor, só hieróglifos. Com o tradutor, conversa; se o jogador disser que veio destruir o Coração, ele entende que o fim chegou e se transforma no boss secreto (o evento `chefe_secreto` da conversa).
- Derrotado: dá os Ion akimbos e a chave do portão da caverna.

## As conversas

As quatro conversas acima já estão como diálogos de rascunho em `Assets/Icaro/Balance/Dialogos/` (menu "Ícaro > Diálogos > Criar diálogos de rascunho"). Dá para editar o texto direto no Inspector de cada asset; o menu não sobrescreve um asset que já existe.

| Asset | Quem | Onde (planejado) |
| --- | --- | --- |
| `Dialogo_Peregrino` | O Peregrino | Vila Escalonada (primeira vila) |
| `Dialogo_Meton` | Méton | Mercante (Bosque, Arquipélago, depois dos chefes) |
| `Dialogo_Varo` | Varo | Grade da barbacã, Ponte do Reino |
| `Dialogo_Oren` | Oren | Bairro Voador (sala 20) |

## A abertura (a reescrever)

Hoje o registro de inicialização fala da missão de chegar à Dédalo. Proposta para o novo registro:

```
ÍCARO-9 // REINICIALIZANDO
IMPACTO CONFIRMADO: PLANETA KAIRÓS
DÉDALO: SEM RESPOSTA
ORIGEM DO DESCOMPASSO: 2.300 KM AO NORTE
MISSÃO: LOCALIZAR E NEUTRALIZAR A FONTE
```

## Perguntas em aberto

1. Quem atacou Archen Kaedra (as três opções acima ou outra)?
2. O gancho do cristal no núcleo do ÍCARO-9 fica?
3. O nome do Méton e o visual dele.
4. O Peregrino fica na Vila Escalonada ou em outro lugar?
