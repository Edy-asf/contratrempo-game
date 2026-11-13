"""Folha de sprites do Pé-de-Musgo (chefe do Bosque), feita sobre o desenho aprovado.

O corpo é o mesmo do conceito (pe_musgo.py nos conceitos): pernas de pelo, tronco curvado com a barra comprida,
braços até os joelhos com garras de osso, juba sobre a corcova com cogumelos acesos num ombro e uma samambaia no
outro, cabeça baixa com o rosto escondido e dois olhos ciano de um pixel, galhos secos e fungos de prateleira
acesos. Cada parte ganha uma transformação (deslocamento, giro, escala) pela pose do quadro; a textura acompanha a
parte (Sprite.org), então o pelo não "nada" de um quadro para o outro. 8 px/m, como o desenho (86 px, ~10,7 m).

Uso:  py pe_de_musgo.py [pasta de saída] [--previa <pasta>]
Grava <saida>/Chefe_PeDeMusgo.png, Chefe_PeDeMusgo_Brilho.png e Chefe_PeDeMusgo.json (formato do LEIAME dos
sprites dos inimigos). Depois, no Unity: "Ícaro > Sprites > Importar sprites dos inimigos" e "Ícaro > Gerar prefabs".
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from px import *  # noqa: E402,F401
from organic import blob, stroke, along  # noqa: E402

NAME = "Chefe_PeDeMusgo"
PPM = 8
W, H = 112, 112
CX = 56          # x do meio do corpo na célula
OY = 24          # o y do conceito (chão em 85) vira y + OY
G = 85           # chão no conceito
DIRECTIONS = 4   # chefes: as quatro direções repetem a frente

LICHEN2 = R(0x0a100e, 0x141f19, 0x203126, 0x314732, 0x4a6340, 0x6b8452, 0x98ab78)
PALE = R(0x1b2420, 0x2c3a31, 0x465748, 0x667a62, 0x8d9f82, 0xb9c6a6)
SOIL = R(0x140d09, 0x24170f, 0x3a2617, 0x553a22, 0x73512f)
DEAD = R(0x1a120c, 0x2d1f14, 0x46301d, 0x5f4428)


# ---------- transformações das partes ----------

class Xf:
    """Transformação de uma parte em coordenadas do conceito (x a partir do meio do corpo, y do conceito):
    escala e giro em volta do pivô, depois o deslocamento; a do pai vem por cima."""

    def __init__(self, tx=0.0, ty=0.0, rot=0.0, piv=(0.0, 0.0), sx=1.0, sy=1.0, parent=None):
        self.tx, self.ty, self.rot, self.piv, self.sx, self.sy, self.parent = tx, ty, rot, piv, sx, sy, parent

    def __call__(self, p):
        x, y = p[0] - self.piv[0], p[1] - self.piv[1]
        x, y = x * self.sx, y * self.sy
        c, s = math.cos(math.radians(self.rot)), math.sin(math.radians(self.rot))
        x, y = x * c - y * s, x * s + y * c
        x, y = x + self.piv[0] + self.tx, y + self.piv[1] + self.ty
        return self.parent((x, y)) if self.parent else (x, y)

    def angle(self):
        return self.rot + (self.parent.angle() if self.parent else 0.0)

    def origin(self):
        """Deslocamento do pivô na tela (a textura da parte anda junto)."""
        q = self(self.piv)
        return (q[0] - self.piv[0], q[1] - self.piv[1])


def C(p):
    """Coordenada do conceito -> pixel da célula."""
    return (CX + p[0], p[1] + OY)


def pts(xf, points):
    return [C(xf(p)) for p in points]


def rot(p, center, deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    x, y = p[0] - center[0], p[1] - center[1]
    return (center[0] + x * c - y * s, center[1] + x * s + y * c)


def use(s, xf):
    s.org = xf.origin()


# ---------- peças do conceito ----------

def shaggy(s, mask, seed, bias=0.0, depth=2.0):
    """Pelo comprido: mechas verticais claras e escuras."""
    streak = s.noise(0.7, seed, 1, sx=0.35, sy=3.0)
    s.paint(mask, LICHEN2, depth=depth, bias=bias, tex=streak * 0.45, dither=0.15)
    s.shift(mask & (streak < -0.35), -1)


def strands(s, pts_from, seed, lo, hi, bias=-0.05, width=2.2, sway=0.0):
    """Mechas soltas penduradas a partir de uma lista de pontos (sway: balanço extra para o lado)."""
    rng = np.random.default_rng(seed)
    out = np.zeros((s.h, s.w), dtype=bool)
    for x, y in pts_from:
        ln = rng.uniform(lo, hi)
        sw = rng.uniform(-1.2, 1.2) + sway
        m = stroke(s, [(x, y), (x + sw * 0.4, y + ln * 0.5), (x + sw, y + ln)], width, 0.6)
        s.paint(m, LICHEN2, depth=0.6, bias=bias + rng.uniform(-0.12, 0.12),
                tex=s.noise(0.7, seed, 1, sx=0.3, sy=3) * 0.3, dither=0.1, sel=True)
        out |= m
    return out


def pale_patches(s, mask, seed, amount=0.25):
    n = s.noise(3, seed, 2)
    streak = s.noise(0.7, seed + 1, 1, sx=0.35, sy=3.0)
    m = mask & (n > amount) & (s.lvl >= 0)
    s.paint(m, PALE, depth=1.0, bias=-0.05, tex=streak * 0.4, dither=0.15, sel=False)


def shelf(s, x, y, w=2, lit=1.0):
    """Fungo de prateleira aceso: chapéu âmbar com a sombra embaixo."""
    s.paint(s.rect(x - w / 2, y, x + w / 2, y + 1), AMBER, normal="flat", glow=lit > 0.3, bias=0.15 - (1 - lit) * 0.6,
            sel=False)
    s.paint(s.rect(x - w / 2 + 0.5, y + 1, x + w / 2 - 0.5, y + 2), AMBER, normal="flat", glow=lit > 0.3,
            bias=-0.35 - (1 - lit) * 0.4, sel=False)


def mushroom(s, x, y, h=2, cap=1.6, state="on", swell=0.0):
    """Cogumelo do ombro (o ponto fraco): aceso, mais aceso (rugido) ou apagado e murcho (atordoado)."""
    s.paint(s.line([(x, y), (x, y - h)], 1), BONE, normal="flat", bias=-0.1, sel=False)
    cap = cap + swell * 0.7
    ry = 1.3 + swell * 0.4
    if state == "dark":
        cap *= 0.8
    c = s.ellipse(x, y - h - 0.5, cap + 0.4, ry) & (s.Y < y - h + 0.3)
    if state == "dark":
        s.paint(c, DEAD, normal="ellipse", center=(x - 0.6, y - h - 1), radii=(cap + 0.5, 1.6), sel=False)
        return
    s.paint(c, AMBER, normal="ellipse", center=(x - 0.6, y - h - 1), radii=(cap + 0.5, 1.6), glow=True,
            bias=0.25 if state == "bright" else 0.0, sel=False)


def vine(s, points, seed):
    m = stroke(s, points, 1.0)
    s.paint(m, MOSS, normal="flat", bias=-0.15, sel=False)
    rng = np.random.default_rng(seed)
    for t in np.linspace(0.2, 0.9, 4):
        x, y = along(points, t)
        d = rng.choice([-1, 1])
        s.paint(s.dot(x + d, y) | s.dot(x + d * 2, y - 1), MOSS, normal="flat", bias=0.2, sel=False)


# ---------- pose ----------

DEFAULT = dict(
    sway=0.0, crouch=0.0, lean=0.0, bsx=1.0, bsy=1.0,          # tronco
    hx=0.0, hy=0.0, htilt=0.0,                                  # cabeça
    mane=0.0,                                                   # juba atrasada (y)
    armL=(0.0, 0.0), armR=(0.0, 0.0),                           # (giro para fora, dobra do cotovelo) em graus
    footL=(0.0, 0.0), footR=(0.0, 0.0),                         # (x, altura) do pé
    eyes="on", mouth=0.0, mush="on", swell=0.0, shelves=1.0,
    clod=0.0,         # torrão na mão esquerda (0 sem, 0..1 tamanho)
    debris=0.0,       # terra voando em volta dos pés (pouso)
    dig=0.0,          # terra saindo do chão perto da mão esquerda
    spores=0.0,       # esporos soltos em volta da juba (rugido)
    stars=-1.0,       # fase das estrelinhas do atordoado (<0 sem)
    swipe=0.0,        # rastro da garra (golpe)
    keep=1.0,         # fração de pixels visíveis (camuflagem), os olhos sempre ficam
    frame=0,
)


def P(**kw):
    p = dict(DEFAULT)
    p.update(kw)
    return p


def arm_path(shoulder, k, a, b):
    """Caminho do braço k (-1 esquerda da tela, 1 direita) a partir do ombro: o de pendurado do conceito, girado
    'a' graus para fora em volta do ombro e o antebraço mais 'b' em volta do cotovelo."""
    f = -k
    base = [(0.0, 0.0), (-7.0 * f, 11.0), (-9.0 * f, 25.0), (-9.0 * f, 37.0)]
    out = [rot(q, (0, 0), a * f) for q in base]
    elbow = out[1]
    out = out[:2] + [rot(q, elbow, b * f) for q in out[2:]]
    return [(shoulder[0] + q[0], shoulder[1] + q[1]) for q in out]


def leg_geo(k, p, body):
    f = -k
    hip = body((-6.0 * f, 54.0))
    fx, lift = p["footL"] if k == -1 else p["footR"]
    foot = (-8.0 * f + fx, 82.0 - lift)
    dx, dy = foot[0] - hip[0], foot[1] - hip[1]
    L = math.hypot(dx, dy)
    bend = math.sqrt(max(0.0, 28.0 ** 2 - L * L)) * 0.55
    knee = (hip[0] + dx * 0.43 - 1.14 * f - bend * f, hip[1] + dy * 0.43)
    ankle = (hip[0] + dx * 0.75 + 0.5 * f - bend * 0.35 * f, hip[1] + dy * 0.75)
    shin = (hip[0] + dx * 0.5 - bend * 0.6 * f, hip[1] + dy * 0.5)
    return [hip, knee, ankle, foot], shin, foot


def draw(p):
    s = Sprite(W, H, 8)
    rng = np.random.default_rng(4)
    ground = Xf()

    body = Xf(tx=p["sway"], ty=p["crouch"], rot=p["lean"], piv=(0.0, 52.0), sx=p["bsx"], sy=p["bsy"])
    mane_xf = Xf(ty=p["mane"], parent=body)
    head = Xf(tx=p["hx"], ty=p["hy"], rot=p["htilt"], piv=(0.0, 24.0), parent=body)

    # ---- pernas de pelo
    legs = {k: leg_geo(k, p, body) for k in (-1, 1)}
    for k in (-1, 1):
        path, _, _ = legs[k]
        s.org = (path[0][0] - (-6.0 * -k), path[0][1] - 54.0)
        leg = stroke(s, [C(q) for q in path], 7, 4.5)
        shaggy(s, leg, 3 + k, -0.2, 1.2)
        # o conceito pinta as mechas e os dedos da outra perna logo depois de cada perna
        _, shin, foot = legs[-k]
        strands(s, [C((shin[0] + dx, shin[1] + abs(dx))) for dx in (-3, -1, 1, 3)], 30 + k, 4, 8, -0.2, 1.8)
        for dx in (-2.5, 0, 2.5):
            a = C((foot[0] + dx, foot[1]))
            b = C((foot[0] + dx * 1.4, foot[1] + 3))
            s.paint(stroke(s, [a, b], 1.3, 0.6), BONE, normal="flat", bias=-0.15, sel=False)

    # ---- tronco curvado (corcova) e a barra comprida
    use(s, body)
    torso = blob(s, pts(body, [(-14, 25), (-8, 17), (8, 17), (15, 24), (15, 35), (12, 46), (8, 52), (-8, 52),
                               (-12, 46), (-15, 35)]), 1.2, 3)
    shaggy(s, torso, 4, 0.0, 2.5)
    pale_patches(s, torso, 21, 0.5)
    hem = [(dx, 48 + rng.uniform(-2, 1)) for dx in np.arange(-12, 13, 1.9)]
    strands(s, pts(body, hem), 5, 5, 16, -0.08)
    strands(s, pts(body, hem[1::3]), 6, 3, 8, 0.1, 1.6)

    # ---- braços compridos até os joelhos, garras de osso
    hands = {}
    for k in (-1, 1):
        a, b = p["armL"] if k == -1 else p["armR"]
        shoulder = body((-13.0 * -k, 25.0))
        path = [C(q) for q in arm_path(shoulder, k, a, b)]
        s.org = (path[0][0] - C((-13.0 * -k, 25.0))[0], path[0][1] - C((-13.0 * -k, 25.0))[1])
        arm = stroke(s, path, 8, 5.5)
        shaggy(s, arm, 6 + k, -0.05 + 0.05 * k, 1.5)
        strands(s, [along(path, t) for t in np.linspace(0.2, 0.75, 7)], 10 + k, 5, 10, -0.08)
        hx, hy = along(path, 1.0)
        ex, ey = along(path, 0.9)
        phi = math.degrees(math.atan2(-(hx - ex), (hy - ey)))
        hand = blob(s, [rot(q, (hx, hy), phi) for q in
                        [(hx - 2.8, hy - 1), (hx + 2.8, hy - 1), (hx + 3, hy + 3), (hx - 3, hy + 3)]])
        s.paint(hand, LICHEN2, depth=0.8, bias=-0.35)
        for dx in (-2, 0, 2):
            claw = [(hx + dx, hy + 2.5), (hx + dx * 1.2, hy + 6), (hx + dx * 1.1 - k * 0.8, hy + 8.5)]
            s.paint(stroke(s, [rot(q, (hx, hy), phi) for q in claw], 1.1, 0.5), BONE, normal="flat", bias=-0.25,
                    sel=False)
        hands[k] = (hx, hy, phi, path)

    # rastro da garra (golpe): arco claro atrás da mão direita
    if p["swipe"] > 0:
        hx, hy, phi, path = hands[1]
        sx0, sy0 = path[0]
        r = math.hypot(hx - sx0, hy - sy0) + 3
        a1 = math.degrees(math.atan2(hy - sy0, hx - sx0))
        for i in range(10):
            a = math.radians(a1 - i * 9 * p["swipe"])
            x, y = sx0 + math.cos(a) * r, sy0 + math.sin(a) * r
            if i % 2 == 0 or i < 4:
                s.paint(s.dot(x, y) | s.dot(x, y + 1), PALE, normal="flat", bias=0.45 - i * 0.05, sel=False)

    # ---- torrão na mão esquerda
    if p["clod"] > 0:
        hx, hy, phi, _ = hands[-1]
        r = 3.2 * p["clod"] + 0.8
        cx, cy = hx, hy + 4
        clod = blob(s, [(cx - r, cy), (cx - r * 0.6, cy - r * 0.9), (cx + r * 0.5, cy - r), (cx + r, cy - r * 0.1),
                        (cx + r * 0.7, cy + r * 0.8), (cx - r * 0.4, cy + r)], 0.5, 12)
        s.paint(clod, SOIL, depth=1.0, bias=0.05, tex=s.noise(1.2, 33, 1) * 0.3)
        s.paint(clod & (s.Y < cy - r * 0.25), MOSS, depth=0.8, bias=0.05, tex=s.noise(1, 34, 1) * 0.3, sel=False)
        s.paint(s.dot(cx - r * 0.4, cy + r * 0.3) | s.dot(cx + r * 0.3, cy + r * 0.5), STONE, normal="flat",
                bias=0.2, sel=False)

    # ---- juba sobre a corcova
    use(s, mane_xf)
    mane = blob(s, pts(mane_xf, [(-19, 27), (-14, 17), (-6, 15), (6, 15), (14, 17), (19, 27), (11, 30), (0, 29),
                                 (-11, 30)]), 1.3, 9)
    shaggy(s, mane, 8, 0.12, 1.8)
    pale_patches(s, mane, 23, 0.45)
    strands(s, pts(mane_xf, [(dx, 27 + rng.uniform(-1, 1.5)) for dx in np.arange(-17, 18, 2.2)]), 12, 4, 10, 0.05)
    # cogumelos num ombro (o ponto fraco) e uma samambaia no outro
    for x, y, h, c in ((-16, 21, 2, 1.3), (-13, 19, 3, 2.0), (-10.5, 19.5, 1.5, 1.1)):
        q = C(mane_xf((x, y)))
        mushroom(s, q[0], q[1], h, c, p["mush"], p["swell"])
    fern = pts(mane_xf, [(13, 19), (16, 14), (20, 11)])
    s.paint(stroke(s, fern, 1), MOSS, normal="flat", bias=0.05, sel=False)
    for t in np.linspace(0.25, 0.95, 4):
        x, y = along(fern, t)
        s.paint(s.line([(x - 1, y - 1.5), (x, y)], 1) | s.line([(x + 1.5, y + 0.5), (x, y)], 1), MOSS,
                normal="flat", bias=0.25, sel=False)
    vine(s, pts(body, [(-8, 27), (-9, 36), (-7, 44)]), 1)
    vine(s, pts(body, [(9, 27), (10, 33)]), 2)

    # ---- cabeça baixa à frente da corcova, rosto escondido
    use(s, head)
    hood = blob(s, pts(head, [(-6, 8), (0, 6), (6, 8), (7.5, 15), (6, 23), (0, 25), (-6, 23), (-7.5, 15)]), 0.8, 6)
    neck_y = C(head((0, 14)))[1]
    s.paint(ndimage.binary_dilation(hood) & ~hood & (s.Y > neck_y), LICHEN2, normal="flat", bias=-0.45, sel=False)
    shaggy(s, hood, 7, 0.22, 1.5)
    m = p["mouth"]
    face = blob(s, pts(head, [(-4, 13), (4, 13), (4.3, 17), (1.8, 21.5 + m * 2), (0, 22.5 + m * 2.4),
                              (-1.8, 21.5 + m * 2), (-4.3, 17)]))
    s.paint(face, DARK, normal="flat", bias=0.15)
    mc = C(head((0, 19 + m * 1.3)))
    s.paint(s.ellipse(mc[0], mc[1], 2.5 + m * 0.8, 2 + m * 1.6, head.angle()), DARK, normal="flat", bias=-0.3,
            sel=False)
    if m > 0.45:
        for dx in (-1.6, 0.0, 1.6):
            q = C(head((dx, 19 + m * 1.3 - (2 + m * 1.6) + 0.6)))
            s.paint(s.dot(*q), BONE, normal="flat", bias=-0.1, sel=False)
        q = C(head((0.0, 19 + m * 1.3 + (2 + m * 1.6) - 0.8)))
        s.paint(s.dot(q[0] - 1, q[1]) | s.dot(q[0] + 1, q[1]), BONE, normal="flat", bias=-0.3, sel=False)
    if p["eyes"] != "off":
        bias = {"on": 0.4, "bright": 0.6, "dim": -0.4}[p["eyes"]]
        for k in (-1, 1):
            q = C(head((-k * 2, 16)))
            e = s.dot(*q)
            if p["eyes"] == "bright":
                e |= s.dot(q[0], q[1] + 1)
            s.paint(e, BIO, normal="flat", glow=True, bias=bias, sel=False)
    strands(s, pts(head, [(dx, 11) for dx in (-5, -3, -1.2, 1.2, 3, 5)]), 13, 2, 5, 0.15, 1.7)
    strands(s, pts(head, [(dx, 22) for dx in (-5, -2.5, 2.5, 5)]), 14, 3, 7, 0.05, 1.6)

    # ---- galhos secos: um em forquilha, o outro partido, musgo e um fio de líquen
    left = pts(head, [(-4, 8), (-8, 3), (-12, 0), (-16, -1)])
    s.paint(stroke(s, left, 2, 0.8) | stroke(s, [along(left, 0.45), C(head((-9, -2))), C(head((-8, -4)))], 1.3, 0.7)
            | stroke(s, [along(left, 0.75), C(head((-15, 3)))], 1.1, 0.6), BARK, depth=0.4, bias=0.15, sel=False)
    right = pts(head, [(4, 8), (7, 3), (9, 1)])
    s.paint(stroke(s, right, 2, 1.3), BARK, depth=0.4, bias=0.15, sel=False)
    q1, q2 = C(head((9, 1))), C(head((10, 0)))
    s.paint(s.dot(*q1) | s.dot(*q2), BARK, normal="flat", bias=0.5, sel=False)
    q = C(head((-9, 3)))
    s.paint(s.ellipse(q[0], q[1], 1.5, 0.8), MOSS, normal="flat", bias=0.15, sel=False)
    s.paint(stroke(s, pts(head, [(-14, 0), (-14, 3), (-13.5, 6)]), 1, 0.6), PALE, normal="flat", bias=0.1, sel=False)

    # ---- fungos de prateleira acesos e pontos ciano espalhados
    use(s, body)
    for x, y in ((9, 36), (-7, 41), (2, 31)):
        qx, qy = C(body((x, y)))
        if 0 <= int(qy) < H and 0 <= int(qx) < W and s.lvl[int(qy), int(qx)] >= 0:
            shelf(s, qx, qy, 2 if y % 2 else 3, p["shelves"])
    for _ in range(6):
        x, y = rng.uniform(-16, 16), rng.uniform(30, 64)
        cyan, bias = rng.random() < 0.4, rng.uniform(-0.2, 0.25)
        qx, qy = C(body((x, y)))
        xi, yi = int(qx), int(qy)
        if 0 <= yi < H and 0 <= xi < W and s.lvl[yi, xi] >= 0 and not face[yi, xi]:
            s.paint(s.dot(qx, qy), BIO if cyan else AMBER, normal="flat", glow=p["shelves"] > 0.3,
                    bias=bias - (1 - p["shelves"]) * 0.6, sel=False)

    # ---- efeitos presos ao corpo
    fx = np.random.default_rng(100 + p["frame"])
    if p["debris"] > 0:
        # terra e musgo voando dos pés para os lados
        for i in range(18):
            side = -1 if i % 2 else 1
            d = fx.uniform(10, 26) * p["debris"]
            x = side * (6 + d)
            y = G - fx.uniform(0, 1) * 12 * p["debris"] - fx.uniform(0, 2)
            ramp = (SOIL, MOSS, STONE)[i % 3]
            q = C((x, y))
            dot = s.dot(*q)
            if i % 4 == 0:
                dot |= s.dot(q[0] + 1, q[1])
            s.paint(dot, ramp, normal="flat", bias=fx.uniform(-0.1, 0.3), sel=False)
    if p["dig"] > 0:
        hx, hy, _, _ = hands[-1]
        for i in range(10):
            a = math.radians(fx.uniform(200, 340))
            d = fx.uniform(2, 8) * p["dig"]
            q = (hx + math.cos(a) * d, hy + 6 + math.sin(a) * d)
            s.paint(s.dot(*q), SOIL if i % 3 else MOSS, normal="flat", bias=fx.uniform(-0.1, 0.3), sel=False)
    if p["spores"] > 0:
        for i in range(int(16 * p["spores"])):
            x = fx.uniform(-24, 24)
            y = fx.uniform(4, 34)
            q = C(mane_xf((x, y)))
            s.paint(s.dot(*q), BIO if i % 3 == 0 else AMBER, normal="flat", glow=True, bias=fx.uniform(-0.3, 0.3),
                    sel=False)
    if p["stars"] >= 0:
        cx0, cy0 = C(head((0, 4)))
        for i in range(3):
            a = p["stars"] * 2 * math.pi + i * 2 * math.pi / 3
            q = (cx0 + math.cos(a) * 9, cy0 + math.sin(a) * 2.5)
            st = s.dot(*q) | s.dot(q[0] + 1, q[1]) | s.dot(q[0] - 1, q[1]) | s.dot(q[0], q[1] + 1) | \
                s.dot(q[0], q[1] - 1)
            s.paint(st, BIO, normal="flat", glow=True, bias=0.3 if math.sin(a) > 0 else -0.2, sel=False)

    s.org = (0.0, 0.0)
    s.finish()
    s._face = face
    return s


# ---------- camuflagem e imagem final ----------

BAYER8 = (np.array([
    [0, 32, 8, 40, 2, 34, 10, 42], [48, 16, 56, 24, 50, 18, 58, 26], [12, 44, 4, 36, 14, 46, 6, 38],
    [60, 28, 52, 20, 62, 30, 54, 22], [3, 35, 11, 43, 1, 33, 9, 41], [51, 19, 59, 27, 49, 17, 57, 25],
    [15, 47, 7, 39, 13, 45, 5, 37], [63, 31, 55, 23, 61, 29, 53, 21]]) + 0.5) / 64.0


def render(p):
    s = draw(p)
    img = np.array(s.image())
    glow = np.array(s.image(glow_only=True))
    if p["keep"] < 1.0:
        # Camuflagem: some pixel a pixel num pontilhado ordenado que escorre um pouco a cada quadro; os olhos ficam
        yy, xx = np.mgrid[0:H, 0:W]
        bay = BAYER8[(yy + p["frame"] * 3) % 8, (xx + p["frame"]) % 8]
        eyes = s.glow & (s.rid == s._rampid(BIO))
        keep = (bay < p["keep"]) | eyes
        img[~keep, 3] = 0
        glow[~keep, 3] = 0
    return Image.fromarray(img, "RGBA"), Image.fromarray(glow, "RGBA")


# ---------- clipes ----------

def ease(t):
    return t * t * (3 - 2 * t)


def clips():
    out = []

    # Parado: respira (a corcova sobe e desce, os braços balançam um pouco)
    idle = []
    for f in range(4):
        b = [0, -1, -1, 0][f]
        idle.append(P(crouch=b, bsy=1 - 0.012 * b, armL=(1.5 * math.sin(f * 1.57), 0),
                      armR=(1.5 * math.sin(f * 1.57 + 1), 0), mane=0.4 * math.sin(f * 1.57), frame=f))
    out.append(("Idle", idle, 3, True))

    # Andando: passadas pesadas, o corpo afunda em cada pisada e os braços vão e voltam
    move = []
    for f in range(6):
        ph = f / 6 * 2 * math.pi
        liftL = max(0.0, math.sin(ph)) * 3.5
        liftR = max(0.0, -math.sin(ph)) * 3.5
        bob = abs(math.cos(ph)) * 1.6
        move.append(P(crouch=bob, sway=math.sin(ph) * 1.2, lean=math.sin(ph) * 1.5,
                      footL=(math.sin(ph) * 0.8, liftL), footR=(math.sin(ph) * 0.8, liftR),
                      armL=(math.sin(ph) * 9, max(0, math.sin(ph)) * 10), armR=(-math.sin(ph) * 9, max(0, -math.sin(ph)) * 10),
                      mane=bob * 0.5, hy=bob * 0.3, frame=f))
    out.append(("Move", move, 6, True))

    # Dano: tranco para trás
    out.append(("Hit", [P(lean=-4, hy=-1, htilt=-5, armL=(10, 10), armR=(10, 10), frame=0),
                        P(lean=-2, hy=-0.5, htilt=-2, armL=(5, 5), armR=(5, 5), frame=1)], 10, False))

    # Morte: cambaleia, cai de joelhos, desaba para a frente e apaga
    death = [
        P(lean=-5, hy=-2, htilt=-8, armL=(25, 20), armR=(20, 25), mouth=0.6, eyes="bright", frame=0),
        P(crouch=6, lean=-2, hy=1, armL=(12, 10), armR=(10, 12), mouth=0.4, frame=1),
        P(crouch=13, hy=3, htilt=6, armL=(8, -10), armR=(8, -10), footL=(-2, 0), footR=(2, 0), eyes="on",
          frame=2),
        P(crouch=19, bsy=0.92, hy=6, htilt=10, armL=(30, -40), armR=(30, -40), footL=(-3, 0), footR=(3, 0),
          eyes="dim", shelves=0.6, frame=3),
        P(crouch=24, bsy=0.85, bsx=1.06, hy=8, htilt=14, armL=(55, -60), armR=(55, -60), footL=(-4, 0),
          footR=(4, 0), eyes="dim", mush="dark", shelves=0.35, frame=4),
        P(crouch=27, bsy=0.8, bsx=1.08, hy=9, htilt=16, armL=(62, -66), armR=(62, -66), footL=(-4, 0),
          footR=(4, 0), eyes="off", mush="dark", shelves=0.15, frame=5),
        P(crouch=28, bsy=0.78, bsx=1.1, hy=9.5, htilt=17, armL=(64, -68), armR=(64, -68), footL=(-4, 0),
          footR=(4, 0), eyes="off", mush="dark", shelves=0.0, frame=6),
    ]
    out.append(("Death", death, 7, False))

    # Salto esmagador: agacha e puxa os braços para cima; no ar, encolhe as pernas; cai batendo os punhos no chão
    out.append(("Salto_Aviso", [
        P(crouch=4, armL=(25, 10), armR=(25, 10), hy=1, eyes="bright", frame=0),
        P(crouch=8, armL=(60, 20), armR=(60, 20), hy=2, htilt=-3, eyes="bright", frame=1),
        P(crouch=11, armL=(95, 30), armR=(95, 30), hy=2.5, htilt=-6, eyes="bright", mouth=0.3, frame=2),
    ], 6, False))
    out.append(("Salto_Ar", [
        P(crouch=-3, bsy=1.04, footL=(-2, 9), footR=(2, 9), armL=(150, 15), armR=(150, 15), mane=-1.5, hy=-1,
          mouth=0.5, eyes="bright", frame=0),
        P(crouch=-2, bsy=1.03, footL=(-2, 8), footR=(2, 8), armL=(155, 25), armR=(155, 25), mane=-1, hy=-1,
          mouth=0.5, eyes="bright", frame=1),
    ], 8, True))
    out.append(("Salto_Queda", [
        P(crouch=13, bsy=0.9, bsx=1.06, armL=(28, -30), armR=(28, -30), footL=(-3, 0), footR=(3, 0), hy=3,
          mane=2, debris=0.45, mouth=0.4, frame=0),
        P(crouch=10, bsy=0.94, bsx=1.04, armL=(26, -22), armR=(26, -22), footL=(-3, 0), footR=(3, 0), hy=2,
          mane=1.5, debris=0.85, frame=1),
        P(crouch=6, bsy=0.98, armL=(18, -10), armR=(18, -10), footL=(-2, 0), footR=(2, 0), hy=1, mane=0.5,
          debris=1.0, frame=2),
    ], 12, False))

    # Agarrão: arma o braço direito para trás por cima do ombro e passa a garra na frente do corpo
    out.append(("Agarrao_Aviso", [
        P(crouch=2, lean=-3, armR=(50, 40), armL=(-6, 8), sway=-1, frame=0),
        P(crouch=4, lean=-6, armR=(105, 70), armL=(-8, 12), sway=-2, htilt=-4, eyes="bright", frame=1),
        P(crouch=5, lean=-8, armR=(140, 80), armL=(-10, 15), sway=-3, htilt=-6, eyes="bright", mouth=0.3,
          frame=2),
    ], 6, False))
    out.append(("Agarrao_Golpe", [
        P(crouch=4, lean=2, armR=(70, 5), armL=(-5, 10), sway=1, swipe=0.6, mouth=0.5, eyes="bright", frame=0),
        P(crouch=4, lean=7, armR=(-20, 0), armL=(5, 10), sway=3, swipe=1.0, mouth=0.6, eyes="bright", frame=1),
        P(crouch=3, lean=8, armR=(-42, -15), armL=(10, 10), sway=3, mouth=0.3, frame=2),
    ], 14, False))

    # Arremesso de torrão: enfia a mão esquerda no chão, arranca o torrão, ergue e arremessa
    out.append(("Torrao_Aviso", [
        P(crouch=6, lean=-4, armL=(-14, 0), armR=(12, 8), hy=2, frame=0),
        P(crouch=11, lean=-7, armL=(-18, -4), armR=(18, 10), hy=3, dig=0.8, frame=1),
        P(crouch=9, lean=-5, armL=(-10, 0), armR=(15, 8), hy=2, dig=1.0, clod=0.8, eyes="bright", frame=2),
    ], 6, False))
    out.append(("Torrao_Golpe", [
        P(crouch=-1, bsy=1.03, lean=3, armL=(165, 20), armR=(20, 10), clod=1.0, hy=-1, mouth=0.4, eyes="bright",
          frame=0),
        P(crouch=2, lean=6, armL=(110, 5), armR=(10, 5), hy=0, mouth=0.6, swipe=0.0, frame=1),
        P(crouch=3, lean=7, armL=(40, -10), armR=(6, 4), hy=1, mouth=0.2, frame=2),
    ], 12, False))

    # Rugido de esporos: ergue a cabeça, os olhos piscam e os cogumelos do ombro incham (o ponto fraco acende)
    out.append(("Rugido_Aviso", [
        P(hy=-1, htilt=-4, armL=(15, 5), armR=(15, 5), mush="bright", swell=0.3, eyes="on", frame=0),
        P(hy=-2, htilt=-7, armL=(25, 8), armR=(25, 8), bsx=1.03, mush="bright", swell=0.6, eyes="off", frame=1),
        P(hy=-3, htilt=-9, armL=(32, 10), armR=(32, 10), bsx=1.05, mush="bright", swell=0.85, eyes="bright",
          mouth=0.2, frame=2),
        P(hy=-3.5, htilt=-10, armL=(36, 12), armR=(36, 12), bsx=1.06, mush="bright", swell=1.0, eyes="off",
          mouth=0.3, frame=3),
    ], 6, False))
    out.append(("Rugido_Golpe", [
        P(hy=-4, htilt=-12, armL=(48, 20), armR=(48, 20), bsx=1.06, sway=s, mouth=1.0, eyes="bright", mush="bright",
          swell=0.5, spores=1.0, mane=-1, frame=f)
        for f, s in enumerate((-1, 1, -1, 1))
    ], 10, True))

    # Atordoado: os cogumelos apagam, a cabeça pende e estrelinhas ciano giram em volta
    out.append(("Atordoado", [
        P(crouch=6, hy=4, htilt=8 * (1 if f % 2 else -1) + 4, armL=(-4, 0), armR=(-4, 0), eyes="dim", mush="dark",
          shelves=0.5, stars=f / 4, sway=(1 if f % 2 else -1) * 0.8, frame=f)
        for f in range(4)
    ], 5, True))

    # Caçada: agachado na névoa, quase invisível; só os dois pixels dos olhos ficam
    out.append(("Cacada", [
        P(crouch=7, lean=4, hy=4, armL=(-6, 15), armR=(-6, 15), eyes="bright", keep=0.1, mane=1, frame=f)
        for f in range(4)
    ], 4, True))
    # Bote: sai da névoa armando as garras
    out.append(("Bote_Aviso", [
        P(crouch=9, lean=5, hy=4, armL=(30, 30), armR=(30, 30), eyes="bright", keep=0.3, frame=0),
        P(crouch=11, lean=5, hy=4, armL=(70, 45), armR=(70, 45), eyes="bright", keep=0.6, mouth=0.4, frame=1),
        P(crouch=12, lean=4, hy=3, armL=(100, 50), armR=(100, 50), eyes="bright", keep=1.0, mouth=0.8, frame=2),
    ], 6, False))
    return out


def main():
    args = sys.argv[1:]
    preview = None
    if "--previa" in args:
        i = args.index("--previa")
        preview = args[i + 1]
        del args[i:i + 2]
    here = os.path.dirname(os.path.abspath(__file__))
    out_dir = args[0] if args else os.path.normpath(
        os.path.join(here, "..", "..", "..", "Assets", "Icaro", "Art", "Sprites", "Inimigos", NAME))
    os.makedirs(out_dir, exist_ok=True)

    spec = clips()
    cols = max(len(c[1]) for c in spec)
    rows = len(spec) * DIRECTIONS
    sheet = Image.new("RGBA", (cols * W, rows * H), (0, 0, 0, 0))
    glow_sheet = Image.new("RGBA", (cols * W, rows * H), (0, 0, 0, 0))
    meta_clips = []
    frames_by_clip = {}
    for ci, (name, poses, fps, loop) in enumerate(spec):
        frames = [render(p) for p in poses]
        frames_by_clip[name] = frames
        for d in range(DIRECTIONS):
            r = ci * DIRECTIONS + d
            for fi, (img, glow) in enumerate(frames):
                sheet.paste(img, (fi * W, r * H))
                glow_sheet.paste(glow, (fi * W, r * H))
        meta_clips.append({"name": name, "row": ci * DIRECTIONS, "frames": len(poses), "fps": fps, "loop": loop})
        print(f"{name}: {len(poses)} quadros", flush=True)

    sheet.save(os.path.join(out_dir, NAME + ".png"))
    glow_sheet.save(os.path.join(out_dir, NAME + "_Brilho.png"))
    meta = {
        "name": NAME, "cellWidth": W, "cellHeight": H, "columns": cols, "rows": rows, "pixelsPerMeter": PPM,
        "directions": DIRECTIONS, "pivotX": CX, "pivotY": H - (G + OY + 1),
        "emission": NAME + "_Brilho.png", "bodyGlow": 0.25, "clips": meta_clips,
    }
    with open(os.path.join(out_dir, NAME + ".json"), "w", encoding="utf-8") as fh:
        json.dump(meta, fh, indent=2)
    print("ok:", out_dir)

    if preview:
        os.makedirs(preview, exist_ok=True)
        bg = (28, 26, 38, 255)
        for name, frames in frames_by_clip.items():
            strip = Image.new("RGBA", (len(frames) * (W + 4) + 4, H + 8), bg)
            for i, (img, _) in enumerate(frames):
                strip.paste(img, (4 + i * (W + 4), 4), img)
            strip.resize((strip.width * 3, strip.height * 3), Image.NEAREST).save(os.path.join(preview, name + ".png"))
        # todos os clipes numa folha só, 2x
        lines = list(frames_by_clip.items())
        big = Image.new("RGBA", (cols * (W + 4) + 4, len(lines) * (H + 4) + 4), bg)
        for r, (name, frames) in enumerate(lines):
            for i, (img, _) in enumerate(frames):
                big.paste(img, (4 + i * (W + 4), 4 + r * (H + 4)), img)
        big.resize((big.width * 2, big.height * 2), Image.NEAREST).save(os.path.join(preview, "todos.png"))
        # GIFs de alguns clipes
        for name in ("Idle", "Move", "Rugido_Golpe", "Cacada", "Atordoado"):
            frames = frames_by_clip[name]
            fps = next(c[2] for c in spec if c[0] == name)
            ims = []
            for img, _ in frames:
                f = Image.new("RGBA", (W, H), bg)
                f.paste(img, (0, 0), img)
                ims.append(f.resize((W * 3, H * 3), Image.NEAREST).convert("P"))
            ims[0].save(os.path.join(preview, name + ".gif"), save_all=True, append_images=ims[1:],
                        duration=int(1000 / fps), loop=0)
        print("prévia:", preview)


if __name__ == "__main__":
    main()
