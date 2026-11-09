# Maciço das Vilas do Alto (Picos acima das Nuvens), esculpido no Blender em segundo plano:
#   blender.exe -b --python Ferramentas/Picos/macico_vilas.py [-- --preview <pasta>]
# Uma montanha grande ao norte da cidade flutuante, no lugar do anel de colunas e das ilhas do primeiro esboço (o
# usuário achou nada natural): cristas com cumes e uma sela, contrafortes descendo para o vale de nuvens, e as vilas
# em lugares naturais dela, cada uma um pouco mais baixa, sempre com a cidade à vista do outro lado do vale:
#   o Terraço do Portal (chegada, 235 m: torre de pedra lavrada em pé no vale ao norte da crista principal, que
#   esconde a cidade, sem encostar na montanha; o caminho até a primeira vila é uma construção só, calçada do começo
#   ao fim: sobe a fenda por uma escadaria reta, contorna as colunas até onde a cidade aparece e desce sem voltar a
#   subir; vãos grandes viram pontes em arcos),
#   Vila Escalonada (180 m, na ponta de um contraforte, com dois terraços mais baixos na frente),
#   Vila dos Sinos (160 m, na sela entre dois cumes), Vila das Engrenagens (128 m, num ombro largo),
#   Vila-Ponte (108 m, uma ponte larga com casas sobre a garganta que racha a montanha),
#   Vila Pendurada (98 m no alto de um paredão e as casas nos patamares do zigue-zague que desce por ele) e
#   o portão da cidade (34 m, na ponta do último contraforte, sala 22), de onde a ponte colossal vai até a cidade (30 m).
# A cidade fica uns 600 m ao sul da chegada e uns 150 m abaixo dela.
#
# Feita como a Montanha do Bastião: colunas de arenito (pilhas de blocos) dispostas pelas cristas e contrafortes (a
# altura de cada coluna vem de um campo de cristas), mais colunas de apoio sob as vilas e os caminhos, fundidas por
# remesh de voxel e esculpidas (blocos de Voronoi, fendas, degraus). Depois, cortes booleanos abrem o espaço do jogo:
# o chão de cada vila (0,45 m abaixo do calçamento), a faixa de cada caminho (com o lado do vale limpo até 20 m para
# não virar trincheira), os patamares do zigue-zague em degraus e a garganta.
# Caminho do terraço (design_road): a linha suave e o perfil do piso saem do relevo das colunas antes do remesh;
# colunas novas fazem a rocha embaixo do piso, as paredes da fenda perto da escadaria e tapam os buracos fundos entre
# as colunas perto do jogo; o corredor do caminho é tirado das colunas (com a largura variando por ruído), e o remesh e
# a escultura deixam as paredes com cara de rocha. O Unity constrói o caminho pela planta (degraus, patamares, rampas,
# muros de arrimo, arcos e balaústres). Depois da redução, pedaços soltos pequenos saem.
# A colisão é uma cópia mais leve (Macico_Vilas_Colisao, sem desenho no Unity).
#
# Coordenadas da planta: (L, N, A) = metros para leste, para o norte e altura, a partir do centro da cidade no nível 0
# (PicosHighlands.k_High no Unity); no Blender x = -L, y = -N, z = A (o FBX chega no Unity como (L, A, N), posto sem
# girar). Exporta Assets/Icaro/Models/Picos/Macico_Vilas.fbx (rocha com colisão e o gelo) e Macico_Vilas_Layout.json
# (vilas, trechos de caminho, ponte da garganta, ponte colossal e patamares com casas), que o PicosBuilder lê.
import json
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import montanhas_destaque as kit  # noqa: E402
import montanha_bastiao as cut  # noqa: E402  (sólidos de corte, corte booleano)

NAME = 'Macico_Vilas'
VOXEL = 2.6
# Faces por zona depois da redução (o que fica perto dos cortes não é reduzido): perto do jogo (até DETAIL_NEAR m de um
# caminho, vila ou ponte, na planta), meio (até DETAIL_MID) e costas; a colisão reduz tudo junto para FACES_COLLISION
DETAIL_NEAR = 150.0
DETAIL_MID = 330.0
FACES_NEAR = 70000
FACES_MID = 30000
FACES_FAR = 25000
FACES_COLLISION = 90000

# ---------- planta ----------

# Vilas: nome, (L, N), chão, raio do chão calçado
VILLAGES = [
    ('Terraco', (-422.6, 778.0), 235.0, 30.56),      # o maior que coube sem encostar na rocha (2026-10-30)
    ('Escalonada', (-300.0, 545.0), 180.0, 40.0),
    ('Escalonada_T1', (-252.0, 490.0), 174.8, 17.0),     # 2026-10-31: 2,8 m mais alto (a escada da vila tinha ~35°)
    ('Escalonada_T2', (-300.0, 452.0), 166.0, 16.0),     # e este 2 m (a do T1 ao T2 passaria de 30°)
    ('Sinos', (-125.0, 672.0), 160.0, 36.0),
    ('Engrenagens', (40.0, 565.0), 128.0, 46.0),
    ('Ponte_O', (140.0, 505.0), 108.0, 18.0),
    ('Ponte_L', (248.0, 520.0), 108.0, 18.0),
    ('Pendurada', (322.0, 480.0), 98.0, 22.0),
    ('Portao', (225.0, 300.0), 34.0, 28.0),
]
V = {name: (Vector((c[0], c[1], floor)), r) for name, c, floor, r in VILLAGES}

# Caminhos entre as vilas: vila de saída, pontos do meio (L, N, A), vila de chegada. Cada trecho vira escada se a
# inclinação passar de STAIR_SLOPE, senão calçada
ROUTES = [
    ('Escalonada', [(-232.0, 600.0, 176.0), (-180.0, 640.0, 168.0)], 'Sinos'),
    ('Escalonada', [], 'Escalonada_T1'),
    ('Escalonada_T1', [], 'Escalonada_T2'),
    ('Sinos', [(-62.0, 640.0, 153.0), (-12.0, 604.0, 140.0)], 'Engrenagens'),
    ('Engrenagens', [(98.0, 532.0, 120.0)], 'Ponte_O'),
    ('Ponte_L', [(286.0, 508.0, 103.0)], 'Pendurada'),
]
STAIR_SLOPE = 0.2
# Os caminhos entre as vilas também são pavimentados (pedido do usuário em 2026-10-31: os trechos retos soltos ficavam
# bugados e com pedaços faltando): cada um vira uma construção só como a do Terraço (design_paved + PicosRoad.BuildRoad),
# pelos pontos de ROUTES e do zigue-zague da Pendurada, com as alturas da planta (sempre descendo), as curvas arredondadas
# e as pontas logo dentro da beira de cada vila
PAVED_EDGE = 0.97           # fração do raio da vila onde o caminho começa e termina
PAVED_AVOID = 45.0          # buracos perto do caminho do Terraço ficam com ele (a vista da descida não é tapada de novo)

# Caminho do Terraço do Portal até a Escalonada (pedido do usuário: uma construção só, calçada do começo ao fim, com
# balaústres dos dois lados; nada de andar na rocha nem de escadas de tamanhos diferentes encaixadas). Os pontos foram
# marcados pelo usuário com a tecla M do modo livre (Prints/pontos.txt) e servem só de guia: a linha passa por eles com
# as curvas arredondadas, e as alturas saem do relevo (sem seguir as marcadas à risca)
ROAD_POINTS = [(-351.0, 798.2), (-235.8, 805.6), (-180.9, 732.5), (-179.3, 696.8), (-245.9, 632.2), (-311.0, 643.1),
               (-341.8, 604.7)]
ROAD_HALF = 2.0             # meia largura do piso
ROAD_FILLET = 14.0          # raio das curvas
ROAD_RAMP = 0.1             # inclinação máxima da rampa calçada (acima, lances de degraus)
ROAD_STEP_RISE, ROAD_STEP_RUN = 0.2, 0.6
ROAD_FLIGHT = 16            # degraus por lance, no máximo
ROAD_FLIGHT_EVERY = 20.0    # num trecho comprido e suave, um lance curto a cada ~20 m
ROAD_LANDING = 2.4
ROAD_SECTION_END = 2.0      # patamar nas pontas de cada trecho
ROAD_MERGE = 0.04           # trechos vizinhos com inclinações a menos disto viram um só
ROAD_PULL = 16.0            # quanto o perfil da descida pode sair do relevo para ficar mais uniforme
ROAD_HARD = 2.6             # do eixo até aqui, nada de rocha até 7 m acima do piso
ROAD_CLEAR = 1.0            # folga do corredor aberto nas colunas (as paredes ficam a ~3,9-5,1 m do eixo)
ROAD_SKY = 600.0
ROAD_FILL_NEAR = 60.0       # buracos entre as colunas tapados até esta distância do caminho
FRAGMENT_SIZE = 30.0        # pedaços soltos menores que isto (diagonal, m) saem
# Lâminas (pedido do usuário em 2026-10-31: uma ponta fina de ~19 m em pé na beira da descida, perto do ponto 9): onde a
# rocha passava por cima do caminho sem massa atrás, o clear_road empurrava os vértices para a beira e ficava uma lâmina.
# Perto dos caminhos e acima do piso, a rocha mais estreita que ~2 x SPIKE_ERODE desce até o chão em volta (abertura
# morfológica pelos vértices: o mínimo num disco e depois o máximo num disco um pouco maior)
SPIKE_NEAR = 14.0           # do eixo do caminho
SPIKE_ERODE, SPIKE_DILATE = 1.6, 2.4
SPIKE_CELL = 0.5
SPIKE_TOLERANCE = 1.2
# Vista da descida (pedido do usuário em 2026-10-31): as colunas à esquerda de quem desce, atravessadas pelo caminho,
# tapavam a vista da cidade; antes do remesh elas são refeitas com o topo abaixo do piso, descendo em degraus para longe
# do caminho (a escultura as trata como qualquer outra coluna). Achatar a malha pronta ficou feio e foi abandonado.
VIEW_START = 8.0            # m depois do alto da subida (as paredes da fenda ficam)
VIEW_END = 10.0             # m antes do fim
VIEW_REACH = 40.0           # colunas com a beira a até isto do eixo (30 deixava pilares altos logo depois)
VIEW_DROP = (2.0, 4.5)      # topo abaixo do piso na beira do caminho (sorteio)
VIEW_SLOPE = 0.2            # e mais isto por metro de distância (degraus descendo para o lado da cidade)
VIEW_PROTECT = 8.0          # folga dos outros caminhos e das vilas, que não são mexidos
# O relevo onde o usuário marcou o caminho é o da versão de 2026-10-30 (terraço antigo em (-411, 750) e o caminho dele
# pela crista): as colunas de apoio daquele terraço e daquele caminho continuam sendo sorteadas (para o sorteio de todo o
# resto não mudar); as do terraço são descartadas (ficariam encostadas na torre nova), as do caminho ficam (o fim delas é
# a descida que o usuário escolheu). Nenhum dos dois é mais cortado.
OLD_TERRACE = ((-411.0, 750.0), 232.0, 23.0)
OLD_ROUTE = [(-404.0, 722.0, 231.0), (-396.0, 700.0, 227.0), (-404.0, 678.0, 223.0), (-388.0, 652.0, 218.0),
             (-372.0, 628.0, 210.0), (-366.0, 600.0, 202.0)]


def old_segments():
    """Trechos do caminho antigo (terraço de 2026-10-30 até a Escalonada), só para as colunas de apoio."""
    (e, n), floor, r = OLD_TERRACE
    c = Vector((e, n, floor))
    first = Vector(OLD_ROUTE[0])
    d = Vector((first.x - c.x, first.y - c.y, 0.0)).normalized()
    start = Vector((c.x + d.x * r * 0.85, c.y + d.y * r * 0.85, c.z))
    chain = [start] + [Vector(q) for q in OLD_ROUTE] + [rim('Escalonada', OLD_ROUTE[-1])]
    return [(a, b, abs(b.z - a.z) / max(0.1, (b.xy - a.xy).length) > STAIR_SLOPE, 'Antigo')
            for a, b in zip(chain, chain[1:])]

