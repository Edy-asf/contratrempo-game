# Montanha do Bastião (sala 19 dos Picos), esculpida no Blender em segundo plano:
#   blender.exe -b --python Ferramentas/Picos/montanha_bastiao.py [-- --preview <pasta>]
# Substitui os pilares do gerador em volta do forte (o maciço de trás, os esporões, os pilares embaixo da plataforma,
# os paredões da fenda e o pilar sob a escadaria do portão) por uma montanha só, bem maior (cume a ~255 m acima do
# pátio), feita como as montanhas de destaque: colunas de arenito de Zhangjiajie fundidas por remesh de voxel e
# esculpidas por ruído (blocos de Voronoi, fendas, degraus). Depois de esculpir, cortes booleanos garantem o espaço
# do jogo: a plataforma do forte fica plana (a rocha para 0,45 m abaixo do pátio e 5 m além da beira), a parede de trás
# fica atrás dos terraços, e ficam livres o corredor da fenda, a escadaria do portão e a ponte para o Bairro Voador.
# Na parede de trás: a boca de uma caverna atrás do portal quebrado e um nicho para o monólito de runas. Por fim, a cor
# do relevo no vértice (o fundo da caverna e do nicho escuro) e um objeto de gelo: a cascata congelada descendo a parede
# a oeste e pingentes debaixo das saliências.
#
# Coordenadas: as locais do forte (PicosBuilder.B): x para oeste, y para o sul (o y do Blender é o z local), z para
# cima, z = 0 no chão do pátio; o PicosBuilder põe o objeto no k_Bastion sem girar (forward -Z, up Y).
# Exporta Assets/Icaro/Models/Picos/Montanha_Bastiao.fbx (rocha com colisão e o gelo, objetos separados).
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector, noise
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import montanhas_destaque as kit  # noqa: E402

NAME = 'Montanha_Bastiao'
FACES = 50000       # polígonos antes de triangular (o FBX sai com ~2x em triângulos)

# Plataforma do forte (PicosBuilder.BuildBastionShelf: superelipse de expoente 4)
HALF, FRONT, BACK = 64.0, 59.0, 57.0
SHELF_Z = -0.45

# Caminhos que a rocha não pode invadir (z = altura do chão do caminho)
LEDGE_E = (-68.0, -80.0, -3.0)          # boca da fenda
LEDGE_F = (-10.0, -76.0, -5.0)          # saída da fenda, pé da escadaria
GATE = (-10.0, -55.0, 0.0)
BRIDGE = ((46.0, -2.0, 3.5), (112.0, -2.0, 3.5))   # ponte reta para o Bairro Voador
LEDGE_A = (100.0, -2.0, 3.5)

# Boca da caverna atrás do portal (anel em x = 4, y = 46) e o nicho do monólito
CAVE_X, CAVE_FLOOR, CAVE_HALF, CAVE_TOP = 4.0, 7.0, 8.0, 27.0
NICHE = (-36.0, 30.0, 5.0, 12.0)        # x, chão, meia largura, altura
ICEFALL_X = 54.0


def column(x, y, top, r_top, r_base, rnd, corners=None, bed=(9.0, 18.0)):
    return kit.column(x, y, top, r_top, r_base, rnd, lean=(rnd.uniform(-3, 3), rnd.uniform(-3, 3)), bed=bed,
                      corners=corners, shift=0.1)


def path_point(t):
    return tuple(LEDGE_E[i] + (LEDGE_F[i] - LEDGE_E[i]) * t for i in range(3))


