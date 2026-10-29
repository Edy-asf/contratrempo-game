# Montanhas de destaque dos Picos acima das Nuvens, esculpidas no Blender em segundo plano:
#   blender.exe -b --python Ferramentas/Picos/montanhas_destaque.py
# Cada uma junta colunas de arenito como as de Zhangjiajie (pilhas de blocos de planta poligonal, paredes quase
# verticais, topo em penhasco, sem pontas) e um arco num volume só pelo remesh de voxel
# (sem costura onde as formas se cruzam) e depois esculpe por ruído, ponto a ponto, ao longo da normal:
# - blocos: células de Voronoi esticadas na vertical viram colunas abauladas separadas por fendas;
# - degraus irregulares em parte da volta (onde a neve assenta);
# - lóbulos grandes e aspereza.
# Por fim suaviza (sem lascas soltas), reduz os polígonos, facetado nas quebras fortes, cor do relevo no vértice (fendas
# escuras, quinas claras, para o shader Icaro/SnowRock) e exporta FBX (forward -Z, up Y) para Assets/Icaro/Models/Picos.
# Em coordenadas do Blender: z para cima, z = 0 é a altura de referência (o PicosBuilder põe o objeto no lugar);
# tudo desce até z = -250 (dentro da nuvem fechada).
import math
import os
import random

import bmesh
import bpy
from mathutils import Vector, noise

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'Icaro', 'Models', 'Picos')
BOTTOM = -250.0


def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)


def prism(bm, cx, cy, z0, z1, angles, radii, scale, top_scale=1.0, crown=0.0):
    """Bloco: prisma de planta poligonal de z0 a z1 (topo encolhido por top_scale; 'crown' sobe o meio do topo)."""
    n = len(angles)
    low = [bm.verts.new((cx + math.cos(angles[k]) * radii[k] * scale, cy + math.sin(angles[k]) * radii[k] * scale, z0))
           for k in range(n)]
    high = [bm.verts.new((cx + math.cos(angles[k]) * radii[k] * scale * top_scale,
                          cy + math.sin(angles[k]) * radii[k] * scale * top_scale, z1)) for k in range(n)]
    for k in range(n):
        m = (k + 1) % n
        bm.faces.new((low[k], low[m], high[m], high[k]))
    bm.faces.new(list(reversed(low)))
    if crown > 0.0:
        apex = bm.verts.new((cx, cy, z1 + crown))
        for k in range(n):
            bm.faces.new((high[k], high[(k + 1) % n], apex))
    else:
        bm.faces.new(high)


def column(x, y, top, r_top, r_base, rnd, lean=(0.0, 0.0), bed=(10.0, 22.0), corners=None, shift=0.08):
    """Pilar de arenito de Zhangjiajie: pilha de blocos de planta poligonal irregular (4 a 7 cantos), cada camada um
    pouco diferente da de baixo (cantos, giro e centro), paredes quase verticais (o alargamento fica dentro da nuvem) e
    o topo em penhasco: último bloco com ombro encolhido e o meio um pouco mais alto. Sem ponta."""
    n = corners or rnd.randint(4, 7)
    base_angles = [(k + rnd.uniform(-0.3, 0.3)) / n * math.tau for k in range(n)]
    base_radii = [rnd.uniform(0.85, 1.15) for _ in range(n)]
    mesh = bpy.data.meshes.new('coluna')
    bm = bmesh.new()
    height = top - BOTTOM
    z = BOTTOM
    while z < top - 0.5:
        h = min(rnd.uniform(*bed), top - z)
        if top - (z + h) < bed[0] * 0.4:
            h = top - z
        t = 1.0 - (z + h - BOTTOM) / height           # 0 no topo, 1 embaixo
        r = r_top + (r_base - r_top) * t ** 3.5
        spin = rnd.uniform(-shift, shift)
        angles = [a + spin for a in base_angles]
        radii = [v * rnd.uniform(1 - shift, 1 + shift) for v in base_radii]
        cx = x + lean[0] * (1 - t) ** 1.5 + rnd.uniform(-shift, shift) * r
        cy = y + lean[1] * (1 - t) ** 1.5 + rnd.uniform(-shift, shift) * r
        last = z + h >= top - 0.01
        prism(bm, cx, cy, z - 0.8, z + h, angles, radii, r, top_scale=0.82 if last else 0.97,
              crown=r * 0.12 if last else 0.0)
        z += h
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new('coluna', mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def arch(x0, x1, y, base_z, rise, thickness):
    """Arco de pedra natural entre dois pontos: esferas sobrepostas ao longo de meia elipse em pé (o remesh as funde),
    mais grosso nas pontas. Devolve a lista de esferas."""
    parts = []
    count = 28
    for k in range(count + 1):
        a = k / count * math.pi
        x = (x0 + x1) / 2 - math.cos(a) * (x1 - x0) / 2
        z = base_z + math.sin(a) * rise
        r = thickness * (1.0 + 0.5 * abs(math.cos(a)))
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, segments=16, ring_count=10, location=(x, y, z))
        parts.append(bpy.context.active_object)
    return parts


