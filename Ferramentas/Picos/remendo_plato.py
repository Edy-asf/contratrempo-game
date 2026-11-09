# Remendo de rocha no platô do antigo portão (Vilas do Alto; pedido do usuário em 2026-11-02). O macico_vilas3.py abria um
# corte de ~28 m de largura saindo do platô para o sudoeste, por onde passava a ponte colossal antiga (o 'bridge' da
# planta). A ponte mudou de lugar (Ponte do Reino, PicosRoyalBridge.cs) e o corte ficou como um buraco na ponta da
# montanha. Gerar a montanha inteira de novo pesa demais (travou o computador do usuário duas vezes); este script só
# fecha o corte, num objeto à parte que o Unity põe junto do maciço:
#   - lê a rocha pronta (Models/Picos/Macico_Vilas.fbx) por raios de cima, numa grade de 1 m em volta do corte;
#   - o chão do corte é preenchido pela rocha em volta (interpolação harmônica a partir da beira dele e do platô) e
#     quebrado em blocos de arenito de 9 m de topo plano, como o resto do maciço; nada passa do piso do platô;
#   - fora do corte o remendo fica 3 m abaixo da rocha que já existe (escondido nela) e desce até o fundo;
#   - remesh de voxel, a escultura do macico_vilas3.py, a cor do relevo e a marca da neve lateral (neve_lateral.py).
#   blender.exe -b --factory-startup --python Ferramentas/Picos/remendo_plato.py -- [--saida <pasta>] [--mapa <npz>]
import math
import os
import sys
import time

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import montanhas_destaque as kit  # noqa: E402
import macico_vilas3 as mv3  # noqa: E402
import neve_lateral  # noqa: E402

NAME = 'Macico_Vilas_Remendo'
SOURCE = os.path.join(kit.OUT, 'Macico_Vilas.fbx')
BRIDGE_A, BRIDGE_B = (210.72, 280.96), (90.0, 120.0)    # a ponte antiga (planta: leste, norte)
PLATEAU, PLATEAU_R, PLATEAU_FLOOR = (225.0, 300.0), 28.0, 34.0
CUT_HALF = 17.0         # o corte tinha 14 m de cada lado; o remesh arredondou a beira
RING = 5.0              # a beira do corte (de onde vem a altura) vai até aqui além dele
DOMAIN_N = 230.0        # o mapa da montanha começa aqui (ao sul não há rocha)
SUB_BLOCK = 9.0
SINK = 3.0
FLANK = 1.1             # inclinação da encosta que desce do platô (m por m)
DEEP = -120.0          # abaixo disto, na faixa da ponte antiga, é o corte
T0 = time.time()


def log(*args):
    print('[Remendo %4.0fs]' % (time.time() - T0), *args, flush=True)


def load_rock():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SOURCE, colors_type='LINEAR')
    rock = next(o for o in bpy.data.objects if o.type == 'MESH' and not o.name.endswith(('_Colisao', '_Gelo')))
    mw = rock.matrix_world
    verts = [mw @ v.co for v in rock.data.vertices]
    polys = [tuple(p.vertices) for p in rock.data.polygons]
    bvh = BVHTree.FromPolygons(verts, polys)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    log('rocha lida: %d faces' % len(polys))
    return bvh


def seg_dist(L, N, a, b):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    t = np.clip(((L - ax) * dx + (N - ay) * dy) / (dx * dx + dy * dy), 0.0, 1.0)
    return np.hypot(L - (ax + t * dx), N - (ay + t * dy))


def surface(bvh, L, N):
    """Altura da rocha pronta em cada ponto (raio de cima); NaN onde não há rocha."""
    h = np.full(L.shape, np.nan)
    down = Vector((0.0, 0.0, -1.0))
    for idx in np.ndindex(L.shape):
        hit, _, _, _ = bvh.ray_cast(Vector((-L[idx], -N[idx], 400.0)), down, 800.0)
        if hit is not None:
            h[idx] = hit.z
    return h


def voronoi_blocks(L, N, size, seed):
    rng = np.random.default_rng(seed)
    l0, l1, n0, n1 = L.min() - size, L.max() + size, N.min() - size, N.max() + size
    count = int((l1 - l0) * (n1 - n0) / (size * size)) + 1
    pts = np.stack([rng.uniform(l0, l1, count), rng.uniform(n0, n1, count)], 1)
    d = (L[..., None] - pts[:, 0]) ** 2 + (N[..., None] - pts[:, 1]) ** 2
    return np.argmin(d, axis=-1)


