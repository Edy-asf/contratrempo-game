# Maciço das Vilas do Alto, versão 2 (pedido do usuário em 2026-11-01): a montanha antiga (macico_vilas.py) era
# esculpida livre e depois cortada para os caminhos (booleanos, corredor tirado das colunas, limpeza da faixa, lâminas
# abaixadas, pedaços soltos tirados, corredor aberto), e cada conserto deixava um defeito novo: pedaços flutuando,
# lâminas finas, buracos, cortes retos, rocha atravessando o caminho e formas sem sentido. Aqui a montanha nasce em volta
# do que já existe: as vilas, a torre do portal, os caminhos, as plataformas do zigue-zague e as pontes ficam onde estão
# (as plantas vêm do macico_vilas.py), e cada coluna de arenito já é feita respeitando o espaço do jogo:
#   - embaixo de cada caminho e vila, a rocha chega a 0,45 m do piso (o caminho corre numa prateleira da rocha);
#   - acima do piso, nada de rocha na faixa do caminho: cada camada de cada coluna é recortada por um plano vertical
#     paralelo à beira, ou a coluna acaba ali (topo plano: vira prateleira; nada de lâmina nem de coluna atravessada);
#   - do lado da montanha o paredão sobe colado à beira (o caminho fica encostado nele, sem mureta, pedido do usuário);
#   - do lado do vale (e do lado da cidade nas vilas) a rocha fica abaixo do piso, descendo em degraus (a vista);
#   - a escultura perto do jogo só afunda a rocha, nunca a empurra para o caminho;
#   - sem booleanos, e sem redução perto do jogo (os triângulos grandes passavam por cima do piso).
# Outras mudanças pedidas: caminhos 2 m mais largos (6 m; o zigue-zague, as plataformas e a calçada até o portão 8 m),
# escadas sem patamares no meio de uma subida ou descida constante (degraus iguais do começo ao fim de cada trecho) e um
# cume dominante bem alto, visível da cidade.
#   blender.exe -b --python Ferramentas/Picos/macico_vilas2.py -- [--saida <pasta>] [--preview <pasta>] [--so-colunas]
# Exporta o Macico_Vilas.fbx (rocha, colisão e gelo) e o Macico_Vilas_Layout.json que o PicosBuilder lê (na pasta dos
# modelos, ou em --saida para conferir antes).
import json
import math
import os
import random
import sys
import time

import bmesh
import bpy
import numpy as np
from mathutils import Vector, noise
from mathutils.kdtree import KDTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import montanhas_destaque as kit  # noqa: E402
import montanha_bastiao as cut  # noqa: E402
import macico_vilas as mv  # noqa: E402  (planta das vilas, dos caminhos e do zigue-zague)

NAME = mv.NAME
VOXEL = 2.6
BOTTOM = kit.BOTTOM
T0 = time.time()

# ---------- larguras e escadas (pedido do usuário em 2026-11-01) ----------
ROAD_HALF = 3.0                 # caminhos de 6 m (eram 4)
mv.SWITCH_HALF = 4.0            # lances do zigue-zague e calçada até o portão: 8 m (eram 6)
mv.SWITCH_PLATFORM = 8.0
# Lances colados uns nos outros, a cada 8 m (a largura deles), como a escadaria da costa do Arquipélago (pedido do
# usuário em 2026-11-08; antes ficavam a 16 m, com vão entre eles)
mv.FLIGHT_N = [448.0, 440.0, 432.0, 424.0, 416.0]
RAMP = 0.1                      # inclinação máxima da rampa calçada (acima, degraus)
STEP_RISE = 0.2
STEP_MIN_RUN = 0.28
SECTION_END = 1.2               # piso plano curto só onde a inclinação muda

# ---------- espaço do jogo ----------
FLOOR_DROP = 0.45               # rocha embaixo do piso
WALL_GAP = 1.2                  # da beira do piso até a face do paredão, antes do remesh (mais um sorteio de até 1,5 m
#                                 por coluna, para a parede não ficar lisa); o Unity estende o piso até a rocha
FINAL_GAP = 0.3                 # garantia no fim: nada de rocha a menos disto da beira, acima do piso
HEADROOM = 7.0                  # a garantia vale até esta altura acima do piso
VALLEY_DROP = 1.0               # do lado do vale, a rocha fica isto abaixo do piso na beira...
VALLEY_SLOPE = 0.3              # ...e mais isto por metro para longe
VALLEY_REACH = 45.0
CITY_SECTOR = 70.0              # graus para cada lado da direção da cidade (vilas: lado da vista)
MIN_INRADIUS = 1.6              # camada recortada mais fina que isto (2 x área / perímetro) acaba a coluna
GUARD = 8.0                     # perto do jogo (m além da beira) a escultura só afunda
PROTECT = 16.0                  # m além da beira dos caminhos e vilas: não suaviza nem reduz

# ---------- cume dominante (pedido do usuário: um cume bem alto, visível da cidade) ----------
DOMINANT = [(60.0, 875.0, 560.0), (115.0, 845.0, 680.0), (180.0, 880.0, 560.0)]
DOMINANT_FALL = 2.2
CREST_STEP = 38.0               # colunas do corpo do maciço (grade em quincôncio)
CREST_RADIUS = (19.0, 26.0)

FACES_NEAR = 90000
FACES_MID = 35000
FACES_FAR = 25000
FACES_COLLISION = 110000


def log(*args):
    print('[Vilas2 %4.0fs]' % (time.time() - T0), *args, flush=True)


def B(e, n, z=0.0):
    return Vector((-e, -n, z))


def seg_height(crest, fall, e, n):
    best = -1000.0
    for (ax, ay, ah), (bx, by, bh) in zip(crest, crest[1:]):
        dx, dy = bx - ax, by - ay
        t = max(0.0, min(1.0, ((e - ax) * dx + (n - ay) * dy) / (dx * dx + dy * dy)))
        d = math.hypot(e - (ax + dx * t), n - (ay + dy * t))
        best = max(best, ah + (bh - ah) * t - max(0.0, d - 8.0) * fall)
    return best


def intent(e, n):
    """Altura que a montanha quer ter em cada ponto (cristas, contrafortes e o cume dominante)."""
    return max(mv.crest_height(e, n), seg_height(DOMINANT, DOMINANT_FALL, e, n))


# ---------- perfis dos caminhos ----------

def lay_section(s0, h0, s1, h1, out):
    """Um trecho de inclinação constante: rampa calçada (até RAMP) ou degraus iguais do começo ao fim, sem patamares no
    meio (pedido do usuário em 2026-11-01); só um piso curto nas pontas, onde a inclinação muda."""
    length, drop = s1 - s0, h1 - h0
    if abs(drop) <= RAMP * length:
        count = max(1, int(round(length)))
        for k in range(count):
            out.append((s0 + length * k / count, h0 + drop * k / count, 'r'))
        return
    edge = min(SECTION_END, length * 0.08)
    avail = length - 2.0 * edge
    steps = max(2, int(round(abs(drop) / STEP_RISE)))
    if avail / steps < STEP_MIN_RUN:
        steps = max(2, int(avail / STEP_MIN_RUN))
    rise, run = drop / steps, avail / steps
    s, h = s0, h0
    out.append((s, h, 'l'))
    s += edge
    for _ in range(steps):
        h += rise
        out.append((s, h, 's'))
        s += run
    if s < s1 - 0.05:
        out.append((s, h, 'l'))


