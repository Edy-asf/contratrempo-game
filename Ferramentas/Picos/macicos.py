# Maciços dos Picos acima das Nuvens: as montanhas grandes e largas, esculpidas no Blender em segundo plano:
#   blender.exe -b --python Ferramentas/Picos/macicos.py
# Cada maciço é um feixe de 7 a 13 colunas de arenito (pilhas de blocos de planta poligonal, uma camada um pouco
# diferente da outra, como os pilares de Zhangjiajie), de alturas que caem do meio para fora; um em cada três é um
# paredão (colunas enfileiradas). As colunas são fundidas por remesh de voxel (fendas fundas onde se encontram) e
# esculpidas como as montanhas de destaque (blocos de Voronoi, degraus irregulares), com a cor do relevo no vértice.
# Usa as peças de montanhas_destaque.py. Exporta Assets/Icaro/Models/Picos/Macico_1..8.fbx (para perto, ~40 mil faces)
# e Macico_1..8_Longe.fbx (para o fundo, ~10 mil faces). Coordenadas como as das montanhas de destaque: z = 0 é a
# altura de referência e tudo desce até z = -250.
import math
import os
import random
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import montanhas_destaque as kit  # noqa: E402

COUNT = 8


def massif(index):
    rnd = random.Random(100 + index)
    radius = rnd.uniform(38.0, 62.0)
    count = rnd.randint(7, 13)
    peak = rnd.uniform(70.0, 150.0)
    wall = index % 3 == 2
    heading = rnd.uniform(0.0, math.tau)
    parts = []
    for i in range(count):
        if wall:
            # Paredão: colunas enfileiradas, mais altas no meio da fila
            along = (i - (count - 1) * 0.5) * radius * 0.28
            x = math.cos(heading) * along + rnd.uniform(-5.0, 5.0)
            y = math.sin(heading) * along + rnd.uniform(-5.0, 5.0)
            d = abs(along)
        else:
            # Feixe: espiral (ângulo de ouro) a partir do meio
            a = i * 2.39996 + rnd.uniform(0.0, 0.4)
            d = 0.0 if i == 0 else radius * 0.75 * math.sqrt(i / (count - 1))
            x, y = math.cos(a) * d, math.sin(a) * d
        r = rnd.uniform(9.0, 20.0) * (1.25 if i == 0 else 1.0)
        top = max(-40.0, peak - d * rnd.uniform(0.5, 1.6) - rnd.uniform(0.0, 25.0))
        parts.append(kit.column(x, y, top, r, r * rnd.uniform(1.3, 1.7), rnd,
                                lean=(rnd.uniform(-4.0, 4.0), rnd.uniform(-4.0, 4.0)), bed=(7.0, 16.0), shift=0.12))
    obj = kit.fuse(parts, 'Macico_%d' % (index + 1), 1.6)
    kit.sculpt(obj, 50 + index, crack_depth=2.4, cell=0.025, ledge_height=12.0, ledge_depth=1.4, lumps=3.0)
    return obj


if __name__ == '__main__':
    for index in range(COUNT):
        kit.clear()
        near = massif(index)
        far = near.copy()
        far.data = near.data.copy()
        far.name = near.name + '_Longe'
        bpy.context.collection.objects.link(far)

        kit.finish(near, 40000)
        kit.export(near, near.name)
        kit.finish(far, 10000)
        kit.export(far, far.name)
