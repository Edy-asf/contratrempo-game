# Maciço das Vilas do Alto, versão 3: montanha sólida (pedido do usuário em 2026-11-01). A versão 2 (macico_vilas2.py)
# montava a montanha com colunas de arenito separadas recortadas pelo espaço do jogo; de perto, os vãos entre as colunas
# viravam poços do lado sem mureta, a parede ficava longe da escada (colunas de apoio no meio) e os topos achatados do
# lado do vale viravam mesas e lâminas sem sentido. Aqui a montanha é uma superfície só, fechada, feita de um mapa de
# altura de 1 m que já nasce com o espaço do jogo:
#   - embaixo de cada caminho, plataforma e vila, a rocha fica a 0,45 m do piso (maciça, sem vão);
#   - do lado da montanha, o paredão sobe direto da beira da laje (colado nela; dos dois lados na subida da fenda);
#   - do lado do vale e da cidade, a rocha fica abaixo do piso e a encosta desce em degraus grandes e contínuos;
#   - por ser altura, não existe rocha por cima de um caminho nem vão entre blocos (nada solto, nada de lâmina, poços
#     estreitos tapados por um fechamento morfológico).
# O detalhe vem do próprio mapa (camadas em degraus, blocos de arenito de ~34 m com alturas e camadas próprias, fendas
# rasas e só longe do jogo) e da escultura depois do remesh (perto do jogo só para dentro da rocha).
# As escadas também mudaram (pedido do usuário: a mureta atravessava a escada numa curva): degraus só nos trechos
# retos; as curvas ficam em rampa calçada.
# Os caminhos, as vilas, o zigue-zague, as larguras e a planta para o Unity vêm do macico_vilas2.py.
#   blender.exe -b --python Ferramentas/Picos/macico_vilas3.py -- [--saida <pasta>] [--preview <pasta>] [--so-mapa]
import math
import os
import sys
import time

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import macico_vilas2 as v2  # noqa: E402

mv, kit, cut = v2.mv, v2.kit, v2.cut
NAME = mv.NAME
T0 = time.time()

# ---------- mapa ----------
CELL = 1.0
DOMAIN = (-820.0, 820.0, 230.0, 1260.0)    # L de, L até, N de, N até
BOTTOM = kit.BOTTOM
VOXEL = 2.0

# ---------- relevo longe do jogo ----------
BLOCK = 32.0                    # blocos de arenito grandes (Voronoi): topo plano na altura que a montanha quer
BLOCK_DROP = (-16.0, 4.0)       # mais este sorteio
SUB_BLOCK = 9.0                 # blocos pequenos: quebram o topo dos grandes e são a unidade do paredão e da encosta
SUB_DROP = (-3.0, 0.0)
CRACK_WIDTH, CRACK_DEPTH = 2.2, 6.0
CRACK_FROM = 35.0               # fendas entre os blocos grandes só a partir daqui (m além da beira do jogo)

# ---------- espaço do jogo ----------
FLOOR_DROP = v2.FLOOR_DROP
WALL_DEPTH = 9.0                # paredão colado à beira: espessura cheia, depois desce em talude
WALL_SLOPE = 1.5
WALL_MIN, WALL_MAX = 7.0, 45.0  # altura do paredão acima do piso
VALLEY_DROP, VALLEY_SLOPE, VALLEY_REACH = 1.0, 0.3, 45.0   # teto da vista (como na versão 2)
VALLEY_RISE = 0.8               # depois do alcance da vista o teto volta a subir (sem paredão de corte)
FLANK_SLOPE = 1.1               # encosta do lado do vale (em blocos pequenos de topo plano)
WALL_BLOCK = (0.0, 6.0)         # cada bloco do paredão sobe mais este sorteio
FLANK_REACH = 70.0
REACH = 120.0
CLEAR_MARGIN = 2.0              # o corte do caminho passa disto além da beira (o remesh arredonda o canto da parede e
#                                 a garantia fazia dentes); o Unity estende a laje até a rocha
NEAR_PIT_RADIUS = 6             # perto do jogo (até 30 m) o fechamento tapa vãos de até ~12 m entre os blocos
SCULPT_IN = 3.0                 # perto do jogo a escultura entra até isto na rocha (relevo nas paredes)
PIT_RADIUS, SPIKE_RADIUS = 3, 2  # fechamento (poços até ~6 m) e abertura (pontas até ~4 m), em células
WALL_CLEAR = 0.6                # do lado do paredão a rocha começa logo depois da beira (pedido do usuário em 2026-11-01:
#                                 a escada tem de encostar na montanha; o Unity estende a laje até a rocha)
KEEP_H, KEEP_SLOPE = 10.0, 1.5  # o paredão de cada caminho vence os cortes da vista (das vilas e de outros caminhos) até
#                                 esta altura acima do piso, descendo em talude depois do WALL_DEPTH
SCULPT_IN_LOW = 0.8             # junto do piso (até ~2 m acima dele) a escultura entra no máximo isto
# Lado do paredão de cada caminho. O lado virado para a cidade (e o lado esquerdo de toda a descida do Terraço, pedido
# do usuário) é o da vista: nunca paredão, e a rocha fica abaixo do piso até VALLEY_REACH. Os outros lados viram
# paredão se os blocos da própria montanha de 4 a 20 m além da beira passam RISE acima do piso (o campo de cristas
# errava); senão são vale curto: a rocha só fica abaixo do piso numa faixa de SHORT_REACH e volta a subir depressa (o
# vale comprido cavava valas na montanha: ponto 20 do usuário). Trechos mais curtos que MIN_RUN somem (o paredão
# trocava de lado em ~25 m e deixava um poço de um lado e blocos altos do outro: ponto 17 do usuário)
WALL_PROBE = (4.0, 8.0, 12.0, 16.0, 20.0)
RISE = 4.0
MIN_RUN = 30.0
CITY_COS = math.cos(math.radians(70.0))
SHORT_REACH, SHORT_RISE = 12.0, 2.5
# Vilas: arcos da beira sem paredão atrás (ficam como o lado da vista). Na Escalonada, entre a chegada da descida do
# Terraço e a saída para os Sinos: o paredão de trás da vila subia ~40 m ali, ao lado da escada (foto 122 do usuário)
OPEN_ARCS = {}

# ---------- escadas nas curvas ----------
CURVE_RATE = math.radians(1.0)  # graus por metro: acima disto é curva (raio < ~57 m)
CURVE_PAD = 0.75
MIN_STRAIGHT = 4.0              # trecho reto mais curto que isto fica junto com a curva
MAX_STAIR_SLOPE = 0.65