def fill_heights(bvh):
    l0 = min(BRIDGE_A[0], BRIDGE_B[0]) - CUT_HALF - RING - 6.0
    l1 = max(BRIDGE_A[0], PLATEAU[0] + PLATEAU_R) + 6.0
    n0, n1 = DOMAIN_N - 6.0, PLATEAU[1] + PLATEAU_R + 6.0
    E = np.arange(math.floor(l0), math.ceil(l1) + 1, 1.0)
    Nn = np.arange(math.floor(n0), math.ceil(n1) + 1, 1.0)
    L, N = np.meshgrid(E, Nn, indexing='ij')
    old = surface(bvh, L, N)
    dist = seg_dist(L, N, BRIDGE_A, BRIDGE_B)
    plat = np.hypot(L - PLATEAU[0], N - PLATEAU[1])
    # Corte: o que ficou no fundo na faixa da ponte antiga (a beira arredondada que ainda tem rocha alta fica como está;
    # o corte entrou uns metros no círculo do platô)
    inside = (dist <= CUT_HALF + 2.0) & (N >= DOMAIN_N) & (np.isnan(old) | (old < DEEP))
    known = ~inside & ~np.isnan(old) & (old >= DEEP)
    log('corte: %d células; rocha em volta conhecida em %d' % (inside.sum(), known.sum()))

    # De uma beira à outra: cada célula do corte anda para os dois lados, atravessando a faixa, até achar rocha, e
    # interpola as duas alturas pela distância (sem rocha de um lado, fica a do outro descendo)
    ux, uy = BRIDGE_B[0] - BRIDGE_A[0], BRIDGE_B[1] - BRIDGE_A[1]
    ul = math.hypot(ux, uy)
    wx, wy = -uy / ul, ux / ul
    z = np.where(known, old, np.nan)
    l_first, n_first = L[0, 0], N[0, 0]

    def probe(l, n, sx, sy):
        for k in range(1, 60):
            i, j = int(round(l + sx * k - l_first)), int(round(n + sy * k - n_first))
            if not (0 <= i < L.shape[0] and 0 <= j < L.shape[1]):
                return None
            if known[i, j]:
                return z[i, j], k
        return None

    for idx in zip(*np.nonzero(inside)):
        l, n = L[idx], N[idx]
        a, b = probe(l, n, wx, wy), probe(l, n, -wx, -wy)
        if a and b:
            z[idx] = (a[0] * b[1] + b[0] * a[1]) / (a[1] + b[1])
        elif a or b:
            h, k = a or b
            z[idx] = h - k * 0.8
        else:
            z[idx] = mv3.BOTTOM + 40.0
    # Suaviza só ao longo da faixa (três passadas), para os blocos não seguirem o serrilhado da beira
    for _ in range(3):
        sm = z.copy()
        for idx in zip(*np.nonzero(inside)):
            vals = []
            for k in (-2, -1, 0, 1, 2):
                i, j = int(round(L[idx] + ux / ul * k * 2 - l_first)), int(round(N[idx] + uy / ul * k * 2 - n_first))
                if 0 <= i < L.shape[0] and 0 <= j < L.shape[1] and not np.isnan(z[i, j]):
                    vals.append(z[i, j])
            sm[idx] = sum(vals) / len(vals)
        z = sm

    # Perto do platô, a rocha desce em encosta a partir da beira dele (como o resto da ponta da montanha), não em poço
    flank = PLATEAU_FLOOR - 0.6 - np.maximum(0.0, plat - PLATEAU_R) * FLANK
    z = np.where(inside, np.fmax(z, flank), z)

    # Blocos de arenito de topo plano (média de cada bloco de 9 m), um pouco abaixo da interpolação
    blocks = voronoi_blocks(L, N, SUB_BLOCK, 31)
    rng = np.random.default_rng(32)
    top = np.zeros(blocks.max() + 1)
    for b in np.unique(blocks[inside]):
        sel = inside & (blocks == b)
        top[b] = z[sel].mean() + rng.uniform(-3.0, 0.5)
    filled = np.where(inside, top[blocks], np.nan_to_num(z, nan=mv3.BOTTOM))
    # Nada acima do piso do platô perto dele (a laje do Unity fica por cima)
    near = plat < PLATEAU_R + 8.0
    filled = np.where(inside & near, np.minimum(filled, PLATEAU_FLOOR - 0.6), filled)

    # Remendo: o preenchimento no corte; em volta dele, 3 m abaixo da rocha que já existe; o resto no fundo
    grow = inside.copy()
    for _ in range(4):
        grow |= np.roll(grow, 1, 0) | np.roll(grow, -1, 0) | np.roll(grow, 1, 1) | np.roll(grow, -1, 1)
    patch = np.where(inside, filled, np.where(grow & ~np.isnan(old), old - SINK, mv3.BOTTOM - 20.0))
    log('preenchimento: %.0f a %.0f m' % (filled[inside].min(), filled[inside].max()))
    return L, N, patch, inside, old, filled


