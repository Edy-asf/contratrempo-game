# Água congelada da garganta da Vila-Ponte (Vilas do Alto; pedido do usuário em 2026-11-06): uma cachoeira gigante
# congelada que enche a fenda de parede a parede e o rio congelado no fundo da garganta, embaixo da Ponte dos Moleiros.
# Lê a rocha pronta (Models/Picos/Macico_Vilas.fbx) por raios e grava Models/Picos/Gelo_Garganta.fbx, que o Unity põe
# junto do maciço (mesma origem, PicosHighlands.PlaceGorgeIce), trocando os materiais pelo nome:
#   - Gelo_Garganta_Cascata (MAT_Gelo_Cascata): a frente da cascata na boca da fenda (n ~698), de uma parede à outra, do
#     fundo até o alto da cabeceira (~436 m), recuando com as paredes; tubos de órgão, um arco de barriga no meio e
#     lábios de escorrido a cada 14 a 30 m, com pingentes embaixo;
#   - Gelo_Garganta_Rio (MAT_Gelo_Lago em cima, MAT_Gelo_Cascata nas faces íngremes): campo de alturas de 1 m no fundo
#     da garganta, do pé da cascata (um leque de gelo que sobe ~22 m até ela) até um pouco antes da beira no sul, onde
#     termina numa língua que desce até a rocha (nada passa da beira: em 2026-11-07 o usuário tirou a queda, que cobria
#     os blocos de rocha lá embaixo); nível plano que só desce rio abaixo, cristas de pressão, colares em volta dos
#     pilares da ponte (que ficam em cima do gelo), as pedras mais altas furando e uma saia de 1,5 m nas beiras soltas;
#   - Gelo_Garganta_Pingentes (MAT_Gelo_Cascata, sem colisão no Unity).
# UV projetada pelo mundo (16 px/m): faces de pé pela horizontal e pela altura (cascata 4 x 8 m), as de cima pelo plano
# (lago 4 x 4 m). Coordenadas da planta (leste, norte, altura); Blender = (-leste, -norte, altura).
#   blender.exe -b --factory-startup --gpu-backend opengl --python Ferramentas/Picos/gelo_garganta.py --
#       [--saida <pasta>] [--preview <pasta>]
import math
import os
import random
import sys
import time

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', 'Icaro', 'Models', 'Picos'))
NAME = 'Gelo_Garganta'
SOURCE = os.path.join(OUT, 'Macico_Vilas.fbx')

# Ponte dos Moleiros (PicosHighlands.BuildBridgeVillage): tabuleiro de a até b a 108 m, pilares a 1/3 e 2/3
DECK_A, DECK_B, DECK_H, DECK_W = (145.943, 505.825), (242.057, 519.175), 108.0, 24.0
PIER_W, PIER_D = 21.6, 8.0

# Fundo da garganta (grade do rio)
E0, E1, N0, N1 = 140, 262, 360, 699
PROBE = -12.0            # os raios do fundo saem daqui (abaixo do alto do leque; dentro da parede = rocha)
FAN_N0, FAN_N1, FAN_H = 604.0, 684.0, 24.0
LEVEL_SLOPE = 0.012      # o rio desce 1,2 cm por metro para o sul
# Fim do rio (pedido do usuário em 2026-11-07: o gelo que descia pela beira cobria os blocos de rocha lá embaixo, onde
# não faz sentido ter água): o rio termina antes do primeiro buraco fundo perto da beira (rocha mais de DEEP m abaixo do
# nível) e desce numa língua de TONGUE m até a rocha; nada passa da beira
DEEP, TONGUE, END_SEARCH = 30.0, 10.0, 40

# Frente da cascata: n da frente por altura (recua com as paredes da fenda: abaixo de ~290 m as duas paredes existem
# na boca, acima a boca vai para o norte até a cabeceira)
FRONT = [(-46, 684.0), (282, 686.0), (296, 712.0), (352, 716.0), (366, 722.0), (378, 757.0), (428, 767.0),
         (436, 773.0), (440, 782.0)]