def log(*args):
    print('[Vilas3 %4.0fs]' % (time.time() - T0), *args, flush=True)


# ---------- escadas só nos trechos retos ----------

_pending = {}
_orig_cross_of, _orig_make_road = v2.cross_of, v2.make_road


def curve_spans(pos, tan, step):
    """Trechos em curva (s de, s até) da linha amostrada."""
    ang = np.unwrap(np.array([math.atan2(t.y, t.x) for t in tan]))
    win = max(1, int(round(1.0 / step)))
    rate = np.zeros(len(ang))
    for i in range(len(ang)):
        a, b = max(0, i - win), min(len(ang) - 1, i + win)
        rate[i] = abs(ang[b] - ang[a]) / max(step, (b - a) * step)
    curved = rate > CURVE_RATE
    spans, start = [], None
    for i, c in enumerate(curved):
        if c and start is None:
            start = i
        if not c and start is not None:
            spans.append([start * step - CURVE_PAD, (i - 1) * step + CURVE_PAD])
            start = None
    if start is not None:
        spans.append([start * step - CURVE_PAD, (len(curved) - 1) * step])
    merged = []
    for a, b in spans:
        if merged and a - merged[-1][1] < MIN_STRAIGHT:
            merged[-1][1] = b
        else:
            merged.append([a, b])
    return merged


def curved_cross(breaks, pos, tan, step):
    """Como o cross_of, mas em cada trecho as curvas viram rampa (até RAMP) e a altura que falta vai para os trechos
    retos, em degraus iguais (o usuário achou a mureta atravessando a escada numa curva)."""
    spans = curve_spans(pos, tan, step)
    cross = []
    for (sa, ha), (sb, hb) in zip(breaks, breaks[1:]):
        length, drop = sb - sa, hb - ha
        if abs(drop) <= v2.RAMP * length:
            v2.lay_section(sa, ha, sb, hb, cross)
            continue
        parts, s = [], sa
        for a, b in spans:
            a, b = max(a, sa), min(b, sb)
            if b <= a:
                continue
            if a - s < MIN_STRAIGHT and parts and parts[-1][2]:
                parts[-1][1] = b
            elif a - s < MIN_STRAIGHT and not parts:
                parts.append([s, b, True])
            else:
                parts.append([s, a, False])
                parts.append([a, b, True])
            s = b
        if sb - s > 0.01:
            if sb - s < MIN_STRAIGHT and parts and parts[-1][2]:
                parts[-1][1] = sb
            else:
                parts.append([s, sb, False])
        curve_len = sum(b - a for a, b, c in parts if c)
        straight_len = length - curve_len
        sign = 1.0 if drop > 0 else -1.0
        cslope = sign * v2.RAMP
        if straight_len < MIN_STRAIGHT or abs((drop - cslope * curve_len) / straight_len) > MAX_STAIR_SLOPE:
            v2.lay_section(sa, ha, sb, hb, cross)
            continue
        sslope = (drop - cslope * curve_len) / straight_len
        h = ha
        for a, b, c in parts:
            nh = h + (cslope if c else sslope) * (b - a)
            v2.lay_section(a, h, b, nh, cross)
            h = nh
    cross.append((breaks[-1][0], breaks[-1][1], 'l'))
    return cross


def _cross_of(breaks):
    _pending['breaks'] = list(breaks)
    return _orig_cross_of(breaks)


def _make_road(name, frm, to, pos, tan, cross, half, step=0.25):
    breaks = _pending.pop('breaks', None)
    if breaks is not None:
        cross = curved_cross(breaks, pos, tan, step)
    return _orig_make_road(name, frm, to, pos, tan, cross, half, step)


v2.cross_of, v2.make_road = _cross_of, _make_road


# ---------- campos ----------

class Grid:
    def __init__(self):
        e0, e1, n0, n1 = DOMAIN
        self.e0, self.n0 = e0, n0
        self.nx, self.ny = int((e1 - e0) / CELL), int((n1 - n0) / CELL)
        self.e = e0 + (np.arange(self.nx) + 0.5) * CELL
        self.n = n0 + (np.arange(self.ny) + 0.5) * CELL
        self.E, self.N = np.meshgrid(self.e, self.n, indexing='ij')

    def box(self, e, n, reach):
        i0 = max(0, int((e - reach - self.e0) / CELL))
        i1 = min(self.nx, int((e + reach - self.e0) / CELL) + 1)
        j0 = max(0, int((n - reach - self.n0) / CELL))
        j1 = min(self.ny, int((n + reach - self.n0) / CELL) + 1)
        return i0, i1, j0, j1

    def sample(self, field, e, n):
        i = np.clip(((np.asarray(e) - self.e0) / CELL).astype(np.int64), 0, self.nx - 1)
        j = np.clip(((np.asarray(n) - self.n0) / CELL).astype(np.int64), 0, self.ny - 1)
        return field[i, j]


def chain_field(E, N, chains, fall):
    out = np.full(E.shape, -1000.0)
    for chain in chains:
        for (ax, ay, ah), (bx, by, bh) in zip(chain, chain[1:]):
            dx, dy = bx - ax, by - ay
            t = np.clip(((E - ax) * dx + (N - ay) * dy) / (dx * dx + dy * dy), 0.0, 1.0)
            d = np.hypot(E - (ax + dx * t), N - (ay + dy * t))
            np.maximum(out, ah + (bh - ah) * t - np.maximum(0.0, d - 8.0) * fall, out=out)
    return out


def value_noise(E, N, scale, seed):
    rng = np.random.default_rng(seed)
    gx, gy = E / scale, N / scale
    ix, iy = np.floor(gx).astype(np.int64), np.floor(gy).astype(np.int64)
    fx, fy = gx - ix, gy - iy
    ox, oy = ix.min(), iy.min()
    table = rng.uniform(-1.0, 1.0, (ix.max() - ox + 2, iy.max() - oy + 2))
    i, j = ix - ox, iy - oy
    sx, sy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a = table[i, j] + (table[i + 1, j] - table[i, j]) * sx
    b = table[i, j + 1] + (table[i + 1, j + 1] - table[i, j + 1]) * sx
    return a + (b - a) * sy


def fbm(E, N, scale, seed, octaves=4):
    out, amp, total = np.zeros(E.shape), 1.0, 0.0
    for k in range(octaves):
        out += value_noise(E, N, scale / (2 ** k), seed + k * 17) * amp
        total += amp
        amp *= 0.5
    return out / total


