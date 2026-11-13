"""Formas orgânicas para os chefes: curvas Catmull-Rom, manchas de contorno curvo e traços que afinam."""
import math
import numpy as np
from px import *


def spline(pts, closed=False, n=10):
    p = list(pts)
    if closed:
        p = [p[-1]] + p + [p[0], p[1]]
    else:
        p = [p[0]] + p + [p[-1]]
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2
                                     + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in (0, 1)))
    if not closed:
        out.append(tuple(pts[-1]))
    return out


def blob(s, pts, wobble=0.0, seed=0):
    """Mancha de contorno curvo; wobble deixa a borda irregular (pele, pelo, musgo)."""
    c = spline(pts, closed=True, n=12)
    if wobble > 0:
        rng = np.random.default_rng(abs(seed) + 7)
        cx = sum(p[0] for p in c) / len(c)
        cy = sum(p[1] for p in c) / len(c)
        ph = rng.uniform(0, 6.28, 3)
        out = []
        for i, (x, y) in enumerate(c):
            a = i / len(c) * 6.283
            w = wobble * (math.sin(a * 7 + ph[0]) * 0.5 + math.sin(a * 13 + ph[1]) * 0.3 + math.sin(a * 23 + ph[2]) * 0.2)
            dx, dy = x - cx, y - cy
            d = math.hypot(dx, dy) or 1
            out.append((x + dx / d * w, y + dy / d * w))
        c = out
    return s.poly(c)


def stroke(s, pts, w0, w1=None, n=10, curve=None):
    """Traço que segue uma curva e afina de w0 a w1 (membros, raízes, chifres, cauda).
    curve(t) opcional altera a largura (ex.: barriga de um músculo)."""
    w1 = w0 if w1 is None else w1
    c = spline(pts, n=n)
    m = np.zeros((s.h, s.w), dtype=bool)
    L = len(c) - 1
    for i in range(L):
        t0, t1 = i / L, (i + 1) / L
        r0 = (w0 + (w1 - w0) * t0) / 2
        r1 = (w0 + (w1 - w0) * t1) / 2
        if curve:
            r0 *= curve(t0)
            r1 *= curve(t1)
        m |= s.capsule(c[i][0], c[i][1], c[i + 1][0], c[i + 1][1], max(0.45, r0), max(0.45, r1))
    return m


def along(pts, t, n=10):
    c = spline(pts, n=n)
    i = min(len(c) - 1, int(t * (len(c) - 1)))
    return c[i]