def build_columns():
    rnd = random.Random(1919)
    parts = []
    # Embaixo da plataforma: colunas largas de topo em -1 m (o corte deixa plano) e contrafortes em degraus abaixo da
    # beira, descendo até a nuvem
    for x, y, top, rt, rb in [(-34, -32, -1, 34, 42), (34, -32, -1, 34, 42), (-34, 22, -1, 33, 40), (34, 22, -1, 33, 40),
                              (0, -42, -1, 28, 36), (0, 8, -1, 32, 42), (-62, -56, -16, 14, 22), (58, -58, -26, 16, 24),
                              (-74, 4, -12, 13, 20), (74, -22, -30, 15, 22), (24, -68, -40, 12, 18), (-30, -70, -34, 12, 18),
                              (-10, -67, -8, 7, 12)]:
        parts.append(column(x, y, top, rt, rb, rnd, corners=7))
    # O maciço atrás do forte: a parede logo atrás dos terraços (fendas fundas entre as colunas) e, subindo para o sul,
    # colunas cada vez mais altas até o cume
    for x, y, top, rt, rb in [(-36, 76, 72, 17, 24), (6, 74, 52, 16, 24), (42, 78, 96, 18, 26), (-8, 92, 140, 22, 32),
                              (22, 95, 120, 20, 30), (0, 120, 255, 40, 60), (-48, 102, 205, 30, 44), (50, 108, 222, 32, 46),
                              (-18, 160, 235, 34, 50), (32, 165, 190, 30, 44), (-80, 140, 170, 32, 46), (85, 145, 160, 30, 44),
                              (-60, 200, 150, 34, 48), (60, 205, 140, 34, 48), (0, 215, 180, 38, 52)]:
        parts.append(column(x, y, top, rt, rb, rnd))
    # Abas dos lados, mais baixas, abraçando o forte (a oeste, longe da ponte)
    for x, y, top, rt, rb in [(-110, 95, 130, 28, 40), (115, 105, 115, 28, 40), (-150, 125, 95, 26, 38), (150, 135, 85, 26, 38),
                              (-165, 180, 70, 28, 40), (165, 185, 60, 28, 40), (-92, 40, 85, 22, 32), (-100, 5, 48, 17, 26),
                              (-95, -28, 26, 13, 20), (95, 50, 90, 22, 32), (128, 70, 60, 20, 30)]:
        parts.append(column(x, y, top, rt, rb, rnd))
    # A fenda: paredões dos dois lados do caminho e o chão de rocha embaixo dele
    ex, ey = LEDGE_F[0] - LEDGE_E[0], LEDGE_F[1] - LEDGE_E[1]
    length = math.hypot(ex, ey)
    sx, sy = -ey / length, ex / length            # lado do forte (sul)
    for t, side, top, r in [(0.15, 1, 20, 8), (0.4, 1, 26, 9), (0.62, 1, 18, 8), (0.1, -1, 24, 9), (0.33, -1, 30, 10),
                            (0.58, -1, 22, 9), (0.8, -1, 14, 8)]:
        px, py, _ = path_point(t)
        off = r + 5.0
        parts.append(column(px + sx * side * off, py + sy * side * off, top, r, r * 1.5, rnd, bed=(6.0, 11.0)))
    for t in (0.1, 0.35, 0.6, 0.85):
        px, py, pz = path_point(t)
        parts.append(column(px, py, pz - 3.0, 7, 10, rnd, corners=6))
    return parts


# ---------- cortes ----------

def superellipse(half, front, back, count=96):
    pts = []
    for i in range(count):
        a = i / count * math.tau
        cx, cy = math.cos(a), math.sin(a)
        depth = front if cy < 0 else back
        r = 1.0 / ((abs(cx) / half) ** 4 + (abs(cy) / depth) ** 4) ** 0.25
        pts.append((cx * r, cy * r))
    return pts


def solid(name, bottom, top):
    """Sólido fechado a partir de dois contornos (mesmo número de pontos, sentido anti-horário visto de cima)."""
    bm = bmesh.new()
    lo = [bm.verts.new(p) for p in bottom]
    hi = [bm.verts.new(p) for p in top]
    n = len(lo)
    for k in range(n):
        m = (k + 1) % n
        bm.faces.new((lo[k], lo[m], hi[m], hi[k]))
    bm.faces.new(list(reversed(lo)))
    bm.faces.new(hi)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def prism(name, outline, z0, z1):
    return solid(name, [(x, y, z0) for x, y in outline], [(x, y, z1) for x, y in outline])


