# Neve lateral nas montanhas esculpidas no Blender (pedido do usuário em 2026-11-01: como o bloco de terra com neve do
# Minecraft, a neve do topo desce um pouco pela parede). O shader Icaro/SnowRock já faz a faixa (_SideSnow): ele só
# precisa saber, em cada vértice, quanto ele está perto da beira de um topo nevado logo acima (UV0.x, 1 rente à beira,
# 0 a partir de SIDE_DEPTH abaixo dela). Os pilares do gerador do Unity gravam isso; as montanhas do Blender não
# gravavam. Este script marca isso nos FBX prontos, sem gerar as montanhas de novo (forma, cor do relevo, normais e
# objetos ficam iguais; só o UV0 muda).
#   blender.exe -b --factory-startup --python Ferramentas/Picos/neve_lateral.py -- [--saida <pasta>] <fbx> [<fbx> ...]
# Sem --saida, regrava cada FBX no lugar. Objetos de colisão (_Colisao) e de gelo (_Gelo) ficam sem marca.
import os
import sys
import time

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

SIDE_DEPTH = 3.5    # m abaixo da beira em que a marca chega a zero (a faixa de neve fica com ~2 m, borda irregular)
INSET = 1.0         # o topo de cima é procurado este tanto para dentro da rocha, atrás da parede
STEEP = 0.85        # só vértices de parede (normal com z abaixo disto) recebem a marca
TOP = 0.6           # o topo de cima tem de ser plano o bastante para ter neve
T0 = time.time()


def log(*args):
    print('[NeveLateral %4.0fs]' % (time.time() - T0), *args, flush=True)


def mark(obj):
    mesh = obj.data
    mw = obj.matrix_world
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.transform(mw)
    bm.normal_update()      # a rocha vem do FBX girada: as normais das faces têm de ir para o mundo também
    bvh = BVHTree.FromBMesh(bm)
    bm.free()
    nv = len(mesh.vertices)
    co = np.empty(nv * 3, dtype=np.float32)
    mesh.vertices.foreach_get('co', co)
    nrm = np.empty(nv * 3, dtype=np.float32)
    mesh.vertex_normals.foreach_get('vector', nrm)
    co = co.reshape(-1, 3)
    nrm = nrm.reshape(-1, 3)
    rot = mw.to_3x3()
    rot_n = rot.inverted().transposed()
    bias = np.zeros(nv, dtype=np.float32)
    down = Vector((0.0, 0.0, -1.0))
    marked = 0
    for i in range(nv):
        n = (rot_n @ Vector(nrm[i])).normalized()
        if n.z >= STEEP:
            continue
        p = mw @ Vector(co[i])
        side = Vector((n.x, n.y))
        if side.length < 1e-4:
            continue
        side.normalize()
        o = Vector((p.x - side.x * INSET, p.y - side.y * INSET, p.z + SIDE_DEPTH + 1.0))
        hit, hn, _, _ = bvh.ray_cast(o, down, SIDE_DEPTH + 2.0)
        if hit is None or hn.z < TOP:
            continue
        depth = hit.z - p.z
        if depth < -0.5:
            continue
        b = 1.0 - max(0.0, depth) / SIDE_DEPTH
        if b > 0.0:
            bias[i] = b
            marked += 1
    # Vértices do topo colados na beira ficam com 1: a faixa da parede começa cheia na quina (senão o triângulo da
    # parede iria de 0 na quina a algo embaixo, ao contrário)
    ev = np.empty(len(mesh.edges) * 2, dtype=np.int64)
    mesh.edges.foreach_get('vertices', ev)
    ev = ev.reshape(-1, 2)
    nz = (nrm @ np.array(rot_n, dtype=np.float32).T)
    nz = nz[:, 2] / np.maximum(np.linalg.norm(nz, axis=1), 1e-6)
    flat = nz >= STEEP
    for a, b in ((0, 1), (1, 0)):
        lip = flat[ev[:, a]] & (bias[ev[:, b]] > 0.7)
        bias[ev[lip, a]] = 1.0
    loops = np.empty(len(mesh.loops), dtype=np.int64)
    mesh.loops.foreach_get('vertex_index', loops)
    uv = np.zeros((len(mesh.loops), 2), dtype=np.float32)
    uv[:, 0] = bias[loops]
    if mesh.uv_layers:
        old = np.empty(len(mesh.loops) * 2, dtype=np.float32)
        mesh.uv_layers[0].data.foreach_get('uv', old)
        log('   UV antigo: média u %.3f, máx %.3f' % (old[0::2].mean(), old[0::2].max()))
    for layer in list(mesh.uv_layers):
        mesh.uv_layers.remove(layer)
    layer = mesh.uv_layers.new(name='UVMap')
    layer.data.foreach_set('uv', uv.ravel())
    mesh.update()
    log('   %s: %d de %d vértices marcados' % (obj.name, marked, nv))


def process(path, out_dir):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, colors_type='LINEAR')   # mantém as normais facetadas do FBX
    objects = [o for o in bpy.data.objects if o.type == 'MESH']
    log(os.path.basename(path), [o.name for o in objects])
    for obj in objects:
        if obj.name.endswith('_Colisao') or obj.name.endswith('_Gelo'):
            continue
        mark(obj)
    # O import deixa o objeto girado (eixos do FBX); a rotação volta para a malha, como no Blender que criou o arquivo,
    # para o FBX sair com a mesma estrutura (senão o objeto chega girado 180° em vez de 90°)
    from mathutils import Matrix
    for obj in objects:
        obj.data.transform(obj.matrix_world)
        obj.matrix_world = Matrix.Identity(4)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    target = os.path.join(out_dir, os.path.basename(path)) if out_dir else path
    os.makedirs(os.path.dirname(os.path.abspath(target)), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=target, use_selection=True, axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL',
                             mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False, colors_type='LINEAR')
    log('   gravado', target)


def main(argv):
    out_dir = None
    if '--saida' in argv:
        k = argv.index('--saida')
        out_dir = argv[k + 1]
        argv = argv[:k] + argv[k + 2:]
    for path in argv:
        process(path, out_dir)


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
