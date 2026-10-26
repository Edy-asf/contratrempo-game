# Estátua colossal de um Aeônida (Picos acima das Nuvens, sala 18), esculpida no Blender em segundo plano:
#   blender.exe -b --python Ferramentas/Picos/estatua_aeonida.py
# Figura encapuzada de manto longo, de pé sobre um pedestal em degraus, segurando com as duas mãos um anel de runas
# partido diante do peito (o anel do portal). Feita de formas simples fundidas por remesh de voxel: manto em anéis com
# pregas que se abrem para baixo, manto dos ombros, capuz, mangas largas, mãos, cinto com placas, gola, o anel com
# dentes na borda e o pedaço quebrado no alto; o rosto é um vão fundo dentro do capuz (diferença booleana). Depois,
# desgaste: algumas rachaduras de Voronoi rasas e lascas nas quinas, ao longo de normais suavizadas. Os olhos (duas fendas) são
# um objeto à parte, "Olhos", que o Unity acende em ciano. Altura de 10 m (o PicosBuilder escala); frente para -Y do
# Blender (+Z no Unity). Exporta Assets/Icaro/Models/Picos/Estatua_Aeonida.fbx (forward -Z, up Y).
import math
import os

import bmesh
import bpy
from mathutils import Vector, noise

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'Icaro', 'Models', 'Picos')


def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)