def corridor(name, a, b, half, top=600.0, extend=3.0):
    """Faixa reta de a até b (meia largura 'half'), o fundo acompanhando a altura de cada ponta."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dy)
    ux, uy = dx / length, dy / length
    nx, ny = -uy, ux
    ax, ay = a[0] - ux * extend, a[1] - uy * extend
    bx, by = b[0] + ux * extend, b[1] + uy * extend
    corners = [(ax - nx * half, ay - ny * half, a[2]), (bx - nx * half, by - ny * half, b[2]),
               (bx + nx * half, by + ny * half, b[2]), (ax + nx * half, ay + ny * half, a[2])]
    return solid(name, corners, [(x, y, top) for x, y, _ in corners])


def disc(name, cx, cy, radius, z0, z1, count=24):
    return prism(name, [(cx + math.cos(i / count * math.tau) * radius, cy + math.sin(i / count * math.tau) * radius)
                        for i in range(count)], z0, z1)


def ogive_tunnel(name, cx, floor, half, top, y0, y1, count=12):
    """Túnel de boca ogival no plano x-z, de y0 a y1."""
    spring = floor + (top - floor) * 0.45
    profile = [(cx - half, floor), (cx + half, floor), (cx + half, spring)]
    for i in range(1, count):
        a = i / count * math.pi
        # dois arcos que se encontram em ponta (ogiva)
        x = cx + half * math.cos(a)
        z = spring + (top - spring) * math.sin(a) ** 0.8
        profile.append((x, z))
    profile.append((cx - half, spring))
    return solid(name, [(x, y0, z) for x, z in profile], [(x, y1, z) for x, z in profile])


def cutters():
    ex, ey, ez = LEDGE_E
    fx, fy, fz = LEDGE_F
    return [
        prism('CUT_Forte', superellipse(HALF + 0.3, FRONT + 0.3, BACK + 0.3), SHELF_Z, 600.0),
        prism('CUT_Beira', [(x, min(y, 40.0)) for x, y in superellipse(HALF + 5.0, FRONT + 5.0, 40.0)], SHELF_Z, 600.0),
        corridor('CUT_Fenda', (ex, ey, ez - 1.5), (fx, fy, fz - 1.5), 4.5),
        disc('CUT_RessaltoE', ex, ey, 9.0, ez - 1.0, 600.0),
        disc('CUT_RessaltoF', fx, fy, 8.0, fz - 1.0, 600.0),
        corridor('CUT_Escadaria', (fx, fy, fz - 1.4), (GATE[0], GATE[1] + 3.0, GATE[2] - 1.4), 4.5, extend=1.0),
        corridor('CUT_Ponte', (HALF + 5.0, BRIDGE[0][1], -14.0), (BRIDGE[1][0], BRIDGE[1][1], -14.0), 5.0, extend=0.0),
        disc('CUT_RessaltoA', LEDGE_A[0], LEDGE_A[1], 11.0, LEDGE_A[2] - 2.0, 600.0),
        ogive_tunnel('CUT_Caverna', CAVE_X, CAVE_FLOOR, CAVE_HALF, CAVE_TOP, 50.0, 70.0),
        solid('CUT_Nicho', [(NICHE[0] - NICHE[2], 50.0, NICHE[1]), (NICHE[0] + NICHE[2], 50.0, NICHE[1]),
                            (NICHE[0] + NICHE[2], 64.0, NICHE[1]), (NICHE[0] - NICHE[2], 64.0, NICHE[1])],
              [(NICHE[0] - NICHE[2], 50.0, NICHE[1] + NICHE[3]), (NICHE[0] + NICHE[2], 50.0, NICHE[1] + NICHE[3]),
               (NICHE[0] + NICHE[2], 64.0, NICHE[1] + NICHE[3]), (NICHE[0] - NICHE[2], 64.0, NICHE[1] + NICHE[3])]),
    ]


def make_manifold(obj):
    """O remesh deixa umas poucas arestas não-manifold (o corte MANIFOLD recusa a malha): apaga e fecha os buracos."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bad = {v for e in bm.edges if not e.is_manifold for v in e.verts}
    if bad:
        bmesh.ops.delete(bm, geom=list(bad), context='VERTS')
        bmesh.ops.holes_fill(bm, edges=bm.edges, sides=0)
        bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bm.to_mesh(obj.data)
    bm.free()
    print('[Bastiao] vértices não-manifold apagados', len(bad))