# Gelo nas paredes da fenda (respingo congelado): de COAT_N0 até a frente, mais grosso e mais alto perto da cascata
COAT_N0, COAT_STEP = 596.0, 1.5
FACE_Z0, FACE_Z1, FACE_COLS = -46, 440, 30
T0 = time.time()


def log(*a):
    print('[%6.1fs]' % (time.time() - T0), *a, flush=True)


def B(e, n, z):
    return Vector((-e, -n, z))


class Rock:
    def __init__(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=SOURCE)
        self.objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        col = [o for o in self.objects if o.name.endswith('_Colisao')] or \
              [o for o in self.objects if not o.name.endswith('_Gelo')]
        verts, faces = [], []
        for o in col:
            mw = o.matrix_world
            base = len(verts)
            verts += [mw @ v.co for v in o.data.vertices]
            faces += [tuple(base + i for i in p.vertices) for p in o.data.polygons]
        self.bvh = BVHTree.FromPolygons(verts, faces)
        log('rocha: %d faces' % len(faces))

    def cast(self, o, d, dist):
        hit = self.bvh.ray_cast(o, d, dist)
        return None if hit[0] is None else hit

    def floor(self, e, n, z_from):
        """Altura do chão abaixo de (e, n, z_from): None se o raio não acha nada, 'rock' se o ponto está dentro da rocha."""
        hit = self.cast(B(e, n, z_from), Vector((0, 0, -1)), 600.0)
        if hit is None:
            return None
        if hit[1].z < 0.0:
            return 'rock'
        return hit[0].z


def interp(table, x):
    if x <= table[0][0]:
        return table[0][1]
    for (x0, y0), (x1, y1) in zip(table, table[1:]):
        if x <= x1:
            return y0 + (y1 - y0) * (x - x0) / (x1 - x0)
    return table[-1][1]


def smoothstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


# ---------- bmesh com UV pelo mundo e material por inclinação ----------

class Builder:
    def __init__(self, name, materials):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UVMap')
        self.materials = materials        # nomes; índice do material por face

    def vert(self, e, n, z):
        return self.bm.verts.new(B(e, n, z))

    def face(self, verts, material=None):
        try:
            f = self.bm.faces.new(verts)
        except ValueError:
            return None
        f.normal_update()
        if material is not None:
            f.material_index = material
        return f

    def finish(self, slope_material=None, sharp_angle=48.0, closed=False):
        bm = self.bm
        if closed:      # peças fechadas (pingentes): normais para fora
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.normal_update()
        for f in bm.faces:
            nrm = f.normal
            ax, ay, az = abs(nrm.x), abs(nrm.y), abs(nrm.z)
            if slope_material is not None:
                f.material_index = slope_material[0] if nrm.z > 0.6 else slope_material[1]
            tile_v = 4.0 if self.materials[f.material_index] == 'MAT_Gelo_Lago' else 8.0
            for loop in f.loops:
                p = loop.vert.co
                e, n, z = -p.x, -p.y, p.z
                if az >= ax and az >= ay:
                    uv = (e / 4.0, n / 4.0)
                elif ax >= ay:
                    uv = (n / 4.0 * (1 if nrm.x < 0 else -1), z / tile_v)
                else:
                    uv = (e / 4.0 * (1 if nrm.y > 0 else -1), z / tile_v)
                loop[self.uv].uv = uv
            f.smooth = True
        cos_sharp = math.cos(math.radians(sharp_angle))
        for edge in bm.edges:
            if len(edge.link_faces) == 2:
                a, b = edge.link_faces
                edge.smooth = a.normal.dot(b.normal) > cos_sharp
        mesh = bpy.data.meshes.new(self.name)
        bm.to_mesh(mesh)
        bm.free()
        for m in self.materials:
            mat = bpy.data.materials.get(m) or bpy.data.materials.new(m)
            mesh.materials.append(mat)
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        log(self.name, len(mesh.polygons), 'faces')
        return obj


# ---------- rio congelado ----------

def pier_frames():
    ax, ay = DECK_B[0] - DECK_A[0], DECK_B[1] - DECK_A[1]
    length = math.hypot(ax, ay)
    along = (ax / length, ay / length)
    right = (along[1], -along[0])            # Cross(up, along) no Unity: para o sul
    return [((DECK_A[0] + ax * k / 3, DECK_A[1] + ay * k / 3), along, right) for k in (1, 2)]