def link(bm, name):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def lathe(name, profile, segments=64, scale_y=0.78, folds=0.0, fold_count=14):
    """Sólido de revolução (elipse achatada em Y) pelo perfil [(z, raio)], de baixo para cima; 'folds' abre pregas
    verticais que crescem para baixo."""
    bm = bmesh.new()
    rings = []
    zs = [p[0] for p in profile]
    z0, z1 = min(zs), max(zs)
    for z, r in profile:
        t = 1.0 - (z - z0) / max(0.001, z1 - z0)      # 1 embaixo, 0 em cima
        ring = []
        for j in range(segments):
            a = j / segments * math.tau
            fold = 1.0 + folds * t * (0.6 * math.sin(a * fold_count) + 0.4 * math.sin(a * fold_count * 0.5 + 1.3))
            ring.append(bm.verts.new((math.cos(a) * r * fold, math.sin(a) * r * fold * scale_y, z)))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for j in range(segments):
            k = (j + 1) % segments
            bm.faces.new((rings[i][j], rings[i][k], rings[i + 1][k], rings[i + 1][j]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    return link(bm, name)


def tube(name, a, b, r_a, r_b, segments=24):
    """Tubo fechado de 'a' até 'b' (raio variando)."""
    a, b = Vector(a), Vector(b)
    axis = (b - a).normalized()
    side = axis.orthogonal().normalized()
    up = axis.cross(side)
    bm = bmesh.new()
    rings = []
    for i in range(9):
        t = i / 8
        c = a.lerp(b, t)
        r = r_a + (r_b - r_a) * t
        rings.append([bm.verts.new(c + (side * math.cos(j / segments * math.tau) + up * math.sin(j / segments * math.tau)) * r)
                      for j in range(segments)])
    for i in range(len(rings) - 1):
        for j in range(segments):
            k = (j + 1) % segments
            bm.faces.new((rings[i][j], rings[i][k], rings[i + 1][k], rings[i + 1][j]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    return link(bm, name)


def sphere(name, location, radius, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, segments=32, ring_count=16, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(scale=True)
    return obj


def box(name, location, size, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    return obj


def torus(name, location, major, minor, rotation=(0, 0, 0), scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=72, minor_segments=16,
                                     location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    return obj


def boolean(target, cutter, operation='DIFFERENCE'):
    """Corte booleano; se um solucionador destruir a malha (perde mais da metade das faces), tenta o outro e, se
    nenhum servir, deixa sem o corte."""
    before = len(target.data.polygons)
    for solver in ('MANIFOLD', 'EXACT'):
        backup = target.data.copy()
        mod = target.modifiers.new('corte', 'BOOLEAN')
        mod.operation = operation
        mod.object = cutter
        try:
            mod.solver = solver
        except TypeError:
            target.modifiers.remove(mod)
            continue
        bpy.context.view_layer.objects.active = target
        bpy.ops.object.modifier_apply(modifier=mod.name)
        if len(target.data.polygons) > before * 0.5:
            bpy.data.meshes.remove(backup)
            break
        old = target.data
        target.data = backup
        bpy.data.meshes.remove(old)
        print('[Picos] corte falhou com', solver, cutter.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


def fuse(objects, name, voxel):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.data.remesh_voxel_size = voxel
    bpy.ops.object.voxel_remesh()
    return obj


def smooth_normals(obj, iterations=12):
    mod = obj.modifiers.new('normais', 'SMOOTH')
    mod.factor = 1.0
    mod.iterations = iterations
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    normals = [v.normal.copy() for v in evaluated.data.vertices]
    obj.modifiers.remove(mod)
    return normals


def weather(obj):
    """Desgaste: rachaduras rasas de Voronoi, lascas nas partes salientes e aspereza leve."""
    normals = smooth_normals(obj)
    for v in obj.data.vertices:
        p = v.co.copy()
        n = normals[v.index]
        dist, _ = noise.voronoi(p * 0.7, distance_metric='DISTANCE')
        # Só parte das juntas vira rachadura (as outras somem), para não parecer mosaico
        mask = max(0.0, noise.noise(p * 0.45 + Vector((2.0, 5.0, 1.0))) + 0.05) * 2.5
        crack = max(0.0, 1.0 - (dist[1] - dist[0]) / 0.05) ** 2 * min(1.0, mask)
        chip = max(0.0, noise.noise(p * 2.2 + Vector((7.0, 1.0, 3.0))) - 0.45) * 0.12
        rough = noise.noise(p * 7.0) * 0.008
        v.co = p + n * (rough - crack * 0.03 - chip)


def finish(obj, faces):
    bpy.context.view_layer.objects.active = obj
    smooth = obj.modifiers.new('suavizar', 'SMOOTH')
    smooth.factor = 0.4
    smooth.iterations = 2
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    dec = obj.modifiers.new('reduzir', 'DECIMATE')
    dec.ratio = min(1.0, faces / max(1, len(obj.data.polygons)))
    bpy.ops.object.modifier_apply(modifier=dec.name)
    bpy.ops.object.shade_smooth()


def statue():
    parts = []
    # Pedestal em degraus, com uma moldura de frisos
    parts.append(box('degrau1', (0, 0, 0.2), (3.0, 2.6, 0.4)))
    parts.append(box('degrau2', (0, 0, 0.6), (2.6, 2.2, 0.4)))
    parts.append(box('degrau3', (0, 0, 1.05), (2.2, 1.85, 0.5)))
    parts.append(box('friso', (0, 0, 1.33), (2.3, 1.95, 0.08)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(box('canto', (sx * 1.25, sy * 1.08, 0.75), (0.32, 0.32, 0.5)))

    # Manto longo: abre embaixo, com pregas fundas que crescem para baixo e a barra arrastando no pedestal
    robe = lathe('manto', [(1.3, 1.05), (1.6, 0.98), (2.4, 0.88), (3.3, 0.78), (4.1, 0.7), (4.9, 0.66), (5.6, 0.66),
                           (6.0, 0.62)], folds=0.09, fold_count=16)
    parts.append(robe)
    # Cinto com placas e a faixa pendurada na frente
    parts.append(torus('cinto', (0, 0, 4.15), 0.72, 0.07, scale=(1.0, 0.8, 1.0)))
    for k in range(10):
        a = k / 10 * math.tau
        parts.append(box('placa', (math.cos(a) * 0.73, math.sin(a) * 0.73 * 0.8, 4.15), (0.12, 0.12, 0.18), (0, 0, a)))
    parts.append(box('faixa', (0, -0.62, 3.4), (0.28, 0.08, 1.4), (math.radians(-6), 0, 0)))

    # Manto dos ombros (capa curta) e gola alta
    parts.append(sphere('ombros', (0, 0.05, 6.05), 1.0, (1.18, 0.78, 0.42)))
    parts.append(lathe('capa', [(4.9, 0.8), (5.4, 0.92), (5.9, 1.08), (6.15, 1.0)], folds=0.05, fold_count=10))
    parts.append(torus('gola', (0, 0.02, 6.35), 0.42, 0.12, scale=(1.0, 0.85, 1.0)))

    # Capuz: cabeça, a dobra de trás caindo nas costas e a borda grossa na frente
    parts.append(sphere('capuz', (0, 0.06, 7.05), 0.6, (1.0, 1.08, 1.22)))
    parts.append(sphere('capuz_tras', (0, 0.42, 6.75), 0.48, (0.95, 0.9, 1.2)))
    parts.append(torus('borda_capuz', (0, -0.36, 7.0), 0.4, 0.08, rotation=(math.radians(80), 0, 0), scale=(0.95, 1.0, 1.25)))

    # Mangas largas dos ombros aos antebraços, que se juntam diante do peito, e as mãos
    for s in (-1, 1):
        parts.append(tube('braco', (s * 0.92, 0.02, 5.95), (s * 0.78, -0.25, 4.95), 0.3, 0.34))
        parts.append(tube('manga', (s * 0.78, -0.25, 4.95), (s * 0.42, -0.72, 4.55), 0.36, 0.42))
        parts.append(sphere('mao', (s * 0.38, -0.86, 4.5), 0.17, (1.0, 1.15, 0.85)))
        for f in range(4):
            parts.append(tube('dedo', (s * (0.3 + f * 0.04), -0.95, 4.58 - f * 0.03),
                              (s * (0.26 + f * 0.04), -1.02, 4.42 - f * 0.04), 0.045, 0.04, 10))

    # O anel de runas, em pé diante do peito, com dentes na borda de fora
    ring = torus('anel', (0, -0.98, 5.25), 0.82, 0.085, rotation=(math.radians(90), 0, 0))
    parts.append(ring)
    parts.append(torus('anel_dentro', (0, -0.98, 5.25), 0.66, 0.035, rotation=(math.radians(90), 0, 0)))
    for k in range(24):
        a = k / 24 * math.tau
        parts.append(box('dente', (math.cos(a) * 0.93, -0.98, 5.25 + math.sin(a) * 0.93), (0.07, 0.1, 0.12), (0, -a, 0)))

    body = fuse(parts, 'Estatua_Aeonida', 0.03)

    # Rosto: vão fundo dentro do capuz
    face = sphere('rosto', (0, -0.42, 6.98), 0.36, (1.0, 0.9, 1.25))
    boolean(body, face)
    # O anel partido no alto: um pedaço caiu
    gap = box('quebra', (0.25, -0.98, 6.12), (0.42, 0.5, 0.42), (0, math.radians(25), 0))
    boolean(body, gap)
    # Lascas: o canto do pedestal e um pedaço da barra do manto
    boolean(body, box('lasca1', (1.45, -1.25, 0.95), (0.6, 0.6, 0.7), (math.radians(20), math.radians(15), math.radians(30))))
    boolean(body, box('lasca2', (-0.9, -0.75, 1.55), (0.5, 0.4, 0.45), (math.radians(10), 0, math.radians(35))))

    # Remesh de novo depois dos cortes: o vão do rosto e as lascas ficam lisos como o resto
    bpy.context.view_layer.objects.active = body
    body.data.remesh_voxel_size = 0.025
    bpy.ops.object.voxel_remesh()
    weather(body)
    finish(body, 90000)

    # Olhos: duas fendas no fundo do rosto (o Unity acende em ciano)
    eyes = []
    for s in (-1, 1):
        eyes.append(box('olho', (s * 0.13, -0.24, 7.0), (0.16, 0.04, 0.035), (0, 0, s * math.radians(-8))))
    bpy.ops.object.select_all(action='DESELECT')
    for e in eyes:
        e.select_set(True)
    bpy.context.view_layer.objects.active = eyes[0]
    bpy.ops.object.join()
    bpy.context.active_object.name = 'Olhos'
    return body


clear()
statue()
os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
path = os.path.join(OUT, 'Estatua_Aeonida.fbx')
bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                         apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='FACE', add_leaf_bones=False,
                         bake_anim=False)
print('[Picos] exportado', path, len(bpy.data.objects['Estatua_Aeonida'].data.polygons), 'faces')