def carve(obj, cuts):
    make_manifold(obj)
    items = [i.identifier for i in bpy.types.BooleanModifier.bl_rna.properties['solver'].enum_items]
    solver = 'MANIFOLD' if 'MANIFOLD' in items else 'FLOAT'
    bpy.context.view_layer.objects.active = obj
    for cut in cuts:
        mod = obj.modifiers.new(cut.name, 'BOOLEAN')
        mod.operation = 'DIFFERENCE'
        mod.solver = solver
        mod.object = cut
        before = len(obj.data.polygons)
        bpy.ops.object.modifier_apply(modifier=mod.name)
        if len(obj.data.polygons) == before and solver == 'MANIFOLD':
            solver = 'FLOAT'        # a malha ainda não serve para o MANIFOLD: o corte rápido aceita
            mod = obj.modifiers.new(cut.name, 'BOOLEAN')
            mod.operation, mod.solver, mod.object = 'DIFFERENCE', solver, cut
            bpy.ops.object.modifier_apply(modifier=mod.name)
        print('[Bastiao] corte', cut.name, before, '->', len(obj.data.polygons), solver)
        bpy.data.objects.remove(cut, do_unlink=True)


# ---------- cor e gelo ----------

def in_cave(p, margin=0.0):
    if p.y < 56.0:
        return False
    if abs(p.x - CAVE_X) < CAVE_HALF + margin and CAVE_FLOOR - 1.0 < p.z < CAVE_TOP + margin:
        return True
    return abs(p.x - NICHE[0]) < NICHE[2] + margin and NICHE[1] - 1.0 < p.z < NICHE[1] + NICHE[3] + margin


def darken_hollows(obj):
    """O fundo da caverna e do nicho escuro (R da cor do relevo), mais escuro quanto mais fundo."""
    mesh = obj.data
    colors = mesh.color_attributes['Relevo']
    for v in mesh.vertices:
        if in_cave(v.co, 1.5):
            depth = min(1.0, max(0.0, (v.co.y - 57.0) / 8.0))
            c = colors.data[v.index].color
            colors.data[v.index].color = (min(c[0], 0.55 - 0.45 * depth), c[1], 0.0, 1.0)


def cone(bm, tip, base, radius, sides, rnd):
    axis = (tip - base).normalized()
    side = axis.orthogonal().normalized()
    other = axis.cross(side)
    ring = []
    for k in range(sides):
        a = k / sides * math.tau + rnd.uniform(-0.2, 0.2)
        r = radius * rnd.uniform(0.75, 1.15)
        ring.append(bm.verts.new(base + (side * math.cos(a) + other * math.sin(a)) * r))
    apex = bm.verts.new(tip)
    for k in range(sides):
        bm.faces.new((ring[k], ring[(k + 1) % sides], apex))
    bm.faces.new(list(reversed(ring)))