def fuse(objects, name, voxel):
    """Junta as formas num volume só (remesh de voxel)."""
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.data.remesh_voxel_size = voxel
    obj.data.use_remesh_fix_poles = True
    bpy.ops.object.voxel_remesh()
    return obj


def smooth_normals(obj):
    """Normais de uma cópia bem suavizada (o remesh deixa as normais ruidosas onde as formas se encontram, e esculpir
    por elas criava placas de calombos)."""
    mod = obj.modifiers.new('normais', 'SMOOTH')
    mod.factor = 1.0
    mod.iterations = 20
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    normals = [v.normal.copy() for v in evaluated.data.vertices]
    obj.modifiers.remove(mod)
    return normals


def sculpt(obj, seed, crack_depth=2.6, cell=0.03, ledge_height=16.0, ledge_depth=2.0, lumps=5.0):
    """Esculpe ao longo da normal (fendas e degraus só nas paredes):
    - blocos: células de Voronoi esticadas na vertical viram colunas de rocha com a face levemente abaulada, separadas
      por fendas onde duas células se encontram (como as juntas das torres de arenito);
    - degraus: faixas de altura irregular onde a parede recua e salta para fora embaixo, só em parte da volta;
    - lóbulos grandes e um pouco de aspereza."""
    mesh = obj.data
    mesh.update()
    normals = smooth_normals(obj)
    off = Vector((seed * 13.1, seed * 7.7, seed * 3.3))
    for v in mesh.vertices:
        p = v.co.copy()
        n = normals[v.index]
        side = max(0.0, 1.0 - abs(n.z) * 1.2)
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
        d = big + fine + side * (ledge + cushion - crack * crack_depth)
        v.co = p + n * d


def finish(obj, faces):
    bpy.context.view_layer.objects.active = obj
    smooth = obj.modifiers.new('suavizar', 'SMOOTH')
    smooth.factor = 0.5
    smooth.iterations = 3
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    dec = obj.modifiers.new('reduzir', 'DECIMATE')
    dec.ratio = min(1.0, faces / max(1, len(obj.data.polygons)))
    bpy.ops.object.modifier_apply(modifier=dec.name)
    # Facetado nas quebras fortes e liso nas faces (combina com as montanhas do gerador, que são facetadas)
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35.0))
    except (AttributeError, RuntimeError, TypeError):
        bpy.ops.object.shade_flat()
    relief_colors(obj)


def relief_colors(obj, depth=2.6):
    """Cor do vértice para o shader Icaro/SnowRock (branco = nada): R = 1 - quanto escurece (o que fica para dentro de
    uma cópia bem suavizada: fendas e juntas), G = 1 - quanto clareia (o que fica para fora: quinas), B = 1."""
    mesh = obj.data
    mod = obj.modifiers.new('relevo', 'SMOOTH')
    mod.factor = 1.0
    mod.iterations = 12
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    smooth = [v.co.copy() for v in evaluated.data.vertices]
    normals = [v.normal.copy() for v in evaluated.data.vertices]
    obj.modifiers.remove(mod)
    if 'Relevo' in mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes['Relevo'])
    attribute = mesh.color_attributes.new('Relevo', 'FLOAT_COLOR', 'POINT')
    for v in mesh.vertices:
        d = (v.co - smooth[v.index]).dot(normals[v.index]) / depth
        cavity = min(1.0, max(0.0, -d))
        edge = min(1.0, max(0.0, d))
        attribute.data[v.index].color = (1.0 - cavity, 1.0 - edge, 1.0, 1.0)
    mesh.color_attributes.active_color = attribute