def voronoi(E, N, size, seed):
    """Voronoi de pontos sorteados em grade: (distância ao ponto mais perto, ao segundo, índice do bloco, centros dos
    blocos (L, N) por índice)."""
    rng = np.random.default_rng(seed)
    gx, gy = E / size, N / size
    ix, iy = np.floor(gx).astype(np.int64), np.floor(gy).astype(np.int64)
    ox, oy = ix.min() - 1, iy.min() - 1
    w, h = ix.max() - ox + 2, iy.max() - oy + 2
    jx, jy = rng.uniform(0.12, 0.88, (w, h)), rng.uniform(0.12, 0.88, (w, h))
    d1 = np.full(E.shape, 1e9)
    d2 = np.full(E.shape, 1e9)
    idx = np.zeros(E.shape, dtype=np.int64)
    for a in (-1, 0, 1):
        for b in (-1, 0, 1):
            cx, cy = ix + a - ox, iy + b - oy
            px = (cx + ox + jx[cx, cy]) * size
            py = (cy + oy + jy[cx, cy]) * size
            d = np.hypot(E - px, N - py)
            closer = d < d1
            d2 = np.where(closer, d1, np.minimum(d2, d))
            idx = np.where(closer, cx * h + cy, idx)
            d1 = np.where(closer, d, d1)
    centers = np.stack([((np.arange(w)[:, None] + ox + jx) * size).ravel(),
                        ((np.arange(h)[None, :] + oy + jy) * size).ravel()], axis=1)
    return d1, d2, idx, centers


def smoothstep(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3 - 2 * x)


def disk_filter(field, radius, op):
    """Máximo (op = np.maximum) ou mínimo num disco de 'radius' células."""
    out = field.copy()
    pad = np.pad(field, radius, mode='edge')
    nx, ny = field.shape
    for a in range(-radius, radius + 1):
        for b in range(-radius, radius + 1):
            if a * a + b * b > radius * radius + 0.5 or (a == 0 and b == 0):
                continue
            op(out, pad[radius + a:radius + a + nx, radius + b:radius + b + ny], out=out)
    return out


def closing(field, radius):
    return disk_filter(disk_filter(field, radius, np.maximum), radius, np.minimum)


def opening(field, radius):
    return disk_filter(disk_filter(field, radius, np.minimum), radius, np.maximum)


def per_block(values, mask, idx, count, how):
    """Valor de cada bloco (máximo, mínimo ou média dos valores das células marcadas dele), espalhado nas células
    marcadas (as outras ficam com NaN)."""
    out = np.full(values.shape, np.nan)
    if not mask.any():
        return out
    ids = idx[mask]
    vals = values[mask]
    if how == 'max':
        acc = np.full(count, -np.inf)
        np.maximum.at(acc, ids, vals)
    elif how == 'min':
        acc = np.full(count, np.inf)
        np.minimum.at(acc, ids, vals)
    else:
        acc = np.bincount(ids, weights=vals, minlength=count) / np.maximum(1, np.bincount(ids, minlength=count))
    out[mask] = acc[ids]
    return out


# ---------- o espaço do jogo vira limites de altura ----------

class Bounds:
    """Limites de cada célula. Miolo (embaixo de caminho, plataforma ou vila): a rocha fica exatamente no fundo, com
    prioridade sobre tudo. Paredão colado (rocha pelo menos até aqui), encosta do vale (de preferência até aqui) e teto
    da vista (no máximo até aqui) viram blocos de topo plano antes de entrar no mapa."""

    def __init__(self, grid):
        shape = (grid.nx, grid.ny)
        self.grid = grid
        self.wall = np.full(shape, -1e9)
        self.flank = np.full(shape, -1e9)
        self.cap = np.full(shape, 1e9)
        self.hard_lb = np.full(shape, -1e9)
        self.hard_ub = np.full(shape, 1e9)
        self.dplay = np.full(shape, 1e9)       # distância além da beira do jogo mais perto
        self.keep = np.full(shape, -1e9)       # paredão dos caminhos que vence os cortes da vista

    def window(self, box):
        i0, i1, j0, j1 = box
        return (slice(i0, i1), slice(j0, j1))


