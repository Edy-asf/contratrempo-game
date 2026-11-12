# Rocha da ilha voadora de Archen Kaedra (pedido do usuário em 2026-11-10: detalhar a rocha flutuante), esculpida no
# Blender em segundo plano:
#   blender.exe -b --factory-startup --python Ferramentas/Picos/rocha_cidade.py [-- --preview <pasta>]
# Substitui o cone liso do SkyIsland da cidade. O topo é plano em z = 0 (a altura do topo da ilha) com a mesma beira de
# antes: o polígono de 40 lados do SkyIsland (raio 340,6 m com o ruído de Perlin da semente 4101, valores tirados do
# Mathf.PerlinNoise do Unity), então a barbacã, os lotes e o resto da cidade não mudam. Embaixo, como na referência
# (ArteFonte/Picos/CidadeFlutuante/ref/concept.png): paredão quase vertical de ~20 m, o corpo de rocha escura em massas
# penduradas e pontas que somem na nuvem, franja de pontas menores sob a beira, pingentes de gelo nas saliências.
# Fica livre: a beira de baixo dos seis motores antigravidade (a 30° da ponte e de 60 em 60 graus) e da chegada da
# ponte, e o anel que gira sob a cidade (raio 359 a 381 m, 100 m abaixo do topo): abaixo de 40 m a rocha fica a menos de
# 352 m do meio.
# Coordenadas: as do Blender com a origem no meio do topo; Unity (x, z) a partir do meio da cidade = (-x, -y) daqui.
# Material das montanhas (Icaro/SnowRock) com a cor do relevo no vértice; depois rodar neve_lateral.py no FBX.
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

NAME = 'Cidade_Rocha'
FACES = 170000
VOXEL = 2.1
RADIUS = 340.0 * 1.0 + 0.6           # SkyIslandRadius(340, 0)
BRIDGE_DEG = 55.0                    # direção da ponte na planta do Unity (x = leste, z = norte)
MOTOR_DEG = [BRIDGE_DEG + 30.0 + 60.0 * k for k in range(6)]
CLOUD_Z = -120.0                     # superfície do mar de nuvens (-90 no mundo; o topo da ilha fica a 30)

# Mathf.PerlinNoise(4101 * 0.37 + j * 0.5, 0) para j = 0..39 (o ruído da beira do SkyIsland)
PERLIN = [0.648148954, 0.47578007, 0.351844937, 0.379179746, 0.5347207, 0.59740144, 0.501386642, 0.364768982,
          0.354624987, 0.424616486, 0.533467948, 0.588394344, 0.552891076, 0.44738105, 0.384034365, 0.430616975,
          0.54315716, 0.600436628, 0.530283272, 0.414838076, 0.36811623, 0.414950848, 0.486426264, 0.539779186,
          0.586373806, 0.582023978, 0.470588446, 0.355452061, 0.343432069, 0.42651245, 0.524316907, 0.588046432,
          0.557213366, 0.444232494, 0.362555802, 0.383614719, 0.49084881, 0.582659304, 0.590036213, 0.506066263]


def unity_angle(x, y):
    """Ângulo (graus) na planta do Unity de um ponto do Blender."""
    return math.degrees(math.atan2(-y, -x)) % 360.0


def angle_gap(a, b):
    return abs((a - b + 180.0) % 360.0 - 180.0)


def rim_vertex(j):
    a = (j % 40) * math.tau / 40.0
    r = RADIUS * (1.0 + (PERLIN[j % 40] - 0.5) * 0.16)
    return Vector((-math.cos(a) * r, -math.sin(a) * r))      # Unity (cos, sin) -> Blender (-x, -y)


def rim(step=4.0):
    """A beira do topo, os 40 lados do SkyIsland divididos a cada ~'step' m (as quinas no mesmo lugar)."""
    pts = []
    for j in range(40):
        a, b = rim_vertex(j), rim_vertex(j + 1)
        n = max(1, int((b - a).length / step))
        pts += [a.lerp(b, k / n) for k in range(n)]
    return pts


def rim_radius(unity_deg):
    """Raio da beira numa direção (graus da planta do Unity)."""
    u = (unity_deg % 360.0) / 9.0
    j = int(u)
    a, b = rim_vertex(j), rim_vertex(j + 1)
    p = a.lerp(b, u - j)
    return p.length


