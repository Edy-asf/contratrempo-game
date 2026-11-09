# Marcas do usuário nas Vilas do Alto, feitas no Blender.
#
#   blender -b --factory-startup --python Ferramentas/Picos/marcas_vilas.py -- --criar <arquivo.blend>
#       monta um .blend com a montanha (Macico_Vilas.fbx), os caminhos, plataformas e vilas da planta em cores,
#       nomes das vilas e uma coleção MARCAS vazia (ativa) para o usuário marcar.
#   blender -b <arquivo.blend> --python Ferramentas/Picos/marcas_vilas.py -- --ler
#       lista cada marca (empty, objeto ou traço de anotação) com o nome, a posição na planta (L, N, A) e no Unity.
#
# Blender = (-L, -N, A); Unity = k_High + (L, A, N), k_High = (-200, 0, -5800).
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Euler, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
MODELS = os.path.join(ROOT, 'Assets', 'Icaro', 'Models', 'Picos')
K_HIGH = (-200.0, 0.0, -5800.0)


def B(l, a, n):
    return Vector((-l, -n, a))


def material(name, rgb):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1.0)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1.0)
    bsdf.inputs['Roughness'].default_value = 0.9
    return mat


def link(obj, coll):
    coll.objects.link(obj)
    return obj


def ribbon(bm, pts, half, lift):
    """Faixa do piso de um caminho: pontos (L, A, N) a cada ~0,5 m."""
    prev = None
    for i in range(0, len(pts) - 1, 2):
        p, q = pts[i], pts[min(i + 2, len(pts) - 1)]
        d = Vector((q[0] - p[0], q[2] - p[2]))
        if d.length < 1e-4:
            continue
        d.normalize()
        nrm = Vector((d.y, -d.x))
        pair = [bm.verts.new(B(p[0] + nrm.x * o, p[1] + lift, p[2] + nrm.y * o)) for o in (-half, half)]
        if prev is not None:
            bm.faces.new((prev[0], prev[1], pair[1], pair[0]))
        prev = pair


def create(path):
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    scene = bpy.context.scene
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, 'Macico_Vilas.fbx'), use_custom_normals=False)
    rock = material('Rocha', (0.32, 0.2, 0.16))
    ice = material('Gelo', (0.62, 0.8, 0.95))
    for obj in list(bpy.data.objects):
        if obj.type != 'MESH':
            continue
        if 'Colisao' in obj.name:
            bpy.data.objects.remove(obj, do_unlink=True)
            continue
        obj.data.materials.clear()
        obj.data.materials.append(ice if obj.name.endswith('_Gelo') else rock)
        obj.hide_select = True
    layout = json.load(open(os.path.join(MODELS, 'Macico_Vilas_Layout.json'), encoding='utf-8'))

    game = bpy.data.collections.new('JOGO (caminhos e vilas)')
    scene.collection.children.link(game)
    road_mat = material('Caminho', (0.85, 0.8, 0.55))
    village_mat = material('Vila', (0.25, 0.55, 0.95))
    text_mat = material('Nome', (1.0, 1.0, 1.0))

    bm = bmesh.new()
    for road in layout['roads']:
        flat = road['points']
        pts = [flat[i:i + 3] for i in range(0, len(flat), 3)]
        ribbon(bm, pts, road.get('width', 6.0) * 0.5, 0.15)
    for plat in layout.get('platforms', []):
        c, (w, d) = plat['center'], plat['size']
        ring = [bm.verts.new(B(c[0] + sx * w * 0.5, c[1] + 0.15, c[2] + sz * d * 0.5))
                for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        bm.faces.new(ring)
    mesh = bpy.data.meshes.new('Caminhos')
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(road_mat)
    link(bpy.data.objects.new('Caminhos', mesh), game).hide_select = True

    for v in layout['villages']:
        c, r = v['center'], v['radius']
        bm = bmesh.new()
        ring = [bm.verts.new(B(c[0] + math.cos(a / 32 * math.tau) * r, c[1] + 0.2, c[2] + math.sin(a / 32 * math.tau) * r))
                for a in range(32)]
        bm.faces.new(ring)
        mesh = bpy.data.meshes.new('Vila_' + v['name'])
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(village_mat)
        link(bpy.data.objects.new('Vila_' + v['name'], mesh), game).hide_select = True
        curve = bpy.data.curves.new('Nome_' + v['name'], 'FONT')
        curve.body = v['name']
        curve.size = 10.0
        curve.align_x = 'CENTER'
        curve.extrude = 0.4
        curve.materials.append(text_mat)
        text = link(bpy.data.objects.new('Nome_' + v['name'], curve), game)
        text.location = B(c[0], c[1] + 25.0, c[2])
        text.rotation_euler = (math.radians(90), 0.0, math.radians(180))
        text.hide_select = True

    marks = bpy.data.collections.new('MARCAS')
    scene.collection.children.link(marks)
    view_layer = bpy.context.view_layer
    view_layer.active_layer_collection = view_layer.layer_collection.children['MARCAS']

    terraco = next(v for v in layout['villages'] if v['name'] == 'Terraco')
    focus = B(terraco['center'][0] + 150.0, terraco['center'][1] - 40.0, terraco['center'][2] - 150.0)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type != 'VIEW_3D':
                continue
            for space in area.spaces:
                if space.type != 'VIEW_3D':
                    continue
                space.clip_start = 0.5
                space.clip_end = 20000.0
                space.shading.type = 'SOLID'
                space.shading.color_type = 'MATERIAL'
                space.shading.show_cavity = True
                space.overlay.show_floor = False
                r3d = space.region_3d
                r3d.view_location = focus
                r3d.view_distance = 700.0
                r3d.view_rotation = Euler((math.radians(62), 0.0, math.radians(200))).to_quaternion()
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(path), compress=True)
    print('MARCAS: arquivo criado em', os.path.abspath(path))