def apply_road(bounds, intent, road, samples):
    g = bounds.grid
    pts = np.array([[s[0].x, s[0].y] for s in samples])
    nrm = np.array([[s[1].x, s[1].y] for s in samples])
    tng = np.stack([-nrm[:, 1], nrm[:, 0]], axis=1)
    deck = np.array([s[2] for s in samples])
    bottom = np.array([s[3] for s in samples])
    half = np.array([s[4] for s in samples])
    walls = road.get('walls') or [(False, False)] * len(samples)
    wl = np.array([w[0] for w in walls])
    wr = np.array([w[1] for w in walls])
    views = road.get('view_sides') or [(True, True)] * len(samples)
    vl = np.array([v[0] for v in views])
    vr = np.array([v[1] for v in views])
    ss = np.array(road.get('sample_s', [0.0] * len(samples)))
    gap = np.zeros(len(samples), dtype=bool)
    for a, b in road.get('gaps', []):
        gap |= (ss >= a) & (ss <= b)
    support = road.get('support', True)
    flank = road.get('flank', True)
    far_l = g.sample(intent, pts[:, 0] - nrm[:, 0] * (half + 12.0), pts[:, 1] - nrm[:, 1] * (half + 12.0))
    far_r = g.sample(intent, pts[:, 0] + nrm[:, 0] * (half + 12.0), pts[:, 1] + nrm[:, 1] * (half + 12.0))
    wall_l = np.clip(far_l - deck, WALL_MIN, WALL_MAX)
    wall_r = np.clip(far_r - deck, WALL_MIN, WALL_MAX)

    tree = KDTree(len(pts))
    for k, p in enumerate(pts):
        tree.insert((p[0], p[1], 0.0), k)
    tree.balance()
    reach = REACH + half.max()
    e_min, n_min = pts.min(axis=0) - reach
    e_max, n_max = pts.max(axis=0) + reach
    win = bounds.window((max(0, int((e_min - g.e0) / CELL)), min(g.nx, int((e_max - g.e0) / CELL) + 1),
                         max(0, int((n_min - g.n0) / CELL)), min(g.ny, int((n_max - g.n0) / CELL) + 1)))
    E, N = g.E[win], g.N[win]
    flat_e, flat_n = E.ravel(), N.ravel()
    near = np.full(flat_e.shape, -1, dtype=np.int64)
    for c in range(len(flat_e)):
        _, k, d = tree.find((flat_e[c], flat_n[c], 0.0))
        if d is not None and d < reach:
            near[c] = k
    ok = near >= 0
    k = near[ok]
    q = np.stack([flat_e[ok], flat_n[ok]], axis=1) - pts[k]
    off = q[:, 0] * nrm[k, 0] + q[:, 1] * nrm[k, 1]
    along = q[:, 0] * tng[k, 0] + q[:, 1] * tng[k, 1]
    inside = np.abs(along) <= 1.0
    k, off = k[inside], off[inside]
    cells = np.nonzero(ok)[0][inside]
    d = np.abs(off) - half[k]
    right = off > 0
    side_wall = np.where(right, wr[k], wl[k])
    is_wall = side_wall & ~gap[k] & flank
    is_valley = ~side_wall & flank
    wall_h = np.where(right, wall_r[k], wall_l[k])

    wall, fl, cap = bounds.wall[win].ravel(), bounds.flank[win].ravel(), bounds.cap[win].ravel()
    keep = bounds.keep[win].ravel()
    hlb, hub = bounds.hard_lb[win].ravel(), bounds.hard_ub[win].ravel()
    dpl = bounds.dplay[win].ravel()
    np.minimum.at(dpl, cells, d)

    core = d <= 0.0
    clear = d <= np.where(is_wall, WALL_CLEAR, CLEAR_MARGIN)
    np.minimum.at(hub, cells[clear], bottom[k[clear]])
    if support:
        sup = core & ~gap[k]
        np.maximum.at(hlb, cells[sup], bottom[k[sup]])

    w = is_wall & (d > 0.0) & (d <= WALL_DEPTH + 30.0)
    np.maximum.at(wall, cells[w], deck[k[w]] + wall_h[w] - np.maximum(0.0, d[w] - WALL_DEPTH) * WALL_SLOPE)
    kz = is_wall & (d > WALL_CLEAR) & (d <= WALL_DEPTH + KEEP_H / KEEP_SLOPE)
    np.maximum.at(keep, cells[kz], deck[k[kz]] + np.minimum(wall_h[kz], KEEP_H) -
                  np.maximum(0.0, d[kz] - WALL_DEPTH) * KEEP_SLOPE)

    v = is_valley & (d > 0.0)
    dv = d[v]
    long_view = np.where(right, vr[k], vl[k])[v]
    reach_v = np.where(long_view, VALLEY_REACH, SHORT_REACH)
    rise_v = np.where(long_view, VALLEY_RISE, SHORT_RISE)
    np.minimum.at(cap, cells[v], deck[k[v]] - VALLEY_DROP - VALLEY_SLOPE * np.minimum(dv, reach_v) +
                  np.maximum(0.0, dv - reach_v) * rise_v)
    f = v & ~gap[k] & (d <= FLANK_REACH) & support
    np.maximum.at(fl, cells[f], bottom[k[f]] - 0.6 - FLANK_SLOPE * d[f])

    for arr, src in ((bounds.wall, wall), (bounds.flank, fl), (bounds.cap, cap), (bounds.hard_lb, hlb),
                     (bounds.hard_ub, hub), (bounds.dplay, dpl), (bounds.keep, keep)):
        arr[win] = src.reshape(E.shape)


def apply_disc(bounds, intent, disc):
    c, r, floor, bottom, city, support = disc
    g = bounds.grid
    win = bounds.window(g.box(c.x, c.y, r + REACH))
    E, N = g.E[win], g.N[win]
    dx, dy = E - c.x, N - c.y
    dist = np.hypot(dx, dy)
    d = dist - r
    np.minimum(bounds.dplay[win], d, out=bounds.dplay[win])
    core = d <= 0.0
    hub = bounds.hard_ub[win]
    clear = d <= CLEAR_MARGIN
    hub[clear] = np.minimum(hub[clear], bottom)
    cap = bounds.cap[win]
    if not support:                                   # a torre do portal: em pé no vale, sem encostar na rocha
        ring = (d > 0.0) & (d <= 12.0)
        cap[ring] = np.minimum(cap[ring], floor - 3.0)
        return
    hlb = bounds.hard_lb[win]
    hlb[core] = np.maximum(hlb[core], bottom)
    if city is None:
        return
    ux, uy = dx / np.maximum(dist, 1e-3), dy / np.maximum(dist, 1e-3)
    toward_city = ux * city.x + uy * city.y > math.cos(math.radians(v2.CITY_SECTOR))
    arc = np.zeros(d.shape, dtype=bool)
    for a0, span in OPEN_ARCS.get((round(c.x, 1), round(c.y, 1)), []):
        arc |= np.mod(np.arctan2(uy, ux) - a0, math.tau) <= span
    far = g.sample(intent, c.x + ux * (r + 12.0), c.y + uy * (r + 12.0))
    rises = ~toward_city & ~arc & (far > floor + 3.0)
    view = toward_city & (d > 0.0)
    dv = d[view]
    cap[view] = np.minimum(cap[view], floor - VALLEY_DROP - VALLEY_SLOPE * np.minimum(dv, VALLEY_REACH) +
                           np.maximum(0.0, dv - VALLEY_REACH) * VALLEY_RISE)
    near = arc & ~toward_city & (d > 0.0)          # arco aberto: vale curto, sem paredão
    dv = d[near]
    cap[near] = np.minimum(cap[near], floor - VALLEY_DROP - VALLEY_SLOPE * np.minimum(dv, SHORT_REACH) +
                           np.maximum(0.0, dv - SHORT_REACH) * SHORT_RISE)
    wall = bounds.wall[win]
    wz = rises & (d > 0.0) & (d <= WALL_DEPTH + 30.0)
    top = floor + np.clip(far - floor, WALL_MIN, WALL_MAX) - np.maximum(0.0, d - WALL_DEPTH) * WALL_SLOPE
    wall[wz] = np.maximum(wall[wz], top[wz])
    fl = bounds.flank[win]
    fz = ~rises & (d > 0.0) & (d <= FLANK_REACH)
    fl[fz] = np.maximum(fl[fz], bottom - 0.6 - FLANK_SLOPE * d[fz])


