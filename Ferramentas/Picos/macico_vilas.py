# Maciço das Vilas do Alto (Picos acima das Nuvens), esculpido no Blender em segundo plano:
#   blender.exe -b --python Ferramentas/Picos/macico_vilas.py [-- --preview <pasta>]
# Uma montanha grande ao norte da cidade flutuante, no lugar do anel de colunas e das ilhas do primeiro esboço (o
# usuário achou nada natural): cristas com cumes e uma sela, contrafortes descendo para o vale de nuvens, e as vilas
# em lugares naturais dela, cada uma um pouco mais baixa, sempre com a cidade à vista do outro lado do vale:
#   Vila Escalonada (chegada, 180 m, na ponta de um contraforte, com dois terraços mais baixos na frente),
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
FACES = 110000
VOXEL = 2.6

# ---------- planta ----------

# Vilas: nome, (L, N), chão, raio do chão calçado
VILLAGES = [
    ('Escalonada', (-300.0, 545.0), 180.0, 40.0),
    ('Escalonada_T1', (-252.0, 490.0), 172.0, 17.0),
    ('Escalonada_T2', (-300.0, 452.0), 164.0, 16.0),
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

# Vila Pendurada: zigue-zague descendo o paredão ao sul do topo, um lance por patamar (N), do leste para o oeste e de
# volta, com as voltas nas pontas; no fim, a calçada pela crista até o portão
FLIGHT_N = [448.0, 436.0, 424.0, 412.0, 400.0]
FLIGHT_E = (290.0, 330.0)
TURN_E = (280.0, 340.0)
FLIGHT_TOP, FLIGHT_DROP = 98.0, 11.5
GATE_PATH = [(274.0, 368.0, 38.0)]

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
        # Casa na ponta de fora do patamar, do lado oposto da volta, encostada na parede de trás (norte)
        other = TURN_E[1] + 12.0 if west else TURN_E[0] - 12.0
        landings.append(Vector((other, n - 1.5, flights[-1][0].z)))
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


# ---------- colunas ----------

def column(e, n, top, r_top, rnd, base=1.35, bed=(8.0, 16.0)):
    x, y, _ = B((e, n, 0.0))
    return kit.column(x, y, top, r_top, r_top * base, rnd, lean=(rnd.uniform(-2, 2), rnd.uniform(-2, 2)), bed=bed,
                      shift=0.1)


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


def cutters(segments, flights):
    made = []
    for name, (c, r) in V.items():
        outline = [(-(c.x + math.cos(k / 32 * math.tau) * (r + 4.0)), -(c.y + math.sin(k / 32 * math.tau) * (r + 4.0)))
                   for k in range(32)]
        made.append(cut.prism('CUT_' + name, outline, c.z - 0.45, 600.0))
    pendurada = [s for s in segments if s[3] == 'Pendurada']
    for k, (a, b, stairs, route) in enumerate(segments):
        if route == 'Pendurada' and (a, b, stairs, route) not in pendurada[:1]:
            continue                            # o zigue-zague tem cortes próprios (patamares em degraus)
        drop = 0.6 if stairs else 0.15
        half = 4.4 if stairs else 3.0
        made.append(slab('CUT_Caminho_%d' % k, a - Vector((0, 0, drop)), b - Vector((0, 0, drop)), half, 20.0,
                         downhill(a, b), 2.0))
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

def write_layout(segments, landings, deck, bridge):
    def u(p):
        return [round(p.x, 3), round(p.z, 3), round(p.y, 3)]       # Unity: (L, A, N)

    data = {
        'villages': [{'name': name, 'center': u(c), 'radius': r} for name, (c, r) in V.items()],
        'segments': [{'a': u(a), 'b': u(b), 'stairs': stairs, 'route': route,
                      'width': 4.5 if stairs else 4.0} for a, b, stairs, route in segments],
        'landings': [{'position': u(p)} for p in landings],
        'deck': {'a': u(deck[0]), 'b': u(deck[1]), 'width': DECK_WIDTH},
        'bridge': {'a': u(bridge[0]), 'b': u(bridge[1]), 'width': 24.0},
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
    for name, eye, target, lens in [('visao_geral', (-150.0, -650.0, 520.0), (0.0, 560.0, 120.0), 24),
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


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    segments, flights, landings, deck, bridge = plan()
    for a, b, stairs, route in segments:
        print('[Vilas] trecho %-24s %s %5.1f m  inclinação %.2f' % (route, 'escada ' if stairs else 'calçada',
              (b.xy - a.xy).length, abs(b.z - a.z) / max(0.1, (b.xy - a.xy).length)))
    write_layout(segments, landings, deck, bridge)
    kit.clear()
    rock = kit.fuse(build_columns(segments), NAME, VOXEL)
    print('[Vilas] remesh', len(rock.data.polygons))
    kit.sculpt(rock, 28, crack_depth=2.6, cell=0.022, ledge_height=13.0, ledge_depth=1.8, lumps=4.0)
    cut.carve(rock, cutters(segments, flights))
    kit.finish(rock, FACES)
    rock.data.materials.clear()
    rock.data.materials.append(bpy.data.materials.new('MAT_Rocha'))
    ice = build_ice(rock, segments)
    cut.NAME = NAME
    cut.export([rock, ice])
    if '--preview' in argv:
        preview(argv[argv.index('--preview') + 1], segments, deck, bridge)