def cross_of(breaks):
    cross = []
    for (sa, ha), (sb, hb) in zip(breaks, breaks[1:]):
        lay_section(sa, ha, sb, hb, cross)
    cross.append((breaks[-1][0], breaks[-1][1], 'l'))
    return cross


def deck_profile(cross, count, step):
    """Altura do piso em cada amostra (degraus como degraus; a rampa interpolada)."""
    deck, k = [], 0
    for i in range(count):
        s = i * step
        while k + 1 < len(cross) and cross[k + 1][0] <= s + 1e-6:
            k += 1
        cs, ch, kind = cross[k]
        if kind == 'r' and k + 1 < len(cross):
            ns, nh, _ = cross[k + 1]
            ch = ch + (nh - ch) * (s - cs) / max(1e-6, ns - cs)
        deck.append(ch)
    return deck


def merge(breaks, first=1):
    while True:
        best, at = mv.ROAD_MERGE, None
        for k in range(max(1, first), len(breaks) - 1):
            (sa, ha), (sb, hb), (sc, hc) = breaks[k - 1], breaks[k], breaks[k + 1]
            diff = abs((hb - ha) / (sb - sa) - (hc - hb) / (sc - sb))
            if diff < best:
                best, at = diff, k
        if at is None:
            return breaks
        breaks.pop(at)


def make_road(name, frm, to, pos, tan, cross, half, step=0.25):
    deck = deck_profile(cross, len(pos), step)
    return {'name': name, 'from': frm, 'to': to, 'pos': pos, 'tan': tan, 'deck': deck, 'cross': cross, 'gaps': [],
            'half': half, 'step': step}


def describe(name, breaks):
    log('%s: %.0f m, trechos %s' % (name, breaks[-1][0], ', '.join(
        '%.0f m %+.1f' % (q[0] - p[0], q[1] - p[1]) for p, q in zip(breaks, breaks[1:]))))


def terrace_road():
    """Do Terraço do Portal até a Escalonada: a mesma linha (pontos do usuário) e o mesmo perfil aprovado (sai plano do
    terraço até o primeiro ponto, a subida da fenda numa escadaria só até o alto, a descida inteira com a mesma
    inclinação)."""
    tc, tr = mv.V['Terraco']
    first = Vector(mv.ROAD_POINTS[0])
    start = tc.xy + (first - tc.xy).normalized() * tr * 0.95
    ec, er = mv.V['Escalonada']
    last = Vector(mv.ROAD_POINTS[-1])
    end = ec.xy + (last - ec.xy).normalized() * er * 0.98
    ctrl = [start] + [Vector(p) for p in mv.ROAD_POINTS] + [end]
    pos, tan = mv.rounded_line(ctrl, mv.ROAD_FILLET, 0.25)
    count = len(pos)
    s_of = [min(range(count), key=lambda i: (pos[i] - c).length_squared) * 0.25 for c in ctrl]
    total = (count - 1) * 0.25
    breaks = [(0.0, tc.z), (s_of[1], tc.z), (s_of[2], tc.z + 27.8), (total, ec.z)]
    describe('Terraco_Escalonada', breaks)
    road = make_road('Terraco_Escalonada', 'Terraco', 'Escalonada', pos, tan, cross_of(breaks), ROAD_HALF)
    road['forced'] = [(s_of[1] + 4.0, s_of[2], 'both')]      # a subida pela fenda: paredão dos dois lados
    return road


def paved_road(spec, half=ROAD_HALF):
    """Como o design_paved do macico_vilas.py, só o perfil: linha suave pelos pontos, inclinação o mais uniforme
    possível perto das alturas da planta, trechos parecidos juntados."""
    name, start, mids, end, fixed = spec

    def tip(x, toward):
        if isinstance(x, str):
            return mv.edge_point(x, toward), mv.V[x][0].z, x
        return Vector(x[0]), x[1], x[2]
    first = mids[0] if mids else (tuple(mv.V[end][0]) if isinstance(end, str) else end[0])
    last = mids[-1] if mids else (tuple(mv.V[start][0]) if isinstance(start, str) else start[0])
    (a, ha, start), (b, hb, end) = tip(start, first), tip(end, last)
    ctrl = [a] + [Vector((m[0], m[1])) for m in mids] + [b]
    heights = [ha] + [m[2] for m in mids] + [hb]
    pos, tan = mv.rounded_line(ctrl, mv.ROAD_FILLET, 0.25)
    count = len(pos)
    s_of = [min(range(count), key=lambda i: (pos[i] - c).length_squared) * 0.25 for c in ctrl]
    s_of[0], s_of[-1] = 0.0, (count - 1) * 0.25
    for k in range(1, len(s_of)):
        s_of[k] = max(s_of[k], s_of[k - 1] + 0.5)
    if not fixed and len(heights) > 2:
        guide = list(heights)
        for _ in range(400):
            for k in range(1, len(heights) - 1):
                t = (s_of[k] - s_of[k - 1]) / (s_of[k + 1] - s_of[k - 1])
                heights[k] = max(guide[k] - mv.ROAD_PULL, min(guide[k] + mv.ROAD_PULL,
                                                               heights[k - 1] + (heights[k + 1] - heights[k - 1]) * t))
        heights = mv.pava(heights, heights[-1] > heights[0])
    breaks = merge(list(zip(s_of, heights)))
    describe(name, breaks)
    return make_road(name, start, end, pos, tan, cross_of(breaks), half)


def straight(a, b, step=0.25):
    a, b = Vector(a[:2]), Vector(b[:2])
    length = (b - a).length
    count = max(2, int(round(length / step)) + 1)
    t = (b - a).normalized()
    return [a.lerp(b, k / (count - 1)) for k in range(count)], [t.copy() for _ in range(count)], length / (count - 1)


def flight_cross(length, top, drop):
    """Lance do zigue-zague: piso curto na beira e degraus iguais até o fim, sem o patamar do meio."""
    steps = max(2, int(round(drop / mv.SWITCH_RISE)))
    lead = mv.SWITCH_LEAD
    run = (length - 2.0 * lead) / steps
    cross, s, h = [(0.0, top, 'l')], lead, top
    for _ in range(steps):
        h -= drop / steps
        cross.append((s, h, 's'))
        s += run
    cross.append((length - lead, h, 'l'))
    cross.append((length, h, 'l'))
    return cross