def collar(e, n, piers):
    best = 0.0
    for (c, along, right) in piers:
        de, dn = e - c[0], n - c[1]
        t = abs(de * along[0] + dn * along[1]) - PIER_D / 2
        s = abs(de * right[0] + dn * right[1]) - PIER_W / 2
        out = math.hypot(max(t, 0.0), max(s, 0.0))
        best = max(best, 1.7 * math.exp(-(out / 2.4) ** 2) + 0.5 * math.exp(-(out / 6.0) ** 2))
    return best


def build_river(rock):
    es = np.arange(E0, E1 + 1, 1.0)
    ns = np.arange(N0, N1 + 1, 1.0)
    F = [[rock.floor(e, n, PROBE) for e in es] for n in ns]
    log('fundo amostrado')

    # Beira do sul: a menor linha a partir da qual o fundo da garganta (mediana) fica acima de -60
    def med(row):
        vals = [v for v in row if isinstance(v, float) and v < -20]
        return np.median(vals) if vals else None

    lip_i = len(ns) - 1
    for i in range(len(ns) - 1, -1, -1):
        m = med(F[i])
        if m is None or m < -60:
            break
        lip_i = i
    n_lip = ns[lip_i]
    log('beira do sul em n = %.0f' % n_lip)

    # Nível: percentil 85 do fundo de cada linha + 0,4, suavizado, e nunca descendo rio acima (o rio corre para o sul)
    level = np.full(len(ns), np.nan)
    for i in range(lip_i, len(ns)):
        vals = [v for v in F[i] if isinstance(v, float) and v < -20]
        if vals:
            level[i] = np.percentile(vals, 85) + 0.4
    valid = ~np.isnan(level)
    level[~valid] = np.interp(np.flatnonzero(~valid), np.flatnonzero(valid), level[valid])
    k = 15
    smooth = np.convolve(np.pad(level, k, mode='edge'), np.ones(2 * k + 1) / (2 * k + 1), mode='same')[k:-k]
    smooth[lip_i:] += LEVEL_SLOPE * (ns[lip_i:] - ns[lip_i])
    level = np.maximum.accumulate(smooth[lip_i:])
    level = np.concatenate([np.full(lip_i, level[0]), level])
    log('nível do rio: %.1f (sul) a %.1f (norte)' % (level[lip_i], level[-1]))

    piers = pier_frames()
    rnd = random.Random(7121)
    ridges = []
    for _ in range(9):
        cn = rnd.uniform(n_lip + 10, FAN_N0 - 5)
        ang = rnd.uniform(-0.6, 0.6)
        ridges.append((rnd.uniform(170, 230), cn, math.cos(ang), math.sin(ang), rnd.uniform(0.25, 0.6), rnd.uniform(14, 40)))
    waves = [(rnd.uniform(0, 6.28), rnd.uniform(0.05, 0.12), rnd.uniform(0.05, 0.12)) for _ in range(4)]

    # Linha do fim: logo ao norte do buraco fundo mais ao norte, até END_SEARCH linhas da beira
    i_end = lip_i
    for i in range(lip_i, min(len(ns), lip_i + END_SEARCH)):
        if any(isinstance(f, float) and f < level[i] - DEEP for f in F[i]):
            i_end = i + 1
    n_end = ns[i_end]
    log('fim do rio em n = %.0f' % n_end)

    def ice_height(e, n, i):
        h = level[i]
        if n > FAN_N0:
            u = (n - FAN_N0) / (FAN_N1 - FAN_N0)
            h += FAN_H * (smoothstep(0, 1, u) ** 1.6)
            h += 0.8 * math.sin(e * 0.45 + n * 0.08) * u     # degraus de escorrido no leque
        h += collar(e, n, piers)
        for (ph, fe, fn) in waves:
            h += 0.06 * math.sin(e * fe + n * fn + ph)
        for (re, rn, ce, cn, height, half) in ridges:     # cristas de pressão atravessando o rio
            de, dn = e - re, n - rn
            along = de * ce + dn * cn
            across = abs(-de * cn + dn * ce)
            if abs(along) < half and across < 1.4:
                h += height * (1 - across / 1.4) * (1 - (abs(along) / half) ** 4)
        return h

    H = np.zeros((len(ns), len(es)))
    ice = np.zeros((len(ns), len(es)), bool)
    for i in range(i_end, len(ns)):
        n = ns[i]
        for j, e in enumerate(es):
            f = F[i][j]
            h = ice_height(e, n, i)
            if isinstance(f, float) and n < n_end + TONGUE:
                # língua do fim: do nível até 0,5 m acima da rocha na linha do fim
                h = f + 0.5 + (h - f - 0.5) * smoothstep(n_end, n_end + TONGUE, n)
            H[i, j] = h
            ice[i, j] = isinstance(f, float) and f < h - 0.05

    b = Builder(NAME + '_Rio', ['MAT_Gelo_Lago', 'MAT_Gelo_Cascata'])
    verts = {}

    def v(i, j):
        if (i, j) not in verts:
            verts[(i, j)] = b.vert(es[j], ns[i], H[i, j])
        return verts[(i, j)]

    for i in range(i_end, len(ns) - 1):
        for j in range(len(es) - 1):
            corners = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            if any(ice[c] for c in corners):
                # ordem anti-horária vista de cima, no Blender (x = -leste, y = -norte)
                b.face([v(*corners[0]), v(*corners[3]), v(*corners[2]), v(*corners[1])])

    # Saia: cada beira solta do gelo desce 1,5 m (na língua do fim fica rente à rocha; junto das paredes, dentro dela),
    # para a beira nunca aparecer solta no ar; a face olha para fora
    low = {}
    skirt = 0
    for edge in [ed for ed in b.bm.edges if len(ed.link_faces) == 1]:
        loop = edge.link_loops[0]
        a, c = loop.vert, loop.link_loop_next.vert
        for vt in (a, c):
            if vt not in low:
                low[vt] = b.bm.verts.new(vt.co - Vector((0, 0, 1.5)))
        if b.face([a, low[a], low[c], c], 1):
            skirt += 1
    log('saia do rio: %d faces' % skirt)
    return b.finish(slope_material=(0, 1))


