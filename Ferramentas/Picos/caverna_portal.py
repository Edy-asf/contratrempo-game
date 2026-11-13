# Caverna do portal das Vilas do Alto (pedido do usuário em 2026-11-12): o portal gêmeo do Terraço do Portal passa a
# ficar dentro de uma caverna, iluminada só pelo brilho dele. O portal, a torre e o caminho não mudam: a montanha ganha
# um teto. Como o remendo do platô (remendo_plato.py), é uma peça à parte que o Unity põe junto do maciço, sem gerar a
# montanha de novo (gerar o maciço inteiro travou o computador do usuário duas vezes):
#   - salão grande em volta da torre, com ela em pé sobre o vão escuro e a ponte do caminho atravessando;
#   - a escadaria da fenda vira um túnel natural mais baixo (teto em arco ~9 m acima do piso, as paredes são as da
#     fenda), até a boca no ponto mais alto do caminho, logo antes da curvinha que começa a descida;
#   - boca natural, recortada, um pouco maior que o caminho, com pingentes no lábio de cima;
#   - colunas de rocha no salão (silhuetas contra o brilho do portal), estalactites no teto, estalagmites no chão longe
#     do caminho e pingentes de gelo (objeto _Gelo, sem colisão).
# Mapa duplo de altura numa grade de 1 m: a peça é o sólido entre o fundo C e o topo T. No salão e no túnel C é o teto
# (ar embaixo dele até a rocha que já existe); em volta, C desce para dentro da rocha (a parede da caverna) e T cai em
# blocos de arenito até sumir dentro da montanha. Depois: remesh de voxel, a escultura do macico_vilas3.py (perto do
# caminho e da torre só para dentro da rocha), a cor do relevo e a marca da neve lateral.
# Grava também a planta (Macico_Vilas_Caverna.json) que o Unity lê para escurecer a luz dentro da caverna.
#   blender.exe -b --factory-startup --python Ferramentas/Picos/caverna_portal.py -- [--saida <pasta>] [--conferir]
#       [--preview <pasta>]   (o render precisa de --gpu-backend opengl antes do --python neste PC)
import json
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
from mathutils.kdtree import KDTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import montanhas_destaque as kit  # noqa: E402
import macico_vilas3 as mv3  # noqa: E402
import neve_lateral  # noqa: E402

NAME = 'Macico_Vilas_Caverna'
SOURCE = os.path.join(kit.OUT, 'Macico_Vilas.fbx')
LAYOUT = os.path.join(kit.OUT, 'Macico_Vilas_Layout.json')
ROAD = 'Terraco_Escalonada'
DOMAIN = (-585.0, -175.0, 640.0, 930.0)     # E de, E até, N de, N até (planta: leste, norte)

# ---------- salão ----------
TOWER, TOWER_R, DECK = (-422.6, 778.0), 30.56, 235.0
HALL_C = (-399.0, 785.0)                    # meio do salão, entre a torre e a boca da fenda
HALL_A, HALL_B = 70.0, 52.0                 # semieixos (ao longo da ponte, de lado)
HALL_EDGE, HALL_TOP = 13.0, 50.0            # o teto vai de DECK + 13 na beira até DECK + 50 no meio
PORTAL_CLEAR = 27.0                         # acima do piso da torre, perto dela (o anel do portal vai a ~20 m)
BRIDGE_CLEAR = 7.5                          # acima do piso do caminho, até 7 m do eixo
ROOF = (8.0, 13.0)                          # espessura do teto

# ---------- túnel ----------
TUNNEL_FROM = 50.0                          # s (m ao longo do caminho) onde começa a escadaria da fenda
MOUTH_S = 155.0                             # boca: no fim da subida, antes da curvinha do alto (s ~160)
TUNNEL_HALF = 5.2                           # meia largura do teto (as paredes da fenda ficam a ~3,6 m do eixo)
TUNNEL_H = 9.0                              # altura do teto no eixo
MOUTH_FLARE = 2.5                           # a boca abre um pouco mais alto
FALL, FALL_GROW, FALL_STEPS = 1.35, 0.02, 95  # queda do topo para fora da caverna (m por m, crescendo a cada m)
NEAR_FILL = 7                               # células em volta da caverna com rocha até dentro da montanha
SINK = 2.5                                  # a rocha que encosta na montanha fica isto abaixo dela
BOTTOM = mv3.BOTTOM
T0 = time.time()


def log(*args):
    print('[Caverna %4.0fs]' % (time.time() - T0), *args, flush=True)


# ---------- entrada ----------