def switchback():
    plats, flights = mv.switchback_plan()
    pieces, by_name = [], {}
    for name, (e, n), h, (sl, sn), openings in plats:
        pos, tan, step = straight((e, n + sn * 0.5), (e, n - sn * 0.5))
        piece = make_road(name, name, name, pos, tan, [(0.0, h, 'l'), (sn, h, 'l')], sl * 0.5, step)
        piece['platform'] = {'center': (e, n), 'height': h, 'size': (sl, sn), 'openings': openings}
        pieces.append(piece)
        by_name[name] = (e, n, h, sl, sn)
    e, n, h, sl, sn = by_name['Zz_0']
    top = Vector((e, n + sn * 0.5))
    start = mv.edge_point('Pendurada', top)
    length = (top - start).length
    cross = []
    lay_section(0.0, mv.V['Pendurada'][0].z, length, h, cross)
    cross.append((length, h, 'l'))
    pos, tan, step = straight(start, top)
    pieces.append(make_road('Pendurada_Zz', 'Pendurada', 'Zz_0', pos, tan, cross, mv.SWITCH_HALF, step))
    lines = []
    for name, frm, to, a, b, h in flights:
        pos, tan, step = straight(a, b)
        length = (Vector(b) - Vector(a)).length
        road = make_road(name, frm, to, pos, tan, flight_cross(length, h, mv.FLIGHT_DROP), mv.SWITCH_HALF, step)
        # Ao norte de cada lance fica a face do lance de cima; o primeiro fica solto, com balaústres dos dois lados
        if name == 'Zz_Lance_1':
            road['no_walls'] = True
        else:
            road['north_wall'] = True
        pieces.append(road)
        lines.append((a, b, h))
    e, n, h, sl, sn = by_name[plats[-1][0]]
    spec = ('Zz_Portao', ((e, n - sn * 0.5), h, plats[-1][0]), [tuple(q) for q in mv.GATE_PATH], 'Portao', False)
    pieces.append(paved_road(spec, half=mv.SWITCH_HALF))
    return pieces, lines


# ---------- o espaço do jogo ----------

class Space:
    """Faixas (caminhos, plataformas, pontes) amostradas a cada 0,5 m e discos (vilas, torre, casas dos patamares)."""

    def __init__(self):
        self.samples = []       # (p 2D, normal à direita, piso, fundo proibido, meia largura, vale à esquerda?,
        #                          vale à direita?, nome da faixa)
        self.discs = []         # (centro 2D, raio, piso, fundo proibido, direção da cidade ou None, apoio?)
        self.max_half = 0.0

    def add_strip(self, road, bottom_drop=FLOOR_DROP, valleys=True):
        pos, tan, deck, half = road['pos'], road['tan'], road['deck'], road['half']
        step = road.get('step', 0.25)
        every = max(1, int(round(0.5 / step)))
        idx = list(range(0, len(pos), every))
        if idx[-1] != len(pos) - 1:
            idx.append(len(pos) - 1)
        win = max(1, int(round(1.0 / step)))
        walls = road_walls(road, idx) if valleys else [(False, False)] * len(idx)
        first = len(self.samples)
        for k, i in enumerate(idx):
            t = tan[i]
            low = min(deck[max(0, i - win):i + win + 1])
            wl, wr = walls[k]
            self.samples.append((pos[i].copy(), Vector((t.y, -t.x)), deck[i], low - bottom_drop, half,
                                 valleys and not wl, valleys and not wr, road['name']))
        self.max_half = max(self.max_half, half)
        road['sample_range'] = (first, len(self.samples))
        road['sample_s'] = [i * step for i in idx]
        road['walls'] = walls

    def add_disc(self, c, r, floor, bottom, city=None, support=True):
        self.discs.append((Vector(c[:2]), r, floor, bottom, city, support))

    def build(self):
        s = self.samples
        self.P = np.array([[q[0].x, q[0].y] for q in s])
        self.N = np.array([[q[1].x, q[1].y] for q in s])
        self.floor = np.array([q[2] for q in s])
        self.bottom = np.array([q[3] for q in s])
        self.half = np.array([q[4] for q in s])
        self.vl = np.array([q[5] for q in s])
        self.vr = np.array([q[6] for q in s])
        self.tree = KDTree(len(s))
        for k, q in enumerate(s):
            self.tree.insert((q[0].x, q[0].y, 0.0), k)
        self.tree.balance()

    def around(self, e, n, reach):
        return np.array([k for _, k, _ in self.tree.find_range((e, n, 0.0), reach)], dtype=np.int64)

    def near_samples(self, q, reach):
        """Amostras cuja perpendicular passa por q (a no máximo 0,45 m ao longo), a até 'reach' do eixo."""
        out = []
        for _, k, _ in self.tree.find_range((q.x, q.y, 0.0), reach):
            s = self.samples[k]
            d = q - s[0]
            if abs(d.x * s[1].y - d.y * s[1].x) <= 0.45:
                out.append((s, d.dot(s[1])))
        return out

    def cap(self, e, n, radius):
        """Teto da rocha do lado do vale (caminhos) e da cidade (vilas), para uma coluna de raio 'radius' em (e, n)."""
        best = 1e9
        idx = self.around(e, n, VALLEY_REACH + self.max_half + radius)
        if len(idx):
            d = np.array([e, n]) - self.P[idx]
            nrm = self.N[idx]
            along = np.abs(d[:, 0] * nrm[:, 1] - d[:, 1] * nrm[:, 0])
            off = d[:, 0] * nrm[:, 0] + d[:, 1] * nrm[:, 1]
            valley = np.where(off > 0, self.vr[idx], self.vl[idx]) & (along <= 0.45)
            if valley.any():
                dist = np.maximum(0.0, np.abs(off) - radius - (self.half[idx] + 1.5))
                best = min(best, float(np.min((self.floor[idx] - VALLEY_DROP - VALLEY_SLOPE * dist)[valley])))
        q = Vector((e, n))
        for c, r, floor, bottom, city, _ in self.discs:
            if city is None:
                continue
            v = q - c
            if v.length < 1e-3 or v.length > r + VALLEY_REACH + radius:
                continue
            if v.normalized().dot(city) < math.cos(math.radians(CITY_SECTOR)):
                continue
            d = max(0.0, v.length - radius - (r + 1.5))
            best = min(best, floor - VALLEY_DROP - VALLEY_SLOPE * d)
        return best


def road_walls(road, idx):
    """Lado do paredão em cada amostra (esquerda, direita): pela altura que a montanha quer ter 10 m além de cada beira.
    Se os dois lados sobem, só o mais alto fica de paredão (o outro vira vale, para a vista), menos onde é forçado (a
    fenda da subida). Lances do zigue-zague: paredão ao norte. Suavizado ao longo (15 m)."""
    pos, tan, deck, half = road['pos'], road['tan'], road['deck'], road['half']
    step = road.get('step', 0.25)
    raw = []
    for i in idx:
        p, t = pos[i], tan[i]
        nrm = Vector((t.y, -t.x))
        s = i * step
        if road.get('north_wall'):
            north_right = nrm.y > 0
            raw.append((not north_right, north_right))
            continue
        if any(a <= s <= b for a, b, kind in road.get('forced', []) if kind == 'both'):
            raw.append((True, True))
            continue
        hl = intent(*(p - nrm * (half + 10.0)))
        hr = intent(*(p + nrm * (half + 10.0)))
        wl, wr = hl > deck[i] + 3.0, hr > deck[i] + 3.0
        if wl and wr:
            wl, wr = hl >= hr, hr > hl
        raw.append((wl, wr))
    span = max(1, int(round(7.5 / (step * max(1, int(round(0.5 / step)))))))
    out = []
    for k in range(len(raw)):
        window = raw[max(0, k - span):k + span + 1]
        out.append((sum(w[0] for w in window) * 2 > len(window), sum(w[1] for w in window) * 2 > len(window)))
    return out