# ---------- cascata ----------

def build_falls(rock):
    rnd = random.Random(4417)
    zs = np.arange(FACE_Z0, FACE_Z1 + 0.1, 1.0)
    rows = []
    center = 196.0
    last = None
    for z in zs:
        nf = interp(FRONT, z)
        found = None
        for c in (center, 194.0, 196.0, 191.0, 199.0, 188.0, 202.0):
            o = B(c, nf, z)
            east = rock.cast(o, Vector((-1, 0, 0)), 70.0)
            west = rock.cast(o, Vector((1, 0, 0)), 70.0)
            if east is None or west is None or east[1].x < 0 or west[1].x > 0:
                continue          # sem uma das paredes ou o ponto está dentro da rocha
            found = (c - west[3], c + east[3])
            break
        if found is None and last is not None:
            found = last
        if found is None:
            continue
        last = found
        center = 0.5 * (found[0] + found[1])
        rows.append((z, nf, found[0], found[1]))
    # Paredes sem picos de uma linha para a outra (mediana de 5), sempre um pouco para dentro da rocha
    w = np.array([r[2] for r in rows])
    ea = np.array([r[3] for r in rows])
    wm = np.array([np.min(w[max(0, i - 2):i + 3]) for i in range(len(w))])
    em = np.array([np.max(ea[max(0, i - 2):i + 3]) for i in range(len(ea))])
    # Onde a frente recua para o norte (z ~350 a 380), a fenda alarga de repente e a cascata saía para o lado por cima da
    # rocha (pedido do usuário em 2026-11-07: tirar esse "cogumelo"): cada linha fica no máximo WIDEN m além da parede
    # mais estreita das linhas a até WINDOW m acima ou abaixo (a faixa larga tem ~25 m de altura)
    WINDOW, WIDEN = 25, 3.0
    wm = np.array([max(wm[i], np.max(wm[max(0, i - WINDOW):i + WINDOW + 1]) - WIDEN) for i in range(len(wm))])
    em = np.array([min(em[i], np.min(em[max(0, i - WINDOW):i + WINDOW + 1]) + WIDEN) for i in range(len(em))])
    log('cascata: %d linhas, largura %.0f a %.0f m' % (len(rows), float(np.min(em - wm)), float(np.max(em - wm))))

    pipes = [(rnd.uniform(0.04, 0.96), rnd.uniform(0.06, 0.16), rnd.uniform(1.5, 4.5), rnd.uniform(0, 6.28),
              rnd.uniform(25, 60)) for _ in range(11)]
    lips = []
    z = FACE_Z0 + rnd.uniform(30, 40)
    while z < FACE_Z1 - 10:
        lips.append(z)
        z += rnd.uniform(24, 45)

    def bulge(s, z, width):
        edge = smoothstep(0.0, 0.14, s) * smoothstep(1.0, 0.86, s)
        out = min(3.2, width * 0.12) * math.sin(math.pi * s)
        for (s0, r, a, ph, per) in pipes:
            sc = s0 + 0.035 * math.sin(z / per + ph)
            d = (s - sc) / r
            if abs(d) < 1:
                out += a * math.sqrt(1 - d * d)
        for zl in lips:
            if zl - 3.0 <= z <= zl:
                out += 1.1 * (z - (zl - 3.0)) / 3.0
        return edge * out

    b = Builder(NAME + '_Cascata', ['MAT_Gelo_Cascata'])
    grid = []
    for (z, nf, _, _), we, ee in zip(rows, wm, em):
        width = ee - we
        line = []
        for k in range(FACE_COLS + 1):
            s = k / FACE_COLS
            e = (we - 1.6) + (width + 3.2) * s
            line.append((e, nf - bulge(s, z, width), z))
        grid.append(line)
    vs = [[b.vert(*p) for p in line] for line in grid]
    for i in range(len(vs) - 1):
        for k in range(FACE_COLS):
            # frente virada para o sul (+y no Blender)
            b.face([vs[i][k], vs[i][k + 1], vs[i + 1][k + 1], vs[i + 1][k]])
    coat_walls(rock, b, rnd)
    obj = b.finish()

    # Pingentes debaixo dos lábios
    p = Builder(NAME + '_Pingentes', ['MAT_Gelo_Cascata'])
    count = 0
    for zl in lips:
        i = int(round(zl - FACE_Z0))
        if i >= len(grid):
            continue
        for _ in range(rnd.randint(10, 18)):
            s = rnd.uniform(0.12, 0.88)
            k = s * FACE_COLS
            k0 = min(FACE_COLS - 1, int(k))
            a, c = grid[i][k0], grid[i][k0 + 1]
            f = k - k0
            top = Vector((a[0] + (c[0] - a[0]) * f, a[1] + (c[1] - a[1]) * f - 0.25, zl - 0.2))
            icicle(p, top, rnd.uniform(1.2, 5.0), rnd.uniform(0.18, 0.5), rnd)
            count += 1
    log('pingentes:', count)
    return obj, p.finish(sharp_angle=40.0, closed=True)