def build_ice(rock):
    """Cascata congelada na parede a oeste (feixes de gelo colados na rocha, do alto até o chão da plataforma) e
    pingentes debaixo das saliências."""
    rnd = random.Random(77)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    tree = BVHTree.FromObject(rock, depsgraph)
    bm = bmesh.new()

    # Cascata: cada feixe desce pela face em anéis, mais grosso onde a água se acumulou
    strands = [(ICEFALL_X + rnd.uniform(-5.0, 5.0), rnd.uniform(0.7, 1.6), rnd.uniform(60.0, 95.0)) for _ in range(9)]
    for sx, thick, start in strands:
        rings = []
        z = start
        while z > SHELF_Z - 0.2:
            hit = tree.ray_cast(Vector((sx, 20.0, z)), Vector((0.0, 1.0, 0.0)), 120.0)
            if hit[0] is None:
                z -= 1.6
                continue
            p, n = hit[0], hit[1]
            t = (start - z) / max(1.0, start)
            r = thick * (0.5 + 0.9 * t) * rnd.uniform(0.8, 1.2)
            centre = p + n * (r * 0.55)
            ring = [bm.verts.new(centre + Vector((math.cos(a) * r, 0.0, 0.0)) + n * (math.sin(a) * r * 0.55))
                    for a in (k / 7 * math.tau for k in range(7))]
            if rings:
                prev = rings[-1]
                for k in range(7):
                    bm.faces.new((prev[k], prev[(k + 1) % 7], ring[(k + 1) % 7], ring[k]))
            else:
                apex = bm.verts.new(centre + Vector((0.0, 0.0, 1.2)))
                for k in range(7):
                    bm.faces.new((ring[(k + 1) % 7], ring[k], apex))
            rings.append(ring)
            z -= 1.6
        if rings:
            bm.faces.new(rings[-1])
    # Montinho de gelo onde a cascata chega na plataforma
    for k in range(6):
        hit = tree.ray_cast(Vector((ICEFALL_X + rnd.uniform(-5.0, 5.0), 20.0, rnd.uniform(0.3, 2.5))), Vector((0, 1, 0)), 120.0)
        if hit[0] is not None:
            base = hit[0] + Vector((0.0, -rnd.uniform(0.5, 2.5), SHELF_Z - hit[0].z))
            cone(bm, base + Vector((0.0, 0.0, rnd.uniform(0.6, 1.6))), base, rnd.uniform(1.2, 2.4), 7, rnd)

    # Pingentes: debaixo das faces viradas para baixo, só da nuvem para cima e fora do espaço do forte
    faces = [(p.center.copy(), p.normal.copy(), p.area) for p in rock.data.polygons
             if p.normal.z < -0.55 and -40.0 < p.center.z < 260.0 and p.area > 0.6]
    rnd.shuffle(faces)
    count = 0
    for centre, normal, _ in faces:
        if count >= 700:
            break
        x, y = centre.x, centre.y
        inside = (abs(x) / (HALF + 3)) ** 4 + (abs(y) / ((FRONT if y < 0 else BACK) + 3)) ** 4 < 1.0
        if inside and centre.z > -12.0:
            continue
        length = rnd.uniform(0.8, 4.5)
        cone(bm, centre + Vector((0.0, 0.0, -length)), centre + Vector((0.0, 0.0, 0.15)), rnd.uniform(0.15, 0.55), 5, rnd)
        count += 1
    print('[Bastiao] pingentes', count)

    mesh = bpy.data.meshes.new(NAME + '_Gelo')
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(NAME + '_Gelo', mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(bpy.data.materials.get('MAT_Gelo') or bpy.data.materials.new('MAT_Gelo'))
    for poly in mesh.polygons:
        poly.use_smooth = False
    return obj


def export(objects):
    os.makedirs(kit.OUT, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    path = os.path.join(kit.OUT, NAME + '.fbx')
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL',
                             mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False, colors_type='LINEAR')
    print('[Bastiao] exportado', path, [len(o.data.polygons) for o in objects])


# ---------- prévia ----------

def proxies():
    """Blocos no lugar do forte, do portal, da ponte e dos caminhos, só para a prévia."""
    made = []
    made.append(prism('PX_Patio', superellipse(HALF, FRONT, BACK), -7.0, -0.35))
    for x0, x1, y0, y1, z in [(-52, 48, -12, 56, 3.5), (-44, 40, 16, 56, 7.0)]:
        made.append(solid('PX_Terraco', [(x0, y0, 0), (x1, y0, 0), (x1, y1, 0), (x0, y1, 0)],
                          [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)]))
    made.append(corridor('PX_Muralha', (-58, -55, 0), (58, -55, 0), 1.0, top=6.0, extend=0.0))
    bpy.ops.mesh.primitive_torus_add(major_radius=7.0, minor_radius=0.8, location=(CAVE_X, 46.0, 15.6),
                                     rotation=(math.pi / 2, 0, 0))
    made.append(bpy.context.active_object)
    made.append(corridor('PX_Ponte', (BRIDGE[0][0], -2, 2.5), (91, -2, 2.5), 2.5, top=3.5, extend=0.0))
    made.append(corridor('PX_Fenda', (LEDGE_E[0], LEDGE_E[1], LEDGE_E[2] - 1), (LEDGE_F[0], LEDGE_F[1], LEDGE_F[2] - 1), 2.0,
                         top=LEDGE_F[2] - 0.2 + 1, extend=0.0))
    made.append(corridor('PX_Escada', (LEDGE_F[0], LEDGE_F[1], LEDGE_F[2] - 1), (GATE[0], GATE[1], -1), 3.0, top=0.0, extend=0.0))
    return made