# Escadaria da Vila Pendurada (pedido do usuário em 2026-11-08): uma construção de cantaria solta na frente do paredão,
# como a escadaria da costa do Arquipélago. Na área dela (lances e voltas, com folga) a rocha fica num chão plano em
# STAIR_BASE; atrás (ao norte) o paredão sobe até perto do chão da vila; dos lados e na frente a rocha só volta a subir
# depois de STAIR_CLEAR metros, em encosta suave
STAIR_BASE = 39.0
STAIR_CLEAR = 6.0
STAIR_RISE = 1.0
STAIR_CLIFF = 96.0


def apply_stair(bounds):
    g = bounds.grid
    e0, e1 = mv.FLIGHT_E[0] - mv.SWITCH_PLATFORM - 0.6, mv.FLIGHT_E[1] + mv.SWITCH_PLATFORM + 0.6
    # Ao norte o paredão entra 0,3 m na cantaria do lance de cima (sem fresta atrás da escada)
    n0, n1 = mv.FLIGHT_N[-1] - mv.SWITCH_HALF - 0.6, mv.FLIGHT_N[0] + mv.SWITCH_HALF - 0.3
    win = bounds.window(g.box((e0 + e1) * 0.5, (n0 + n1) * 0.5, 140.0))
    E, N = g.E[win], g.N[win]
    rect = (E >= e0) & (E <= e1) & (N >= n0) & (N <= n1)
    hub, hlb, cap = bounds.hard_ub[win], bounds.hard_lb[win], bounds.cap[win]
    hub[rect] = np.minimum(hub[rect], STAIR_BASE)
    hlb[rect] = np.maximum(hlb[rect], STAIR_BASE - 0.5)
    # Atrás da escadaria (ao norte, do lado da vila) a rocha também sai (pedido do usuário em 2026-11-08): a escadaria
    # fica solta, e a vila no alto do próprio paredão; o que sustenta a vila e o caminho dela até a primeira volta
    # (limite de baixo alto) fica
    north = (N > n1) & (N <= n1 + 40.0) & (E >= e0 - 14.0) & (E <= e1 + 14.0) & (hlb < STAIR_BASE + 10.0)
    de_n = np.maximum(np.maximum(e0 - E, E - e1), 0.0)
    dn_n = np.hypot(de_n, N - n1)
    hub[north] = np.minimum(hub[north], STAIR_BASE + np.maximum(0.0, dn_n[north] - STAIR_CLEAR) * STAIR_RISE)
    # Na frente e dos lados: chão livre e depois encosta
    de = np.maximum(np.maximum(e0 - E, E - e1), 0.0)
    dn = np.maximum(n0 - N, 0.0)
    d = np.hypot(de, dn)
    side = ~rect & (N <= n1) & (d <= 90.0) & (hlb < STAIR_BASE + 10.0)
    cap[side] = np.minimum(cap[side], STAIR_BASE + np.maximum(0.0, d[side] - STAIR_CLEAR) * STAIR_RISE)
    hub[side & (d <= STAIR_CLEAR)] = np.minimum(hub[side & (d <= STAIR_CLEAR)], STAIR_BASE)
    for arr, src in ((bounds.hard_ub, hub), (bounds.hard_lb, hlb), (bounds.cap, cap)):
        arr[win] = src


def edge_fall(e, n):
    e0, e1, n0, n1 = DOMAIN
    edge = np.minimum(np.minimum(e - e0, e1 - e), np.minimum(n - n0, n1 - n))
    return BOTTOM + 20.0 + edge * 2.5


_BASE = {}


def base_blocks():
    """Os blocos de arenito sem o espaço do jogo (calculados uma vez; o lado do paredão de cada caminho olha para eles).
    Blocos grandes (BLOCK) com o topo plano na altura que a montanha quer no centro deles, mais um sorteio; os pequenos
    (SUB_BLOCK) quebram o topo e servem de unidade para o paredão, a encosta e o corte da vista."""
    if _BASE:
        return _BASE
    g = Grid()
    intent = np.minimum(intent_at(g.E, g.N), edge_fall(g.E, g.N))
    d1, d2, big, big_c = voronoi(g.E, g.N, BLOCK, 11)
    _, _, small, small_c = voronoi(g.E, g.N, SUB_BLOCK, 13)
    rng = np.random.default_rng(12)
    big_top = np.minimum(intent_at(big_c[:, 0], big_c[:, 1]), edge_fall(big_c[:, 0], big_c[:, 1])) + \
        rng.uniform(*BLOCK_DROP, len(big_c))
    small_drop = rng.uniform(*SUB_DROP, len(small_c))
    _BASE.update(g=g, intent=intent, d1=d1, d2=d2, small=small, small_c=small_c, rng=rng,
                 base=big_top[big] + small_drop[small])
    return _BASE


def road_walls(road, idx):
    """Lado do paredão (esquerda, direita) em cada amostra, pelos blocos da montanha (ver WALL_PROBE e MIN_RUN)."""
    pos, tan, deck, half = road['pos'], road['tan'], road['deck'], road['half']
    step = road.get('step', 0.25)
    blocks = base_blocks()
    g, base = blocks['g'], blocks['base']
    forced = [(a, b) for a, b, kind in road.get('forced', []) if kind == 'both']
    view_left_from = forced[0][1] if road['name'] == 'Terraco_Escalonada' and forced else None
    raw, views = [], []
    for i in idx:
        p, t = pos[i], tan[i]
        nrm = Vector((t.y, -t.x))
        s = i * step
        city = -p.normalized() if p.length > 1e-3 else Vector((0.0, -1.0))
        view = [-nrm.dot(city) > CITY_COS, nrm.dot(city) > CITY_COS]
        if view_left_from is not None and s > view_left_from:
            view = [True, False]
        views.append(tuple(view))
        if road.get('no_walls'):
            raw.append((False, False))
            continue
        if road.get('north_wall'):
            north_right = nrm.y > 0
            raw.append((not north_right, north_right))
            continue
        if any(a <= s <= b for a, b in forced):
            raw.append((True, True))
            continue
        rises = []
        for c, side in enumerate((-1.0, 1.0)):
            q = [p + nrm * side * (half + o) for o in WALL_PROBE]
            hs = g.sample(base, [v.x for v in q], [v.y for v in q])
            rises.append(not view[c] and float(np.median(hs)) > deck[i] + RISE)
        raw.append(tuple(rises))
    road['view_sides'] = views
    every = idx[1] - idx[0] if len(idx) > 1 else 1
    ds = step * every
    span = max(1, int(round(7.5 / ds)))
    min_len = int(round(MIN_RUN / ds))
    sides = []
    for c in range(2):
        vals = []
        for k in range(len(raw)):
            window = raw[max(0, k - span):k + span + 1]
            vals.append(sum(w[c] for w in window) * 2 > len(window))
        while True:
            runs, a = [], 0
            for k in range(1, len(vals) + 1):
                if k == len(vals) or vals[k] != vals[a]:
                    runs.append((a, k))
                    a = k
            short = [r for r in runs if r[1] - r[0] < min_len]
            if len(runs) <= 1 or not short:
                break
            a, b = min(short, key=lambda r: r[1] - r[0])
            for k in range(a, b):
                vals[k] = not vals[k]
        sides.append(vals)
    return list(zip(sides[0], sides[1]))