def solid(L, N, z):
    nx, ny = L.shape
    z = z.copy()
    z[0, :] = z[-1, :] = z[:, 0] = z[:, -1] = mv3.BOTTOM - 20.0
    co = np.empty((nx * ny, 3), dtype=np.float32)
    co[:, 0] = -L.ravel()
    co[:, 1] = -N.ravel()
    co[:, 2] = z.ravel()
    i, j = np.meshgrid(np.arange(nx - 1), np.arange(ny - 1), indexing='ij')
    a = (i * ny + j).ravel()
    quads = np.stack([a, a + ny, a + ny + 1, a + 1], axis=1)
    ring = ([i * ny for i in range(nx)] + [(nx - 1) * ny + j for j in range(1, ny)] +
            [i * ny + ny - 1 for i in range(nx - 2, -1, -1)] + [j for j in range(ny - 2, 0, -1)])
    mesh = bpy.data.meshes.new(NAME)
    mesh.from_pydata(co.tolist(), [], [tuple(q) for q in quads.tolist()] + [tuple(ring[::-1])])
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(NAME, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


class NoGame:
    """A escultura do macico_vilas3 pede a distância até o jogo; aqui tudo fica longe (escultura cheia)."""

    @staticmethod
    def lookup(world):
        n = len(world)
        return np.full(n, 1e3), np.full(n, -1e3), np.full(n, -1e3)


def clamp_plateau(obj):
    """Depois da escultura, nada acima do piso do platô perto dele."""
    for v in obj.data.vertices:
        L, N = -v.co.x, -v.co.y
        if math.hypot(L - PLATEAU[0], N - PLATEAU[1]) < PLATEAU_R + 8.0 and v.co.z > PLATEAU_FLOOR - 0.5:
            v.co.z = PLATEAU_FLOOR - 0.5
    obj.data.update()


def height_dump(bvh_old, obj, L, N, inside, path):
    """Alturas de cima (sem render), a rocha antiga e com o remendo, num .npz para conferir fora do Blender."""
    mw = obj.matrix_world
    bvh_new = BVHTree.FromPolygons([mw @ v.co for v in obj.data.vertices], [tuple(p.vertices) for p in obj.data.polygons])
    before = surface(bvh_old, L, N)
    after = np.fmax(before, surface(bvh_new, L, N))
    np.savez(path, L=L, N=N, before=before, after=after, inside=inside)
    log('alturas de conferência em', path)


def main(argv):
    out = argv[argv.index('--saida') + 1] if '--saida' in argv else kit.OUT
    bvh = load_rock()
    L, N, patch, inside, old, filled = fill_heights(bvh)
    obj = solid(L, N, patch)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    obj.data.remesh_voxel_size = mv3.VOXEL
    bpy.ops.object.voxel_remesh()
    log('remesh: %d faces' % len(obj.data.polygons))
    mv3.sculpt(obj, NoGame(), 41)
    clamp_plateau(obj)
    kit.relief_colors(obj)
    neve_lateral.mark(obj)
    os.makedirs(out, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    path = os.path.join(out, NAME + '.fbx')
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL',
                             mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False, colors_type='LINEAR')
    log('exportado', path, len(obj.data.polygons), 'faces')
    if '--mapa' in argv:
        height_dump(bvh, obj, L, N, inside, argv[argv.index('--mapa') + 1])


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