class Field:
    """Grade de 1 m na planta: distância além da beira do pedaço de jogo mais perto (negativa dentro), o fundo proibido
    e o piso dele (para a escultura, a proteção e a garantia, sem laço por vértice)."""

    def __init__(self, space, reach=PROTECT + GUARD + 4.0):
        pts = [s[0] for s in space.samples] + [d[0] for d in space.discs]
        pad = reach + max([d[1] for d in space.discs] + [space.max_half]) + 2.0
        self.x0 = min(p.x for p in pts) - pad
        self.y0 = min(p.y for p in pts) - pad
        nx = int(max(p.x for p in pts) + pad - self.x0) + 1
        ny = int(max(p.y for p in pts) + pad - self.y0) + 1
        self.d = np.full((nx, ny), 1e9)
        self.bottom = np.full((nx, ny), 1e9)
        self.floor = np.full((nx, ny), -1e9)
        gx = self.x0 + np.arange(nx) + 0.5
        gy = self.y0 + np.arange(ny) + 0.5

        def splat(cx, cy, r, extra, bottom, floor):
            i0, i1 = max(0, int(cx - r - extra - self.x0)), min(nx, int(cx + r + extra - self.x0) + 2)
            j0, j1 = max(0, int(cy - r - extra - self.y0)), min(ny, int(cy + r + extra - self.y0) + 2)
            if i0 >= i1 or j0 >= j1:
                return
            dd = np.hypot(gx[i0:i1, None] - cx, gy[None, j0:j1] - cy) - r
            win = self.d[i0:i1, j0:j1]
            better = dd < win
            win[better] = dd[better]
            self.bottom[i0:i1, j0:j1][better] = bottom
            self.floor[i0:i1, j0:j1][better] = floor
        for s in space.samples:
            splat(s[0].x, s[0].y, s[4], reach, s[3], s[2])
        for c, r, floor, bottom, *_ in space.discs:
            splat(c.x, c.y, r, reach, bottom, floor)
        self.nx, self.ny = nx, ny

    def lookup(self, world):
        """(distância, fundo, piso) de cada ponto do mundo do Blender (x = -L, y = -N)."""
        i = np.floor(-world[:, 0] - self.x0).astype(np.int64)
        j = np.floor(-world[:, 1] - self.y0).astype(np.int64)
        ok = (i >= 0) & (i < self.nx) & (j >= 0) & (j < self.ny)
        d = np.full(len(world), 1e9)
        b = np.full(len(world), 1e9)
        f = np.full(len(world), -1e9)
        d[ok], b[ok], f[ok] = self.d[i[ok], j[ok]], self.bottom[i[ok], j[ok]], self.floor[i[ok], j[ok]]
        return d, b, f


# ---------- colunas que respeitam o espaço do jogo ----------

def poly_area(poly):
    x, y = poly[:, 0], poly[:, 1]
    return abs(np.dot(x, np.roll(y, -1)) - np.dot(np.roll(x, -1), y)) * 0.5


def poly_perimeter(poly):
    return float(np.sum(np.linalg.norm(np.roll(poly, -1, axis=0) - poly, axis=1)))


def inside_poly(poly, pts):
    """Pontos (k x 2) dentro do polígono (cruzamentos)."""
    hit = np.zeros(len(pts), dtype=bool)
    x, y = pts[:, 0], pts[:, 1]
    for k in range(len(poly)):
        (ax, ay), (bx, by) = poly[k], poly[(k + 1) % len(poly)]
        cond = (ay > y) != (by > y)
        with np.errstate(divide='ignore', invalid='ignore'):
            xi = ax + (y - ay) * (bx - ax) / (by - ay)
        hit ^= cond & (x < xi)
    return hit


def crosses(poly, a, b):
    """Segmentos a->b (k x 2) que cortam alguma aresta do polígono."""
    hit = np.zeros(len(a), dtype=bool)
    s = b - a
    for k in range(len(poly)):
        p, q = poly[k], poly[(k + 1) % len(poly)]
        r = q - p
        den = r[0] * s[:, 1] - r[1] * s[:, 0]
        ap = a - p
        with np.errstate(divide='ignore', invalid='ignore'):
            t = (ap[:, 0] * s[:, 1] - ap[:, 1] * s[:, 0]) / den
            u = (ap[:, 0] * r[1] - ap[:, 1] * r[0]) / den
        hit |= (np.abs(den) > 1e-9) & (t >= 0) & (t <= 1) & (u >= 0) & (u <= 1)
    return hit


def seg_dist(poly, p):
    a = poly
    ab = np.roll(poly, -1, axis=0) - a
    t = np.clip(np.sum((p - a) * ab, axis=1) / np.maximum(np.sum(ab * ab, axis=1), 1e-9), 0.0, 1.0)
    return np.linalg.norm(a + ab * t[:, None] - p, axis=1)


def clip_half(poly, p, nrm, dist):
    """Sutherland-Hodgman com o semiplano (x - p) . nrm >= dist."""
    out = []
    m = len(poly)
    for k in range(m):
        a, b = poly[k], poly[(k + 1) % m]
        da, db = np.dot(a - p, nrm) - dist, np.dot(b - p, nrm) - dist
        if da >= 0:
            out.append(a)
        if (da >= 0) != (db >= 0):
            out.append(a + (b - a) * (da / (da - db)))
    return np.array(out) if out else np.zeros((0, 2))