def export(obj, name):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(OUT, name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL',
                             mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False, colors_type='LINEAR')
    print('[Picos] exportado', path, len(obj.data.polygons), 'faces')


def catedral():
    """Catedral: coluna maciça com quatro colunas de alturas diferentes coladas e blocos soltos no alto."""
    rnd = random.Random(1)
    parts = [column(0, 0, 55, 24, 36, rnd, corners=6)]
    for a, d, top, r in [(20, 26, 30, 13), (110, 24, 42, 11), (200, 28, 12, 15), (290, 22, 25, 12)]:
        a = math.radians(a)
        parts.append(column(math.cos(a) * d, math.sin(a) * d, top, r, r * 1.5, rnd, lean=(math.cos(a) * 3, math.sin(a) * 3)))
    for k in range(4):
        a = k / 4 * math.tau + rnd.uniform(0, 0.5)
        parts.append(column(math.cos(a) * 12, math.sin(a) * 12, 55 + rnd.uniform(3, 8), rnd.uniform(4, 7),
                            rnd.uniform(4, 7), rnd, bed=(3.0, 6.0)))
    obj = fuse(parts, 'Pico_Destaque_1', 1.4)
    sculpt(obj, 1, crack_depth=1.8, lumps=2.0, ledge_depth=1.2)
    return obj


def torres():
    """Torres gêmeas: duas colunas altíssimas e esguias, levemente inclinadas, nascendo da mesma base."""
    rnd = random.Random(2)
    parts = [column(-14, 0, 120, 12, 26, rnd, lean=(6, 2), corners=5),
             column(16, 4, 85, 10, 22, rnd, lean=(-4, -2), corners=6),
             column(2, -18, 30, 9, 18, rnd)]
    obj = fuse(parts, 'Pico_Destaque_2', 1.3)
    sculpt(obj, 2, crack_depth=1.6, cell=0.04, ledge_height=12.0, ledge_depth=1.0, lumps=1.5)
    return obj


def arco():
    """Arco: duas colunas ligadas lá no alto por um arco natural de pedra."""
    rnd = random.Random(3)
    parts = [column(-38, 0, 38, 13, 26, rnd, lean=(3, 0)),
             column(38, 0, 22, 14, 28, rnd, lean=(-2, 0))]
    parts += arch(-38, 38, 0, 0, 30, 9.0)
    obj = fuse(parts, 'Pico_Destaque_3', 1.4)
    sculpt(obj, 3, crack_depth=1.8, cell=0.035, lumps=2.0, ledge_depth=1.2)
    return obj


def mesa():
    """Mesa em degraus: platô largo em três patamares, com colunas mais baixas na borda."""
    rnd = random.Random(4)
    parts = [column(0, 0, -20, 44, 56, rnd, corners=7),
             column(4, -3, -2, 30, 32, rnd, corners=6, bed=(6.0, 10.0)),
             column(8, -6, 14, 16, 17, rnd, corners=5, bed=(6.0, 10.0))]
    for k in range(5):
        a = k / 5 * math.tau + rnd.uniform(-0.3, 0.3)
        parts.append(column(math.cos(a) * 40, math.sin(a) * 40, -20 + rnd.uniform(6, 22), rnd.uniform(6, 10),
                            rnd.uniform(8, 12), rnd))
    obj = fuse(parts, 'Pico_Destaque_4', 1.5)
    sculpt(obj, 4, ledge_height=10.0, ledge_depth=1.5, lumps=2.5, crack_depth=1.8)
    return obj


if __name__ == '__main__':
    for build, faces in [(catedral, 60000), (torres, 40000), (arco, 45000), (mesa, 55000)]:
        clear()
        obj = build()
        finish(obj, faces)
        export(obj, obj.name)
