"""Kit de pixel art dos conceitos dos inimigos (copiado para gerar a folha do Pé-de-Musgo).

Cada parte é uma máscara pintada com uma rampa: o nível da rampa sai da normal (analítica ou do contorno
borrado), da luz da lua (alto à esquerda), de textura de material e de um pontilhado ordenado. Depois de cada
parte, a beira dela que encosta em outra parte escurece (linha interna). No fim, luz de borda e contorno escuro.
"""
import math
import numpy as np
from PIL import Image
from scipy import ndimage

L = np.array([-0.55, -0.62, 0.56])
L = L / np.linalg.norm(L)
BAYER = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) + 0.5) / 16.0 - 0.5
OUTLINE = (7, 6, 11)


def R(*hexes):
    return [((h >> 16) & 255, (h >> 8) & 255, h & 255) for h in hexes]


# ---------- rampas (do escuro ao claro, sombra fria e luz quente) ----------
METAL = R(0x121019, 0x222330, 0x353a49, 0x4f5766, 0x717c89, 0x9fa9b1)
RUST = R(0x26120e, 0x452015, 0x67351f, 0x8b502b, 0xb0723d)
MOSS = R(0x0b1c16, 0x143223, 0x204d2f, 0x316a3a, 0x4d8a47, 0x7aae5a)
BIO = R(0x0b3644, 0x136e7c, 0x20a6b0, 0x5cdfe4, 0xc6fffa)
AMBER = R(0x572405, 0x984a0b, 0xdc861c, 0xffc24a, 0xffefb6)
CLOAK = R(0x110e1a, 0x1d182b, 0x2d2640, 0x423958, 0x5c5174, 0x7f7698)
PORCELAIN = R(0x2b2729, 0x4b4543, 0x736b63, 0x9d9381, 0xc7bea6, 0xeae3ce)
WOOD = R(0x18100b, 0x2c1d13, 0x45301d, 0x634528, 0x856036)
BARK = R(0x150e0a, 0x281b13, 0x3e281a, 0x593b24, 0x785332, 0x9a7147)
BRASS = R(0x372210, 0x63401a, 0x966a29, 0xc49842, 0xe9c978)
LILAC = R(0x2a1848, 0x50307c, 0x8256b6, 0xb68ce0, 0xe6d4ff)
WING = R(0x3b3150, 0x594b70, 0x827394, 0xad9fba, 0xd5cbdc, 0xf3eef9)
FUR = R(0x140e19, 0x24192b, 0x36273f, 0x4d3955, 0x684f6e, 0x8a7090)
STONE = R(0x14161d, 0x232833, 0x373e4c, 0x505968, 0x6f7987, 0x96a0ac)
SCALE = R(0x091818, 0x112e2a, 0x1b473d, 0x2a634f, 0x418164, 0x67a37f)
BELLY = R(0x3a3425, 0x5f573e, 0x89805e, 0xb4aa82, 0xdad2aa)
BONE = R(0x2e2a22, 0x4f4838, 0x7a7056, 0xa69b78, 0xcfc6a0, 0xf0ead0)
CLOTH = R(0x1c1414, 0x332322, 0x4d3532, 0x6a4b44, 0x8c665a)
RED = R(0x3a0d10, 0x661a1c, 0x92302c, 0xbc4c3e)
DARK = R(0x07060b, 0x0f0c14, 0x1a1520)