def loft(bm, rings):
    verts = [[bm.verts.new(p) for p in ring] for ring in rings]
    for upper, lower in zip(verts, verts[1:]):
        n = len(upper)
        for k in range(n):
            m = (k + 1) % n
            bm.faces.new((lower[k], lower[m], upper[m], upper[k]))
    bm.faces.new(verts[0])
    bm.faces.new(list(reversed(verts[-1])))


def body(bm, seed=5):
    """O corpo: paredão quase vertical na beira e o corpo encolhendo em degraus de massa até dentro da nuvem, com a
    planta ondulando por ruído (sem cone liso)."""
    outline = rim()
    profile = [(3.0, 0.985), (-8.0, 0.975), (-18.0, 0.95), (-30.0, 0.9), (-48.0, 0.82), (-70.0, 0.74), (-95.0, 0.65),
               (-125.0, 0.54), (-165.0, 0.4), (-210.0, 0.26), (-255.0, 0.12)]
    rings = []
    for z, s in profile:
        wobble = 0.0 if z > -20.0 else min(1.0, (-20.0 - z) / 30.0)
        ring = []
        for p in outline:
            a = math.atan2(p.y, p.x)
            k = s * (1.0 + wobble * 0.09 * noise.noise(Vector((math.cos(a) * 3.0, math.sin(a) * 3.0, z * 0.02 + seed))))
            ring.append((p.x * k, p.y * k, z))
        rings.append(ring)
    loft(bm, rings)


def hang(bm, x, y, z_top, z_bottom, r_top, rnd, bed=(9.0, 18.0)):
    """Massa de rocha pendurada: pilha de blocos de planta poligonal (4 a 7 cantos), cada um um pouco diferente do de
    cima, afinando para baixo numa curva côncava até uma ponta grossa (o remesh não a parte)."""
    n = rnd.randint(4, 7)
    angles = [(k + rnd.uniform(-0.3, 0.3)) / n * math.tau for k in range(n)]
    radii = [rnd.uniform(0.8, 1.2) for _ in range(n)]
    lean = (rnd.uniform(-1, 1) * r_top * 0.25, rnd.uniform(-1, 1) * r_top * 0.25)
    power = rnd.uniform(0.9, 1.5)
    length = z_top - z_bottom
    z = z_top
    while z > z_bottom + 1.0:
        h = min(rnd.uniform(*bed), z - z_bottom)
        t = (z_top - (z - h)) / length
        r = max(2.2, r_top * (1.0 - t) ** power + 2.0)
        spin = rnd.uniform(-0.12, 0.12)
        cx = x + lean[0] * t + rnd.uniform(-0.06, 0.06) * r
        cy = y + lean[1] * t + rnd.uniform(-0.06, 0.06) * r
        kit.prism(bm, cx, cy, z - h, z + 0.8, [a + spin for a in angles], [v * rnd.uniform(0.92, 1.08) for v in radii], r,
                  top_scale=1.0 + 0.06 * rnd.random())
        z -= h


def blocked(unity_deg, depth):
    """Setores livres: a chegada da ponte (até 50 m de fundo) e a parede de baixo de cada motor."""
    if angle_gap(unity_deg, BRIDGE_DEG) < 8.0 and depth < 60.0:
        return True
    return any(angle_gap(unity_deg, m) < 10.0 for m in MOTOR_DEG)