def load_road():
    data = json.load(open(LAYOUT, encoding='utf-8'))
    road = next(r for r in data['roads'] if r['name'] == ROAD)
    p = np.array(road['points'], dtype=np.float64).reshape(-1, 3)      # E, altura, N
    s = np.concatenate([[0.0], np.cumsum(np.hypot(np.diff(p[:, 0]), np.diff(p[:, 2])))])
    keep = s <= 230.0
    return p[keep], s[keep], float(road['width'])


def load_rock():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SOURCE, colors_type='LINEAR')
    rock = next(o for o in bpy.data.objects if o.type == 'MESH' and not o.name.endswith(('_Colisao', '_Gelo')))
    mw = rock.matrix_world
    verts = [mw @ v.co for v in rock.data.vertices]
    polys = [tuple(p.vertices) for p in rock.data.polygons]
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    log('rocha lida: %d faces' % len(polys))
    return BVHTree.FromPolygons(verts, polys), verts, polys


def surface(bvh, E, N):
    h = np.full(E.shape, np.nan)
    down = Vector((0.0, 0.0, -1.0))
    for idx in np.ndindex(E.shape):
        hit = bvh.ray_cast(Vector((-E[idx], -N[idx], 600.0)), down, 1200.0)[0]
        if hit is not None:
            h[idx] = hit.z
    return h


class RoadField:
    """Distância horizontal ao eixo do caminho, o s e a altura do piso no ponto mais perto."""

    def __init__(self, p, s):
        self.p, self.s = p, s
        self.tree = KDTree(len(p))
        for k, q in enumerate(p):
            self.tree.insert((q[0], q[2], 0.0), k)
        self.tree.balance()

    def query(self, E, N):
        d = np.empty(E.shape)
        s = np.empty(E.shape)
        h = np.empty(E.shape)
        for idx in np.ndindex(E.shape):
            _, k, dist = self.tree.find((E[idx], N[idx], 0.0))
            d[idx], s[idx], h[idx] = dist, self.s[k], self.p[k, 1]
        return d, s, h


def hall_q(E, N):
    ux, uy = TOWER[0] - HALL_C[0], TOWER[1] - HALL_C[1]
    ul = math.hypot(ux, uy)
    ux, uy = -ux / ul, -uy / ul                     # da torre para a fenda
    de, dn = E - HALL_C[0], N - HALL_C[1]
    along = de * ux + dn * uy
    across = -de * uy + dn * ux
    q = np.hypot(along / HALL_A, across / HALL_B)
    ang = np.arctan2(across / HALL_B, along / HALL_A)
    wobble = 0.07 * np.sin(ang * 5.0 + 1.3) + 0.05 * np.sin(ang * 9.0 - 0.4) + 0.04 * np.sin(ang * 13.0 + 2.2)
    return q * (1.0 + wobble), (ux, uy)


# ---------- mapa duplo ----------

def shift(field, a, b):
    """field deslocado de (a, b) células, sem dar a volta na borda."""
    out = np.full(field.shape, -1e9)
    nx, ny = field.shape
    out[max(a, 0):nx + min(a, 0), max(b, 0):ny + min(b, 0)] = field[max(-a, 0):nx + min(-a, 0), max(-b, 0):ny + min(-b, 0)]
    return out


