# Renderiza um modelo 3D animado (glTF/GLB) numa folha de sprites para os inimigos do Ícaro (estilo Doom).
#
# Uso (Blender em segundo plano):
#   blender -b --python render_enemy_sprites.py -- <config.json>
#
# config.json:
#   {
#     "name": "Cascudo",                      nome do inimigo (pasta e arquivos de saída)
#     "model": "E:/.../CrabEnemy.glb",
#     "out": "E:/.../Assets/Icaro/Art/Sprites/Inimigos/Cascudo",
#     "height": 1.4,                          altura do modelo no jogo (m), na pose de descanso
#     "pixels_per_meter": 32,
#     "elevation": 12,                        graus da câmera acima do horizonte
#     "outline": true,                        contorno escuro (pixel art)
#     "hide": ["Icosphere"],                  objetos que não entram na imagem
#     "clips": [                              cada clipe: animação do modelo, trecho (0 a 1), quadros, laço
#       {"name": "Idle", "action": "Idle", "frames": 6, "loop": true},
#       {"name": "Windup", "action": "Bite_Front", "from": 0, "to": 0.45, "frames": 4, "loop": false}
#     ]
#   }
#
# Saída: <name>.png (grade: uma linha por clipe e direção, na ordem frente, direita, costas, esquerda; uma coluna por
# quadro; a linha 0 é a de cima) e <name>.json (tamanho da célula, pivô em pixels a partir do canto de baixo, e
# para cada clipe a primeira linha, os quadros, os quadros por segundo e o laço). É o formato que o Unity lê
# ("Ícaro > Sprites > Importar sprites dos inimigos").

import bpy
import json
import math
import os
import sys

import numpy as np
from mathutils import Matrix, Vector

DIRECTIONS = ["front", "right", "back", "left"]


def load_config():
    path = sys.argv[sys.argv.index("--") + 1]
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def find_action(name):
    for action in bpy.data.actions:
        if action.name == name or action.name.endswith("|" + name):
            return action
    raise RuntimeError("Animação não encontrada: " + name + " (" + ", ".join(a.name for a in bpy.data.actions) + ")")


def action_fcurve_lists(action):
    # Ações em camadas (Blender 4.4+) guardam as curvas em canais por slot; as antigas, direto na ação
    lists = []
    for layer in getattr(action, "layers", []):
        for strip in layer.strips:
            for channelbag in getattr(strip, "channelbags", []):
                lists.append(channelbag.fcurves)
    if not lists and hasattr(action, "fcurves"):
        lists.append(action.fcurves)
    return lists