class Sprite:
    def __init__(self, w, h, seed=1):
        self.w, self.h = w, h
        self.lvl = np.full((h, w), -1, dtype=np.int32)
        self.rid = np.zeros((h, w), dtype=np.int32)
        self.part = np.zeros((h, w), dtype=np.int32)
        self.glow = np.zeros((h, w), dtype=bool)
        self.ramps = []
        self.nparts = 0
        self.rng = np.random.default_rng(abs(int(seed)))
        # origem da textura (deslocamento da parte que está sendo pintada): a textura acompanha a parte quando ela se
        # mexe de um quadro para o outro, em vez de ficar presa à tela
        self.org = (0.0, 0.0)
        yy, xx = np.mgrid[0:h, 0:w]
        self.X = xx + 0.5
        self.Y = yy + 0.5

    # ---------- máscaras ----------
    def ellipse(self, cx, cy, rx, ry, rot=0.0):
        c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        dx, dy = self.X - cx, self.Y - cy
        u = (dx * c + dy * s) / rx
        v = (-dx * s + dy * c) / ry
        return u * u + v * v <= 1.0

    def poly(self, pts):
        from matplotlib.path import Path
        p = Path(np.array(pts, dtype=float))
        pts2 = np.stack([self.X.ravel(), self.Y.ravel()], axis=1)
        return p.contains_points(pts2).reshape(self.h, self.w)

    def capsule(self, x0, y0, x1, y1, r0, r1=None):
        r1 = r0 if r1 is None else r1
        dx, dy = x1 - x0, y1 - y0
        ll = dx * dx + dy * dy
        t = np.clip(((self.X - x0) * dx + (self.Y - y0) * dy) / max(ll, 1e-6), 0, 1)
        px, py = x0 + t * dx, y0 + t * dy
        d = np.hypot(self.X - px, self.Y - py)
        return d <= r0 + (r1 - r0) * t

    def rect(self, x0, y0, x1, y1):
        return (self.X >= x0) & (self.X < x1) & (self.Y >= y0) & (self.Y < y1)

    def ring(self, cx, cy, ro, ri):
        d = np.hypot(self.X - cx, self.Y - cy)
        return (d <= ro) & (d >= ri)

    # ---------- texturas (deslocamento do nível, ~-1 a 1) ----------
    def noise(self, scale, seed=0, octaves=2, sx=1.0, sy=1.0):
        out = np.zeros((self.h, self.w))
        amp, tot = 1.0, 0.0
        rng = np.random.default_rng(abs(int(seed)))
        for o in range(octaves):
            gs = max(2, int(max(self.w, self.h) / (scale / (2 ** o))) + 3)
            grid = rng.random((gs, gs)) * 2 - 1
            gx = (self.X - self.org[0]) / (scale / (2 ** o)) * sx
            gy = (self.Y - self.org[1]) / (scale / (2 ** o)) * sy
            out += amp * ndimage.map_coordinates(grid, [gy, gx], order=1, mode="wrap")
            tot += amp
            amp *= 0.5
        return out / tot

    def voronoi(self, scale, seed=0, sx=1.0, sy=1.0):
        rng = np.random.default_rng(abs(int(seed)))
        n = int(self.w * self.h / (scale * scale) * 1.3) + 4
        pts = rng.random((n, 2)) * [self.w, self.h]
        X, Y = self.X / sx, self.Y / sy
        P = pts / [sx, sy]
        d = np.full((self.h, self.w), 1e9)
        d2 = np.full((self.h, self.w), 1e9)
        cid = np.zeros((self.h, self.w), dtype=np.int32)
        for i, (px, py) in enumerate(P):
            dd = np.hypot(X - px, Y - py)
            closer = dd < d
            d2 = np.where(closer, d, np.minimum(d2, dd))
            cid = np.where(closer, i, cid)
            d = np.where(closer, dd, d)
        cell = rng.random(n)[cid] * 2 - 1
        return d2 - d, cell

    # ---------- pintura ----------
    def _rampid(self, ramp):
        for i, r in enumerate(self.ramps):
            if r is ramp:
                return i
        self.ramps.append(ramp)
        return len(self.ramps) - 1

    def paint(self, mask, ramp, normal="blur", depth=2.5, bias=0.0, tex=None, contrast=1.0,
              glow=False, sel=True, center=None, radii=None, dither=0.5, over=True):
        mask = mask & (self.lvl >= 0) if not over else mask
        if not mask.any():
            return mask
        n = len(ramp)
        if normal == "flat":
            ndl = np.full((self.h, self.w), L[2])
        elif normal == "ellipse":
            cx, cy = center
            rx, ry = radii
            nx = (self.X - cx) / rx
            ny = (self.Y - cy) / ry
            nz = np.sqrt(np.clip(1 - nx * nx - ny * ny, 0.02, 1))
            nn = np.sqrt(nx * nx + ny * ny + nz * nz)
            ndl = (nx * L[0] + ny * L[1] + nz * L[2]) / nn
        elif normal == "cylv":  # cilindro vertical: normal só em x
            x0, x1 = center
            nx = np.clip((self.X - (x0 + x1) / 2) / ((x1 - x0) / 2), -1, 1)
            nz = np.sqrt(np.clip(1 - nx * nx, 0.02, 1))
            ndl = nx * L[0] + nz * L[2]
        else:
            b = ndimage.gaussian_filter(mask.astype(float), depth)
            gy, gx = np.gradient(b)
            k = depth * 2.2
            nx, ny, nz = -gx * k, -gy * k, np.ones_like(b)
            nn = np.sqrt(nx * nx + ny * ny + nz * nz)
            ndl = (nx * L[0] + ny * L[1] + nz * L[2]) / nn
        v = 0.5 + (ndl - 0.45) * 1.25 * contrast + bias
        if tex is not None:
            v = v + tex
        bay = np.tile(BAYER, (self.h // 4 + 1, self.w // 4 + 1))[: self.h, : self.w]
        lv = np.clip(np.floor(v * (n - 1) + 0.5 + bay * dither), 0, n - 1).astype(np.int32)

        self.nparts += 1
        pid = self.nparts
        # linha interna: a beira desta parte que encosta em algo já desenhado
        if sel:
            inner = mask & ~ndimage.binary_erosion(mask)
            ring = ndimage.binary_dilation(mask) & ~mask
            touch = ndimage.binary_dilation(ring & (self.lvl >= 0)) & inner
            # embaixo e à direita escurece mais
            lower = touch & (np.roll(~mask, -1, 0) | np.roll(~mask, -1, 1))
            lv = np.where(touch, np.maximum(lv - 1, 0), lv)
            lv = np.where(lower, np.maximum(lv - 1, 0), lv)
        self.lvl = np.where(mask, lv, self.lvl)
        self.rid = np.where(mask, self._rampid(ramp), self.rid)
        self.part = np.where(mask, pid, self.part)
        self.glow = np.where(mask, glow, self.glow)
        return mask

    def shift(self, mask, d):
        """Clareia (d > 0) ou escurece o que já está pintado na máscara."""
        m = mask & (self.lvl >= 0)
        n = np.array([len(r) for r in self.ramps])[self.rid]
        self.lvl = np.where(m, np.clip(self.lvl + d, 0, n - 1), self.lvl)

    def setlvl(self, mask, ramp, level, glow=None):
        m = mask
        self.lvl = np.where(m, min(level, len(ramp) - 1), self.lvl)
        self.rid = np.where(m, self._rampid(ramp), self.rid)
        if glow is not None:
            self.glow = np.where(m, glow, self.glow)

    def line(self, pts, width=1.0):
        m = np.zeros((self.h, self.w), dtype=bool)
        for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
            m |= self.capsule(x0, y0, x1, y1, width / 2)
        return m

    def dot(self, x, y):
        m = np.zeros((self.h, self.w), dtype=bool)
        xi, yi = int(x), int(y)
        if 0 <= xi < self.w and 0 <= yi < self.h:
            m[yi, xi] = True
        return m

    def crack(self, x, y, length, seed, ang=90.0, spread=35.0, width=1.0):
        """Rachadura em zigue-zague: devolve a máscara do traço."""
        rng = np.random.default_rng(abs(int(seed)))
        pts = [(x, y)]
        a = ang
        for _ in range(int(length / 2.5)):
            a += rng.uniform(-spread, spread)
            x += math.cos(math.radians(a)) * 2.5
            y += math.sin(math.radians(a)) * 2.5
            pts.append((x, y))
        return self.line(pts, width)

    def finish(self, rim=True):
        op = self.lvl >= 0
        if rim:
            up = np.roll(op, 1, 0)
            up[0, :] = False
            left = np.roll(op, 1, 1)
            left[:, 0] = False
            edge = op & (~up | ~left)
            self.shift(edge & ~self.glow, 1)
        return self

    def image(self, glow_only=False):
        img = np.zeros((self.h, self.w, 4), dtype=np.uint8)
        op = self.lvl >= 0
        for i, r in enumerate(self.ramps):
            arr = np.array(r, dtype=np.uint8)
            m = op & (self.rid == i)
            img[m, :3] = arr[np.clip(self.lvl[m], 0, len(r) - 1)]
        img[op, 3] = 255
        # contorno
        ring = ndimage.binary_dilation(op) & ~op
        img[ring, :3] = OUTLINE
        img[ring, 3] = 255
        if glow_only:
            img[~self.glow & op, :3] = (img[~self.glow & op, :3] * 0.25).astype(np.uint8)
            img[ring, :3] = 0
        return Image.fromarray(img, "RGBA")


def sheet(items, scale, bg=(28, 26, 38), pad=10, label_h=0, ground=True):
    """items: lista de imagens RGBA; alinha pelo pé."""
    h = max(i.height for i in items)
    w = sum(i.width for i in items) + pad * (len(items) + 1)
    out = Image.new("RGBA", (w, h + pad * 2), bg + (255,))
    if ground:
        g = Image.new("RGBA", (w, pad), (24, 38, 34, 255))
        out.paste(g, (0, h + pad))
    x = pad
    for i in items:
        out.paste(i, (x, pad + h - i.height), i)
        x += i.width + pad
    return out.resize((out.width * scale, out.height * scale), Image.NEAREST)