v2.road_walls = road_walls


def intent_at(e, n):
    e, n = np.asarray(e, dtype=float), np.asarray(n, dtype=float)
    return np.maximum(chain_field(e, n, mv.CRESTS, mv.CREST_FALL), chain_field(e, n, [v2.DOMINANT], v2.DOMINANT_FALL))


def height_map(space, roads):
    blocks = base_blocks()
    g, intent = blocks['g'], blocks['intent']
    log('mapa %d x %d' % (g.nx, g.ny))

    bounds = Bounds(g)
    for road in roads:
        first, last = road['sample_range']
        apply_road(bounds, intent, road, space.samples[first:last])
    for disc in space.discs:
        apply_disc(bounds, intent, disc)
    apply_stair(bounds)
    log('limites do jogo prontos')

    # Blocos de arenito (base_blocks)
    d1, d2, small, small_c = blocks['d1'], blocks['d2'], blocks['small'], blocks['small_c']
    rng = blocks['rng']
    base = blocks['base']

    count = len(small_c)
    wall_mask = bounds.wall > -1e8
    wall_blk = per_block(bounds.wall, wall_mask, small, count, 'max')
    wall_blk = wall_blk + rng.uniform(*WALL_BLOCK, count)[small]
    flank_mask = bounds.flank > -1e8
    flank_blk = per_block(bounds.flank, flank_mask, small, count, 'mean')
    cap_mask = bounds.cap < 1e8
    cap_blk = per_block(bounds.cap, cap_mask, small, count, 'min')
    keep_mask = bounds.keep > -1e8
    keep_blk = per_block(bounds.keep, keep_mask, small, count, 'max')

    h = base.copy()
    np.fmax(h, np.where(wall_mask, wall_blk, np.nan), out=h)
    np.fmax(h, np.where(flank_mask, flank_blk, np.nan), out=h)
    h = np.fmin(h, np.where(cap_mask, cap_blk, np.nan))
    # A garganta da Vila-Ponte: blocos com o centro dentro dela descem até o fundo
    gorge = mv.GORGE
    centers_in = np.array([mv.inside(gorge, e, n) for e, n in small_c])
    h = np.where(centers_in[small], np.minimum(h, -40.0), h)
    # Poços estreitos tapados e pontas finas aparadas (os limites do jogo têm a última palavra)
    h = closing(h, PIT_RADIUS)
    h = np.where(bounds.dplay < 30.0, closing(h, NEAR_PIT_RADIUS), h)
    h = opening(h, SPIKE_RADIUS)
    h = np.fmin(h, np.where(cap_mask, cap_blk, np.nan))
    # O paredão dos caminhos vence os cortes da vista (até KEEP_H acima do piso), em blocos como o resto
    h = np.fmax(h, np.where(keep_mask, keep_blk, np.nan))
    # Fendas rasas entre os blocos grandes, só longe do jogo
    crack = smoothstep((bounds.dplay - CRACK_FROM) / 20.0) * np.clip(1.0 - (d2 - d1) / CRACK_WIDTH, 0.0, 1.0)
    h -= crack * CRACK_DEPTH
    h = np.maximum(h, bounds.hard_lb)
    h = np.minimum(h, bounds.hard_ub)
    h = np.maximum(h, BOTTOM)
    log('mapa de altura: %.0f a %.0f m' % (h.min(), h.max()))
    return g, h, bounds