# Vila Pendurada: zigue-zague descendo o paredão ao sul do topo, um lance por patamar (N), do leste para o oeste e de
# volta, com as voltas nas pontas; no fim, a calçada pela crista até o portão
# Lances colados uns nos outros (pedido do usuário em 2026-11-08: escadaria toda de pedra, sem rocha entre os lances,
# como a da costa do Arquipélago); antes ficavam 12 m um do outro com a rocha no meio
FLIGHT_N = [448.0, 442.0, 436.0, 430.0, 424.0]
FLIGHT_E = (290.0, 330.0)
TURN_E = (280.0, 340.0)
FLIGHT_TOP, FLIGHT_DROP = 98.0, 11.5
GATE_PATH = [(274.0, 368.0, 38.0)]
# Escadaria do zigue-zague (pedido do usuário em 2026-10-31: como a escadaria da costa do Arquipélago, sem espelhar):
# lances retos e largos pelas prateleiras de FLIGHT_N, de degraus iguais (0,84 x ~0,27, como os da costa) com um patamar
# no meio de cada lance, e as voltas em plataformas retangulares planas que juntam um lance ao próximo, com balaústres;
# as prateleiras, as alturas e as casas continuam. Da última plataforma, a calçada até o portão (design_paved)
SWITCH_HALF = 3.0           # meia largura dos lances, das plataformas e da calçada até o portão (6 m)
SWITCH_RUN, SWITCH_RISE = 0.84, 0.27
SWITCH_LEAD = 0.42          # piso na beira da plataforma antes do primeiro degrau
SWITCH_PLATFORM = 6.0       # largura (leste-oeste) das plataformas

# Garganta que racha a montanha entre a Vila das Engrenagens e a Vila Pendurada (a Vila-Ponte passa por cima)
GORGE = [(150.0, 420.0), (166.0, 515.0), (180.0, 650.0), (190.0, 770.0), (198.0, 770.0), (206.0, 650.0), (224.0, 515.0),
         (238.0, 420.0)]
DECK_WIDTH = 24.0

# Cristas: (L, N, A) de ponta a ponta; o campo de altura cai para os lados (CREST_FALL por metro)
CRESTS = [
    # crista principal, de oeste para leste, com a sela da Vila dos Sinos
    [(-600.0, 640.0, 90.0), (-430.0, 700.0, 280.0), (-262.0, 732.0, 340.0), (-125.0, 674.0, 166.0), (5.0, 728.0, 320.0),
     (120.0, 792.0, 392.0), (270.0, 772.0, 340.0), (430.0, 702.0, 270.0), (600.0, 640.0, 90.0)],
    # crista de trás, mais alta (silhueta)
    [(-520.0, 860.0, 250.0), (-380.0, 885.0, 330.0), (-120.0, 905.0, 410.0), (150.0, 935.0, 372.0), (380.0, 885.0, 332.0),
     (540.0, 850.0, 230.0)],
    # contrafortes descendo para o vale (as vilas ficam nas pontas e nos ombros deles)
    [(-262.0, 732.0, 340.0), (-300.0, 562.0, 186.0), (-288.0, 452.0, 150.0), (-280.0, 380.0, 40.0)],
    [(120.0, 792.0, 392.0), (40.0, 580.0, 134.0), (20.0, 470.0, 70.0), (10.0, 400.0, 0.0)],
    [(270.0, 772.0, 340.0), (322.0, 498.0, 104.0)],
    [(5.0, 728.0, 320.0), (-40.0, 630.0, 150.0)],
    [(430.0, 702.0, 270.0), (420.0, 520.0, 120.0), (400.0, 420.0, 30.0)],
    [(-430.0, 700.0, 280.0), (-470.0, 540.0, 130.0), (-480.0, 440.0, 20.0)],
    # contraforte do portão, descendo do paredão da Vila Pendurada até a ponta onde fica o portão
    [(300.0, 420.0, 50.0), (240.0, 320.0, 26.0), (215.0, 285.0, 10.0)],
]
CREST_FALL = 1.35


def B(p):
    """Planta (L, N, A) -> Blender."""
    return Vector((-p[0], -p[1], p[2]))


def crest_height(e, n):
    best = -1000.0
    for crest in CRESTS:
        for (ax, ay, ah), (bx, by, bh) in zip(crest, crest[1:]):
            dx, dy = bx - ax, by - ay
            length2 = dx * dx + dy * dy
            t = max(0.0, min(1.0, ((e - ax) * dx + (n - ay) * dy) / length2))
            px, py = ax + dx * t, ay + dy * t
            d = math.hypot(e - px, n - py)
            best = max(best, ah + (bh - ah) * t - max(0.0, d - 8.0) * CREST_FALL)
    return best


def inside(poly, e, n):
    hit = False
    for (ax, ay), (bx, by) in zip(poly, poly[1:] + poly[:1]):
        if (ay > n) != (by > n) and e < ax + (n - ay) * (bx - ax) / (by - ay):
            hit = not hit
    return hit


def rim(name, toward):
    """Ponto na beira calçada de uma vila, virado para 'toward' (no chão dela)."""
    c, r = V[name]
    d = Vector((toward[0] - c.x, toward[1] - c.y, 0.0)).normalized()
    return Vector((c.x + d.x * r * 0.85, c.y + d.y * r * 0.85, c.z))


def plan():
    """Trechos de caminho (a, b, escada?, largura), a ponte da garganta, a ponte colossal e os patamares com casas."""
    segments = []
    for start, mids, end in ROUTES:
        points = [Vector(m) for m in mids]
        first = points[0] if points else V[end][0]
        last = points[-1] if points else V[start][0]
        chain = [rim(start, first)] + points + [rim(end, last)]
        for a, b in zip(chain, chain[1:]):
            slope = abs(b.z - a.z) / max(0.1, (b.xy - a.xy).length)
            segments.append((a, b, slope > STAIR_SLOPE, start + '-' + end))

    # Zigue-zague da Vila Pendurada
    top = Vector((FLIGHT_E[1], FLIGHT_N[0], FLIGHT_TOP))
    segments.append((rim('Pendurada', top), top, False, 'Pendurada'))
    h = FLIGHT_TOP
    flights, landings = [], []
    for k, n in enumerate(FLIGHT_N):
        west = k % 2 == 0                       # os lances pares descem para o oeste
        e0, e1 = (FLIGHT_E[1], FLIGHT_E[0]) if west else FLIGHT_E
        a, b = Vector((e0, n, h)), Vector((e1, n, h - FLIGHT_DROP))
        segments.append((a, b, True, 'Pendurada'))
        flights.append((a, b))
        h -= FLIGHT_DROP
        turn = TURN_E[0] if west else TURN_E[1]
        if k + 1 < len(FLIGHT_N):
            c, d = Vector((turn, n, h)), Vector((turn, FLIGHT_N[k + 1], h))
            segments.append((b, c, False, 'Pendurada'))
            segments.append((c, d, False, 'Pendurada'))
            segments.append((d, Vector((e1, FLIGHT_N[k + 1], h)), False, 'Pendurada'))
        # As casas dos patamares saíram em 2026-11-08 (pedido do usuário): 'landings' fica vazio
    end = flights[-1][1]
    chain = [end] + [Vector(p) for p in GATE_PATH] + [rim('Portao', GATE_PATH[-1])]
    for a, b in zip(chain, chain[1:]):
        slope = abs(b.z - a.z) / max(0.1, (b.xy - a.xy).length)
        segments.append((a, b, slope > STAIR_SLOPE, 'Pendurada-Portao'))

    # Vila-Ponte: das duas cabeceiras, entrando um pouco nelas
    o, l = V['Ponte_O'][0], V['Ponte_L'][0]
    along = (l - o).normalized()
    deck = (o + along * 6.0, l - along * 6.0)
    # Ponte colossal: do portão até a beira da cidade (raio 170, chão a 30 m)
    gate = V['Portao'][0]
    to_city = Vector((-gate.x, -gate.y, 0.0)).normalized()
    bridge = (rim('Portao', (0.0, 0.0)), Vector((-to_city.x * 150.0, -to_city.y * 150.0, 30.0)))
    return segments, flights, landings, deck, bridge


def paved_specs():
    """(nome, vila de saída, pontos do meio (L, N, A), vila de chegada, alturas fixas?) de cada caminho pavimentado."""
    specs = [((start + '_' + end).replace('Escalonada_Escalonada', 'Escalonada'), start, [tuple(m) for m in mids], end,
              False) for start, mids, end in ROUTES]
    return specs


def switchback_plan():
    """Peças do zigue-zague da Vila Pendurada: plataformas (nome, centro (L, N), altura, tamanho (L, N), aberturas
    [(lado, de, até)]) e lances (nome, de, para, ponto do alto (L, N), ponto de baixo, altura do alto). Lados: 'N', 'S',
    'E' (leste), 'W' (oeste); a abertura vai de 'de' a 'até' ao longo do lado (None = o lado todo)."""
    plats, flights = [], []
    w = SWITCH_PLATFORM
    east, west = FLIGHT_E[1] + w * 0.5, FLIGHT_E[0] - w * 0.5
    h = FLIGHT_TOP
    # No alto, na ponta leste do primeiro lance: chega-se da vila pelo norte
    plats.append(('Zz_0', (east, FLIGHT_N[0]), h, (w, w), [('N', None, None), ('W', None, None)]))
    for k, n in enumerate(FLIGHT_N):
        goes_west = k % 2 == 0
        x0, x1 = (FLIGHT_E[1], FLIGHT_E[0]) if goes_west else FLIGHT_E
        flights.append(('Zz_Lance_%d' % (k + 1), 'Zz_%d' % k, 'Zz_%d' % (k + 1), (x0, n), (x1, n), h))
        h -= FLIGHT_DROP
        x = west if goes_west else east
        inner = 'E' if goes_west else 'W'
        if k + 1 < len(FLIGHT_N):
            m = FLIGHT_N[k + 1]
            hs = SWITCH_HALF
            # O lado norte encosta na volta de cima (mais alta): sem balaústres; a primeira volta fica solta
            opens = [(inner, n - hs, n + hs), (inner, m - hs, m + hs)]
            if k > 0:
                opens.append(('N', None, None))
            plats.append(('Zz_%d' % (k + 1), (x, (n + m) * 0.5), h, (w, n - m + 2.0 * hs), opens))
        else:                               # a última: daqui a calçada sai para o sul, até o portão
            plats.append(('Zz_%d' % (k + 1), (x, n), h, (w, w), [(inner, None, None), ('S', None, None), ('N', None, None)]))
    return plats, flights


def flight_cross(length, top, drop):
    """Perfil de um lance: piso curto na beira, metade dos degraus, patamar no meio, a outra metade (degraus iguais de
    SWITCH_RUN de piso; a altura divide a queda em partes iguais perto de SWITCH_RISE)."""
    steps = max(2, int(round(drop / SWITCH_RISE)))
    rise = drop / steps
    middle = length - SWITCH_LEAD - steps * SWITCH_RUN
    assert middle > 1.5, 'lance curto demais para os degraus'
    cross, s, h = [(0.0, top, 'l')], SWITCH_LEAD, top
    for k in range(steps):
        if k == steps // 2:
            cross.append((s, h, 'l'))
            s += middle
        h -= rise
        cross.append((s, h, 's'))
        s += SWITCH_RUN
    cross.append((length, h, 'l'))
    return cross


def design_piece(parts, name, frm, to, a, b, cross, half, seed):
    """Uma peça reta (lance ou plataforma) do zigue-zague, como os caminhos: amostras a cada 0,25 m, o piso pelo
    perfil, os vãos e as colunas de apoio embaixo (os patamares do zigue-zague já são cortados na rocha)."""
    rnd = random.Random(seed)
    cover = Cover([COL_OF[o.name] for o in parts if o.name in COL_OF])
    a, b = Vector(a), Vector(b)
    length = (b - a).length
    count = max(2, int(round(length / 0.25)) + 1)
    t = (b - a).normalized()
    pos = [a.lerp(b, k / (count - 1)) for k in range(count)]
    tan = [t.copy() for _ in range(count)]
    side = Vector((t.y, -t.x))
    ground = [max(cover.top(*(p + side * o)) for o in (-half * 0.8, 0.0, half * 0.8)) for p in pos]

    def deck_at(s):
        h = cross[0][1]
        for cs, ch, kind in cross:
            if cs > s + 1e-6:
                break
            h = ch
        return h
    step = length / (count - 1)
    deck = [deck_at(i * step) for i in range(count)]
    low = [min(deck[max(0, i - 12):i + 13]) for i in range(count)]
    high = [max(deck[max(0, i - 12):i + 13]) for i in range(count)]
    gaps, i = [], 0
    while i < count:
        if ground[i] < deck[i] - 10.0:
            j = i
            while j + 1 < count and ground[j + 1] < deck[j + 1] - 10.0:
                j += 1
            if (j - i) * step >= 10.0:
                gaps.append((i * step, j * step))
            i = j + 1
        else:
            i += 1
    added = 0
    s = 0.0
    while s < length:
        i = min(count - 1, int(s / step))
        r = rnd.uniform(4.2, 5.2)
        if ground[i] < deck[i] - 1.6 and not any(g0 - r - 1 < s < g1 + r + 1 for g0, g1 in gaps):
            p = pos[i]
            for o in ((-half * 0.5, half * 0.5) if half > 2.5 else (0.0,)):
                q = p + side * o
                obj = column(q.x, q.y, low[i] - 1.5, r, rnd, base=1.25, bed=(4.0, 8.0))
                parts.append(obj)
                cover.add(COL_OF[obj.name])
                added += 1
        s += rnd.uniform(4.0, 5.0)
    print('[Vilas] %s: %.0f m, %+.1f m, colunas novas %d, vãos %s' % (name, length, deck[-1] - deck[0], added,
                                                                      gaps or 'nenhum'))
    return {'name': name, 'from': frm, 'to': to, 'pos': pos, 'tan': tan, 'deck': deck, 'low': low, 'high': high,
            'cross': cross, 'gaps': gaps, 'ground': ground, 'half': half}