def build_maps(bvh, road):
    E0, E1, N0, N1 = DOMAIN
    Es = np.arange(E0, E1 + 0.5, 1.0)
    Ns = np.arange(N0, N1 + 0.5, 1.0)
    E, N = np.meshgrid(Es, Ns, indexing='ij')
    H = surface(bvh, E, N)
    H = np.nan_to_num(H, nan=BOTTOM)
    log('relevo amostrado: %d células, %.0f a %.0f m' % (H.size, H.min(), H.max()))
    dist, s_at, deck = road.query(E, N)
    tower_d = np.hypot(E - TOWER[0], N - TOWER[1])
    q, axis = hall_q(E, N)
    rng = np.random.default_rng(812)

    # Salão: a elipse e uma faixa de 13 m em volta do caminho até o túnel
    hall = (q <= 1.0) | ((dist <= 13.0) & (s_at <= TUNNEL_FROM + 6.0))
    hall_c = DECK + HALL_EDGE + (HALL_TOP - HALL_EDGE) * np.clip(1.0 - q * q, 0.0, 1.0) ** 0.6
    hall_c += mv3.fbm(E, N, 26.0, 813) * 4.0

    # Túnel: até a boca, que é recortada (o fim do teto muda com o lado) e um pouco mais alta
    side = np.clip(dist / TUNNEL_HALF, 0.0, 1.5)
    lip = MOUTH_S + 1.4 * mv3.value_noise(E, N, 3.5, 814) - 1.2 * side
    tunnel = (dist <= TUNNEL_HALF + 0.8 * mv3.value_noise(E, N, 6.0, 815)) & (s_at >= TUNNEL_FROM - 4.0) & (s_at <= lip)
    flare = np.clip((s_at - (MOUTH_S - 9.0)) / 9.0, 0.0, 1.0)
    tunnel_c = deck + TUNNEL_H + MOUTH_FLARE * flare - 2.6 * side ** 2 + 1.6 * mv3.fbm(E, N, 9.0, 816)
    cave = hall | tunnel
    C = np.where(hall, hall_c, -1e9)
    C = np.where(tunnel, np.maximum(C, tunnel_c), C)
    # Saída do salão para o túnel: o teto do salão desce até o do túnel (sem degrau)
    join = hall & (s_at > TUNNEL_FROM - 15.0) & (dist < 16.0)
    blend = np.clip((s_at - (TUNNEL_FROM - 15.0)) / 30.0, 0.0, 1.0)
    C = np.where(join, C * (1.0 - blend) + np.maximum(tunnel_c, DECK + TUNNEL_H) * blend, C)

    C_smooth = C.copy()
    # Estalactites (pontas de rocha penduradas; as finas vêm depois, em cones)
    spots = []
    for _ in range(140):
        e, n = rng.uniform(E0, E1), rng.uniform(N0, N1)
        i, j = int(e - E0), int(n - N0)
        if not cave[i, j]:
            continue
        in_tunnel = tunnel[i, j] and not hall[i, j]
        depth = rng.uniform(1.0, 2.8) if in_tunnel else rng.uniform(4.0, 15.0)
        radius = rng.uniform(1.4, 2.6) if in_tunnel else rng.uniform(2.2, 5.5)
        if in_tunnel:       # o túnel fica sem estalactites (pedido do usuário em 2026-11-12)
            continue
        spots.append((e, n, depth, radius))
    for e, n, depth, radius in spots:
        r = np.hypot(E - e, N - n)
        dip = depth * np.clip(1.0 - r / radius, 0.0, 1.0) ** 1.7
        C = np.where(cave, C - dip, C)
    log('estalactites: %d' % len(spots))

    # Folgas: nada desce sobre o caminho, a torre e o portal, nem encosta no chão
    clear = np.full(E.shape, -1e9)
    clear = np.where(dist <= 7.0, deck + BRIDGE_CLEAR, clear)
    clear = np.where(tower_d <= TOWER_R + 6.0, DECK + PORTAL_CLEAR, clear)
    clear = np.where(tower_d <= TOWER_R + 14.0, np.maximum(clear, DECK + 12.0), clear)
    # Onde a rocha que já existe chega perto do teto (barrancos, as paredes da fenda), o teto entra nela (nada de fresta)
    merge = cave & (H > C - 5.0)
    C = np.where(merge, H - 3.0, C)
    C_smooth = np.where(merge, H - 3.0, C_smooth)
    C = np.where(cave, np.maximum(C, clear), C)
    C_smooth = np.where(cave, np.maximum(C_smooth, clear), C_smooth)

    # Colunas de rocha no salão, do chão ao teto, longe do caminho e da torre
    columns = []
    tries = 0
    while len(columns) < 7 and tries < 4000:
        tries += 1
        e, n = rng.uniform(E0, E1), rng.uniform(N0, N1)
        i, j = int(e - E0), int(n - N0)
        if not hall[i, j] or q[i, j] > 0.86 or dist[i, j] < 14.0 or tower_d[i, j] < TOWER_R + 15.0:
            continue
        if C[i, j] - H[i, j] < 18.0 or any(math.hypot(e - a, n - b) < 26.0 for a, b, _ in columns):
            continue
        columns.append((e, n, rng.uniform(3.5, 6.5)))
    column = np.zeros(E.shape, dtype=bool)
    for e, n, radius in columns:
        ang = np.arctan2(N - n, E - e)
        rr = radius * (1.0 + 0.22 * np.sin(ang * 3.0 + e) + 0.12 * np.sin(ang * 5.0 + n))
        column |= np.hypot(E - e, N - n) <= rr
        # um ombro encostado (as colunas não são cilindros)
        e2, n2 = e + rng.uniform(-1, 1) * radius, n + rng.uniform(-1, 1) * radius
        column |= np.hypot(E - e2, N - n2) <= radius * 0.55
    column &= hall & (dist >= 11.0) & (tower_d >= TOWER_R + 10.0)
    log('colunas: %d' % len(columns))

    # Topo: o teto (sem as estalactites) mais a espessura, e no túnel a fenda fica cheia até perto do alto das paredes
    roof = ROOF[0] + (ROOF[1] - ROOF[0]) * (0.5 + 0.5 * mv3.fbm(E, N, 30.0, 817))
    walls_top = mv3.disk_filter(H, 8, np.maximum) - 1.5
    T_cave = np.where(cave, np.maximum(C_smooth + roof, np.where(tunnel & ~hall, walls_top, -1e9)), -1e9)
    # Além da boca (a descida e a vista para a cidade) nada sai da montanha
    # (desde um pouco antes da boca: o recorte do lábio deixa células fora da caverna, que não podem virar rocha)
    beyond = (s_at > MOUTH_S - 4.0) & (dist < 34.0) & ~cave
    # Para fora da caverna o topo cai em encosta (dilatação com queda crescente), que depois vira blocos
    D = T_cave.copy()
    for k in range(FALL_STEPS):
        drop = FALL + FALL_GROW * k
        grown = D.copy()
        for a, b, w in ((1, 0, 1.0), (-1, 0, 1.0), (0, 1, 1.0), (0, -1, 1.0),
                        (1, 1, 1.414), (1, -1, 1.414), (-1, 1, 1.414), (-1, -1, 1.414)):
            np.maximum(grown, shift(D, a, b) - drop * w, out=grown)
        D = np.where(beyond, -1e9, grown)
    top = np.where(cave, T_cave, D)
    # Blocos de arenito de topo plano (a média de cada bloco de 9 m, como a montanha em volta)
    _, _, block, _ = mv3.voronoi(E, N, 9.0, 818)
    count = block.max() + 1
    exposed = (top > H - SINK) & ~beyond
    total = np.zeros(count)
    cells = np.zeros(count)
    np.add.at(total, block[exposed], top[exposed])
    np.add.at(cells, block[exposed], 1.0)
    mean = total / np.maximum(cells, 1.0)
    drop = np.random.default_rng(819).uniform(-3.0, 0.5, count)
    T = np.where(exposed, mean[block] + drop[block], H - SINK)
    T = np.where(cave, np.maximum(T, C_smooth + ROOF[0] * 0.6), T)
    T = np.where(beyond, np.minimum(T, H - SINK), T)

    # Fundo: na caverna o teto; nas colunas o chão; em volta, para dentro da montanha
    near = cave.copy()
    for _ in range(NEAR_FILL):
        near |= np.roll(near, 1, 0) | np.roll(near, -1, 0) | np.roll(near, 1, 1) | np.roll(near, -1, 1)
    keep = near | (T > H - SINK + 0.3)
    C_out = np.where(cave, C, H - 9.0)
    C_out = np.where(column, H - 9.0, C_out)
    T = np.where(column, np.maximum(T, C_smooth + roof), T)
    T = np.where(keep, np.maximum(T, C_out + 4.0), C_out)
    T = np.where(cave & ~column, np.maximum(T, C + 4.0), T)
    # Além da boca não há peça: o fundo não pode descer da beira do teto até o chão (o sólido entre as duas grades
    # virava uma cortina fechando a boca)
    T = np.where(beyond, C_out, T)
    # Bordas da grade sem espessura (o remesh fecha)
    for arr in (T,):
        arr[0, :] = C_out[0, :]
        arr[-1, :] = C_out[-1, :]
        arr[:, 0] = C_out[:, 0]
        arr[:, -1] = C_out[:, -1]
    log('caverna: %d células (salão %d, túnel %d), colunas %d células' %
        (cave.sum(), hall.sum(), (tunnel & ~hall).sum(), column.sum()))
    info = dict(E=E, N=N, H=H, C=C_out, T=T, cave=cave, hall=hall, tunnel=tunnel, dist=dist, s=s_at, deck=deck,
                column=column, axis=axis)
    return info