class Columns:
    def __init__(self, space):
        self.space = space
        self.bm = bmesh.new()
        self.cover = mv.Cover([])
        self.count = 0
        self.ended = 0
        self.free = 0

    def add(self, e, n, top, r_top, rnd, base=1.35, bed=(8.0, 16.0), lean=None, capped=True, gap=None,
            flat=False):
        """Uma coluna de arenito (pilha de blocos de planta poligonal, como a do montanhas_destaque.column), com cada
        camada recortada pelo espaço do jogo. Devolve o topo de verdade (ou None)."""
        sp = self.space
        if mv.inside(mv.GORGE, e, n):
            return None
        if gap is None:
            gap = WALL_GAP + rnd.uniform(0.0, 1.2)
        reach = r_top * base * 1.3
        cand = sp.around(e, n, reach + sp.max_half + gap + 1.0)
        q = Vector((e, n))
        discs = [d for d in sp.discs if (q - d[0]).length < d[1] + reach + gap + 1.0]
        near = len(cand) > 0 or len(discs) > 0
        if capped:
            c = sp.cap(e, n, r_top)
            if c < top:
                top, flat = c, True
        if top < BOTTOM + 10.0:
            return None
        corners = rnd.randint(4, 7)
        angles = [(k + rnd.uniform(-0.3, 0.3)) / corners * math.tau for k in range(corners)]
        radii = [rnd.uniform(0.85, 1.15) for _ in range(corners)]
        if lean is None:
            lean = (0.0, 0.0) if near else (rnd.uniform(-2, 2), rnd.uniform(-2, 2))
        cuts = []
        if near:
            bottoms = list(sp.bottom[cand]) + [d[3] for d in discs]
            cuts = sorted({math.floor(b * 2.0) / 2.0 for b in bottoms if BOTTOM + 2.0 < b < top - 1.0})
        height = top - BOTTOM
        layers = []
        z = BOTTOM
        shift = 0.1
        while z < top - 0.5:
            h = min(rnd.uniform(*bed), top - z)
            if top - (z + h) < bed[0] * 0.4:
                h = top - z
            for zc in cuts:
                if z + 1.0 < zc < z + h:
                    h = zc - z
                    break
            t = max(0.0, 1.0 - (z + h - BOTTOM) / height)
            r = r_top + (r_top * base - r_top) * t ** 3.5
            spin = rnd.uniform(-shift, shift)
            cx = e + lean[0] * (1 - t) ** 1.5 + rnd.uniform(-shift, shift) * r
            cy = n + lean[1] * (1 - t) ** 1.5 + rnd.uniform(-shift, shift) * r
            poly = np.array([(cx + math.cos(a + spin) * v * rnd.uniform(1 - shift, 1 + shift) * r,
                              cy + math.sin(a + spin) * v * rnd.uniform(1 - shift, 1 + shift) * r)
                             for a, v in zip(angles, radii)])
            if near:
                # cada camada recua um pouco diferente da beira: a parede fica em degraus de blocos, não lisa
                poly = self.clip(poly, z + h, gap + rnd.uniform(0.0, 1.0), cand, discs)
                if poly is None:
                    flat = True
                    self.ended += 1
                    break
            layers.append((poly, z, z + h))
            z += h
        if not layers:
            return None
        for k, (poly, z0, z1) in enumerate(layers):
            last = k == len(layers) - 1
            crown = 0.0 if (flat or near or not last) else r_top * 0.12
            self.prism(poly, z0 - 0.8 if k else z0, z1, 0.82 if (last and crown > 0.0) else 0.97, crown)
        real_top = layers[-1][2]
        self.cover.add((e, n, real_top, r_top))
        self.count += 1
        if not near:
            self.free += 1
        return real_top

    def clip(self, poly, z_top, gap, cand, discs):
        """Recorta a camada até ela não invadir nenhuma faixa ou disco acima do fundo proibido; None se o centro fica
        dentro ou se sobra fina demais."""
        sp = self.space
        sel = cand[sp.bottom[cand] < z_top] if len(cand) else cand
        active = [d for d in discs if d[3] < z_top]
        if not len(sel) and not active:
            return poly
        if len(sel):
            P, Nn = sp.P[sel], sp.N[sel]
            W = sp.half[sel] + gap
            A, Bq = P - Nn * W[:, None], P + Nn * W[:, None]
        for _ in range(14):
            c = poly.mean(axis=0)
            plane = None
            if len(sel):
                hit = crosses(poly, A, Bq) | inside_poly(poly, P)
                if hit.any():
                    k = np.nonzero(hit)[0]
                    k = k[np.argmin(np.linalg.norm(P[k] - c, axis=1))]
                    off = np.dot(c - P[k], Nn[k])
                    if abs(off) < W[k]:
                        return None
                    plane = (P[k], Nn[k] if off > 0 else -Nn[k], W[k])
            if plane is None:
                for centre, r, *_ in active:
                    ctr = np.array([centre.x, centre.y])
                    rad = r + gap
                    if inside_poly(poly, ctr[None])[0] or np.min(seg_dist(poly, ctr)) < rad:
                        v = c - ctr
                        length = np.linalg.norm(v)
                        if length < rad:
                            return None
                        plane = (ctr, v / length, rad)
                        break
            if plane is None:
                break
            poly = clip_half(poly, *plane)
            if len(poly) < 3:
                return None
        else:
            return None
        area = poly_area(poly)
        if area < 10.0 or 2.0 * area / max(poly_perimeter(poly), 1e-6) < MIN_INRADIUS:
            return None
        return poly

    def prism(self, poly, z0, z1, top_scale, crown):
        bm = self.bm
        c = poly.mean(axis=0)
        low = [bm.verts.new(B(p[0], p[1], z0)) for p in poly]
        high = [bm.verts.new(B(c[0] + (p[0] - c[0]) * top_scale, c[1] + (p[1] - c[1]) * top_scale, z1)) for p in poly]
        m = len(poly)
        for k in range(m):
            j = (k + 1) % m
            bm.faces.new((low[k], low[j], high[j], high[k]))
        bm.faces.new(list(reversed(low)))
        if crown > 0.0:
            apex = bm.verts.new(B(c[0], c[1], z1 + crown))
            for k in range(m):
                bm.faces.new((high[k], high[(k + 1) % m], apex))
        else:
            bm.faces.new(high)

    def to_object(self):
        mesh = bpy.data.meshes.new(NAME)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(NAME, mesh)
        bpy.context.collection.objects.link(obj)
        return obj


# ---------- a montanha ----------