def report(label, co):
    l, n, a = -co.x, -co.y, co.z
    u = (K_HIGH[0] + l, K_HIGH[1] + a, K_HIGH[2] + n)
    print('MARCA %-30s planta L %7.1f N %7.1f A %6.1f | Unity (%.1f, %.1f, %.1f)' % (label, l, n, a, *u))


def read():
    marks = bpy.data.collections.get('MARCAS')
    objs = list(marks.all_objects) if marks else []
    others = [o for o in bpy.data.objects if o not in objs and (o.type == 'EMPTY' or o.name.startswith(('Marca', 'Empty')))]
    for obj in objs + others:
        size = max(obj.matrix_world.to_scale()) * (obj.empty_display_size if obj.type == 'EMPTY' else 1.0)
        report('%s (raio %.1f m)' % (obj.name, size), obj.matrix_world.translation)
    count = len(objs) + len(others)
    for attr in ('annotations', 'grease_pencils'):
        data = getattr(bpy.data, attr, None)
        if not data:
            continue
        for gp in data:
            for layer in getattr(gp, 'layers', []):
                for frame in getattr(layer, 'frames', []):
                    strokes = getattr(frame, 'strokes', None)
                    if strokes is None and getattr(frame, 'drawing', None) is not None:
                        drawing = frame.drawing
                        strokes = [[p.position for p in s.points] for s in drawing.strokes]
                    else:
                        strokes = [[p.co for p in s.points] for s in strokes or []]
                    for k, pts in enumerate(strokes):
                        if not pts:
                            continue
                        mid = sum((Vector(p) for p in pts), Vector()) / len(pts)
                        name = '%s/%s traço %d' % (gp.name, layer.info if hasattr(layer, 'info') else layer.name, k + 1)
                        report(name + ' meio', mid)
                        step = max(1, len(pts) // 12)
                        for p in list(pts[::step]) + [pts[-1]]:
                            report(name + ' ponto', Vector(p))
                        count += 1
    print('MARCAS: %d no total' % count)


argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
if '--criar' in argv:
    create(argv[argv.index('--criar') + 1])
elif '--ler' in argv:
    read()
else:
    print(__doc__ if __doc__ else 'use --criar <arquivo.blend> ou --ler')