def dual_solid(E, N, C, T):
    """Sólido entre o fundo C e o topo T, só onde há espessura (T > C): as quadras com os quatro cantos sólidos viram
    topo e fundo, e as beiras da região viram paredes. Sem espessura não sobra nada (nem lâmina no remesh)."""
    nx, ny = E.shape
    n = nx * ny
    co = np.empty((2 * n, 3), dtype=np.float32)
    co[:n, 0] = co[n:, 0] = -E.ravel()
    co[:n, 1] = co[n:, 1] = -N.ravel()
    co[:n, 2] = T.ravel()
    co[n:, 2] = C.ravel()
    solid = T > C + 0.5
    q = solid[:-1, :-1] & solid[1:, :-1] & solid[1:, 1:] & solid[:-1, 1:]
    qi, qj = np.nonzero(q)
    a = qi * ny + qj
    corners = np.stack([a, a + ny, a + ny + 1, a + 1], axis=1)       # (i,j) (i+1,j) (i+1,j+1) (i,j+1)
    faces = [tuple(f) for f in corners.tolist()] + [tuple(f) for f in (corners[:, ::-1] + n).tolist()]
    qp = np.pad(q, 1)
    # vizinhos de cada quadra: j-1 (aresta 0-1), i+1 (1-2), j+1 (2-3), i-1 (3-0)
    for k, (di, dj) in enumerate(((0, -1), (1, 0), (0, 1), (-1, 0))):
        missing = ~qp[qi + 1 + di, qj + 1 + dj]
        u = corners[missing, k]
        v = corners[missing, (k + 1) % 4]
        faces += [tuple(f) for f in np.stack([v, u, u + n, v + n], axis=1).tolist()]
    mesh = bpy.data.meshes.new(NAME)
    mesh.from_pydata(co.tolist(), [], faces)
    mesh.update(calc_edges=True)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=[v for v in bm.verts if not v.link_faces], dist=0.0)
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(NAME, mesh)
    bpy.context.collection.objects.link(obj)
    log('sólido: %d quadras' % len(qi))
    return obj