def walls_at(rock, n, z, center):
    """Paredes da fenda em (n, z), por raios para leste e oeste a partir de um ponto no vão: (oeste, leste) ou None."""
    for c in (center, 194.0, 196.0, 191.0, 199.0, 188.0, 202.0, 185.0, 205.0):
        o = B(c, n, z)
        east = rock.cast(o, Vector((-1, 0, 0)), 45.0)
        west = rock.cast(o, Vector((1, 0, 0)), 45.0)
        if east is None or west is None or east[1].x < 0 or west[1].x > 0:
            continue
        return c - west[3], c + east[3]
    return None


def coat_walls(rock, b, rnd):
    """Respingo congelado nas duas paredes da fenda, da boca da cascata para o sul: gelo de 0,3 a 3,4 m de grossura,
    em caneluras verticais, do rio até uma altura que sobe de ~45 m (sul) a ~270 m (junto da cascata), com a beira de
    cima recortada."""
    front = FRONT[0][1]
    ns = np.arange(COAT_N0, front + 0.5, COAT_STEP)
    zs = np.arange(-34.0, 292.0, COAT_STEP)
    phase = [rnd.uniform(0, 6.28) for _ in range(6)]
    tops = []
    for n in ns:
        u = (n - COAT_N0) / (front - COAT_N0)
        wobble = 14 * math.sin(n * 0.21 + phase[0]) + 8 * math.sin(n * 0.57 + phase[1]) + 5 * math.sin(n * 1.31 + phase[2])
        tops.append(45 + 225 * u ** 1.4 + wobble * (0.4 + 0.6 * u))
    W = np.full((len(ns), len(zs)), np.nan)
    E = np.full((len(ns), len(zs)), np.nan)
    # Cada lado para de subir quando a parede "pula" mais de WALL_JUMP m de uma linha para a de cima: a parede acabou
    # (topo dela) e o raio achou outra rocha mais longe; sem isso, o gelo saía para o lado acima do topo da parede
    WALL_JUMP = 2.5
    for i, n in enumerate(ns):
        center = 196.0
        ended = [False, False]
        prev = [None, None]
        for k, z in enumerate(zs):
            if z > tops[i] + COAT_STEP or all(ended):
                break
            found = walls_at(rock, n, z, center)
            if found is None:
                continue
            for side, grid in ((0, W), (1, E)):
                if ended[side]:
                    continue
                if prev[side] is not None and abs(found[side] - prev[side]) > WALL_JUMP:
                    ended[side] = True
                    continue
                grid[i, k] = found[side]
                prev[side] = found[side]
            center = 0.5 * (found[0] + found[1])

    # Falhas curtas (raio que não achou a parede) preenchidas na vertical e na horizontal, para o gelo não ficar com
    # buracos retangulares
    for grid in (W, E):
        for _ in range(2):
            for axis in (1, 0):
                g = grid if axis == 1 else grid.T
                for line in g:
                    idx = np.flatnonzero(~np.isnan(line))
                    for i0, i1 in zip(idx, idx[1:]):
                        if 1 < i1 - i0 <= 5:
                            line[i0 + 1:i1] = np.interp(np.arange(i0 + 1, i1), [i0, i1], [line[i0], line[i1]])

    def thickness(i, k, side):
        n, z = ns[i], zs[k]
        u = (n - COAT_N0) / (front - COAT_N0)
        flute = 0.55 + 0.45 * math.sin(n * 0.83 + side * 1.7 + 0.6 * math.sin(z * 0.05 + phase[3]))
        top_fade = smoothstep(tops[i] + 2, tops[i] - 10, z)
        return 0.3 + (0.6 + 2.8 * u ** 1.5) * flute * top_fade

    faces = 0
    for side, grid in ((-1, W), (1, E)):
        verts = {}

        def v(i, k):
            if (i, k) not in verts:
                e = grid[i, k] - side * thickness(i, k, side)
                verts[(i, k)] = b.vert(e, ns[i], zs[k])
            return verts[(i, k)]

        for i in range(len(ns) - 1):
            for k in range(len(zs) - 1):
                corners = [(i, k), (i + 1, k), (i + 1, k + 1), (i, k + 1)]
                if any(np.isnan(grid[c]) for c in corners):
                    continue
                quad = [v(*c) for c in corners]
                if side > 0:
                    quad.reverse()      # parede leste: a face olha para o oeste
                if b.face(quad):
                    faces += 1
    log('gelo nas paredes: %d faces' % faces)