def main():
    cfg = load_config()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=cfg["model"])
    scene = bpy.context.scene

    hidden = set(cfg.get("hide", []))
    for obj in bpy.data.objects:
        if obj.name in hidden:
            obj.hide_render = True
            obj.hide_viewport = True

    armature = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and not o.hide_render]

    # Raiz que gira o modelo (as direções) e escala para a altura pedida
    pivot = bpy.data.objects.new("Pivo", None)
    scene.collection.objects.link(pivot)
    for obj in bpy.data.objects:
        if obj.parent is None and obj is not pivot:
            obj.parent = pivot

    # Cor do material no Workbench (sem textura: a cor base do Principled)
    for material in bpy.data.materials:
        if not material.node_tree:
            continue
        for node in material.node_tree.nodes:
            if node.type == "BSDF_PRINCIPLED":
                color = node.inputs["Base Color"].default_value
                gain = cfg.get("brightness", 1.0)
                material.diffuse_color = (min(1.0, color[0] * gain), min(1.0, color[1] * gain),
                                          min(1.0, color[2] * gain), 1.0)
                break

    if armature.animation_data is None:
        armature.animation_data_create()
    # As animações que o importador deixa em faixas do NLA não podem somar por cima da ativa
    for track in armature.animation_data.nla_tracks:
        track.mute = True

    # Só os ossos animam: algumas animações também gravam a posição e o giro do próprio objeto do esqueleto (o
    # Idle do Crab Enemy o deita de lado), o que tiraria o modelo do lugar
    for action in bpy.data.actions:
        for curves in action_fcurve_lists(action):
            for curve in [c for c in curves if not c.data_path.startswith("pose.bones")]:
                curves.remove(curve)

    def evaluated_points():
        depsgraph = bpy.context.evaluated_depsgraph_get()
        points = []
        for obj in meshes:
            evaluated = obj.evaluated_get(depsgraph)
            mesh = evaluated.to_mesh()
            matrix = evaluated.matrix_world
            points.extend(matrix @ v.co for v in mesh.vertices)
            evaluated.to_mesh_clear()
        return points

    def set_pose(action, frame):
        # Ossos que a animação não grava voltariam com a pose da anterior (o Idle herdaria o fim do Death)
        for bone in armature.pose.bones:
            bone.location = (0.0, 0.0, 0.0)
            bone.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
            bone.rotation_euler = (0.0, 0.0, 0.0)
            bone.scale = (1.0, 1.0, 1.0)
        armature.animation_data.action = action
        slots = getattr(action, "slots", None)
        if slots and len(slots) > 0:
            armature.animation_data.action_slot = slots[0]
        scene.frame_set(int(math.floor(frame)), subframe=frame - math.floor(frame))

    # Escala pela altura na pose de descanso (primeiro quadro do Idle, ou da primeira animação)
    first = cfg["clips"][0]
    set_pose(find_action(first["action"]), find_action(first["action"]).frame_range[0])
    rest = evaluated_points()
    height = max(p.z for p in rest) - min(p.z for p in rest)
    pivot.scale = (cfg["height"] / height,) * 3
    bpy.context.view_layer.update()

    # Lista de quadros: (clipe, quadro do clipe, quadro da animação)
    jobs = []
    for clip in cfg["clips"]:
        action = find_action(clip["action"])
        start, end = action.frame_range
        a = start + (end - start) * clip.get("from", 0.0)
        b = start + (end - start) * clip.get("to", 1.0)
        count = clip["frames"]
        frames = []
        for i in range(count):
            # Laço: o último quadro não repete o primeiro; sem laço, o último é o fim do trecho
            t = i / count if clip.get("loop", False) else (i / max(1, count - 1))
            frames.append(a + (b - a) * t)
        seconds = (b - a) / scene.render.fps
        clip["_action"] = action
        clip["_frames"] = frames
        clip["_fps"] = count / seconds if seconds > 0 and clip.get("loop", False) else (
            (count - 1) / seconds if seconds > 0 else 8.0)

    # Câmera ortográfica olhando para +Y (o modelo do glTF olha para -Y), um pouco de cima
    elevation = math.radians(cfg.get("elevation", 12))
    view_dir = Vector((0.0, math.cos(elevation), -math.sin(elevation)))
    right = Vector((1.0, 0.0, 0.0))
    up = right.cross(view_dir).normalized()

    # Enquadramento: todos os quadros em todas as direções, centrado no pé (origem) na horizontal
    max_side, min_up, max_up = 0.0, 0.0, 0.0
    for clip in cfg["clips"]:
        for frame in clip["_frames"]:
            set_pose(clip["_action"], frame)
            for d in range(4):
                pivot.rotation_euler = (0.0, 0.0, math.radians(90.0 * d))
                bpy.context.view_layer.update()
                for p in evaluated_points():
                    u, v = p.dot(right), p.dot(up)
                    max_side = max(max_side, abs(u))
                    min_up = min(min_up, v)
                    max_up = max(max_up, v)

    ppm = cfg["pixels_per_meter"]
    margin = 2.0 / ppm
    cell_w = int(math.ceil((max_side + margin) * 2.0 * ppm))
    cell_w += cell_w % 2
    cell_h = int(math.ceil((max_up - min_up + margin * 2.0) * ppm))
    width_m, height_m = cell_w / ppm, cell_h / ppm
    bottom = min_up - margin

    camera_data = bpy.data.cameras.new("Camera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(width_m, height_m)
    camera = bpy.data.objects.new("Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    center = up * (bottom + height_m * 0.5)
    camera.location = center - view_dir * 20.0
    camera.rotation_euler = Matrix((right, up, -view_dir)).transposed().to_euler()
    # O ortho_scale vale para o lado maior; o outro é cortado pela proporção da imagem
    scene.render.resolution_x = cell_w
    scene.render.resolution_y = cell_h
    scene.render.resolution_percentage = 100
    scene.render.pixel_aspect_x = scene.render.pixel_aspect_y = 1.0

    # Workbench: cor chapada com luz de estúdio, sem suavização (pixels nítidos), fundo transparente
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.render_aa = "OFF"
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "MATERIAL"
    shading.show_object_outline = bool(cfg.get("outline", True))
    shading.object_outline_color = (0.03, 0.02, 0.04)
    shading.show_cavity = False
    shading.show_specular_highlight = False
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "Standard"
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"

    out = cfg["out"]
    os.makedirs(out, exist_ok=True)
    temp = os.path.join(out, "_quadros")
    os.makedirs(temp, exist_ok=True)

    rows = len(cfg["clips"]) * 4
    columns = max(clip["frames"] for clip in cfg["clips"])
    atlas = np.zeros((rows * cell_h, columns * cell_w, 4), dtype=np.float32)
    meta_clips = []
    for c, clip in enumerate(cfg["clips"]):
        for d in range(4):
            pivot.rotation_euler = (0.0, 0.0, math.radians(90.0 * d))
            row = c * 4 + d
            for i, frame in enumerate(clip["_frames"]):
                set_pose(clip["_action"], frame)
                path = os.path.join(temp, f"{clip['name']}_{DIRECTIONS[d]}_{i}.png")
                scene.render.filepath = path
                bpy.ops.render.render(write_still=True)
                image = bpy.data.images.load(path)
                pixels = np.empty(cell_w * cell_h * 4, dtype=np.float32)
                image.pixels.foreach_get(pixels)
                bpy.data.images.remove(image)
                # Pixels do Blender começam embaixo; a linha 0 da folha é a de cima
                block = pixels.reshape(cell_h, cell_w, 4)
                y0 = (rows - 1 - row) * cell_h
                atlas[y0:y0 + cell_h, i * cell_w:(i + 1) * cell_w] = block
        meta_clips.append({
            "name": clip["name"],
            "row": c * 4,
            "frames": clip["frames"],
            "fps": round(clip["_fps"], 3),
            "loop": bool(clip.get("loop", False)),
        })

    # Alfa binário (o Unity usa recorte, sem meio-termo)
    atlas[..., 3] = np.where(atlas[..., 3] >= 0.5, 1.0, 0.0)
    sheet = bpy.data.images.new(cfg["name"], width=columns * cell_w, height=rows * cell_h, alpha=True)
    sheet.pixels.foreach_set(atlas.ravel())
    sheet.filepath_raw = os.path.join(out, cfg["name"] + ".png")
    sheet.file_format = "PNG"
    sheet.save()

    for name in os.listdir(temp):
        os.remove(os.path.join(temp, name))
    os.rmdir(temp)

    meta = {
        "name": cfg["name"],
        "cellWidth": cell_w,
        "cellHeight": cell_h,
        "columns": columns,
        "rows": rows,
        "pixelsPerMeter": ppm,
        "pivotX": cell_w * 0.5,
        "pivotY": round((0.0 - bottom) * ppm, 3),
        "directions": DIRECTIONS,
        "clips": meta_clips,
    }
    with open(os.path.join(out, cfg["name"] + ".json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
    print("SPRITES OK", cfg["name"], cell_w, "x", cell_h, "células", columns, "x", rows)


main()
