# Peças de cenário da Via Aeônida

Cada pasta (`Bosque`, `Charco`, `Ruinas`, `Cratera`, `Comum`) tem um prefab por tipo de peça. Os que vieram do
gerador são **placeholders** feitos de primitivas. Para trocar por um modelo de verdade:

1. Abra o prefab (duplo clique) e substitua o conteúdo pelo modelo novo. **Não renomeie nem mova o prefab.**
2. Pivô na **base** da peça, frente apontando para **+Z**.
3. Escala real: o Ícaro tem cerca de **1,8 m**. As árvores placeholder têm ~10 m; rochas grandes, ~3 m de altura.
4. Collider: peças de cobertura precisam de collider (de preferência Box/Capsule simples). Decoração pequena
   (cogumelos, arbustos, brotos, fendas de lava) fica sem collider.
5. Materiais em **URP/Lit**, com **Enable GPU Instancing** ligado.
6. Registre o asset em `Assets/Icaro/CREDITOS.md` (só CC0 ou CC-BY).

Casos especiais:

- `Arvore`: é protótipo de árvore do **Terrain**. Precisa de um **LODGroup** (ou MeshRenderer) na **raiz** e,
  para bloquear a passagem, um **CapsuleCollider na raiz**.
- `Arbusto` e `Broto_Luminoso`: são **detalhes** do Terrain. O mesh e o MeshRenderer ficam na **raiz** do prefab.
- `Barreira`: mantenha o componente **RoomDoor** e o **NavMeshObstacle** na raiz (ela afunda no chão ao abrir).
- `Cogumelo_*` e `Fenda_Lava` têm uma luz **Baked**: mantenha-a se quiser as poças de luz no bake.
- `Poleiro`: rocha alta de topo plano (cerca de 5 m) onde o Espinhento nasce. Mantenha o filho **Topo** no centro do
  topo (é o ponto de spawn), colliders sólidos e um topo plano de pelo menos 3 x 3 m (para o NavMesh).

Depois de trocar, rode **Ícaro > Gerar Via Aeônida** para o Terrain recarregar árvores e detalhes. As peças
avulsas (rochas, ruínas, portais...) já aparecem trocadas na cena, por serem instâncias de prefab.

Modelos reais (FBX exportados do Blender; fontes em `ArteFonte/*.blend`, fora do git) ficam em `Assets/Icaro/Models/{Grupo}`.
O menu **Ícaro > Montar modelos importados** cria os materiais URP/Lit a partir de `Textures/{Grupo}/{Material}_BaseMap`,
`_Normal`, `_MetallicSmoothness` e `_Emission` e regrava estes prefabs com os modelos (scripts em `Scripts/Editor/Modelos`):

- `Arvore`, `Arvore_B`, `Arvore_C` (árvores alienígenas A, B e C, com LODs): o Terrain sorteia entre as variações que existirem.
- `Ruina_Muro`, `Ruina_Coluna` (pilar), `Ruina_Plataforma` (escadaria), `Ruina_Arco` e `Ruina_Bloco` (monólito de runas).
- `Comum/Nave_Dedalo` (nave caída; o filho `Embarque` marca onde fica o objetivo final).

Depois de montar, rode **Ícaro > Gerar Via Aeônida** para o Terrain e as arenas usarem as peças novas.