def build():
    rnd = random.Random(4101)
    bm = bmesh.new()
    body(bm)
    # Massas penduradas sob o corpo: em grade solta, mais fundas no meio (o meio some na nuvem, a beira fica à vista)
    count = 0
    for gx in range(-6, 7):
        for gy in range(-6, 7):
            x = gx * 52.0 + rnd.uniform(-16, 16)
            y = gy * 52.0 + rnd.uniform(-16, 16)
            d = math.hypot(x, y)
            f = d / RADIUS
            if f > 0.82:
                continue
            bottom = -255.0 + 150.0 * f ** 1.4 + rnd.uniform(-20.0, 20.0)
            r = rnd.uniform(30.0, 46.0) * (1.0 - 0.3 * f)
            hang(bm, x, y, -24.0 - 40.0 * f ** 2 * 0.0 - 20.0 * (1.0 - f), bottom, r, rnd)
            count += 1
    # O paredão da beira: colunas de rocha encostadas umas nas outras, do topo (a beira fica no polígono de antes:
    # o que passa dela é cortado depois) até fundos diferentes, como os blocos pendurados da referência. Nos setores
    # dos motores e da chegada da ponte só colunas rasas (nada avança embaixo deles)
    fringe = 0
    a = rnd.uniform(0, 4)
    while a < 360.0:
        rr = rnd.uniform(16.0, 32.0)
        rc = rim_radius(a) - rr * 0.62
        u = math.radians(a)
        x, y = -math.cos(u) * rc, -math.sin(u) * rc
        if any(angle_gap(a, m) < 10.0 for m in MOTOR_DEG):
            bottom = rnd.uniform(-26.0, -36.0)
        elif angle_gap(a, BRIDGE_DEG) < 8.0:
            bottom = rnd.uniform(-30.0, -45.0)
        else:
            bottom = -rnd.choice([rnd.uniform(35.0, 60.0), rnd.uniform(55.0, 95.0), rnd.uniform(85.0, 130.0)])
        hang(bm, x, y, 3.0, bottom, rr, rnd, bed=(8.0, 16.0))
        fringe += 1
        # uma segunda fileira atrás, mais funda, para a beira não ficar uma parede só
        if rnd.random() < 0.6 and not blocked(a, 100.0):
            r2 = rnd.uniform(22.0, 36.0)
            c2 = rc - rr - r2 * 0.4
            hang(bm, -math.cos(u) * c2, -math.sin(u) * c2, -6.0, bottom - rnd.uniform(15.0, 50.0), r2, rnd)
            fringe += 1
        a += math.degrees(rr * 1.05 / rc)
    # Pontas grandes no meio do caminho entre a franja e o miolo, descendo até perto da nuvem e além
    mid = 0
    for k in range(60):
        if mid >= 16:
            break
        a = rnd.uniform(0, 360)
        if blocked(a, 100.0):
            continue
        rc = rim_radius(a) * rnd.uniform(0.62, 0.78)
        u = math.radians(a)
        hang(bm, -math.cos(u) * rc, -math.sin(u) * rc, -40.0, rnd.uniform(-130.0, -175.0), rnd.uniform(20.0, 30.0), rnd)
        mid += 1
    print('[Cidade] massas', count, 'franja', fringe, 'pontas', mid)
    mesh = bpy.data.meshes.new(NAME)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(NAME, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    for o in bpy.context.view_layer.objects:
        o.select_set(o is obj)
    mesh.remesh_voxel_size = VOXEL
    mesh.use_remesh_fix_poles = True
    bpy.ops.object.voxel_remesh()
    print('[Cidade] remesh', len(obj.data.polygons))
    return obj


def keep_largest(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    seen, parts = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, part = [v], []
        seen.add(v.index)
        while stack:
            u = stack.pop()
            part.append(u)
            for e in u.link_edges:
                w = e.other_vert(u)
                if w.index not in seen:
                    seen.add(w.index)
                    stack.append(w)
        parts.append(part)
    parts.sort(key=len, reverse=True)
    bmesh.ops.delete(bm, geom=[v for part in parts[1:] for v in part], context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    return len(parts) - 1


def sculpt(obj):
    """O esculpir das montanhas (blocos, fendas, degraus, lóbulos); perto do topo a rocha só recua (a beira não passa da
    de antes) e embaixo da beira dos motores nada avança."""
    mesh = obj.data
    for v in mesh.vertices:
        if v.co.z > -30.0:
            r = math.hypot(v.co.x, v.co.y)
            w = min(1.0, (v.co.z + 30.0) / 16.0)
            limit = rim_radius(unity_angle(v.co.x, v.co.y)) + (1.0 - w) * 30.0
            if r > limit:
                v.co.x *= limit / r
                v.co.y *= limit / r
    before = [v.co.copy() for v in mesh.vertices]
    kit.sculpt(obj, 41, crack_depth=4.2, cell=0.03, ledge_height=9.0, ledge_depth=2.6, lumps=5.5)
    for v in mesh.vertices:
        o = before[v.index]
        delta = v.co - o
        radial = Vector((o.x, o.y, 0.0)).normalized()
        out = delta.dot(radial)
        top = min(1.0, max(0.0, (o.z + 14.0) / 10.0))
        if out > 0.0 and top > 0.0:
            delta -= radial * out * top
        v.co = o + delta
        if v.co.z > -30.0:
            # a beira de cima fica no polígono de antes (as colunas encostadas passam dele)
            r = math.hypot(v.co.x, v.co.y)
            w = min(1.0, (v.co.z + 30.0) / 16.0)
            limit = rim_radius(unity_angle(v.co.x, v.co.y)) + (1.0 - w) * 30.0
            if r > limit:
                v.co.x *= limit / r
                v.co.y *= limit / r
        if v.co.z < -40.0:
            r = math.hypot(v.co.x, v.co.y)
            if r > 352.0:
                v.co.x *= 352.0 / r
                v.co.y *= 352.0 / r
        if v.co.z > 0.0:
            v.co.z = 0.0


def cap(obj):
    """Troca o topo por uma tampa só em z = 0, presa na beira. Roda depois da redução (a redução e a suavização
    abriam buracos na tampa). Tira todo o miolo do topo (o remesh e a escultura deixam depressões de até 2 m) e a faixa
    perto da beira que ficou plana, guarda só o corpo maior, e fecha o contorno de fora com um polígono só, triangulado
    e virado para cima (o preenchimento automático falhava no contorno irregular e deixava um buraco perto da ponte)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)

    def zone(c):
        return math.hypot(c.x, c.y) < rim_radius(unity_angle(c.x, c.y)) - 6.0

    # a faixa da beira: o que é quase plano e está perto do topo vai para z = 0, para sair junto com a tampa velha
    for v in bm.verts:
        if v.co.z > -3.0 and v.normal.z > 0.5:
            v.co.z = 0.0
    top = [f for f in bm.faces if all(v.co.z > -0.05 for v in f.verts) or
           (f.calc_center_median().z > -12.0 and zone(f.calc_center_median()))]
    bmesh.ops.delete(bm, geom=top, context='FACES')

    # só o corpo maior (o fundo das fendas do miolo fica solto)
    seen, parts = set(), []
    for f in bm.faces:
        if f in seen:
            continue
        stack, part = [f], []
        seen.add(f)
        while stack:
            g = stack.pop()
            part.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in seen:
                        seen.add(h)
                        stack.append(h)
        parts.append(part)
    parts.sort(key=len, reverse=True)
    for part in parts[1:]:
        bmesh.ops.delete(bm, geom=part, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')

    # A tampa: a cada 0,2 grau, o vértice de contorno mais de fora (perto do topo) vai para z = 0, e um leque a partir
    # do centro liga todos, em ordem de ângulo. Sem percorrer o contorno (ele passa duas vezes por alguns vértices); a
    # corda entre dois vizinhos fica sempre por fora do contorno de verdade, então não sobra fresta, e o que fica de
    # contorno por baixo (fundo de fendas perto da beira) fica tapado.
    bins = {}
    for e in bm.edges:
        if not e.is_boundary:
            continue
        for v in e.verts:
            a = math.atan2(v.co.y, v.co.x)
            k = int(math.floor(math.degrees(a) / 0.2))
            r = math.hypot(v.co.x, v.co.y)
            if k not in bins or r > bins[k][0]:
                bins[k] = (r, a, v)
    ring = [item for _, item in sorted(bins.items())]
    zs = sorted(v.co.z for _, _, v in ring)
    print('[Cidade] contorno da tampa:', len(ring), 'vértices, z mínimo', round(zs[0], 1), 'mediano', round(zs[len(zs) // 2], 1))
    for _, _, v in ring:
        v.co.z = 0.0
    # Onde o contorno pula mais de 0,6 grau, pontos novos na beira de antes, para a corda não entrar na cidade
    filled, fresh = [], []
    for item, nxt in zip(ring, ring[1:] + ring[:1]):
        filled.append(item)
        a0, a1 = item[1], nxt[1] if nxt[1] > item[1] else nxt[1] + math.tau
        steps = int((a1 - a0) / math.radians(0.6))
        for k in range(1, steps):
            a = a0 + (a1 - a0) * k / steps
            r = rim_radius(unity_angle(math.cos(a), math.sin(a))) - 0.3
            filled.append((r, a, bm.verts.new((math.cos(a) * r, math.sin(a) * r, 0.0))))
            fresh.append(filled[-1][2])
    print('[Cidade] pontos novos na beira', len(filled) - len(ring))
    ring = filled
    centre = bm.verts.new((0.0, 0.0, 0.0))
    relief = bm.verts.layers.float_color.get('Relevo')
    if relief is not None:
        for v in fresh + [centre]:
            v[relief] = (1.0, 1.0, 1.0, 1.0)
    tris = []
    for (_, _, v), (_, _, w) in zip(ring, ring[1:] + ring[:1]):
        if v is w:
            continue
        try:
            f = bm.faces.new((centre, v, w))
        except ValueError:
            continue
        f.normal_update()
        if f.normal.z < 0.0:
            f.normal_flip()
        f.smooth = False
        tris.append(f)
    bm.to_mesh(obj.data)
    bm.free()
    gaps = max(abs(b[1] - a[1]) for a, b in zip(ring, ring[1:]))
    print('[Cidade] tampa', len(tris), 'triângulos, faces do topo tiradas', len(top), ', maior salto de ângulo',
          round(math.degrees(gaps), 2), 'graus')


def build_ice(rock):
    """Pingentes debaixo das saliências, da beira até a nuvem (fora da parede dos motores)."""
    rnd = random.Random(77)
    bm = bmesh.new()
    faces = [(p.center.copy(), p.area) for p in rock.data.polygons
             if p.normal.z < -0.6 and CLOUD_Z + 10.0 < p.center.z < -2.0 and p.area > 5.0]
    rnd.shuffle(faces)
    count = 0
    for centre, _ in faces:
        if count >= 900:
            break
        a = unity_angle(centre.x, centre.y)
        if any(angle_gap(a, m) < 8.0 for m in MOTOR_DEG) and math.hypot(centre.x, centre.y) > rim_radius(a) - 30.0:
            continue
        # em cachos: o maior no meio e uns menores em volta
        for k in range(rnd.randint(2, 5)):
            off = Vector((rnd.uniform(-1.6, 1.6), rnd.uniform(-1.6, 1.6), 0.0)) if k else Vector()
            length = rnd.uniform(4.0, 11.0) if k == 0 else rnd.uniform(1.2, 5.0)
            base = centre + off
            kit_cone(bm, base + Vector((0.0, 0.0, -length)), base + Vector((0.0, 0.0, 0.5)), length * rnd.uniform(0.08, 0.13), 5, rnd)
        count += 1
    print('[Cidade] pingentes', count)
    mesh = bpy.data.meshes.new(NAME + '_Gelo')
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(NAME + '_Gelo', mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(bpy.data.materials.get('MAT_Gelo') or bpy.data.materials.new('MAT_Gelo'))
    return obj


def kit_cone(bm, tip, base, radius, sides, rnd):
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
    print('[Cidade] exportado', path, [len(o.data.polygons) for o in objects])


# ---------- prévia ----------

def proxies():
    """Só para a prévia: blocos da cidade em cima, os motores, o anel de baixo e o mar de nuvens."""
    rnd = random.Random(3)
    made = []
    for k in range(260):
        r = math.sqrt(rnd.random()) * 300.0
        a = rnd.uniform(0, math.tau)
        h = rnd.uniform(10, 26) * (1.4 if r < 120 else 1.0)
        bpy.ops.mesh.primitive_cube_add(size=1, location=(math.cos(a) * r, math.sin(a) * r, h / 2))
        o = bpy.context.active_object
        o.scale = (rnd.uniform(10, 18), rnd.uniform(10, 18), h)
        made.append(o)
    bpy.ops.mesh.primitive_cylinder_add(radius=36, depth=60, location=(-math.cos(math.radians(BRIDGE_DEG)) * -190, -math.sin(math.radians(BRIDGE_DEG)) * -190, 58))
    made.append(bpy.context.active_object)
    for m in MOTOR_DEG:
        u = math.radians(m)
        rc = rim_radius(m) - 8.0 + 13.0
        bpy.ops.mesh.primitive_cone_add(radius1=24, radius2=6, depth=52, location=(-math.cos(u) * rc, -math.sin(u) * rc, -66),
                                        rotation=(math.pi, 0, 0))
        made.append(bpy.context.active_object)
    bpy.ops.mesh.primitive_torus_add(major_radius=370, minor_radius=6, location=(0, 0, -100), major_segments=96)
    made.append(bpy.context.active_object)
    u = math.radians(BRIDGE_DEG)
    for k in range(10):
        d = RADIUS + 20 + k * 37
        bpy.ops.mesh.primitive_cube_add(size=1, location=(-math.cos(u) * d, -math.sin(u) * d, -2))
        o = bpy.context.active_object
        o.scale = (14, 14, 4)
        o.rotation_euler.z = u
        made.append(o)
    bpy.ops.mesh.primitive_plane_add(size=4000, location=(0, 0, CLOUD_Z))
    made.append(bpy.context.active_object)
    for o in made:
        o.name = 'PX_' + o.name
    return made


def look_colors():
    for obj in bpy.data.objects:
        if obj.type != 'MESH':
            continue
        mesh = obj.data
        relief = mesh.color_attributes.get('Relevo')
        look = mesh.color_attributes.new('Look', 'FLOAT_COLOR', 'POINT')
        for v in mesh.vertices:
            if obj.name.endswith('_Gelo'):
                c = (0.62, 0.8, 0.95)
            elif obj.name.startswith('PX_Plane'):
                c = (0.72, 0.72, 0.8)
            elif obj.name.startswith('PX_'):
                c = (0.5, 0.48, 0.52)
            elif relief is None:
                c = (0.3, 0.28, 0.3)
            else:
                r, g, b = relief.data[v.index].color[:3]
                k = 0.45 + 0.55 * r
                c = (0.9, 0.92, 1.0) if v.normal.z > 0.6 else (0.24 * k * (1.6 - 0.6 * g), 0.2 * k * (1.6 - 0.6 * g), 0.24 * k * (1.6 - 0.6 * g))
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
    scene.display.shading.show_shadows = True
    scene.render.resolution_x, scene.render.resolution_y = 1500, 900
    cam_data = bpy.data.cameras.new('cam')
    cam_data.clip_end = 8000.0
    cam = bpy.data.objects.new('cam', cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    u = math.radians(BRIDGE_DEG)
    out = Vector((-math.cos(u), -math.sin(u), 0.0))           # da cidade para a ponte
    side = Vector((-out.y, out.x, 0.0))
    views = [('das_vilas', out * 1250 + side * 250 + Vector((0, 0, 170)), Vector((0, 0, -40)), 35),
             ('de_lado', side * 900 + Vector((0, 0, -10)), Vector((0, 0, -50)), 32),
             ('da_ponte', out * 520 + side * 40 + Vector((0, 0, 4)), out * 250 + Vector((0, 0, -40)), 24),
             ('de_baixo', out * 420 - side * 160 + Vector((0, 0, -95)), Vector((0, 0, -30)), 24),
             ('outro_lado', -out * 900 + side * 300 + Vector((0, 0, 60)), Vector((0, 0, -40)), 32)]
    for name, eye, target, lens in views:
        cam.location = eye
        cam.rotation_euler = (target - eye).to_track_quat('-Z', 'Y').to_euler()
        cam_data.lens = lens
        scene.render.filepath = os.path.join(folder, name + '.png')
        bpy.ops.render.render(write_still=True)


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    kit.clear()
    rock = build()
    print('[Cidade] pedaços soltos apagados', keep_largest(rock))
    sculpt(rock)
    kit.finish(rock, FACES)
    cap(rock)
    for v in rock.data.vertices:
        if v.co.z > 0.0:
            v.co.z = 0.0
    rock.data.materials.clear()
    rock.data.materials.append(bpy.data.materials.new('MAT_Rocha'))
    ice = build_ice(rock)
    export([rock, ice])
    if '--preview' in argv:
        preview(argv[argv.index('--preview') + 1])