def icicle(b, top, length, radius, rnd, sides=6):
    """Cone pendurado em 'top' (planta), com a ponta um pouco desviada."""
    e, n, z = top
    tip = b.vert(e + rnd.uniform(-0.2, 0.2) * length * 0.2, n + rnd.uniform(-0.2, 0.2) * length * 0.2, z - length)
    ring = []
    rot = rnd.uniform(0, math.tau)
    for k in range(sides):
        a = rot + math.tau * k / sides
        r = radius * rnd.uniform(0.8, 1.15)
        ring.append(b.vert(e + math.cos(a) * r, n + math.sin(a) * r, z + rnd.uniform(-0.1, 0.1)))
    for k in range(sides):
        b.face([ring[k], ring[(k + 1) % sides], tip])
    b.face(list(reversed(ring)))


# ---------- prévia ----------

def preview(folder, rock, objs):
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    for o in rock.objects:
        o.color = (0.34, 0.33, 0.37, 1)
        o.hide_render = o.name.endswith('_Colisao') or '--sem-rocha' in sys.argv
    colors = {'Cascata': (0.62, 0.82, 0.96, 1), 'Rio': (0.45, 0.66, 0.85, 1), 'Pingentes': (0.8, 0.92, 1.0, 1)}
    for o in objs:
        o.color = next(c for k, c in colors.items() if k in o.name)
    # Ponte e pilares em bloco, para dar a escala
    bm = bmesh.new()
    ax, ay = DECK_B[0] - DECK_A[0], DECK_B[1] - DECK_A[1]
    length = math.hypot(ax, ay)
    al = Vector((-ax / length, -ay / length, 0))
    rt = Vector((-(ay / length), ax / length, 0))      # (along.y, -along.x) da planta, em Blender
    def box(center, half_a, half_r, z0, z1):
        cs = [center + al * sa * half_a + rt * sr * half_r for sa, sr in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        bot = [bm.verts.new((c.x, c.y, z0)) for c in cs]
        top = [bm.verts.new((c.x, c.y, z1)) for c in cs]
        bm.faces.new(top)
        bm.faces.new(list(reversed(bot)))
        for k in range(4):
            bm.faces.new([bot[k], bot[(k + 1) % 4], top[(k + 1) % 4], top[k]])
    mid = B((DECK_A[0] + DECK_B[0]) / 2, (DECK_A[1] + DECK_B[1]) / 2, 0)
    box(mid, length / 2, DECK_W / 2, DECK_H - 2.5, DECK_H)
    for (c, _, _) in pier_frames():
        box(B(c[0], c[1], 0), PIER_D / 2, PIER_W / 2, -70.0, DECK_H - 2.5)
    mesh = bpy.data.meshes.new('PREVIEW_Ponte')
    bm.to_mesh(mesh)
    bridge = bpy.data.objects.new('PREVIEW_Ponte', mesh)
    scene.collection.objects.link(bridge)
    bridge.color = (0.7, 0.62, 0.5, 1)

    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'OBJECT'
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    world = bpy.data.worlds.new('W')
    scene.world = world
    world.color = (0.3, 0.33, 0.42)
    scene.render.resolution_x, scene.render.resolution_y = 1280, 800
    cam_data = bpy.data.cameras.new('cam')
    cam_data.clip_end = 4000
    cam = bpy.data.objects.new('cam', cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    views = {
        'ponte_norte': ((194, 528, 111), (196, 690, 70), 22),
        'ponte_baixo': ((200, 528, 111), (196, 580, -30), 20),
        'ponte_sul': ((194, 496, 111), (196, 420, -70), 24),
        'cascata_perto': ((196, 590, 0), (196, 684, 60), 20),
        'geral': ((330, 330, 340), (196, 600, 20), 30),
        'alto': ((196, 560, 700), (196, 600, 0), 24),
        'fim_do_rio': ((232, 478, 40), (196, 428, -55), 22),
        'cascata_alto': ((236, 650, 330), (195, 722, 365), 24),
    }
    for name, (eye, target, lens) in views.items():
        cam_data.lens = lens
        cam.location = B(*eye)
        d = B(*target) - B(*eye)
        cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(folder, name + '.png')
        bpy.ops.render.render(write_still=True)
        log('prévia', name)
    bpy.data.objects.remove(bridge)


def main(argv):
    out = argv[argv.index('--saida') + 1] if '--saida' in argv else OUT
    rock = Rock()
    river = build_river(rock)
    falls, icicles = build_falls(rock)
    objs = [river, falls, icicles]
    os.makedirs(out, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    path = os.path.join(out, NAME + '.fbx')
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL',
                             mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
    log('exportado', path)
    if '--preview' in argv:
        preview(argv[argv.index('--preview') + 1], rock, objs)


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