def design_switchback(parts, seed, avoid=()):
    """O zigue-zague inteiro: a ligação da vila à primeira plataforma, os lances, as plataformas e a calçada da última
    plataforma até o portão. As plataformas vão como caminhos retos e planos (para o corte, o acabamento e a colisão)
    com 'platform' preenchido (o Unity as monta à parte)."""
    plats, flights = switchback_plan()
    pieces = []
    by_name = {}
    for k, (name, (e, n), h, (sl, sn), openings) in enumerate(plats):
        piece = design_piece(parts, name, name, name, (e, n + sn * 0.5), (e, n - sn * 0.5),
                             [(0.0, h, 'l'), (sn, h, 'l')], sl * 0.5, seed + k)
        piece['platform'] = {'center': (e, n), 'height': h, 'size': (sl, sn), 'openings': openings}
        pieces.append(piece)
        by_name[name] = (e, n, h, sl, sn)
    # Da vila à primeira plataforma (plana, entra pelo lado norte dela)
    e, n, h, sl, sn = by_name['Zz_0']
    top = Vector((e, n + sn * 0.5))
    start = edge_point('Pendurada', top)
    length = (top - start).length
    cross = []
    lay_section(0.0, V['Pendurada'][0].z, length, h, cross)
    cross.append((length, h, 'l'))
    pieces.append(design_piece(parts, 'Pendurada_Zz', 'Pendurada', 'Zz_0', start, top, cross, SWITCH_HALF, seed + 20))
    for k, (name, frm, to, a, b, h) in enumerate(flights):
        length = (Vector(b) - Vector(a)).length
        pieces.append(design_piece(parts, name, frm, to, a, b, flight_cross(length, h, FLIGHT_DROP), SWITCH_HALF,
                                   seed + 30 + k))
    # Da última plataforma ao portão: calçada pela crista (sai pelo lado sul dela)
    e, n, h, sl, sn = by_name[plats[-1][0]]
    spec = ('Zz_Portao', ((e, n - sn * 0.5), h, plats[-1][0]), [tuple(q) for q in GATE_PATH], 'Portao', False)
    pieces.append(design_paved(parts, spec, 6106, avoid=avoid, half=SWITCH_HALF))
    return pieces


def edge_point(name, toward, frac=PAVED_EDGE):
    """Ponto (planta) logo dentro da beira de uma vila, virado para 'toward'."""
    c, r = V[name]
    d = (Vector((toward[0], toward[1])) - c.xy).normalized()
    return c.xy + d * r * frac


# ---------- colunas ----------

COL_OF = {}                     # nome do objeto -> (L, N, topo, raio do topo)
COL_SHAPE = {}                  # nome do objeto -> (base, bed)


def column(e, n, top, r_top, rnd, base=1.35, bed=(8.0, 16.0)):
    x, y, _ = B((e, n, 0.0))
    obj = kit.column(x, y, top, r_top, r_top * base, rnd, lean=(rnd.uniform(-2, 2), rnd.uniform(-2, 2)), bed=bed,
                     shift=0.1)
    COL_OF[obj.name] = (e, n, top, r_top)
    COL_SHAPE[obj.name] = (base, bed)
    return obj


def support(parts, rnd, e, n, floor, radius):
    """Colunas largas embaixo de um chão (o corte deixa plano): uma no meio e uma coroa em volta."""
    parts.append(column(e, n, floor - 1.0, radius * 0.75, rnd))
    count = 6 if radius > 20 else 4
    for k in range(count):
        a = k / count * math.tau + rnd.uniform(-0.3, 0.3)
        parts.append(column(e + math.cos(a) * radius * 0.62, n + math.sin(a) * radius * 0.62, floor - 1.0,
                            radius * 0.48, rnd))


def downhill(a, b):
    """Lado do vale de um trecho (o de crista mais baixa a 18 m): +1 = à direita de a->b, -1 = à esquerda."""
    d = (b.xy - a.xy).normalized()
    right = Vector((d.y, -d.x))
    mid = (a.xy + b.xy) * 0.5
    hr = crest_height(*(mid + right * 18.0))
    hl = crest_height(*(mid - right * 18.0))
    return 1.0 if hr < hl else -1.0


def build_columns(segments):
    rnd = random.Random(2809)
    parts = []
    # Campo das cristas: grade com sorteio, a altura de cada coluna vem do campo (com um degrau sorteado)
    spacing = 26.0
    count = 0
    n = 360.0
    while n < 1000.0:
        e = -620.0
        while e < 620.0:
            pe, pn = e + rnd.uniform(-7.0, 7.0), n + rnd.uniform(-7.0, 7.0)
            h = crest_height(pe, pn)
            if h > -45.0 and not inside(GORGE, pe, pn):
                parts.append(column(pe, pn, h - rnd.uniform(0.0, 9.0), rnd.uniform(12.0, 17.0), rnd))
                count += 1
            e += spacing
        n += spacing
    print('[Vilas] colunas das cristas', count)

    # Apoio das vilas
    for name, (c, r) in V.items():
        if name == 'Terraco':                   # a torre do terraço fica no ar, sem tocar na montanha
            (e, n), floor, r_old = OLD_TERRACE
            support([], rnd, e, n, floor, r_old + 3.0)
        else:
            support(parts, rnd, c.x, c.y, c.z, r + 3.0)

    # Apoio dos caminhos: colunas ao longo do trecho, um pouco para o lado do vale
    for a, b, stairs, _ in segments:
        length = (b.xy - a.xy).length
        side = downhill(a, b)
        d = (b.xy - a.xy).normalized()
        right = Vector((d.y, -d.x)) * side
        steps = max(1, int(length / 10.0))
        for k in range(steps + 1):
            t = k / steps
            p = a.lerp(b, t)
            q = p.xy + right * 3.0
            parts.append(column(q.x, q.y, p.z - 1.2, rnd.uniform(6.5, 8.0), rnd, bed=(5.0, 10.0)))

    # Paredão da Vila Pendurada (o zigue-zague é cortado nele)
    e = 262.0
    while e <= 358.0:
        n = 392.0
        while n <= 466.0:
            parts.append(column(e + rnd.uniform(-3, 3), n + rnd.uniform(-3, 3), 101.0 + rnd.uniform(0.0, 4.0),
                                rnd.uniform(9.0, 11.0), rnd, bed=(7.0, 13.0)))
            n += 15.0
        e += 15.0
    return parts


# ---------- caminho do terraço até a Escalonada: uma construção só ----------