def rock_cones(info, rock, road, rng):
    """Estalagmites no chão do salão (longe do caminho, da torre e das colunas) e estalactites finas penduradas no teto,
    em cones de rocha somados à peça depois da escultura (o voxel de 2 m arredondaria as pontas)."""
    E, N, H, hall, dist = info['E'], info['N'], info['H'], info['hall'], info['dist']
    tower_d = np.hypot(E - TOWER[0], N - TOWER[1])
    E0, N0 = E[0, 0], N[0, 0]
    bm = bmesh.new()
    mites = 0
    for _ in range(4000):
        if mites >= 34:
            break
        e, n = rng.uniform(E.min(), E.max()), rng.uniform(N.min(), N.max())
        i, j = int(e - E0), int(n - N0)
        if not hall[i, j] or info['column'][i, j] or dist[i, j] < 12.0 or tower_d[i, j] < TOWER_R + 9.0:
            continue
        ground = H[i, j]
        if info['C'][i, j] - ground < 16.0:
            continue
        height = rng.uniform(3.0, 11.0)
        radius = rng.uniform(1.2, 2.8)
        base = Vector((-e, -n, ground - 1.5))
        tip = Vector((-e + rng.uniform(-0.6, 0.6), -n + rng.uniform(-0.6, 0.6), ground + height))
        mv3.cut.cone(bm, tip, base, radius, 8, random.Random(mites))
        if rng.uniform() < 0.5:     # uma menor encostada
            off = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0.0)).normalized() * radius
            mv3.cut.cone(bm, tip + off - Vector((0, 0, height * 0.45)), base + off, radius * 0.6, 6,
                         random.Random(mites + 99))
        mites += 1

    mw = rock.matrix_world
    faces = [mw @ p.center for p in rock.data.polygons if (mw.to_3x3() @ p.normal).z < -0.7 and p.area > 1.0]
    rnd = random.Random(824)
    rnd.shuffle(faces)
    tites = 0
    for centre in faces:
        if tites >= 150:
            break
        e, n = -centre.x, -centre.y
        i, j = int(round(e - E0)), int(round(n - N0))
        if not (0 <= i < E.shape[0] and 0 <= j < E.shape[1]) or not info['cave'][i, j] or not hall[i, j]:
            continue        # só no salão: o túnel fica sem estalactites
        d, sv, h = road.query(np.array([e]), np.array([n]))
        d, sv, h = float(d[0]), float(sv[0]), float(h[0])
        length = rnd.uniform(2.0, 7.0) if hall[i, j] else rnd.uniform(0.8, 2.2)
        if d < 7.0 and sv < MOUTH_S + 3.0:
            length = min(length, centre.z - (h + BRIDGE_CLEAR - 1.0))
        if math.hypot(e - TOWER[0], n - TOWER[1]) < TOWER_R + 6.0:
            length = min(length, centre.z - (DECK + PORTAL_CLEAR - 3.0))
        length = min(length, centre.z - H[i, j] - 4.0)
        if length < 0.8:
            continue
        mv3.cut.cone(bm, centre + Vector((rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), -length)),
                     centre + Vector((0.0, 0.0, 0.8)), min(2.0, length * rnd.uniform(0.16, 0.26)), 7, rnd)
        tites += 1
    mesh = bpy.data.meshes.new(NAME + '_Cones')
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(NAME + '_Cones', mesh)
    bpy.context.collection.objects.link(obj)
    log('estalagmites: %d, estalactites finas: %d' % (mites, tites))
    return obj