def look_colors():
    """Só para a prévia: rocha escura (juntas pela cor do relevo), neve onde a face olha para cima, gelo azulado."""
    for obj in bpy.data.objects:
        if obj.type != 'MESH':
            continue
        mesh = obj.data
        relief = mesh.color_attributes.get('Relevo')
        look = mesh.color_attributes.new('Look', 'FLOAT_COLOR', 'POINT')
        for v in mesh.vertices:
            if obj.name.endswith('_Gelo'):
                c = (0.62, 0.8, 0.95)
            elif obj.name.startswith('PX_') or relief is None:
                c = (0.55, 0.52, 0.5)
            else:
                r, g, b = relief.data[v.index].color[:3]
                k = 0.45 + 0.55 * r
                c = (0.9, 0.92, 1.0) if v.normal.z > 0.6 and b > 0.5 else (0.24 * k * (1.6 - 0.6 * g), 0.2 * k * (1.6 - 0.6 * g), 0.24 * k * (1.6 - 0.6 * g))
            look.data[v.index].color = (*c, 1.0)
        mesh.color_attributes.active_color = look


def preview(folder):
    os.makedirs(folder, exist_ok=True)
    proxies()
    look_colors()
    scene = bpy.context.scene
    try:
        scene.render.engine = 'BLENDER_WORKBENCH'
    except TypeError:
        pass
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'VERTEX'
    scene.render.resolution_x, scene.render.resolution_y = 1400, 900
    scene.world = scene.world or bpy.data.worlds.new('W')
    cam_data = bpy.data.cameras.new('cam')
    cam_data.clip_end = 5000.0
    cam = bpy.data.objects.new('cam', cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    for name, eye, target, lens in [('vista_norte', (-160, -330, 150), (0, 40, 30), 30),
                                    ('do_portao', (-10, -48, 4), (5, 60, 30), 22),
                                    ('do_alto', (40, -20, 14), (-20, 70, 30), 24),
                                    ('fenda', (-90, -86, 2), (-10, -76, 4), 24),
                                    ('ponte', (115, -6, 6), (40, 10, 10), 26)]:
        cam.location = Vector(eye)
        cam.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat('-Z', 'Y').to_euler()
        cam_data.lens = lens
        scene.render.filepath = os.path.join(folder, name + '.png')
        bpy.ops.render.render(write_still=True)


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    kit.clear()
    rock = kit.fuse(build_columns(), NAME, 1.7)
    print('[Bastiao] remesh', len(rock.data.polygons))
    kit.sculpt(rock, 19, crack_depth=2.4, cell=0.025, ledge_height=12.0, ledge_depth=1.6, lumps=3.0)
    carve(rock, cutters())
    kit.finish(rock, FACES)
    darken_hollows(rock)
    rock.data.materials.clear()
    rock.data.materials.append(bpy.data.materials.new('MAT_Rocha'))
    ice = build_ice(rock)
    export([rock, ice])
    if '--preview' in argv:
        preview(argv[argv.index('--preview') + 1])