class Cover:
    """Relevo aproximado antes do remesh: a altura do topo da coluna mais alta que cobre cada ponto (planta)."""

    def __init__(self, cols, cell=24.0):
        self.cell = cell
        self.grid = {}
        for col in cols:
            self.add(col)

    def add(self, col):
        e, n, top, r = col
        for i in range(int((e - r) // self.cell), int((e + r) // self.cell) + 1):
            for j in range(int((n - r) // self.cell), int((n + r) // self.cell) + 1):
                self.grid.setdefault((i, j), []).append(col)

    def top(self, e, n, shrink=0.85):
        best = -1000.0
        for ce, cn, top, r in self.grid.get((int(e // self.cell), int(n // self.cell)), ()):
            if top > best and (e - ce) ** 2 + (n - cn) ** 2 < (r * shrink) ** 2:
                best = top
        return best


def rounded_line(points, radius, step):
    """Linha pelos pontos (planta) com as quinas arredondadas por arcos de até 'radius', amostrada a cada 'step' m:
    listas de posições e tangentes (Vector 2D)."""
    pts = [Vector(p[:2]) for p in points]
    dense = [pts[0]]
    for k in range(1, len(pts) - 1):
        a, b, c = pts[k - 1], pts[k], pts[k + 1]
        d0, d1 = (b - a).normalized(), (c - b).normalized()
        turn = math.acos(max(-1.0, min(1.0, d0.dot(d1))))
        if turn < 1e-3:
            continue
        cut = min(radius * math.tan(turn / 2), (b - a).length * 0.45, (c - b).length * 0.45)
        r = cut / math.tan(turn / 2)
        p0, p1 = b - d0 * cut, b + d1 * cut
        sign = 1.0 if d0.x * d1.y - d0.y * d1.x > 0 else -1.0
        center = p0 + Vector((-d0.y, d0.x)) * sign * r
        a0 = math.atan2(p0.y - center.y, p0.x - center.x)
        dense.append(p0)
        count = max(2, int(r * turn / 0.2))
        for i in range(1, count + 1):
            ang = a0 + sign * turn * i / count
            dense.append(center + Vector((math.cos(ang), math.sin(ang))) * r)
    dense.append(pts[-1])
    # Reamostra por comprimento
    out, tangents = [dense[0].copy()], []
    carry = 0.0
    for a, b in zip(dense, dense[1:]):
        seg = (b - a).length
        if seg < 1e-6:
            continue
        d = (b - a) / seg
        t = step - carry
        while t <= seg:
            out.append(a + d * t)
            t += step
        carry = seg - (t - step)
    if (out[-1] - dense[-1]).length > step * 0.3:
        out.append(dense[-1].copy())
    for i in range(len(out)):
        a, b = out[max(0, i - 1)], out[min(len(out) - 1, i + 1)]
        tangents.append((b - a).normalized())
    return out, tangents


def pava(values, increasing=True):
    """Ajuste monótono (média dos vizinhos que violam a ordem)."""
    blocks = []
    for v in (values if increasing else [-x for x in values]):
        blocks.append([v, 1])
        while len(blocks) > 1 and blocks[-2][0] > blocks[-1][0]:
            v2, n2 = blocks.pop()
            v1, n1 = blocks.pop()
            blocks.append([(v1 * n1 + v2 * n2) / (n1 + n2), n1 + n2])
    out = []
    for v, n in blocks:
        out += [v] * n
    return out if increasing else [-x for x in out]


def lay_section(s0, h0, s1, h1, out):
    """Um trecho de inclinação constante: rampa calçada (até ROAD_RAMP) ou lances de degraus iguais com patamares.
    Acrescenta (s, altura do piso, tipo do intervalo que começa em s) em 'out'."""
    length, drop = s1 - s0, h1 - h0
    if abs(drop) <= ROAD_RAMP * length:
        n = max(1, int(round(length)))
        for k in range(n):
            out.append((s0 + length * k / n, h0 + drop * k / n, 'r'))
        return
    edge = min(ROAD_SECTION_END, length * 0.12)
    avail = length - 2.0 * edge
    steps = max(2, int(round(abs(drop) / ROAD_STEP_RISE)))
    rise = drop / steps
    # Lances de no máximo ROAD_FLIGHT degraus; num trecho comprido e suave, lances curtos a cada ~ROAD_FLIGHT_EVERY m
    flights = max(1, math.ceil(steps / ROAD_FLIGHT), min(steps // 3, int(length // ROAD_FLIGHT_EVERY)))
    per = [steps // flights + (1 if k < steps % flights else 0) for k in range(flights)]
    run = ROAD_STEP_RUN
    if flights > 1 and avail - steps * run < (flights - 1) * ROAD_LANDING:
        run = max(0.28, (avail - (flights - 1) * ROAD_LANDING) / steps)
    if steps * run > avail:
        run = max(0.26, avail / steps)
    landing = (avail - steps * run) / (flights - 1) if flights > 1 else 0.0
    lead = edge if flights > 1 else edge + max(0.0, avail - steps * run) * 0.5
    s, h = s0, h0
    out.append((s, h, 'l'))
    s += lead
    for k, count in enumerate(per):
        for _ in range(count):
            h += rise
            out.append((s, h, 's'))
            s += run
        if k < flights - 1:
            out.append((s, h, 'l'))
            s += landing
    if s < s1 - 0.05:
        out.append((s, h, 'l'))


def road_frame(world, road, reach):
    """Para cada vértice (mundo do Blender) perto da linha: índice da amostra mais perta e afastamento lateral (planta).
    Devolve (máscara dos vértices tratados, índices, afastamento)."""
    import numpy as np
    from mathutils.kdtree import KDTree
    pos, tan = road['pos'], road['tan']
    e, n = -world[:, 0], -world[:, 1]
    xs, ys = [p.x for p in pos], [p.y for p in pos]
    near = (e > min(xs) - reach) & (e < max(xs) + reach) & (n > min(ys) - reach) & (n < max(ys) + reach)
    tree = KDTree(len(pos))
    for i, p in enumerate(pos):
        tree.insert((p.x, p.y, 0.0), i)
    tree.balance()
    cand = np.nonzero(near)[0]
    index = np.zeros(len(cand), dtype=np.int64)
    offset = np.full(len(cand), 1e9)
    for k, v in enumerate(cand):
        co, i, dist = tree.find((e[v], n[v], 0.0))
        if dist > reach:
            continue
        t, p = tan[i], pos[i]
        de, dn = e[v] - p.x, n[v] - p.y
        if (i == 0 or i == len(pos) - 1) and abs(de * t.x + dn * t.y) > 0.3:
            continue                            # além das pontas
        index[k] = i
        offset[k] = de * t.y - dn * t.x         # > 0 à direita
    keep = offset < 1e8
    return cand[keep], index[keep], offset[keep]


def design_road(parts, others=()):
    """Projeta o caminho e prepara a rocha em volta dele, antes do remesh:
    - linha suave pelos pontos marcados (só como guia), com as quinas em arcos de ROAD_FILLET;
    - perfil pelo relevo das colunas, suavizado e sem voltar a subir na descida (a subida da fenda é uma escadaria reta
      e uniforme do primeiro ponto até o alto); trechos parecidos são juntados; cada trecho vira rampa ou lances;
    - colunas de apoio embaixo do piso (rampa de rocha em degraus) menos nos vãos grandes, que ficam com ponte;
    - paredes da fenda: colunas dos dois lados da subida, perto da escadaria;
    - buracos e fendas fundas perto do caminho e das vilas tapados com colunas um pouco abaixo da rocha em volta;
    - na descida, as colunas à esquerda (lado da cidade) que passam do piso são refeitas abaixo dele.
    O corredor (carve_corridor) é tirado depois, para todos os caminhos juntos."""
    rnd = random.Random(4111)
    cover = Cover([COL_OF[o.name] for o in parts if o.name in COL_OF])
    tc, tr = V['Terraco']
    first = Vector(ROAD_POINTS[0])
    start = tc.xy + (first - tc.xy).normalized() * tr * 0.95
    ec, er = V['Escalonada']
    last = Vector(ROAD_POINTS[-1])
    end = ec.xy + (last - ec.xy).normalized() * er * 0.98
    ctrl = [start] + [Vector(p) for p in ROAD_POINTS] + [end]
    pos, tan = rounded_line(ctrl, ROAD_FILLET, 0.25)
    count = len(pos)
    s_of = [min(range(count), key=lambda i: (pos[i] - c).length_squared) * 0.25 for c in ctrl]
    s_of[0], s_of[-1] = 0.0, (count - 1) * 0.25

    # Relevo na linha (o mais alto de três pontos na largura do piso)
    ground = []
    for p, t in zip(pos, tan):
        side = Vector((t.y, -t.x))
        ground.append(max(cover.top(*(p + side * o)) for o in (-1.6, 0.0, 1.6)))

    def robust(s):
        window = [ground[i] for i in range(max(0, int((s - 10) / 0.25)), min(count, int((s + 10) / 0.25) + 1))]
        top = max(window)
        good = sorted(v for v in window if v > top - 10.0)
        return good[len(good) // 2] if top > -500 else None

    heights = [robust(s) for s in s_of]
    heights[0], heights[-1] = V['Terraco'][0].z, V['Escalonada'][0].z
    for k in range(len(heights)):
        if heights[k] is None:
            heights[k] = heights[k - 1]
    summit = max(range(1, len(ctrl) - 1), key=lambda k: heights[k])
    # Subida: do terraço ao primeiro ponto e daí, reta, até o alto; descida sem voltar a subir
    climb = pava([heights[0], max(heights[0], heights[1]), heights[summit]], True)   # sai do terraço na altura dele
    # Descida: o perfil mais esticado (inclinação o mais uniforme possível) que fica a até ROAD_PULL do relevo em cada
    # ponto (relaxação: cada ponto vai para a reta dos vizinhos, preso à faixa), sem voltar a subir
    relief = pava(heights[summit:], False)
    down = list(relief)
    ss = s_of[summit:]
    for _ in range(400):
        for k in range(1, len(down) - 1):
            t = (ss[k] - ss[k - 1]) / (ss[k + 1] - ss[k - 1])
            down[k] = max(relief[k] - ROAD_PULL, min(relief[k] + ROAD_PULL, down[k - 1] + (down[k + 1] - down[k - 1]) * t))
    down = pava(down, False)
    print('[Vilas] alturas: relevo %s; piso %s' % (' '.join('%.0f' % h for h in heights),
                                                   ' '.join('%.0f' % h for h in climb + down[1:])))
    breaks = [(s_of[0], climb[0]), (s_of[1], climb[1])]
    if summit > 1:
        breaks.append((s_of[summit], climb[2]))
    breaks += [(s_of[summit + k], down[k]) for k in range(1, len(down))]
    first_free = len(breaks) - len(down) + 1    # daqui em diante (descida) os trechos podem ser juntados
    # Junta trechos vizinhos de inclinação parecida (a escadaria da subida fica de um trecho só)
    while True:
        best, at = ROAD_MERGE, None
        for k in range(first_free, len(breaks) - 1):
            (sa, ha), (sb, hb), (sc, hc) = breaks[k - 1], breaks[k], breaks[k + 1]
            diff = abs((hb - ha) / (sb - sa) - (hc - hb) / (sc - sb))
            if diff < best:
                best, at = diff, k
        if at is None:
            break
        breaks.pop(at)
    print('[Vilas] caminho: %.0f m, trechos %s' % (s_of[-1], ', '.join(
        '%.0f m %+.1f' % (b[0] - a[0], b[1] - a[1]) for a, b in zip(breaks, breaks[1:]))))

    cross = []
    for (sa, ha), (sb, hb) in zip(breaks, breaks[1:]):
        lay_section(sa, ha, sb, hb, cross)
    cross.append((s_of[-1], breaks[-1][1], 'l'))

    def deck_at(s):
        h = cross[0][1]
        for cs, ch, kind in cross:
            if cs > s + 1e-6:
                break
            h = ch
        return h

    deck = [deck_at(i * 0.25) for i in range(count)]
    low = [min(deck[max(0, i - 12):i + 13]) for i in range(count)]
    high = [max(deck[max(0, i - 12):i + 13]) for i in range(count)]

    # Vãos grandes (rocha mais de 10 m abaixo do piso por 10 m ou mais) ficam com ponte
    gaps, i = [], 0
    while i < count:
        if ground[i] < deck[i] - 10.0:
            j = i
            while j + 1 < count and ground[j + 1] < deck[j + 1] - 10.0:
                j += 1
            if (j - i) * 0.25 >= 10.0:
                gaps.append((i * 0.25, j * 0.25))
            i = j + 1
        else:
            i += 1
    print('[Vilas] vãos com ponte:', ', '.join('%.0f a %.0f m' % g for g in gaps))

    def in_gap(s, pad):
        return any(a - pad < s < b + pad for a, b in gaps)

    added = []
    fissure = set()

    def place(e, n, top, r, base=1.25, bed=(4.0, 8.0)):
        obj = column(e, n, top, r, rnd, base=base, bed=bed)
        parts.append(obj)
        cover.add(COL_OF[obj.name])
        added.append(obj)
        return obj

    # Rampa de rocha embaixo do piso (e ombros mais baixos dos lados)
    s = 0.0
    while s < s_of[-1]:
        i = int(s / 0.25)
        r = rnd.uniform(4.2, 5.2)
        if ground[i] < deck[i] - 1.6 and not in_gap(s, r + 1.0) and s > 2.0:
            p, t = pos[i], tan[i]
            place(p.x, p.y, low[i] - 1.5, r)
            side = Vector((t.y, -t.x))
            for sign in (-1.0, 1.0):
                if rnd.random() < 0.8:
                    q = p + side * sign * rnd.uniform(6.5, 8.5)
                    place(q.x, q.y, low[i] - rnd.uniform(2.5, 6.0), rnd.uniform(3.8, 5.0))
        s += rnd.uniform(4.0, 5.0)

    # Paredes da fenda: dos dois lados da escadaria da subida
    s = s_of[1]
    while s < s_of[summit]:
        i = int(s / 0.25)
        p, t = pos[i], tan[i]
        side = Vector((t.y, -t.x))
        for sign in (-1.0, 1.0):
            r = rnd.uniform(3.5, 5.0)
            q = p + side * sign * (ROAD_HARD + ROAD_CLEAR + 1.0 + r * 0.8)
            if not in_gap(s, r + 2.0) and cover.top(q.x, q.y) < high[i] + 6.0:
                fissure.add(place(q.x, q.y, high[i] + rnd.uniform(9.0, 20.0), r, base=1.3, bed=(5.0, 11.0)).name)
        s += rnd.uniform(5.0, 6.5)

    # Buracos e fendas fundas perto do caminho (até ROAD_FILL_NEAR m): onde em 6 das 8 direções há rocha
    # mais de 8 m acima, uma coluna tapa até um pouco abaixo da rocha em volta
    xs = [p.x for p in pos]
    ys = [p.y for p in pos]
    dirs = [Vector((math.cos(k * math.pi / 4), math.sin(k * math.pi / 4))) for k in range(8)]
    samples = pos[::8]
    filled = []
    e = min(xs) - ROAD_FILL_NEAR
    while e < max(xs) + ROAD_FILL_NEAR:
        n = min(ys) - ROAD_FILL_NEAR
        while n < max(ys) + ROAD_FILL_NEAR:
            q = Vector((e, n))
            near_road = min((q - p).length for p in samples)
            near_village = min((q - c.xy).length - r for c, r in V.values())
            ok = near_road < ROAD_FILL_NEAR and near_road > ROAD_HARD + ROAD_CLEAR + 1.0
            ok = ok and near_village > 3.0 and (q - tc.xy).length > tr + 8.0 and not inside(GORGE, e, n)
            ok = ok and not (255.0 < e < 365.0 and 385.0 < n < 470.0)          # zigue-zague da Pendurada
            if ok:
                for a, b in gaps:
                    pa, pb = pos[int(a / 0.25)], pos[int(b / 0.25)]
                    d = pb - pa
                    t = max(0.0, min(1.0, (q - pa).dot(d) / max(1e-6, d.length_squared)))
                    if (q - (pa + d * t)).length < 12.0:
                        ok = False
            if ok and all((q - f).length > 6.0 for f in filled):
                h = cover.top(e, n)
                walls = []
                for d in dirs:
                    best = max(cover.top(*(q + d * k)) for k in (3.0, 6.0, 9.0, 12.0, 15.0))
                    if best > h + 8.0:
                        walls.append(best)
                if len(walls) >= 6:
                    walls.sort()
                    place(e, n, walls[len(walls) // 2] - rnd.uniform(1.5, 3.0), rnd.uniform(4.0, 5.2), base=1.2,
                          bed=(5.0, 10.0))
                    filled.append(q)
            n += 3.0
        e += 3.0
    print('[Vilas] colunas novas: %d (buracos tapados %d)' % (len(added), len(filled)))

    # Vista da descida: colunas à esquerda que passam do piso refeitas abaixo dele, mais baixas quanto mais longe
    from mathutils.kdtree import KDTree
    tree = KDTree(count)
    for k, p in enumerate(pos):
        tree.insert((p.x, p.y, 0.0), k)
    tree.balance()
    s_from, s_to = s_of[summit] + VIEW_START, s_of[-1] - VIEW_END
    lines = [(Vector((a.x, a.y)), Vector((b.x, b.y))) for a, b, _, _ in others]

    def protected(q, r):
        for a, b in lines:
            d = b - a
            t = max(0.0, min(1.0, (q - a).dot(d) / max(1e-6, d.length_squared)))
            if (q - (a + d * t)).length < r + VIEW_PROTECT:
                return True
        return any((q - c.xy).length < cr + r + VIEW_PROTECT for c, cr in V.values())

    vrnd = random.Random(5123)
    lowered = 0
    for obj in list(parts):
        info = COL_OF.get(obj.name)
        if info is None or obj.name in fissure:
            continue
        e, n, top, r = info
        q = Vector((e, n))
        _, i, _ = tree.find((e, n, 0.0))
        si = i * 0.25
        if not (s_from < si < s_to):
            continue
        t = tan[i]
        offset = (q - pos[i]).dot(Vector((t.y, -t.x)))       # > 0 à direita
        edge = -offset - r * 0.85                           # beira da coluna do lado da cidade
        # à esquerda, ou atravessada pelo caminho com uma parte do lado da cidade (as grandes do campo das cristas)
        if offset - r * 0.85 > -ROAD_HARD or offset > r * 0.3 or edge > VIEW_REACH - ROAD_HARD:
            continue
        new_top = low[i] - vrnd.uniform(*VIEW_DROP) - max(0.0, edge) * VIEW_SLOPE
        if top <= new_top + 1.0 or protected(q, r):
            continue
        base, bed = COL_SHAPE[obj.name]
        parts.remove(obj)
        del COL_OF[obj.name]
        bpy.data.objects.remove(obj, do_unlink=True)
        parts.append(column(e, n, new_top, r, vrnd, base=base, bed=bed))
        lowered += 1
    # Onde a coluna baixada era o chão do caminho, volta a rampa de rocha embaixo do piso (menos nos vãos)
    cover = Cover([COL_OF[o.name] for o in parts if o.name in COL_OF])
    support_added = 0
    s = s_from
    while s < s_to:
        i = int(s / 0.25)
        p, t = pos[i], tan[i]
        side = Vector((t.y, -t.x))
        g = max(cover.top(*(p + side * o)) for o in (-1.6, 0.0, 1.6))
        r = vrnd.uniform(4.2, 5.2)
        if g < deck[i] - 1.6 and not in_gap(s, r + 1.0):
            obj = column(p.x, p.y, low[i] - 1.5, r, vrnd, base=1.25, bed=(4.0, 8.0))
            parts.append(obj)
            cover.add(COL_OF[obj.name])
            support_added += 1
        s += vrnd.uniform(4.0, 5.0)
    print('[Vilas] vista da descida: %d colunas baixadas, %d apoios novos' % (lowered, support_added))

    return {'name': 'Terraco_Escalonada', 'from': 'Terraco', 'to': 'Escalonada', 'pos': pos, 'tan': tan, 'deck': deck,
            'low': low, 'high': high, 'cross': cross, 'gaps': gaps, 'ground': ground}


def design_paved(parts, spec, seed, avoid=(), half=ROAD_HALF):
    """Um caminho pavimentado entre duas vilas, como o do Terraço (antes do remesh): linha suave pelos pontos, com as
    quinas em arcos; perfil pelas alturas da planta (das pontas das vilas e dos pontos do meio), cada trecho uma rampa ou
    lances iguais, trechos vizinhos de inclinação parecida juntados; colunas de apoio embaixo do piso menos nos vãos
    grandes (ponte); buracos e fendas fundas perto tapados (menos a até PAVED_AVOID das amostras em 'avoid')."""
    name, start, mids, end, fixed = spec
    rnd = random.Random(seed)
    cover = Cover([COL_OF[o.name] for o in parts if o.name in COL_OF])

    # Cada ponta é uma vila (nome) ou um ponto fixo ((L, N), altura, nome), como a plataforma do fim do zigue-zague
    def tip(x, toward):
        if isinstance(x, str):
            return edge_point(x, toward), V[x][0].z, x
        return Vector(x[0]), x[1], x[2]
    first = mids[0] if mids else (tuple(V[end][0]) if isinstance(end, str) else end[0])
    last = mids[-1] if mids else (tuple(V[start][0]) if isinstance(start, str) else start[0])
    (a, ha, start), (b, hb, end) = tip(start, first), tip(end, last)
    ctrl = [a] + [Vector((m[0], m[1])) for m in mids] + [b]
    heights = [ha] + [m[2] for m in mids] + [hb]
    pos, tan = rounded_line(ctrl, ROAD_FILLET, 0.25)
    count = len(pos)
    s_of = [min(range(count), key=lambda i: (pos[i] - c).length_squared) * 0.25 for c in ctrl]
    s_of[0], s_of[-1] = 0.0, (count - 1) * 0.25
    for k in range(1, len(s_of)):
        s_of[k] = max(s_of[k], s_of[k - 1] + 0.5)
    ground = []
    for p, t in zip(pos, tan):
        side = Vector((t.y, -t.x))
        ground.append(max(cover.top(*(p + side * o)) for o in (-1.6, 0.0, 1.6)))

    # Sem alturas fixas, o perfil mais esticado (inclinação o mais uniforme possível) a até ROAD_PULL das alturas da
    # planta, sempre descendo
    if not fixed and len(heights) > 2:
        guide = list(heights)
        for _ in range(400):
            for k in range(1, len(heights) - 1):
                t = (s_of[k] - s_of[k - 1]) / (s_of[k + 1] - s_of[k - 1])
                heights[k] = max(guide[k] - ROAD_PULL, min(guide[k] + ROAD_PULL,
                                                            heights[k - 1] + (heights[k + 1] - heights[k - 1]) * t))
        heights = pava(heights, heights[-1] > heights[0])
    breaks = list(zip(s_of, heights))
    while True:
        best, at = ROAD_MERGE, None
        for k in range(1, len(breaks) - 1):
            (sa, ha), (sb, hb), (sc, hc) = breaks[k - 1], breaks[k], breaks[k + 1]
            diff = abs((hb - ha) / (sb - sa) - (hc - hb) / (sc - sb))
            if diff < best:
                best, at = diff, k
        if at is None:
            break
        breaks.pop(at)
    print('[Vilas] %s: %.0f m, trechos %s' % (name, s_of[-1], ', '.join(
        '%.0f m %+.1f' % (b[0] - a[0], b[1] - a[1]) for a, b in zip(breaks, breaks[1:]))))
    cross = []
    for (sa, ha), (sb, hb) in zip(breaks, breaks[1:]):
        lay_section(sa, ha, sb, hb, cross)
    cross.append((s_of[-1], breaks[-1][1], 'l'))

    def deck_at(s):
        h = cross[0][1]
        for cs, ch, kind in cross:
            if cs > s + 1e-6:
                break
            h = ch
        return h

    deck = [deck_at(i * 0.25) for i in range(count)]
    low = [min(deck[max(0, i - 12):i + 13]) for i in range(count)]
    high = [max(deck[max(0, i - 12):i + 13]) for i in range(count)]

    gaps, i = [], 0
    while i < count:
        if ground[i] < deck[i] - 10.0:
            j = i
            while j + 1 < count and ground[j + 1] < deck[j + 1] - 10.0:
                j += 1
            if (j - i) * 0.25 >= 10.0:
                gaps.append((i * 0.25, j * 0.25))
            i = j + 1
        else:
            i += 1

    def in_gap(s, pad):
        return any(a - pad < s < b + pad for a, b in gaps)

    added = []

    def place(e, n, top, r, base=1.25, bed=(4.0, 8.0)):
        obj = column(e, n, top, r, rnd, base=base, bed=bed)
        parts.append(obj)
        cover.add(COL_OF[obj.name])
        added.append(obj)
        return obj

    # Rampa de rocha embaixo do piso (e ombros mais baixos dos lados)
    s = 0.0
    while s < s_of[-1]:
        i = int(s / 0.25)
        r = rnd.uniform(4.2, 5.2)
        if ground[i] < deck[i] - 1.6 and not in_gap(s, r + 1.0) and s > 2.0:
            p, t = pos[i], tan[i]
            place(p.x, p.y, low[i] - 1.5, r)
            side = Vector((t.y, -t.x))
            for sign in (-1.0, 1.0):
                if rnd.random() < 0.8:
                    q = p + side * sign * rnd.uniform(6.5, 8.5)
                    place(q.x, q.y, low[i] - rnd.uniform(2.5, 6.0), rnd.uniform(3.8, 5.0))
        s += rnd.uniform(4.0, 5.0)

    # Buracos e fendas fundas perto do caminho: onde em 6 das 8 direções há rocha mais de 8 m acima, uma coluna tapa
    # até um pouco abaixo da rocha em volta
    xs, ys = [p.x for p in pos], [p.y for p in pos]
    dirs = [Vector((math.cos(k * math.pi / 4), math.sin(k * math.pi / 4))) for k in range(8)]
    samples = pos[::8]
    tc, tr = V['Terraco']
    filled = []
    e = min(xs) - ROAD_FILL_NEAR
    while e < max(xs) + ROAD_FILL_NEAR:
        n = min(ys) - ROAD_FILL_NEAR
        while n < max(ys) + ROAD_FILL_NEAR:
            q = Vector((e, n))
            near_road = min((q - p).length for p in samples)
            ok = ROAD_HARD + ROAD_CLEAR + 1.0 < near_road < ROAD_FILL_NEAR
            ok = ok and min((q - c.xy).length - r for c, r in V.values()) > 3.0 and (q - tc.xy).length > tr + 8.0
            ok = ok and not inside(GORGE, e, n) and not (255.0 < e < 365.0 and 385.0 < n < 470.0)
            ok = ok and all((q - a).length > PAVED_AVOID for a in avoid)
            if ok:
                for a, b in gaps:
                    pa, pb = pos[int(a / 0.25)], pos[int(b / 0.25)]
                    d = pb - pa
                    t = max(0.0, min(1.0, (q - pa).dot(d) / max(1e-6, d.length_squared)))
                    if (q - (pa + d * t)).length < 12.0:
                        ok = False
            if ok and all((q - f).length > 6.0 for f in filled):
                h = cover.top(e, n)
                walls = []
                if h < -500.0:          # fora de qualquer coluna (o vale): não é buraco
                    dirs_here = []
                else:
                    dirs_here = dirs
                for d in dirs_here:
                    best = max(cover.top(*(q + d * k)) for k in (3.0, 6.0, 9.0, 12.0, 15.0))
                    if best > h + 8.0:
                        walls.append(best)
                if len(walls) >= 6:
                    walls.sort()
                    place(e, n, walls[len(walls) // 2] - rnd.uniform(1.5, 3.0), rnd.uniform(4.0, 5.2), base=1.2,
                          bed=(5.0, 10.0))
                    filled.append(q)
            n += 3.0
        e += 3.0
    print('[Vilas] %s: colunas novas %d (buracos tapados %d), vãos com ponte: %s' % (
        name, len(added), len(filled), ', '.join('%.0f a %.0f m' % g for g in gaps) or 'nenhum'))
    return {'name': name, 'from': start, 'to': end, 'pos': pos, 'tan': tan, 'deck': deck, 'low': low, 'high': high,
            'cross': cross, 'gaps': gaps, 'ground': ground, 'half': half}


def road_hard(road):
    """Do eixo até onde a rocha não pode ficar (ROAD_HARD nos caminhos de 4 m; mais nos largos)."""
    return ROAD_HARD + road.get('half', ROAD_HALF) - ROAD_HALF


def corridor_piece(road, i0, i1):
    """Sólido do corredor entre as amostras i0 e i1: do piso (1,2 m abaixo) até o céu, com a largura variando por ruído
    (as paredes ficam irregulares, um pouco abertas para cima)."""
    from mathutils import noise
    pos, tan, low = road['pos'], road['tan'], road['low']
    levels = [0.0, 1.5, 4.0, 8.0, 13.0, 20.0, 30.0, 45.0]
    bm = bmesh.new()
    rings = []
    for i in list(range(i0, i1, 6)) + [i1]:
        p, t = pos[i], tan[i]
        side = Vector((t.y, -t.x))
        s = i * 0.25
        zb = low[i] - 1.2
        ring = []
        for sign in (-1.0, 1.0):
            hs = levels + [ROAD_SKY - zb]
            if sign > 0:
                hs = hs[::-1]
            for dz in hs:
                wiggle = 0.3 + 1.2 * (noise.noise(Vector((s * 0.07, min(dz, 45.0) * 0.09, sign * 7.3))) + 1.0) * 0.5
                w = road_hard(road) + ROAD_CLEAR + wiggle + 0.05 * min(dz, 30.0)
                q = p + side * sign * w
                ring.append(bm.verts.new(B((q.x, q.y, zb + dz))))
        rings.append(ring)
    m = len(rings[0])
    for a, b in zip(rings, rings[1:]):
        for k in range(m):
            bm.faces.new((a[k], a[(k + 1) % m], b[(k + 1) % m], b[k]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new('CUT_Corredor')
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new('CUT_Corredor', mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def carve_corridor(parts, road):
    """Tira o corredor das colunas que entram nele (antes do remesh): pedaços de 30 m do corredor, corte exato em cada
    coluna perto; a coluna que fica toda dentro some."""
    pos, high = road['pos'], road['high']
    count = len(pos)
    pieces = []
    for i0 in range(0, count - 1, 120):
        i1 = min(count - 1, i0 + 126)
        obj = corridor_piece(road, i0, i1)
        xs = [p.x for p in pos[i0:i1 + 1]]
        ys = [p.y for p in pos[i0:i1 + 1]]
        pieces.append((obj, min(xs) - 12, max(xs) + 12, min(ys) - 12, max(ys) + 12, min(road['low'][i0:i1 + 1]) - 2))
    removed = carved = 0
    bpy.context.view_layer.objects.active = None
    for obj in list(parts):
        info = COL_OF.get(obj.name)
        if info is None:
            continue
        e, n, top, r = info
        hits = [p for p in pieces if p[1] - r < e < p[2] + r and p[3] - r < n < p[4] + r and top > p[5]]
        if not hits:
            continue
        before = len(obj.data.polygons)
        for piece in hits:
            mod = obj.modifiers.new('corredor', 'BOOLEAN')
            mod.operation, mod.solver, mod.object = 'DIFFERENCE', 'EXACT', piece[0]
            mod.use_self, mod.use_hole_tolerant = True, True
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.modifier_apply(modifier=mod.name)
        if len(obj.data.polygons) == 0:
            parts.remove(obj)
            bpy.data.objects.remove(obj, do_unlink=True)
            removed += 1
        elif len(obj.data.polygons) != before:
            carved += 1
    for piece in pieces:
        bpy.data.objects.remove(piece[0], do_unlink=True)
    print('[Vilas] corredor: %d colunas cortadas, %d tiradas' % (carved, removed))


def clear_road(obj, road):
    """Garantia depois da escultura e da redução: nada de rocha no piso (fica 0,8 m abaixo do mais baixo dos degraus em
    volta) nem a menos de ROAD_HARD do eixo acima dele (a folga abre 5 cm por metro de altura, até 40 m acima). Antes,
    as arestas compridas perto do caminho são divididas (a redução deixava triângulos grandes passando por cima do piso,
    com os vértices todos fora da faixa)."""
    import numpy as np
    mesh = obj.data
    hard = road_hard(road)
    for _ in range(5):
        world = world_coords(obj)
        cand, index, offset = road_frame(world, road, hard + 4.0)
        z = world[cand, 2]
        near = (np.abs(offset) < hard + 3.0) & (z > np.array(road['low'])[index] - 2.0) &                (z < np.array(road['high'])[index] + 40.0)
        marked = np.zeros(len(mesh.vertices), dtype=bool)
        marked[cand[near]] = True
        bm = bmesh.new()
        bm.from_mesh(mesh)
        long = [e for e in bm.edges if (marked[e.verts[0].index] or marked[e.verts[1].index]) and e.calc_length() > 1.2]
        if long:
            bmesh.ops.subdivide_edges(bm, edges=long, cuts=1, use_grid_fill=True)
            bm.to_mesh(mesh)
        bm.free()
        if not long:
            break
    mesh.update()

    world = world_coords(obj)
    cand, index, offset = road_frame(world, road, hard + 3.0)
    low = np.array(road['low'])[index]
    high = np.array(road['high'])[index]
    z = world[cand, 2]
    limit = hard + np.clip(z - (high + 1.6), 0.0, None) * 0.05
    floor = (np.abs(offset) < hard) & (z > low - 0.8) & (z < high + 1.6)
    wall = (np.abs(offset) < limit) & (z >= high + 1.6) & (z < high + 40.0)
    m = np.array(obj.matrix_world)
    inv = np.linalg.inv(m)
    tan = road['tan']
    moved = 0
    for k in np.nonzero(floor | wall)[0]:
        v = cand[k]
        p = world[v].copy()
        if floor[k]:
            p[2] = low[k] - 0.8
        else:
            t = tan[index[k]]
            push = (limit[k] - abs(offset[k])) * (1.0 if offset[k] >= 0 else -1.0)
            p[0] -= t.y * push                  # Blender x = -L
            p[1] += t.x * push                  # Blender y = -N
        mesh.vertices[v].co = Vector((inv[:3, :3] @ p + inv[:3, 3]))
        moved += 1
    mesh.update()
    print('[Vilas] caminho livre:', obj.name, moved, 'vértices, malha', len(mesh.polygons))


def despike(obj, roads):
    """Abaixa as lâminas e pontas finas de rocha perto dos caminhos (acima do piso) até o chão em volta delas. A altura
    do topo da rocha vem de raios para baixo numa grade de SPIKE_CELL (não depende de quantos vértices a malha tem);
    na grade, abertura morfológica: o mínimo num disco de SPIKE_ERODE e depois o máximo num disco de SPIKE_DILATE. A
    rocha mais estreita que ~2 x SPIKE_ERODE some; beiras de penhasco e paredes largas ficam."""
    import numpy as np
    from mathutils.bvhtree import BVHTree
    from mathutils.kdtree import KDTree
    mesh = obj.data
    world = world_coords(obj)
    e, n, z = -world[:, 0], -world[:, 1], world[:, 2]
    samples = [(p, d) for road in roads for p, d in zip(road['pos'][::4], road['deck'][::4])]
    stree = KDTree(len(samples))
    for k, (p, _) in enumerate(samples):
        stree.insert((p.x, p.y, 0.0), k)
    stree.balance()
    # Vértices candidatos: perto do eixo e acima do piso
    cand = []
    for v in range(len(world)):
        _, k, dist = stree.find((e[v], n[v], 0.0))
        if dist < SPIKE_NEAR and z[v] > samples[k][1] + 1.0:
            cand.append((v, samples[k][1]))
    if not cand:
        return
    # Grade de alturas do topo em volta deles
    cell = SPIKE_CELL
    pad = SPIKE_ERODE + SPIKE_DILATE + 1.0
    ce = np.array([e[v] for v, _ in cand])
    cn = np.array([n[v] for v, _ in cand])
    e0, n0 = ce.min() - pad, cn.min() - pad
    w = int((ce.max() + pad - e0) / cell) + 1
    h = int((cn.max() + pad - n0) / cell) + 1
    need = np.zeros((w, h), dtype=bool)
    r = int(math.ceil(pad / cell))
    for a, b in zip(((ce - e0) / cell).astype(int), ((cn - n0) / cell).astype(int)):
        need[max(0, a - r):a + r + 1, max(0, b - r):b + r + 1] = True
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.transform(obj.matrix_world)
    bvh = BVHTree.FromBMesh(bm)
    bm.free()
    top = np.full((w, h), -1000.0)
    down = Vector((0.0, 0.0, -1.0))
    for a, b in zip(*np.nonzero(need)):
        hit = bvh.ray_cast(Vector((-(e0 + (a + 0.5) * cell), -(n0 + (b + 0.5) * cell), 1500.0)), down, 3000.0)
        if hit[0] is not None:
            top[a, b] = hit[0].z

    def disk(radius):
        q = int(math.ceil(radius / cell))
        return [(i, j) for i in range(-q, q + 1) for j in range(-q, q + 1) if (i * i + j * j) * cell * cell <= radius ** 2]

    def morph(grid, radius, fn, fill):
        out = np.full_like(grid, fill)
        padded = np.pad(grid, int(math.ceil(radius / cell)), constant_values=fill)
        q = int(math.ceil(radius / cell))
        for i, j in disk(radius):
            fn(out, padded[q + i:q + i + w, q + j:q + j + h], out=out)
        return out
    opened = morph(morph(top, SPIKE_ERODE, np.minimum, 1e9), SPIKE_DILATE, np.maximum, -1e9)
    m = np.array(obj.matrix_world)
    inv = np.linalg.inv(m)
    lowered, deepest = 0, 0.0
    for v, deck in cand:
        a, b = int((e[v] - e0) / cell), int((n[v] - n0) / cell)
        limit = opened[a, b]
        if z[v] > limit + SPIKE_TOLERANCE:
            target = max(limit, deck - 3.0)
            if z[v] - target < SPIKE_TOLERANCE:
                continue
            p = world[v].copy()
            p[2] = target
            mesh.vertices[v].co = Vector((inv[:3, :3] @ p + inv[:3, 3]))
            lowered += 1
            deepest = max(deepest, z[v] - target)
    mesh.update()
    print('[Vilas] lâminas: %s %d vértices abaixados (até %.1f m)' % (obj.name, lowered, deepest))


def corridor_hits(obj, roads, headroom=2.3):
    """Faces da malha que barram quem anda nos caminhos: raios para cima saindo de 5 cm acima do piso (o mais alto dos
    dois pontos vizinhos: a rampa invisível passa pelas quinas), a cada ~0,4 m ao longo e na largura (até 0,3 m da
    beira). Devolve (bmesh, índices das faces, pontos (L, N, A))."""
    from mathutils.bvhtree import BVHTree
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    world = bm.copy()
    world.transform(obj.matrix_world)
    bvh = BVHTree.FromBMesh(world)
    world.free()
    up = Vector((0.0, 0.0, 1.0))
    faces, spots = {}, []
    for road in roads:
        half = road.get('half', ROAD_HALF)
        pos, tan, deck = road['pos'], road['tan'], road['deck']
        offsets = [o * 0.4 for o in range(-int((half - 0.3) / 0.4), int((half - 0.3) / 0.4) + 1)]
        for i in range(len(pos) - 1):
            side = Vector((tan[i].y, -tan[i].x))
            floor = max(deck[i], deck[i + 1])
            steps = max(1, math.ceil((pos[i + 1] - pos[i]).length / 0.4))
            for k in range(steps):
                c = pos[i].lerp(pos[i + 1], k / steps)
                for o in offsets:
                    q = c + side * o
                    hit = bvh.ray_cast(Vector(B((q.x, q.y, floor + 0.05))), up, headroom)
                    if hit[2] is not None:
                        faces[hit[2]] = max(faces.get(hit[2], floor), floor)
                        spots.append((q.x, q.y, floor))
    return bm, faces, spots


def open_corridor(obj, roads, rounds=4, delete=False, headroom=2.3):
    """Nada da rocha pode barrar quem anda nos caminhos (pedido do usuário em 2026-10-31: a colisão da montanha
    barrava em dois trechos do caminho do Terraço; a redução deixa triângulos grandes passando por cima do piso com os
    vértices fora da faixa que o clear_road mexe). A malha é triangulada antes (o Unity divide os quadriláteros tortos
    de outro jeito). Na colisão ('delete') as faces que os raios de corridor_hits acertam saem; na rocha visível os
    vértices delas que ficam abaixo do teto livre sobem até ele (a face vira um beiral acima da cabeça), algumas
    vezes. Sem dividir faces: dividir a cada rodada fazia a malha crescer até estourar a memória (2026-11-01)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    inverse = obj.matrix_world.inverted()
    for k in range(rounds + 1):
        bm, faces, spots = corridor_hits(obj, roads, headroom)
        if not faces:
            bm.free()
            print('[Vilas] corredor livre: %s (rodada %d)' % (obj.name, k), flush=True)
            return
        if delete or k == rounds:
            if delete:
                bmesh.ops.delete(bm, geom=[bm.faces[f] for f in faces], context='FACES')
                bm.to_mesh(obj.data)
                obj.data.update()
            print('[Vilas] corredor: %s barrado em %d pontos (%d faces%s)' % (
                obj.name, len(spots), len(faces), ', tiradas' if delete else ''), flush=True)
            bm.free()
            if delete:
                continue
            return
        lift = {}
        for f, floor in faces.items():
            for v in bm.faces[f].verts:
                lift[v] = max(lift.get(v, -1e9), floor + headroom + 0.3)
        for v, top in lift.items():
            w = obj.matrix_world @ v.co
            if top - headroom - 0.8 < w.z < top:     # só os que estão acima do piso (os de baixo ficam: senão viram pico)
                w.z = top
                v.co = inverse @ w
        print('[Vilas] corredor: %s rodada %d, %d faces, %d vértices erguidos' % (
            obj.name, k, len(faces), len(lift)), flush=True)
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()


def road_from_layout(r):
    """Um caminho lido da planta gravada (para o retoque sobre o FBX pronto)."""
    pts, dirs = r['points'], r['dirs']
    count = len(r['kinds'])
    pos = [Vector((pts[3 * i], pts[3 * i + 2])) for i in range(count)]
    tan = [Vector((dirs[2 * i], dirs[2 * i + 1])).normalized() for i in range(count)]
    deck = [pts[3 * i + 1] for i in range(count)]
    low = [min(deck[max(0, i - 3):i + 4]) for i in range(count)]
    high = [max(deck[max(0, i - 3):i + 4]) for i in range(count)]
    return {'pos': pos, 'tan': tan, 'deck': deck, 'low': low, 'high': high, 'half': r.get('width', ROAD_HALF * 2) * 0.5}


def platform_from_layout(pl):
    """Uma plataforma do zigue-zague lida da planta, como um caminho reto e plano no sentido norte-sul (para o
    retoque)."""
    (e, h, n), (sl, sn) = pl['center'], pl['size']
    a, b = Vector((e, n + sn * 0.5)), Vector((e, n - sn * 0.5))
    count = max(2, int(round(sn)) + 1)
    pos = [a.lerp(b, k / (count - 1)) for k in range(count)]
    return {'pos': pos, 'tan': [Vector((0.0, -1.0))] * count, 'deck': [h] * count, 'low': [h] * count,
            'high': [h] * count, 'half': sl * 0.5}


def road_layout(road):
    """Planta do caminho para o Unity: pontos (L, A, N) de cada mudança do piso e a cada 1 m no máximo (as curvas ficam
    redondas), a direção (L, N) em cada um, o tipo do intervalo que começa nele ('s' degrau, 'l' patamar, 'r' rampa) e
    se fica num vão com ponte (1)."""
    pos, tan = road['pos'], road['tan']
    count = len(pos)

    def at(s, items):
        f = max(0.0, min(count - 1.0, s / 0.25))
        i = min(count - 2, int(f))
        return items[i].lerp(items[i + 1], f - i)

    points, dirs, kinds, gaps = [], [], '', []

    def emit(s, h, kind):
        nonlocal kinds
        p, t = at(s, pos), at(s, tan).normalized()
        points.extend([round(p.x, 3), round(h, 3), round(p.y, 3)])
        dirs.extend([round(t.x, 4), round(t.y, 4)])
        kinds += kind
        gaps.append(1 if any(g0 <= s <= g1 for g0, g1 in road['gaps']) else 0)

    cross = road['cross']
    for (s, h, kind), (s2, h2, _) in zip(cross, cross[1:]):
        n = max(1, math.ceil((s2 - s) / 1.0 - 1e-6))
        for k in range(n):
            t = k / n
            emit(s + (s2 - s) * t, h + (h2 - h) * t if kind == 'r' else h, kind)
    emit(*cross[-1])
    return {'name': road['name'], 'start': road['from'], 'end': road['to'], 'width': road.get('half', ROAD_HALF) * 2.0,
            'points': points,
            'dirs': dirs, 'kinds': kinds, 'gaps': gaps}


def platform_layout(piece):
    """Planta de uma plataforma do zigue-zague para o Unity: centro (L, A, N), tamanho (L, N) e as aberturas nos
    balaústres em trincas (lado 0 N, 1 S, 2 L, 3 O; de; até), na coordenada ao longo do lado."""
    pl = piece['platform']
    (e, n), h, (sl, sn) = pl['center'], pl['height'], pl['size']
    openings = []
    for side, a, b in pl['openings']:
        openings += ['NSEW'.index(side), -1e4 if a is None else a, 1e4 if b is None else b]
    return {'name': piece['name'], 'center': [round(e, 3), round(h, 3), round(n, 3)], 'size': [sl, sn],
            'openings': openings}


def road_segments(road):
    """O caminho como trechos (a, b, escada?, rota), a cada ~8 m, para as zonas de detalhe e o gelo."""
    pos, deck = road['pos'], road['deck']
    picks = list(range(0, len(pos), 32)) + [len(pos) - 1]
    return [(Vector((pos[i].x, pos[i].y, deck[i])), Vector((pos[j].x, pos[j].y, deck[j])), False, road['name'])
            for i, j in zip(picks, picks[1:])]


def remove_fragments(obj, size=None):
    """Tira os pedaços soltos pequenos (cacos que os cortes e a redução deixam no ar)."""
    import numpy as np
    size = size or FRAGMENT_SIZE
    mesh = obj.data
    nv = len(mesh.vertices)
    edges = np.empty(len(mesh.edges) * 2, dtype=np.int64)
    mesh.edges.foreach_get('vertices', edges)
    parent = np.arange(nv)

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    for a, b in edges.reshape(-1, 2):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb
    roots = np.array([find(i) for i in range(nv)])
    _, part = np.unique(roots, return_inverse=True)
    world = world_coords(obj)
    groups = part.max() + 1
    low = np.full((groups, 3), 1e9)
    high = np.full((groups, 3), -1e9)
    np.minimum.at(low, part, world)
    np.maximum.at(high, part, world)
    small = np.linalg.norm(high - low, axis=1) < size
    gone = np.nonzero(small[part])[0]
    drop = np.nonzero(small)[0]
    if len(drop) == 0:
        print('[Vilas] pedaços soltos', obj.name, 0)
        return
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in gone], context='VERTS')
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    print('[Vilas] pedaços soltos', obj.name, len(drop), 'tirados (%d vértices)' % len(gone))


# ---------- cortes ----------

def slab(name, a, b, up_half, down_extent, side, extend, top=600.0):
    """Faixa de corte de a até b (planta), o chão seguindo a altura das pontas: 'up_half' para o lado da montanha e
    'down_extent' para o lado do vale (side = +1 vale à direita)."""
    d = (b.xy - a.xy).normalized()
    right = Vector((d.y, -d.x)) * side
    a2, b2 = a.xy - d * extend, b.xy + d * extend
    corners = [(a2 - right * up_half, a.z), (b2 - right * up_half, b.z), (b2 + right * down_extent, b.z),
               (a2 + right * down_extent, a.z)]
    bottom = [B((p.x, p.y, z)) for p, z in corners]
    return cut.solid(name, bottom, [Vector((p.x, p.y, top)) for p in bottom])


def cutters(flights):
    """Sólidos de corte: o chão de cada vila, os patamares do zigue-zague e a garganta (os caminhos não têm faixa de corte:
    o corredor é tirado das colunas antes do remesh, como o do Terraço)."""
    made = []
    for name, (c, r) in V.items():
        if name == 'Terraco':
            continue                            # a torre fica no ar, sem tocar na montanha
        outline = [(-(c.x + math.cos(k / 32 * math.tau) * (r + 4.0)), -(c.y + math.sin(k / 32 * math.tau) * (r + 4.0)))
                   for k in range(32)]
        made.append(cut.prism('CUT_' + name, outline, c.z - 0.45, 600.0))
    # Patamares do zigue-zague: cada lance corta tudo ao sul da beira de trás dele (o próximo, mais baixo, corta de
    # novo), então ficam degraus de 12 m de fundo; as pontas planas viram as voltas e o lugar das casas
    for k, (a, b) in enumerate(flights):
        west, east = (b, a) if a.x > b.x else (a, b)
        n = a.y
        for p, q, label in [(Vector((TURN_E[0] - 20.0, n, west.z)), west, 'O'), (west, east, 'M'),
                            (east, Vector((TURN_E[1] + 20.0, n, east.z)), 'L')]:
            p = Vector((p.x, n, p.z - 0.6))
            q = Vector((q.x, n, q.z - 0.6))
            made.append(slab('CUT_Patamar_%d%s' % (k, label), p, q, 3.5, 34.0, 1.0, 0.5))   # vale ao sul
    made.append(cut.prism('CUT_Garganta', [(-e, -n) for e, n in GORGE], -400.0, 700.0))
    return made


def footprints(cuts):
    """Planta de cada sólido de corte: (contorno convexo, ponto do fundo, normal do fundo, teto)."""
    result = []
    for c in cuts:
        cw = [c.matrix_world @ v.co for v in c.data.vertices]
        ceiling = max(p.z for p in cw)
        bottom = [p for p in cw if p.z < ceiling - 1e-3]
        ring = convex_hull([(p.x, p.y) for p in bottom])
        normal = None
        for k in range(1, len(bottom) - 1):
            n = (bottom[k] - bottom[0]).cross(bottom[k + 1] - bottom[0])
            if n.length > 1e-3 and abs(n.z) > 1e-3:
                normal = n
                break
        if len(ring) >= 3 and normal is not None:
            result.append((ring, bottom[0], normal, ceiling))
    return result


def world_coords(obj):
    import numpy as np
    mesh = obj.data
    co = np.empty(len(mesh.vertices) * 3, dtype=np.float64)
    mesh.vertices.foreach_get('co', co)
    co = co.reshape(-1, 3)
    m = np.array(obj.matrix_world)
    return co @ m[:3, :3].T + m[:3, 3]


def protected_mask(world, cuts, margin=4.0):
    """Vértices a até 'margin' da planta de um corte e acima de 4 m abaixo do fundo dele (não são suavizados nem
    reduzidos)."""
    import numpy as np
    x, y, z = world[:, 0], world[:, 1], world[:, 2]
    keep = np.zeros(len(world), dtype=bool)
    for ring, a, normal, ceiling in footprints(cuts):
        xs, ys = [q[0] for q in ring], [q[1] for q in ring]
        near = (x > min(xs) - margin) & (x < max(xs) + margin) & (y > min(ys) - margin) & (y < max(ys) + margin)
        floor = a.z - (normal.x * (x - a.x) + normal.y * (y - a.y)) / normal.z
        near &= z > floor - 4.0
        for k in range(len(ring)):
            (x0, y0), (x1, y1) = ring[k], ring[(k + 1) % len(ring)]
            length = math.hypot(x1 - x0, y1 - y0)
            if length > 1e-6:
                near &= ((x1 - x0) * (y - y0) - (y1 - y0) * (x - x0)) / length > -margin
        keep |= near
    return keep


def play_lines(segments, deck, bridge):
    """Onde o jogo passa, na planta: (a, b, folga) de cada trecho, vila (círculo), deque da Vila-Ponte e ponte colossal."""
    lines = [(a.xy, b.xy, 0.0) for a, b, _, _ in segments]
    lines += [(c.xy, c.xy, r) for c, r in V.values()]
    lines += [(deck[0].xy, deck[1].xy, DECK_WIDTH * 0.5), (bridge[0].xy, bridge[1].xy, 12.0)]
    return lines


def zone_ids(world, lines):
    """0 = perto do jogo, 1 = meio, 2 = costas (distância na planta até o caminho, vila ou ponte mais perto)."""
    import numpy as np
    e, n = -world[:, 0], -world[:, 1]
    d = np.full(len(world), 1e9)
    for a, b, pad in lines:
        dx, dy = b.x - a.x, b.y - a.y
        length2 = dx * dx + dy * dy
        t = 0.0 if length2 < 1e-9 else np.clip(((e - a.x) * dx + (n - a.y) * dy) / length2, 0.0, 1.0)
        np.minimum(d, np.hypot(e - (a.x + t * dx), n - (a.y + t * dy)) - pad, out=d)
    return np.where(d < DETAIL_NEAR, 0, np.where(d < DETAIL_MID, 1, 2))


def set_group(obj, name, indices, weight=1.0, clear=True):
    group = obj.vertex_groups.get(name)
    if group and clear:
        obj.vertex_groups.remove(group)
        group = None
    group = group or obj.vertex_groups.new(name=name)
    if len(indices):
        group.add([int(i) for i in indices], weight, 'REPLACE')
    return group


def reduce_where(obj, free, target, label):
    """Reduz só os vértices marcados em 'free' (o resto fica num grupo excluído) até sobrarem ~'target' faces deles."""
    import numpy as np
    mesh = obj.data
    starts = np.empty(len(mesh.polygons), dtype=np.int64)
    mesh.polygons.foreach_get('loop_start', starts)
    loops = np.empty(len(mesh.loops), dtype=np.int64)
    mesh.loops.foreach_get('vertex_index', loops)
    zone_faces = int(free[loops[starts]].sum())
    total = len(mesh.polygons)
    if zone_faces <= target:
        print('[Vilas] redução %s: %d faces, nada a reduzir' % (label, zone_faces))
        return
    set_group(obj, 'Fixo', np.nonzero(~free)[0])
    bpy.context.view_layer.objects.active = obj
    dec = obj.modifiers.new('reduzir', 'DECIMATE')
    dec.ratio = (total - zone_faces + target) / total
    dec.vertex_group, dec.invert_vertex_group = 'Fixo', True
    bpy.ops.object.modifier_apply(modifier=dec.name)
    print('[Vilas] redução %s: %d -> ~%d faces (malha %d -> %d)' % (label, zone_faces, target, total,
                                                                      len(mesh.polygons)))


def finish_protected(obj, cuts, lines):
    """Como o finish das montanhas, mas o que fica perto dos cortes não é suavizado nem reduzido, e a redução vai pela
    distância do jogo (perto, meio, costas). Devolve a cópia leve para a colisão."""
    import numpy as np
    world = world_coords(obj)
    keep = protected_mask(world, cuts)
    zone = zone_ids(world, lines)
    print('[Vilas] vértices protegidos', int(keep.sum()), 'perto', int((zone == 0).sum()), 'meio',
          int((zone == 1).sum()), 'costas', int((zone == 2).sum()))
    # Suavização: forte nas costas, leve perto do jogo (guarda o relevo esculpido)
    set_group(obj, 'Suavizar', np.nonzero((zone == 2) & ~keep)[0], 1.0)
    set_group(obj, 'Suavizar', np.nonzero((zone == 1) & ~keep)[0], 0.6, clear=False)
    set_group(obj, 'Suavizar', np.nonzero((zone == 0) & ~keep)[0], 0.25, clear=False)
    bpy.context.view_layer.objects.active = obj
    smooth = obj.modifiers.new('suavizar', 'SMOOTH')
    smooth.factor, smooth.iterations = 0.5, 3
    smooth.vertex_group = 'Suavizar'
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    for zone_id, target, label in [(2, FACES_FAR, 'costas'), (1, FACES_MID, 'meio'), (0, FACES_NEAR, 'perto')]:
        world = world_coords(obj)
        reduce_where(obj, (zone_ids(world, lines) == zone_id) & ~protected_mask(world, cuts), target, label)
    print('[Vilas] faces depois da redução', len(obj.data.polygons))
    # Colisão: cópia reduzida por igual (o que fica perto dos cortes é o mesmo da malha que aparece)
    collision = obj.copy()
    collision.data = obj.data.copy()
    collision.name = collision.data.name = NAME + '_Colisao'
    bpy.context.collection.objects.link(collision)
    reduce_where(collision, ~protected_mask(world_coords(collision), cuts), FACES_COLLISION, 'colisão')
    collision.hide_render = True
    for o in (obj, collision):
        for g in list(o.vertex_groups):
            o.vertex_groups.remove(g)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35.0))
    except (AttributeError, RuntimeError, TypeError):
        bpy.ops.object.shade_flat()
    kit.relief_colors(obj)
    return collision


def flatten(obj, cuts, remove=True):
    """Depois da redução de polígonos, sem corte booleano (o corte exato na malha reduzida deixava as paredes dos
    sólidos de corte em pé até 600 m): dentro da planta de cada sólido de corte (com 0,6 m de folga das beiras), os
    vértices que a redução deixou acima do fundo dele voltam para o fundo (o plano das pontas de baixo do sólido)."""
    mesh = obj.data
    mw = obj.matrix_world
    inv = mw.inverted()
    points = [mw @ v.co for v in mesh.vertices]
    moved = 0
    for ring, a, normal, ceiling in footprints(cuts):
        xs, ys = [p[0] for p in ring], [p[1] for p in ring]
        for k, p in enumerate(points):
            if not (min(xs) < p.x < max(xs) and min(ys) < p.y < max(ys)):
                continue
            floor = a.z - (normal.x * (p.x - a.x) + normal.y * (p.y - a.y)) / normal.z
            if not (floor + 0.02 < p.z < ceiling) or not inside_by(ring, p.x, p.y, 0.6):
                continue
            p.z = floor
            mesh.vertices[k].co = inv @ p
            moved += 1
    if remove:
        for c in cuts:
            bpy.data.objects.remove(c, do_unlink=True)
    mesh.update()
    print('[Vilas] vértices achatados', obj.name, moved)


def convex_hull(pts):
    pts = sorted(set((round(x, 4), round(y, 4)) for x, y in pts))
    if len(pts) < 3:
        return pts

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


def inside_by(ring, x, y, margin):
    """Dentro do polígono convexo anti-horário e a mais de 'margin' de cada lado."""
    for k in range(len(ring)):
        (x0, y0), (x1, y1) = ring[k], ring[(k + 1) % len(ring)]
        ex, ey = x1 - x0, y1 - y0
        length = math.hypot(ex, ey)
        if length < 1e-6:
            continue
        if (ex * (y - y0) - ey * (x - x0)) / length < margin:
            return False
    return True


# ---------- gelo ----------

def build_ice(rock, segments):
    """Pingentes debaixo das saliências (da nuvem para cima), fora de cima dos caminhos."""
    rnd = random.Random(29)
    bm = bmesh.new()
    faces = [(p.center.copy(), p.normal.copy()) for p in rock.data.polygons
             if p.normal.z < -0.55 and -60.0 < p.center.z < 420.0 and p.area > 0.8]
    rnd.shuffle(faces)
    count = 0
    for centre, _ in faces:
        if count >= 1200:
            break
        e, n = -centre.x, -centre.y
        near = any((Vector((e, n)) - (a.xy + (b.xy - a.xy) * max(0.0, min(1.0, (Vector((e, n)) - a.xy).dot(b.xy - a.xy) /
                   max(1e-3, (b.xy - a.xy).length_squared))))).length < 7.0 for a, b, _, _ in segments)
        if near:
            continue
        length = rnd.uniform(0.8, 5.0)
        cut.cone(bm, centre + Vector((0.0, 0.0, -length)), centre + Vector((0.0, 0.0, 0.15)), rnd.uniform(0.15, 0.6), 5, rnd)
        count += 1
    print('[Vilas] pingentes', count)
    mesh = bpy.data.meshes.new(NAME + '_Gelo')
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(NAME + '_Gelo', mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(bpy.data.materials.get('MAT_Gelo') or bpy.data.materials.new('MAT_Gelo'))
    return obj


# ---------- planta para o Unity ----------

def write_layout(landings, deck, bridge, roads):
    def u(p):
        return [round(p.x, 3), round(p.z, 3), round(p.y, 3)]       # Unity: (L, A, N)

    data = {
        'villages': [{'name': name, 'center': u(c), 'radius': r} for name, (c, r) in V.items()],
        'segments': [],                     # todos os caminhos viraram construções (roads)
        'landings': [{'position': u(p)} for p in landings],
        'deck': {'a': u(deck[0]), 'b': u(deck[1]), 'width': DECK_WIDTH},
        'bridge': {'a': u(bridge[0]), 'b': u(bridge[1]), 'width': 24.0},
        'roads': [road_layout(r) for r in roads if 'platform' not in r],
        'platforms': [platform_layout(r) for r in roads if 'platform' in r],
    }
    path = os.path.join(kit.OUT, NAME + '_Layout.json')
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=1)
    print('[Vilas] planta', path)


# ---------- prévia ----------

def proxies(segments, deck, bridge):
    made = []
    for name, (c, r) in V.items():
        made.append(cut.disc('PX_' + name, -c.x, -c.y, r, c.z - 0.6, c.z, count=24))
    for k, (a, b, stairs, _) in enumerate(segments):
        made.append(strip('PX_Caminho_%d' % k, a, b, 2.0, 0.4))
    made.append(cut.disc('PX_Cidade', 0.0, 0.0, 170.0, -60.0, 30.0, count=48))
    for (a, b), w, name in [(deck, DECK_WIDTH, 'PX_VilaPonte'), (bridge, 24.0, 'PX_PonteColossal')]:
        made.append(strip(name, a, b, w * 0.5, 2.5))
    return made


def strip(name, a, b, half, thick):
    """Faixa de a até b (planta) com o topo na altura das pontas, só para a prévia."""
    d = (b.xy - a.xy).normalized()
    right = Vector((d.y, -d.x)) * half
    top = [(a.xy - right, a.z), (b.xy - right, b.z), (b.xy + right, b.z), (a.xy + right, a.z)]
    return cut.solid(name, [B((p.x, p.y, z - thick)) for p, z in top], [B((p.x, p.y, z)) for p, z in top])


def preview(folder, segments, deck, bridge):
    os.makedirs(folder, exist_ok=True)
    proxies(segments, deck, bridge)
    cut.look_colors()
    scene = bpy.context.scene
    try:
        scene.render.engine = 'BLENDER_WORKBENCH'
    except TypeError:
        pass
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'VERTEX'
    scene.render.resolution_x, scene.render.resolution_y = 1400, 800
    scene.world = scene.world or bpy.data.worlds.new('W')
    cam_data = bpy.data.cameras.new('cam')
    cam_data.clip_end = 6000.0
    cam = bpy.data.objects.new('cam', cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    for name, eye, target, lens in [('terraco', (-480.0, 860.0, 300.0), (-422.0, 790.0, 235.0), 28),
                                    ('subida', (-351.0, 798.0, 236.5), (-235.8, 805.6, 262.0), 24),
                                    ('contorno', (-181.0, 732.0, 242.5), (-246.0, 632.0, 231.0), 24),
                                    ('descida', (-311.0, 643.0, 216.5), (-328.0, 567.0, 181.0), 24),
                                    ('visao_geral', (-150.0, -650.0, 520.0), (0.0, 560.0, 120.0), 24),
                                    ('da_chegada', (-300.0, 528.0, 183.0), (0.0, 0.0, 30.0), 24),
                                    ('da_cidade', (0.0, 120.0, 40.0), (0.0, 600.0, 140.0), 22),
                                    ('dos_sinos', (-125.0, 660.0, 162.0), (60.0, 560.0, 120.0), 24),
                                    ('pendurada', (380.0, 330.0, 90.0), (300.0, 440.0, 70.0), 26),
                                    ('garganta', (190.0, 380.0, 120.0), (195.0, 560.0, 100.0), 26)]:
        cam.location = B(eye)
        cam.rotation_euler = (B(target) - B(eye)).to_track_quat('-Z', 'Y').to_euler()
        cam_data.lens = lens
        scene.render.filepath = os.path.join(folder, name + '.png')
        bpy.ops.render.render(write_still=True)


def retouch():
    """Só o acabamento do caminho sobre o FBX já exportado (sem esculpir de novo): limpa a faixa do caminho e tira os
    pedaços soltos na rocha e na colisão, e exporta de novo."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(kit.OUT, NAME + '.fbx'), use_custom_normals=False, colors_type='LINEAR')
    objects = [bpy.data.objects[n] for n in (NAME, NAME + '_Colisao', NAME + '_Gelo')]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    with open(os.path.join(kit.OUT, NAME + '_Layout.json'), encoding='utf-8') as f:
        data = json.load(f)
    roads = [road_from_layout(r) for r in (data.get('roads') or [data['road']])]
    roads += [platform_from_layout(pl) for pl in data.get('platforms', [])]
    for o in objects[:2]:
        for road in roads:
            clear_road(o, road)
        despike(o, roads)
        open_corridor(o, roads, delete=o is objects[1])
        remove_fragments(o)
    objects[1].hide_render = True
    cut.NAME = NAME
    cut.export(objects)


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    if '--retoque' in argv:
        retouch()
        sys.exit(0)
    segments, flights, landings, deck, bridge = plan()
    kit.clear()
    olds = old_segments()
    parts = build_columns(olds + segments)
    road = design_road(parts, others=segments)
    roads = [road] + [design_paved(parts, spec, 6100 + k, avoid=road['pos'][::8])
                      for k, spec in enumerate(paved_specs())]
    roads += design_switchback(parts, 6900, avoid=road['pos'][::8])
    if '--dry' in argv:                             # só o projeto dos caminhos
        sys.exit(0)
    for r in roads:
        carve_corridor(parts, r)
    if '--corredor' in argv:                        # e o corte do corredor nas colunas
        sys.exit(0)
    rock = kit.fuse(parts, NAME, VOXEL)
    print('[Vilas] remesh', len(rock.data.polygons))
    kit.sculpt(rock, 28, crack_depth=2.6, cell=0.022, ledge_height=13.0, ledge_depth=1.8, lumps=4.0)
    write_layout(landings, deck, bridge, roads)
    cut.carve(rock, cutters(flights))
    # Perto dos cortes a malha não é suavizada nem reduzida (a redução passava triângulos grandes por cima das escadas)
    again = cutters(flights)
    for c in [c for c in again if c.name == 'CUT_Garganta']:
        again.remove(c)
        bpy.data.objects.remove(c, do_unlink=True)
    walked = segments + [q for r in roads for q in road_segments(r)]
    collision = finish_protected(rock, again, play_lines(walked, deck, bridge))
    # O que a redução ainda tiver levantado acima do fundo de algum corte volta para ele
    flatten(rock, again, remove=False)
    flatten(collision, again)
    for o in (rock, collision):
        for r in roads:
            clear_road(o, r)
        despike(o, roads)
        open_corridor(o, roads, delete=o is collision)
        remove_fragments(o)
    material = bpy.data.materials.new('MAT_Rocha')
    for o in (rock, collision):
        o.data.materials.clear()
        o.data.materials.append(material)
    ice = build_ice(rock, walked)
    cut.NAME = NAME
    cut.export([rock, collision, ice])
    if '--preview' in argv:
        preview(argv[argv.index('--preview') + 1], walked, deck, bridge)