class GameField:
    """Para a escultura: perto do caminho e da torre ela só entra na rocha."""

    def __init__(self, road):
        self.road = road

    def lookup(self, world):
        e, n = -world[:, 0], -world[:, 1]
        d, s, h = self.road.query(e, n)
        d = np.where(s <= MOUTH_S + 12.0, d - 3.0, 1e3)
        td = np.hypot(e - TOWER[0], n - TOWER[1]) - (TOWER_R + 2.0)
        floor = np.where(td < d, DECK, h)
        d = np.minimum(d, td)
        return np.maximum(d, 0.0), np.full(len(world), -1e3), floor


def clamp_clearance(obj, road):
    """Depois da escultura, conta os vértices abaixo das folgas do caminho e do portal."""
    world = mv3.mv.world_coords(obj)
    e, n = -world[:, 0], -world[:, 1]
    d, s, h = road.query(e, n)
    td = np.hypot(e - TOWER[0], n - TOWER[1])
    moved = 0
    for k, v in enumerate(obj.data.vertices):
        z = world[k, 2]
        lim = -1e9
        if d[k] <= 4.0 and s[k] <= MOUTH_S + 3.0 and h[k] - 1.0 < z:
            lim = h[k] + BRIDGE_CLEAR - 1.0
        if td[k] <= TOWER_R + 5.0 and DECK - 1.0 < z:
            lim = max(lim, DECK + PORTAL_CLEAR - 2.0)
        if z < lim:
            moved += 1
    # Só avisa: levantar vértices soltos esticava faces em espinhos (a folga já sai do mapa; a conferência mede)
    log('folgas: %d vértices abaixo do limite' % moved)