def solid_from(g, h):
    """Malha fechada: o mapa por cima, a borda descendo até o fundo e uma tampa embaixo."""
    nx, ny = g.nx, g.ny
    z = h.copy()
    z[0, :] = z[-1, :] = z[:, 0] = z[:, -1] = BOTTOM - 20.0
    co = np.empty((nx * ny, 3), dtype=np.float32)
    co[:, 0] = -g.E.ravel()
    co[:, 1] = -g.N.ravel()
    co[:, 2] = z.ravel()
    i, j = np.meshgrid(np.arange(nx - 1), np.arange(ny - 1), indexing='ij')
    a = (i * ny + j).ravel()
    quads = np.stack([a, a + ny, a + ny + 1, a + 1], axis=1).ravel()
    ring = ([i * ny for i in range(nx)] + [(nx - 1) * ny + j for j in range(1, ny)] +
            [i * ny + ny - 1 for i in range(nx - 2, -1, -1)] + [j for j in range(ny - 2, 0, -1)])
    mesh = bpy.data.meshes.new(NAME)
    mesh.vertices.add(nx * ny)
    mesh.vertices.foreach_set('co', co.ravel())
    nq = len(a)
    mesh.loops.add(nq * 4 + len(ring))
    mesh.loops.foreach_set('vertex_index', np.concatenate([quads, np.array(ring[::-1])]).astype(np.int32))
    mesh.polygons.add(nq + 1)
    starts = np.concatenate([np.arange(nq) * 4, [nq * 4]]).astype(np.int32)
    sizes = np.concatenate([np.full(nq, 4), [len(ring)]]).astype(np.int32)
    mesh.polygons.foreach_set('loop_start', starts)
    mesh.polygons.foreach_set('loop_total', sizes)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(NAME, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def sculpt(obj, field, seed, crack_depth=1.8, cell=0.022, ledge_height=13.0, ledge_depth=1.8, lumps=3.0):
    """O sculpt da versão 2 (blocos de Voronoi, fendas, degraus, lóbulos); perto do jogo só para dentro da rocha, até
    SCULPT_IN (a versão 2 parava em 1,2 m e as paredes ao lado do caminho ficavam lisas)."""
    from mathutils import noise
    mesh = obj.data
    mesh.update()
    normals = kit.smooth_normals(obj)
    world = mv.world_coords(obj)
    d, bottom, floor = field.lookup(world)
    weights = np.where(world[:, 2] > bottom - 3.0, np.clip((d - 0.5) / v2.GUARD, 0.0, 1.0), 1.0)
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
        # Relevo fino das paredes (blocos de ~9 m, fendas e frisos a cada ~4,5 m), sempre para dentro
        q2 = p * 0.11 + off
        dist2, _ = noise.voronoi(Vector((q2.x, q2.y, q2.z * 0.3)), distance_metric='DISTANCE')
        crack2 = max(0.0, 1.0 - (dist2[1] - dist2[0]) / 0.18) ** 2
        band2 = p.z / 4.5 + noise.noise(q2 * 0.6) * 0.8
        frieze = (band2 - math.floor(band2)) * 0.9
        detail = -side * (crack2 * 1.3 + frieze)
        disp = big + fine + side * (ledge + cushion - crack * crack_depth) + detail
        w = weights[v.index]
        if w < 1.0:
            inward = side * (ledge - 0.5 * ledge_depth + cushion - 1.0 - crack * crack_depth * 1.6) + min(0.0, big) +                 detail * 1.3
            lim = SCULPT_IN_LOW + (SCULPT_IN - SCULPT_IN_LOW) * min(1.0, max(0.0, (p.z - floor[v.index] - 2.0) / 6.0))
            disp = disp * w + max(-lim, min(0.0, inward)) * (1.0 - w)
        v.co = p + nrm * disp
    mesh.update()


# ---------- conferência ----------

def audit(obj, g, bounds, h, label):
    """Procura poços e lâminas a até 25 m do jogo na rocha pronta (raios de cima a cada 1 m)."""
    from mathutils.bvhtree import BVHTree
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(obj.matrix_world)
    bvh = BVHTree.FromBMesh(bm)
    bm.free()
    near = (bounds.dplay > 0.6) & (bounds.dplay < 25.0)
    band = (bounds.dplay > 0.6) & (bounds.dplay < 32.0)
    hm = h.copy()
    down = Vector((0.0, 0.0, -1.0))
    for i, j in zip(*np.nonzero(band)):
        hit = bvh.ray_cast(Vector((-g.e[i], -g.n[j], 1500.0)), down, 3000.0)
        hm[i, j] = hit[0].z if hit[0] is not None else BOTTOM
    pits = (closing(hm, PIT_RADIUS) - hm > 3.0) & near
    blades = (hm - opening(hm, SPIKE_RADIUS) > 5.0) & near
    log('conferência %s: %d células de poço, %d de lâmina perto do jogo' % (label, int(pits.sum()), int(blades.sum())))
    for name, mask in (('poço', pits), ('lâmina', blades)):
        for i, j in list(zip(*np.nonzero(mask)))[:: max(1, int(mask.sum() // 8) or 1)][:8]:
            log('   %s em L %.0f N %.0f, rocha a %.1f m' % (name, g.e[i], g.n[j], hm[i, j]))


# ---------- prévia com cor de jogo ----------

MARKS = v2.MARKS + [(15, -341.0, 595.1, 208.3, 97.0)]


def game_colors():
    """Só para a prévia: rocha marrom-avermelhada escura (juntas mais escuras pela cor do relevo) e neve onde a face
    olha para cima, como no jogo; o caminho em cinza claro."""
    for obj in bpy.data.objects:
        if obj.type != 'MESH':
            continue
        mesh = obj.data
        nv = len(mesh.vertices)
        nrm = np.empty(nv * 3, dtype=np.float32)
        mesh.vertex_normals.foreach_get('vector', nrm)
        nz = nrm.reshape(-1, 3)[:, 2]
        relief = mesh.color_attributes.get('Relevo')
        dark = np.ones(nv, dtype=np.float32)
        if relief is not None and relief.domain == 'POINT':
            col = np.empty(nv * 4, dtype=np.float32)
            relief.data.foreach_get('color', col)
            dark = 0.45 + 0.55 * col.reshape(-1, 4)[:, 0]
        c = np.empty((nv, 4), dtype=np.float32)
        c[:, 3] = 1.0
        if obj.name.startswith('PX_'):
            c[:, :3] = (0.62, 0.6, 0.58)
        elif obj.name.endswith('_Gelo'):
            c[:, :3] = (0.62, 0.8, 0.95)
        else:
            snow = nz > 0.72
            c[:, 0] = np.where(snow, 0.86, 0.30 * dark)
            c[:, 1] = np.where(snow, 0.86, 0.15 * dark)
            c[:, 2] = np.where(snow, 0.95, 0.12 * dark)
        look = mesh.color_attributes.new('Look', 'FLOAT_COLOR', 'POINT')
        look.data.foreach_set('color', c.ravel())
        mesh.color_attributes.active_color = look


def path_proxy(roads, space):
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
                    ring.append(bm.verts.new(v2.B(q.x, q.y, deck[k] + 0.05)))
            bm.faces.new((ring[0], ring[1], ring[3], ring[2]))
    for c, r, floor, bottom, city, _ in space.discs:
        ring = [bm.verts.new(v2.B(c.x + math.cos(a / 24 * math.tau) * r, c.y + math.sin(a / 24 * math.tau) * r,
                                  floor + 0.05)) for a in range(24)]
        bm.faces.new(ring)
    mesh = bpy.data.meshes.new('PX_Jogo')
    bm.to_mesh(mesh)
    bm.free()
    bpy.context.collection.objects.link(bpy.data.objects.new('PX_Jogo', mesh))


def preview(folder, roads, space, quick=False):
    """Visão geral, da cidade, de cima, os pontos marcados pelo usuário (10 a 15), quatro vistas ao longo de cada
    caminho comprido e, a cada 70 m, uma vista para cada lado do caminho (rápido: só o caminho do Terraço)."""
    os.makedirs(folder, exist_ok=True)
    path_proxy(roads, space)
    game_colors()
    scene = bpy.context.scene
    try:
        scene.render.engine = 'BLENDER_WORKBENCH'
    except TypeError:
        pass
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'VERTEX'
    scene.display.shading.show_cavity = True
    scene.display.render_aa = 'OFF'
    scene.render.resolution_x, scene.render.resolution_y = 1100, 620
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
        if 'platform' in road or (quick and road['name'] != 'Terraco_Escalonada'):
            continue
        pos, tan, deck = road['pos'], road['tan'], road['deck']
        step = road.get('step', 0.25)
        if len(pos) >= 160:
            for f in (0.15, 0.4, 0.65, 0.9):
                i = int(f * (len(pos) - 1))
                j = min(len(pos) - 1, i + 60)
                views.append(('%s_%02d' % (road['name'], int(f * 100)), (pos[i].x, pos[i].y, deck[i] + 1.8),
                              (pos[j].x, pos[j].y, deck[j] + 1.2), 16))
        for i in range(int(round(12.0 / step)), len(pos), max(1, int(round(70.0 / step)))):
            nrm = Vector((tan[i].y, -tan[i].x))
            for side, label in ((-1.0, 'esq'), (1.0, 'dir')):
                look = nrm * side * 30.0 + tan[i] * 8.0
                views.append(('lado_%s_%03d_%s' % (road['name'], int(i * step), label),
                              (pos[i].x, pos[i].y, deck[i] + 1.8),
                              (pos[i].x + look.x, pos[i].y + look.y, deck[i] - 4.0), 14))
    for name, eye, target, lens in views:
        cam.location = v2.B(*eye)
        cam.rotation_euler = (v2.B(*target) - v2.B(*eye)).to_track_quat('-Z', 'Y').to_euler()
        cam_data.lens = lens
        scene.render.filepath = os.path.join(folder, name + '.png')
        bpy.ops.render.render(write_still=True)
    log('prévia: %d vistas em %s' % (len(views), folder))


# ---------- principal ----------

def main(argv):
    if '--saida' in argv:
        kit.OUT = argv[argv.index('--saida') + 1]
        os.makedirs(kit.OUT, exist_ok=True)
    segments, _, landings, deck, bridge = mv.plan()
    kit.clear()
    pieces, flights = v2.switchback()
    roads = [v2.terrace_road()] + [v2.paved_road(spec) for spec in mv.paved_specs()] + pieces

    space = v2.Space()
    for road in roads:
        space.add_strip(road, valleys='platform' not in road)
        if 'platform' in road:
            road['flank'] = False
    o, l = deck
    extras = []
    for name, (a, b), half, drop in [('Deque', (o, l), mv.DECK_WIDTH * 0.5 + 1.0, 30.0),
                                     ('Ponte', bridge, 14.0, 34.0 - BOTTOM + 25.0)]:
        pos, tan, step = v2.straight(a.xy, b.xy, 0.5)
        dk = [a.z + (b.z - a.z) * k / (len(pos) - 1) for k in range(len(pos))]
        strip = {'name': name, 'pos': pos, 'tan': tan, 'deck': dk, 'half': half, 'step': step, 'gaps': [],
                 'support': False, 'flank': False}
        space.add_strip(strip, bottom_drop=drop, valleys=False)
        extras.append(strip)
    city = Vector((0.0, 0.0))
    for name, (c, r) in mv.V.items():
        if name == 'Terraco':
            space.add_disc(c, r + 4.0, c.z, c.z - 25.0, None, support=False)
            continue
        space.add_disc(c, r, c.z, c.z - FLOOR_DROP, (city - c.xy).normalized())
    for p in landings:
        space.add_disc(p, 7.0, p.z, p.z - FLOOR_DROP, None)
    space.build()
    log('espaço do jogo: %d amostras, %d discos' % (len(space.samples), len(space.discs)))

    # Pontes em arcos onde a montanha fica muito abaixo do caminho (como na versão 2, pela altura que ela quer ter)
    for road in roads:
        if 'platform' in road or road['name'].startswith('Zz_Lance'):
            continue
        pos, tan, dk, half = road['pos'], road['tan'], road['deck'], road['half']
        step = road.get('step', 0.25)
        gap_from = None
        for i in range(0, len(pos), int(round(1.0 / step))):
            nrm = Vector((tan[i].y, -tan[i].x))
            ground = max(v2.intent(*(pos[i] + nrm * o)) for o in (-half, 0.0, half))
            s = i * step
            if ground < dk[i] - 30.0:
                gap_from = s if gap_from is None else gap_from
            else:
                if gap_from is not None and s - gap_from >= 12.0:
                    road['gaps'].append((gap_from, s))
                gap_from = None
        if gap_from is not None and (len(pos) - 1) * step - gap_from >= 12.0:
            road['gaps'].append((gap_from, (len(pos) - 1) * step))
        if road['gaps']:
            log('%s: pontes em %s' % (road['name'], ', '.join('%.0f a %.0f m' % q for q in road['gaps'])))

    by_name = {r['name']: r for r in roads}
    ec = mv.V['Escalonada'][0]
    arrive, leave = by_name['Terraco_Escalonada']['pos'][-1], by_name['Escalonada_Sinos']['pos'][0]
    a, b = (math.atan2(q.y - ec.y, q.x - ec.x) for q in (arrive, leave))
    span = (a - b) % math.tau
    start = b if span <= math.pi else a
    OPEN_ARCS[(round(ec.x, 1), round(ec.y, 1))] = [(start, min(span, math.tau - span))]
    g, h, bounds = height_map(space, roads + extras)
    if '--tabela-escada' in argv:              # alturas em volta da escadaria da Vila Pendurada (conferência)
        print('E:   ' + ' '.join('%5d' % e for e in range(262, 362, 6)))
        for n in range(470, 388, -3):
            print('N%3d ' % n + ' '.join('%5.0f' % float(g.sample(h, e, n)) for e in range(262, 362, 6)))
    rock = solid_from(g, h)
    log('malha do mapa: %d faces' % len(rock.data.polygons))
    if '--so-mapa' in argv:
        audit(rock, g, bounds, h, 'mapa')
        if '--preview' in argv:
            preview(argv[argv.index('--preview') + 1], roads, space, quick=True)
        return
    bpy.ops.object.select_all(action='DESELECT')
    rock.select_set(True)
    bpy.context.view_layer.objects.active = rock
    rock.data.remesh_voxel_size = VOXEL
    rock.data.use_remesh_fix_poles = True
    bpy.ops.object.voxel_remesh()
    log('remesh', len(rock.data.polygons))
    mv.remove_fragments(rock)
    field = v2.Field(space)
    sculpt(rock, field, 28)
    log('esculpido')
    v2.write_layout(landings, deck, bridge, roads)
    collision = v2.finish(rock, space, field, deck, bridge)
    for obj in (rock, collision):
        v2.guarantee(obj, space, field)
        mv.remove_fragments(obj)
    kit.relief_colors(rock)
    spots = v2.check_corridor(rock, roads, delete=False)
    for e, n, f in spots[::max(1, len(spots) // 10)][:10]:
        log('   barrado em L %.0f N %.0f piso %.1f' % (e, n, f))
    v2.check_corridor(collision, roads, delete=True)
    audit(rock, g, bounds, h, 'rocha')
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