def build(space, roads, flights):
    cols = Columns(space)
    rnd = random.Random(1101)

    # Apoio embaixo das vilas e das casas dos patamares (topo plano a 0,45 m do piso)
    for c, r, floor, bottom, city, support in space.discs:
        if not support:
            continue
        cols.add(c.x, c.y, bottom, r * 0.75, rnd, capped=False, flat=True)
        ring = 6 if r > 20 else 4
        for k in range(ring):
            a = k / ring * math.tau + rnd.uniform(-0.3, 0.3)
            cols.add(c.x + math.cos(a) * r * 0.62, c.y + math.sin(a) * r * 0.62, bottom, r * 0.48, rnd, capped=False,
                     flat=True)

    # Apoio embaixo dos caminhos: duas colunas lado a lado a cada ~4,5 m (prateleira de rocha), menos onde o vale é
    # fundo demais (lá fica ponte em arcos); ombros mais baixos dos dois lados
    for road in roads:
        pos, tan, deck, half = road['pos'], road['tan'], road['deck'], road['half']
        step = road.get('step', 0.25)
        total = (len(pos) - 1) * step
        gap_from, s = None, 0.0
        bridgeable = 'platform' not in road and not road['name'].startswith('Zz_Lance')
        while s <= total:
            i = min(len(pos) - 1, int(s / step))
            p, t = pos[i], tan[i]
            nrm = Vector((t.y, -t.x))
            ground = max(intent(*(p + nrm * o)) for o in (-half, 0.0, half))
            if bridgeable and ground < deck[i] - 30.0:
                gap_from = s if gap_from is None else gap_from
            else:
                if gap_from is not None and s - gap_from >= 12.0:
                    road['gaps'].append((gap_from, s))
                gap_from = None
                win = int(round(5.0 / step))
                low = min(deck[max(0, i - win):i + win + 1]) - FLOOR_DROP
                for o in ((-half * 0.45, half * 0.45) if half > 2.5 else (0.0,)):
                    q = p + nrm * o
                    cols.add(q.x, q.y, low, rnd.uniform(4.2, 5.2), rnd, base=1.25, bed=(4.0, 8.0), capped=False,
                             flat=True)
                for sign in (-1.0, 1.0):
                    if rnd.random() < 0.8:
                        q = p + nrm * sign * (half + rnd.uniform(3.5, 6.0))
                        cols.add(q.x, q.y, low - rnd.uniform(2.0, 6.0), rnd.uniform(3.8, 5.0), rnd, base=1.25,
                                 bed=(4.0, 8.0))
            s += rnd.uniform(4.0, 5.0)
        if gap_from is not None and total - gap_from >= 12.0:
            road['gaps'].append((gap_from, total))
        if road['gaps']:
            log('%s: pontes em %s' % (road['name'], ', '.join('%.0f a %.0f m' % g for g in road['gaps'])))
    log('apoios: %d colunas' % cols.count)

    # Paredões colados à beira do lado da montanha (o caminho fica encostado neles)
    walls = 0
    for road in roads:
        first, last = road['sample_range']
        half = road['half']
        for k in range(first, last, 9):                 # ~4,5 m
            p, nrm, floor = space.samples[k][0], space.samples[k][1], space.samples[k][2]
            wl, wr = road['walls'][k - first]
            for sign, wall in ((-1.0, wl), (1.0, wr)):
                if not wall:
                    continue
                r = rnd.uniform(4.0, 6.0)
                gap = WALL_GAP + rnd.uniform(0.0, 1.5)
                q = p + nrm * sign * (half + gap + r * 0.7)
                top = max(intent(q.x, q.y), floor + rnd.uniform(8.0, 18.0))
                if cols.add(q.x, q.y, top, r, rnd, base=1.3, bed=(5.0, 11.0), capped=False, gap=gap):
                    walls += 1
    # Paredões em volta das vilas, do lado da montanha (fora do setor da cidade), onde ela sobe
    for c, r, floor, bottom, city, support in space.discs:
        if city is None:
            continue
        count = max(8, int(math.tau * r / 6.0))
        for k in range(count):
            a = k / count * math.tau
            d = Vector((math.cos(a), math.sin(a)))
            if d.dot(city) > math.cos(math.radians(CITY_SECTOR + 15.0)):
                continue
            rr = rnd.uniform(4.5, 6.5)
            gap = WALL_GAP + rnd.uniform(0.0, 1.5)
            q = c + d * (r + gap + rr * 0.7)
            h = intent(q.x, q.y)
            if h > floor + 3.0:
                if cols.add(q.x, q.y, max(h, floor + rnd.uniform(8.0, 18.0)), rr, rnd, base=1.3, bed=(5.0, 11.0),
                            capped=False, gap=gap):
                    walls += 1
    log('paredões: %d colunas' % walls)

    # Paredão da Vila Pendurada: o zigue-zague desce por ele em prateleiras. Cada coluna sobe até o piso do lance logo
    # ao norte dela (é a prateleira daquele lance), e a parte que passa para o lance de baixo é recortada
    def flight_deck(e, n):
        north = [f for f in flights if f[0][1] >= n - mv.SWITCH_HALF]
        if not north:
            return None
        a, b, h = min(north, key=lambda f: f[0][1])
        e0, e1 = a[0], b[0]
        t = max(0.0, min(1.0, (e - e0) / (e1 - e0)))
        return h - mv.FLIGHT_DROP * t, abs(e1 - e0)
    e = 284.0
    while e <= 336.0:
        n = 380.0
        while n <= 452.0:
            pe, pn = e + rnd.uniform(-2, 2), n + rnd.uniform(-2, 2)
            fd = flight_deck(pe, pn)
            if fd:
                r = rnd.uniform(5.5, 7.0)
                top = fd[0] - FLOOR_DROP - mv.FLIGHT_DROP / fd[1] * r * 0.8
                cols.add(pe, pn, top, r, rnd, base=1.3, bed=(6.0, 12.0), capped=False, flat=True)
            n += 9.0
        e += 9.0

    # Corpo do maciço: colunas pelo campo das cristas e do cume dominante (topo pelo campo, com um degrau sorteado)
    # (pedido do usuário: colunas maiores e em menor número que as da montanha antiga, de 12 a 17 m a cada 26 m)
    count = 0
    n = 330.0
    row = 0
    while n < 1030.0:
        e = -650.0 + (CREST_STEP * 0.5 if row % 2 else 0.0)
        while e < 650.0:
            pe, pn = e + rnd.uniform(-8.0, 8.0), n + rnd.uniform(-8.0, 8.0)
            h = intent(pe, pn)
            if h > -45.0:
                big = 1.0 + max(0.0, h - 420.0) / 400.0       # o cume dominante em colunas mais grossas
                if cols.add(pe, pn, h - rnd.uniform(0.0, 12.0), rnd.uniform(*CREST_RADIUS) * big, rnd,
                            bed=(10.0, 22.0)) is not None:
                    count += 1
            e += CREST_STEP
        n += CREST_STEP * 0.87
        row += 1
    log('colunas das cristas', count)

    # Buracos e fendas fundas perto do jogo: onde em 6 das 8 direções há rocha mais de 8 m acima, uma coluna tapa até um
    # pouco abaixo da rocha em volta (também recortada pelo espaço do jogo)
    dirs = [Vector((math.cos(k * math.pi / 4), math.sin(k * math.pi / 4))) for k in range(8)]
    filled = []
    xs, ys = space.P[:, 0], space.P[:, 1]
    e = xs.min() - 60.0
    while e < xs.max() + 60.0:
        n = ys.min() - 60.0
        while n < ys.max() + 60.0:
            q = Vector((e, n))
            near = space.tree.find((e, n, 0.0))
            if near[2] is not None and 6.0 < near[2] < 60.0 and all((q - f).length > 6.0 for f in filled[-200:]):
                h = cols.cover.top(e, n)
                if h > -500.0:
                    tops = [max(cols.cover.top(*(q + d * k)) for k in (3.0, 6.0, 9.0, 12.0, 15.0)) for d in dirs]
                    up = sorted(t for t in tops if t > h + 8.0)
                    if len(up) >= 6:
                        if cols.add(e, n, up[len(up) // 2] - rnd.uniform(1.5, 3.0), rnd.uniform(4.0, 5.2), rnd,
                                    base=1.2, bed=(5.0, 10.0)) is not None:
                            filled.append(q)
            n += 4.0
        e += 4.0
    log('buracos tapados', len(filled))
    log('colunas: %d (%d longe do jogo), %d acabaram no espaço do jogo' % (cols.count, cols.free, cols.ended))
    return cols.to_object()


# ---------- escultura, acabamento e garantia ----------

def sculpt(obj, field, seed, crack_depth=2.6, cell=0.022, ledge_height=13.0, ledge_depth=1.8, lumps=4.0):
    """O sculpt das montanhas (blocos de Voronoi, fendas, degraus, lóbulos), mas perto do jogo só para dentro e pouco."""
    mesh = obj.data
    mesh.update()
    normals = kit.smooth_normals(obj)
    world = mv.world_coords(obj)
    d, bottom, _ = field.lookup(world)
    weights = np.where(world[:, 2] > bottom - 3.0, np.clip((d - 0.5) / GUARD, 0.0, 1.0), 1.0)
    off = Vector((seed * 13.1, seed * 7.7, seed * 3.3))
    for v in mesh.vertices:
        p = v.co.copy()
        nrm = normals[v.index]
        side = max(0.0, 1.0 - abs(nrm.z) * 1.2)
        q = p * cell + off
        dist, _ = noise.voronoi(Vector((q.x, q.y, q.z * 0.22)), distance_metric='DISTANCE')
        edge = dist[1] - dist[0]
        crack = max(0.0, 1.0 - edge / 0.16) ** 2
        cushion = min(edge, 0.45) * 2.2
        band = p.z / ledge_height + noise.fractal(q * 0.5, 0.6, 2.0, 2) * 1.2
        shelf = band - math.floor(band)
        ledge_mask = max(0.0, noise.noise(q * 0.7 + Vector((3.0, 0.0, 0.0))) + 0.15)
        ledge = ledge_depth * (0.5 - shelf) * min(1.0, ledge_mask * 3.0)
        big = noise.fractal(p * 0.011 + off, 0.5, 2.0, 3) * lumps
        fine = noise.noise(p * 0.08 + off) * 0.6
        disp = big + fine + side * (ledge + cushion - crack * crack_depth)
        w = weights[v.index]
        if w < 1.0:
            disp = disp * w + max(-1.2, min(0.0, disp)) * (1.0 - w)
        v.co = p + nrm * disp
    mesh.update()


def protect_mask(obj, field):
    world = mv.world_coords(obj)
    d, bottom, _ = field.lookup(world)
    return (d < PROTECT) & (world[:, 2] > bottom - 6.0)


def play_lines(space, deck, bridge):
    lines = [(s[0], s[0], s[4]) for s in space.samples[::16]]
    lines += [(c, c, r) for c, r, *_ in space.discs]
    lines += [(deck[0].xy, deck[1].xy, mv.DECK_WIDTH * 0.5), (bridge[0].xy, bridge[1].xy, 12.0)]
    return lines


def finish(obj, space, field, deck, bridge):
    """Suaviza e reduz longe do jogo (perto nada muda); devolve a cópia leve para a colisão."""
    keep = protect_mask(obj, field)
    lines = play_lines(space, deck, bridge)
    zone = mv.zone_ids(mv.world_coords(obj), lines)
    log('vértices perto do jogo', int(keep.sum()), 'de', len(keep))
    mv.set_group(obj, 'Suavizar', np.nonzero((zone == 2) & ~keep)[0], 1.0)
    mv.set_group(obj, 'Suavizar', np.nonzero((zone == 1) & ~keep)[0], 0.6, clear=False)
    mv.set_group(obj, 'Suavizar', np.nonzero((zone == 0) & ~keep)[0], 0.25, clear=False)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    smooth = obj.modifiers.new('suavizar', 'SMOOTH')
    smooth.factor, smooth.iterations = 0.5, 3
    smooth.vertex_group = 'Suavizar'
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    for zone_id, target, label in [(2, FACES_FAR, 'costas'), (1, FACES_MID, 'meio'), (0, FACES_NEAR, 'perto')]:
        world = mv.world_coords(obj)
        mv.reduce_where(obj, (mv.zone_ids(world, lines) == zone_id) & ~protect_mask(obj, field), target, label)
    log('faces depois da redução', len(obj.data.polygons))
    collision = obj.copy()
    collision.data = obj.data.copy()
    collision.name = collision.data.name = NAME + '_Colisao'
    bpy.context.collection.objects.link(collision)
    mv.reduce_where(collision, ~protect_mask(collision, field), FACES_COLLISION, 'colisão')
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
    return collision


def guarantee(obj, space, field):
    """Garantia final (deve mexer em pouca coisa): rocha dentro da faixa de um caminho ou de uma vila, do fundo proibido
    até HEADROOM acima do piso, desce para o fundo (perto do piso) ou sai de lado até FINAL_GAP além da beira."""
    mesh = obj.data
    world = mv.world_coords(obj)
    d, bottom, floor = field.lookup(world)
    cand = np.nonzero((d < FINAL_GAP + 0.75) & (world[:, 2] > bottom - 0.1) & (world[:, 2] < floor + HEADROOM + 1.0))[0]
    mw = obj.matrix_world
    inv = mw.inverted()
    moved, worst = 0, 0.0
    for vi in cand:
        v = mesh.vertices[int(vi)]
        p = mw @ v.co
        q = Vector((-p.x, -p.y))
        changed = False
        for s, off in space.near_samples(q, space.max_half + FINAL_GAP + 0.5):
            if not (s[3] < p.z < s[2] + HEADROOM) or abs(off) >= s[4] + FINAL_GAP:
                continue
            if p.z < s[2] + 0.5:
                worst = max(worst, p.z - s[3])
                p.z = s[3]
            else:
                push = (s[4] + FINAL_GAP - abs(off)) * (1.0 if off >= 0 else -1.0)
                worst = max(worst, abs(push))
                q = q + s[1] * push
                p.x, p.y = -q.x, -q.y
            changed = True
        for c, r, fl, bt, city, _ in space.discs:
            dv = q - c
            if bt < p.z < fl + HEADROOM and dv.length < r + FINAL_GAP:
                if p.z < fl + 0.5:
                    worst = max(worst, p.z - bt)
                    p.z = bt
                else:
                    q = c + dv.normalized() * (r + FINAL_GAP)
                    p.x, p.y = -q.x, -q.y
                changed = True
        if changed:
            v.co = inv @ p
            moved += 1
    mesh.update()
    log('garantia %s: %d de %d candidatos mexidos (o maior %.2f m)' % (obj.name, moved, len(cand), worst))


def check_corridor(obj, roads, delete):
    """Raios para cima de cada caminho (como o retoque): na colisão, as faces que ainda barram saem."""
    bm, faces, spots = mv.corridor_hits(obj, roads, 2.3)
    if faces and delete:
        bmesh.ops.delete(bm, geom=[bm.faces[f] for f in faces], context='FACES')
        bm.to_mesh(obj.data)
        obj.data.update()
    bm.free()
    log('corredor %s: %d pontos barrados (%d faces%s)' % (obj.name, len(spots), len(faces),
                                                          ', tiradas' if delete and faces else ''))
    return spots


# ---------- planta para o Unity ----------

def write_layout(landings, deck, bridge, roads):
    """A mesma planta do macico_vilas.py, mais o lado do paredão em cada ponto de cada caminho ('walls': 1 = à esquerda,
    2 = à direita de quem anda do começo para o fim; o Unity não põe mureta nem muro de arrimo desse lado)."""
    def u(p):
        return [round(p.x, 3), round(p.z, 3), round(p.y, 3)]

    out = []
    for road in roads:
        if 'platform' in road:
            continue
        data = mv.road_layout(road)
        ss = np.array(road['sample_s'])
        walls = road['walls']
        pos = road['pos']
        step = road.get('step', 0.25)
        tree = KDTree(len(pos))
        for i, p in enumerate(pos):
            tree.insert((p.x, p.y, 0.0), i)
        tree.balance()
        bits = []
        pts = data['points']
        for k in range(len(data['kinds'])):
            _, i, _ = tree.find((pts[3 * k], pts[3 * k + 2], 0.0))
            j = int(np.argmin(np.abs(ss - i * step)))
            wl, wr = walls[j]
            bits.append((1 if wl else 0) | (2 if wr else 0))
        data['walls'] = bits
        out.append(data)
    layout = {
        'villages': [{'name': name, 'center': u(c), 'radius': r} for name, (c, r) in mv.V.items()],
        'segments': [],
        'landings': [{'position': u(p)} for p in landings],
        'deck': {'a': u(deck[0]), 'b': u(deck[1]), 'width': mv.DECK_WIDTH},
        'bridge': {'a': u(bridge[0]), 'b': u(bridge[1]), 'width': 24.0},
        'roads': out,
        'platforms': [mv.platform_layout(r) for r in roads if 'platform' in r],
    }
    path = os.path.join(kit.OUT, NAME + '_Layout.json')
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(layout, f, indent=1)
    log('planta', path)


# ---------- prévia ----------

MARKS = [(10, -73.0, 646.3, 160.1, 121.0), (11, 39.6, 610.0, 160.6, 312.0), (12, -206.2, 619.8, 171.0, 80.0),
         (13, 270.0, 372.8, 82.8, 341.0), (14, 344.3, 390.0, 78.2, 49.0)]


def preview(folder, roads, space):
    os.makedirs(folder, exist_ok=True)
    bm = bmesh.new()
    for road in roads:
        pos, tan, deck, half = road['pos'], road['tan'], road['deck'], road['half']
        step = road.get('step', 0.25)
        every = max(1, int(round(1.0 / step)))
        for i in range(0, len(pos) - every, every):
            ring = []
            for k in (i, i + every):
                nrm = Vector((tan[k].y, -tan[k].x))
                for o in (-half, half):
                    q = pos[k] + nrm * o
                    ring.append(bm.verts.new(B(q.x, q.y, deck[k] + 0.05)))
            bm.faces.new((ring[0], ring[1], ring[3], ring[2]))
    for c, r, floor, bottom, city, _ in space.discs:
        ring = [bm.verts.new(B(c.x + math.cos(a / 24 * math.tau) * r, c.y + math.sin(a / 24 * math.tau) * r,
                               floor + 0.05)) for a in range(24)]
        bm.faces.new(ring)
    mesh = bpy.data.meshes.new('PX_Jogo')
    bm.to_mesh(mesh)
    bm.free()
    px = bpy.data.objects.new('PX_Jogo', mesh)
    bpy.context.collection.objects.link(px)
    cut.look_colors()
    scene = bpy.context.scene
    try:
        scene.render.engine = 'BLENDER_WORKBENCH'
    except TypeError:
        pass
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'VERTEX'
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    cam_data = bpy.data.cameras.new('cam')
    cam_data.clip_end = 6000.0
    cam = bpy.data.objects.new('cam', cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    views = [('00_visao_geral', (-150.0, -650.0, 520.0), (0.0, 560.0, 160.0), 24),
             ('01_da_cidade', (0.0, 120.0, 40.0), (0.0, 600.0, 230.0), 20),
             ('02_de_cima', (0.0, 560.0, 1500.0), (0.0, 561.0, 0.0), 20)]
    for k, e, n, h, yaw in MARKS:
        d = Vector((math.sin(math.radians(yaw)), math.cos(math.radians(yaw))))
        views.append(('ponto_%d' % k, (e, n, h), (e + d.x * 40.0, n + d.y * 40.0, h - 8.0), 18))
    for road in roads:
        if 'platform' in road or len(road['pos']) < 160:
            continue
        pos, deck = road['pos'], road['deck']
        for f in (0.15, 0.4, 0.65, 0.9):
            i = int(f * (len(pos) - 1))
            j = min(len(pos) - 1, i + 60)
            views.append(('%s_%02d' % (road['name'], int(f * 100)), (pos[i].x, pos[i].y, deck[i] + 1.8),
                          (pos[j].x, pos[j].y, deck[j] + 1.2), 16))
    for name, eye, target, lens in views:
        cam.location = B(*eye)
        cam.rotation_euler = (B(*target) - B(*eye)).to_track_quat('-Z', 'Y').to_euler()
        cam_data.lens = lens
        scene.render.filepath = os.path.join(folder, name + '.png')
        bpy.ops.render.render(write_still=True)
    log('prévia em', folder)


# ---------- principal ----------

def main(argv):
    if '--saida' in argv:
        kit.OUT = argv[argv.index('--saida') + 1]
        os.makedirs(kit.OUT, exist_ok=True)
    segments, _, landings, deck, bridge = mv.plan()
    kit.clear()
    pieces, flights = switchback()
    roads = [terrace_road()] + [paved_road(spec) for spec in mv.paved_specs()] + pieces

    space = Space()
    for road in roads:
        space.add_strip(road, valleys='platform' not in road)
    # Deque da Vila-Ponte (sobre a garganta) e ponte colossal até a cidade: sem rocha em volta
    o, l = deck
    for name, (a, b), half, drop in [('Deque', (o, l), mv.DECK_WIDTH * 0.5 + 1.0, 30.0),
                                     ('Ponte', bridge, 14.0, 400.0)]:
        pos, tan, step = straight(a.xy, b.xy, 0.5)
        dk = [a.z + (b.z - a.z) * k / (len(pos) - 1) for k in range(len(pos))]
        space.add_strip({'name': name, 'pos': pos, 'tan': tan, 'deck': dk, 'half': half, 'step': step},
                        bottom_drop=drop, valleys=False)
    city = Vector((0.0, 0.0))
    for name, (c, r) in mv.V.items():
        if name == 'Terraco':                       # a torre fica em pé no vale, sem tocar na montanha
            space.add_disc(c, r + 4.0, c.z, c.z - 25.0, None, support=False)
            continue
        space.add_disc(c, r, c.z, c.z - FLOOR_DROP, (city - c.xy).normalized())
    for p in landings:                              # casas dos patamares do zigue-zague
        space.add_disc(p, 7.0, p.z, p.z - FLOOR_DROP, None)
    space.build()
    log('espaço do jogo: %d amostras, %d discos' % (len(space.samples), len(space.discs)))
    for road in roads:
        if 'platform' in road:
            continue
        w = road['walls']
        log('  %s: paredão à esquerda %.0f%%, à direita %.0f%%' % (
            road['name'], 100.0 * sum(x[0] for x in w) / len(w), 100.0 * sum(x[1] for x in w) / len(w)))

    rock = build(space, roads, flights)
    log('colunas: %d faces' % len(rock.data.polygons))
    if '--so-colunas' in argv:
        if '--preview' in argv:
            preview(argv[argv.index('--preview') + 1], roads, space)
        return
    field = Field(space)
    bpy.ops.object.select_all(action='DESELECT')
    rock.select_set(True)
    bpy.context.view_layer.objects.active = rock
    rock.data.remesh_voxel_size = VOXEL
    rock.data.use_remesh_fix_poles = True
    bpy.ops.object.voxel_remesh()
    log('remesh', len(rock.data.polygons))
    mv.remove_fragments(rock)
    sculpt(rock, field, 28)
    log('esculpido')
    write_layout(landings, deck, bridge, roads)
    collision = finish(rock, space, field, deck, bridge)
    for obj in (rock, collision):
        guarantee(obj, space, field)
        mv.remove_fragments(obj)
    kit.relief_colors(rock)
    check_corridor(rock, roads, delete=False)
    check_corridor(collision, roads, delete=True)
    material = bpy.data.materials.new('MAT_Rocha')
    for o in (rock, collision):
        o.data.materials.clear()
        o.data.materials.append(material)
    walked = [(Vector((s[0].x, s[0].y, s[2])), Vector((s[0].x, s[0].y, s[2])), False, s[7])
              for s in space.samples[::16]]
    ice = mv.build_ice(rock, walked)
    cut.NAME = NAME
    cut.export([rock, collision, ice])
    if '--preview' in argv:
        collision.hide_set(True)
        preview(argv[argv.index('--preview') + 1], roads, space)


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