def fix_spikes(obj, limit=5.5, rounds=8):
    """A escultura empurra pela normal suavizada; nas quinas finas isso estica faces em espinhos. Vértice com aresta
    mais comprida que 'limit' (o remesh deixa ~2 m) volta para a média dos vizinhos, até não sobrar nenhum."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    total = 0
    for _ in range(rounds):
        bad = set()
        for e in bm.edges:
            if e.calc_length() > limit:
                bad.update(e.verts)
        if not bad:
            break
        total += len(bad)
        new = {v: sum((e.other_vert(v).co for e in v.link_edges), Vector()) / max(1, len(v.link_edges)) for v in bad}
        for v, co in new.items():
            v.co = co
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    log('espinhos: %d correções' % total)


def build_ice(rock, road):
    """Pingentes no teto do salão e do túnel (mais densos e compridos no lábio da boca)."""
    rnd = random.Random(821)
    bm = bmesh.new()
    mw = rock.matrix_world
    faces = [(mw @ p.center, p.area) for p in rock.data.polygons if (mw.to_3x3() @ p.normal).z < -0.6 and p.area > 0.5]
    rnd.shuffle(faces)
    count = mouth = 0
    for centre, _ in faces:
        if count >= 900:
            break
        e, n = -centre.x, -centre.y
        d, s, h = road.query(np.array([e]), np.array([n]))
        d, s, h = float(d[0]), float(s[0]), float(h[0])
        td = math.hypot(e - TOWER[0], n - TOWER[1])
        in_mouth = d < 7.0 and MOUTH_S - 4.0 < s < MOUTH_S + 2.5 and centre.z > h + 5.0
        if not in_mouth and d < 14.0 and TUNNEL_FROM + 6.0 < s < MOUTH_S + 3.0:
            continue        # o túnel fica sem pingentes (só a beira da boca)
        if not in_mouth and rnd.random() < 0.55:
            continue
        length = rnd.uniform(1.2, 3.6) if in_mouth else rnd.uniform(0.6, 4.5)
        if d < 6.0 and s < MOUTH_S + 3.0:
            length = min(length, centre.z - (h + 4.2))      # a ponta fica acima da cabeça
        if td < TOWER_R + 4.0:
            length = min(length, centre.z - (DECK + 22.0))
        if length < 0.5:
            continue
        cut_cone = mv3.cut.cone
        cut_cone(bm, centre + Vector((0.0, 0.0, -length)), centre + Vector((0.0, 0.0, 0.2)),
                 rnd.uniform(0.12, 0.45) * (1.2 if in_mouth else 1.0), 5, rnd)
        count += 1
        mouth += in_mouth
    # Lábio da boca: em cada ponto da largura, o último teto antes do céu (raios para cima a cada 0,4 m ao longo)
    bvh = BVHTree.FromPolygons([mw @ v.co for v in rock.data.vertices], [tuple(p.vertices) for p in rock.data.polygons])
    up = Vector((0.0, 0.0, 1.0))
    sel = np.nonzero((road.s > MOUTH_S - 14.0) & (road.s < MOUTH_S + 4.0))[0]
    for o in np.arange(-5.0, 5.01, 0.45):
        lip = None
        for k in sel:
            q, q2 = road.p[k], road.p[min(k + 1, len(road.p) - 1)]
            de, dn = q2[0] - q[0], q2[2] - q[2]
            l = math.hypot(de, dn) or 1.0
            start = Vector((-(q[0] - dn / l * o), -(q[2] + de / l * o), q[1] + 1.0))
            hit = bvh.ray_cast(start, up, 25.0)[0]
            if hit is not None:
                lip = (hit, q[1])
        if lip is None:
            continue
        hit, floor = lip
        for _ in range(rnd.randint(1, 3)):
            top = hit + Vector((rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4), 0.0))
            length = min(rnd.uniform(1.0, 3.4), top.z - (floor + 4.2))
            if length < 0.5:
                continue
            mv3.cut.cone(bm, top + Vector((0.0, 0.0, -length)), top + Vector((0.0, 0.0, 0.6)),
                         rnd.uniform(0.15, 0.5), 5, rnd)
            count += 1
            mouth += 1
    mesh = bpy.data.meshes.new(NAME + '_Gelo')
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(NAME + '_Gelo', mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(bpy.data.materials.get('MAT_Gelo') or bpy.data.materials.new('MAT_Gelo'))
    log('pingentes: %d (%d na boca)' % (count, mouth))
    return obj


def remove_fragments(obj, min_faces=200):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    seen = set()
    drop = []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, island = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop()
            island.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        if len(island) < min_faces:
            drop.extend(island)
    bmesh.ops.delete(bm, geom=drop, context='FACES')
    bm.to_mesh(obj.data)
    bm.free()
    log('pedaços soltos: %d faces tiradas' % len(drop))


def write_plan(info, road, path):
    p, s = road.p, road.s
    keep = s <= MOUTH_S + 2.0
    pts = []
    for q, sv in zip(p[keep][::2], s[keep][::2]):
        pts += [round(float(q[0]), 2), round(float(q[1]), 2), round(float(q[2]), 2), round(float(sv), 2)]
    plan = {
        'hall': {'center': [HALL_C[0], DECK, HALL_C[1]], 'axis': [round(info['axis'][0], 4), round(info['axis'][1], 4)],
                 'a': HALL_A, 'b': HALL_B, 'floor': 140.0, 'ceiling': DECK + HALL_TOP + 15.0},
        'tower': {'center': [TOWER[0], DECK, TOWER[1]], 'radius': TOWER_R},
        'mouth_s': MOUTH_S,
        'tunnel_half': TUNNEL_HALF + 2.0,
        'tunnel': pts,
    }
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(plan, f, indent=1)
    log('planta gravada em', path)


def audit(rock_bvh, obj, road):
    """Confere: (1) folga acima do caminho; (2) raios saindo do salão e do túnel que escapam sem bater em rocha."""
    mw = obj.matrix_world
    bvh = BVHTree.FromPolygons([mw @ v.co for v in obj.data.vertices], [tuple(p.vertices) for p in obj.data.polygons])
    up = Vector((0, 0, 1))
    low = []
    for q, sv in zip(road.p, road.s):
        if sv > MOUTH_S - 1.0:
            break
        for o in (-2.5, 0.0, 2.5):
            k = int(np.searchsorted(road.s, sv))
            k = min(k + 1, len(road.p) - 1)
            de, dn = road.p[k, 0] - q[0], road.p[k, 2] - q[2]
            l = math.hypot(de, dn) or 1.0
            le, ln = -dn / l, de / l
            start = Vector((-(q[0] + le * o), -(q[2] + ln * o), q[1] + 0.3))
            hits = [b.ray_cast(start, up, 40.0)[3] for b in (bvh, rock_bvh)]
            hits = [x for x in hits if x is not None]
            gap = min(hits) if hits else 99.0
            if gap < 5.5:
                low.append((round(sv, 1), o, round(gap, 2)))
    log('folga acima do caminho < 5,5 m em %d pontos' % len(low), low[:12])
    rnd = random.Random(5)
    leaks = []
    sources = [(TOWER[0], DECK + 6.0, TOWER[1])] + [(road.p[k, 0], road.p[k, 1] + 2.0, road.p[k, 2])
                                                     for k in range(0, len(road.p)) if road.s[k] < MOUTH_S - 30.0][::25]
    for e, z, n in sources:
        for _ in range(400):
            d = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1))).normalized()
            start = Vector((-e, -n, z))
            hit = False
            for b in (bvh, rock_bvh):
                if b.ray_cast(start, d, 3000.0)[0] is not None:
                    hit = True
                    break
            if not hit:
                leaks.append((round(e), round(n), round(z), tuple(round(c, 2) for c in d)))
    log('raios que escapam (de %d pontos): %d' % (len(sources), len(leaks)), leaks[:10])
    return low, leaks


def preview(folder, obj, ice):
    """Vistas de dentro (luz rasante do portal) e de cima, com blocos no lugar da torre e do caminho."""
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    try:
        scene.render.engine = 'BLENDER_WORKBENCH'
    except TypeError:
        pass
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'SINGLE'
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    cam_data = bpy.data.cameras.new('cam')
    cam = bpy.data.objects.new('cam', cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    views = {
        'cima': ((-400.0, 790.0, 700.0), (-400.0, 790.0, 0.0), 60.0),
        'torre_para_boca': ((-430.0, 776.0, 238.0), (-340.0, 798.0, 245.0), 80.0),
        'ponte_para_torre': ((-352.0, 798.0, 237.0), (-422.0, 778.0, 245.0), 80.0),
        'tunel_meio': ((-300.0, 801.6, 250.0), (-245.0, 805.0, 263.0), 75.0),
        'boca_de_fora': ((-229.0, 796.5, 263.2), (-244.6, 805.0, 266.0), 75.0),
        'boca_dentro': ((-262.0, 803.9, 259.6), (-236.0, 803.6, 264.0), 75.0),
        'de_longe_sul': ((-330.0, 560.0, 330.0), (-360.0, 790.0, 250.0), 50.0),
    }
    for name, (eye, look, fov) in views.items():
        loc = Vector((-eye[0], -eye[1], eye[2]))
        tgt = Vector((-look[0], -look[1], look[2]))
        cam.location = loc
        cam.rotation_euler = (tgt - loc).to_track_quat('-Z', 'Y').to_euler()
        cam_data.angle = math.radians(fov)
        cam_data.clip_end = 5000.0
        scene.render.filepath = os.path.join(folder, name + '.png')
        bpy.ops.render.render(write_still=True)
    log('prévia em', folder)


def main(argv):
    out = argv[argv.index('--saida') + 1] if '--saida' in argv else kit.OUT
    p, s, width = load_road()
    road = RoadField(p, s)
    rock_bvh, rock_verts, rock_polys = load_rock()
    info = build_maps(rock_bvh, road)
    if '--mapa' in argv:
        np.savez(argv[argv.index('--mapa') + 1], **{k: v for k, v in info.items() if k != 'axis'})
    obj = dual_solid(info['E'], info['N'], info['C'], info['T'])
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    obj.data.remesh_voxel_size = mv3.VOXEL
    bpy.ops.object.voxel_remesh()
    log('remesh: %d faces' % len(obj.data.polygons))
    remove_fragments(obj)
    mv3.sculpt(obj, GameField(road), 823)
    fix_spikes(obj)
    clamp_clearance(obj, road)
    cones = rock_cones(info, obj, road, np.random.default_rng(822))
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    cones.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.join()
    kit.relief_colors(obj)
    neve_lateral.mark(obj)
    ice = build_ice(obj, road)
    if '--conferir' in argv:
        audit(rock_bvh, obj, road)
    os.makedirs(out, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    ice.select_set(True)
    path = os.path.join(out, NAME + '.fbx')
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL',
                             mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False, colors_type='LINEAR')
    log('exportado', path, len(obj.data.polygons), 'faces')
    write_plan(info, road, os.path.join(out, NAME + '.json'))
    if '--preview' in argv and '--sem-antiga' in argv:
        preview(argv[argv.index('--preview') + 1], obj, ice)
    elif '--preview' in argv:
        rock = bpy.data.meshes.new('Rocha_Antiga')
        rock.from_pydata([tuple(v) for v in rock_verts], [], rock_polys)
        ro = bpy.data.objects.new('Rocha_Antiga', rock)
        bpy.context.collection.objects.link(ro)
        preview(argv[argv.index('--preview') + 1], obj, ice)


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
