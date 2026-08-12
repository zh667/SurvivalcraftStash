#!/usr/bin/env python3
"""生成三档背包的衣物贴图。

    python3 tools/gen_backpacks.py

产物：shared/Assets/Textures/Stash/Backpack{Copper,Iron,Diamond}.png（各 64×64）
预览：refs/preview/stash_backpacks.png（把用到的几块 UV 区域标出来放大）

── UV 布局是从本体的模型里量出来的，不是猜的 ────────────────────────────
`Assets/Models/OuterClothingMale.dae` 的 Body 网格，按面法线分组、
把 COLLADA 的 v 翻成自上而下的贴图坐标，得到：

    正面   x 4..18,  y 15..35   （法线 +Y；拿原版 LeatherJerkin 验证过——
                                  这一块有系带花纹，另一块是素的，所以带花纹的是正面）
    背面   x 27..41, y 15..35
    左侧   x 46..52, y 16..35   u=46 是**前**沿、u=52 是**后**沿
    右侧   x 55..61, y 16..35   u=55 是**后**沿、u=61 是**前**沿
    顶面   x 47..60, y 12..16   两个半边的**中间**（u≈50..57）朝后

躯干盒子是 14 宽 × 6 深 × 20 高，所以背面那块 14×20 就是背包本体能占的全部地方。

── 造型 ───────────────────────────────────────────────────────────
照 SophisticatedBackpacks 的读法：布面包体 + 顶盖 + 两条扣带 + 前袋，
金属扣件用该档的颜色（铜/铁/钻石），一眼看出等级。
正面画两条肩带和一条胸带，侧面和顶面接上，绕身体一圈能对得上。
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pngio  # noqa: E402

SIZE = 64
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, 'shared', 'Assets', 'Textures', 'Stash')
PREVIEW = os.path.join(ROOT, 'refs', 'preview', 'stash_backpacks.png')

# 从 .dae 量出来的 UV 区域（左闭右闭，像素坐标）
FRONT = (4, 15, 17, 34)
BACK = (27, 15, 40, 34)
SIDE_L = (46, 16, 51, 34)     # 46 前 → 51 后
SIDE_R = (55, 16, 60, 34)     # 55 后 → 60 前
TOP = (47, 12, 59, 15)

TIERS = {
    'Copper': {
        'cloth': (120, 84, 52), 'cloth_light': (142, 102, 66), 'cloth_dark': (92, 62, 36),
        'metal': (194, 116, 58), 'metal_light': (232, 158, 96), 'metal_dark': (132, 72, 32),
    },
    'Iron': {
        'cloth': (96, 90, 82), 'cloth_light': (118, 112, 102), 'cloth_dark': (70, 66, 60),
        'metal': (172, 174, 180), 'metal_light': (214, 216, 222), 'metal_dark': (110, 112, 120),
    },
    'Diamond': {
        'cloth': (68, 82, 100), 'cloth_light': (88, 104, 126), 'cloth_dark': (48, 58, 74),
        'metal': (78, 190, 210), 'metal_light': (146, 232, 242), 'metal_dark': (38, 130, 156),
    },
}


def noise(x, y, seed):
    n = (x * 374761393 + y * 668265263 + seed * 1442695040888963407) & 0xFFFFFFFF
    n = (n ^ (n >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFF) / 255.0


def put(img, x, y, color):
    if 0 <= x < SIZE and 0 <= y < SIZE:
        img[y][x] = color if len(color) == 4 else color + (255,)


def rect(img, x0, y0, x1, y1, color):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(img, x, y, color)


def outline(img, x0, y0, x1, y1, color):
    for x in range(x0, x1 + 1):
        put(img, x, y0, color)
        put(img, x, y1, color)
    for y in range(y0, y1 + 1):
        put(img, x0, y, color)
        put(img, x1, y, color)


def cloth(img, x0, y0, x1, y1, c, seed):
    """带一点织物噪点的布面。"""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            n = noise(x, y, seed)
            col = c['cloth']
            if n > 0.84:
                col = c['cloth_light']
            elif n < 0.2:
                col = c['cloth_dark']
            put(img, x, y, col)


def buckle(img, x, y, c):
    """2×2 金属扣。"""
    rect(img, x, y, x + 1, y + 1, c['metal'])
    put(img, x, y, c['metal_light'])
    put(img, x + 1, y + 1, c['metal_dark'])


def build(c):
    img = pngio.blank(SIZE, SIZE)

    # ── 背面：包体本身 ────────────────────────────────────────────
    bx0, by0, bx1, by1 = BACK
    px0, py0, px1, py1 = bx0 + 1, by0 + 2, bx1 - 1, by1
    cloth(img, px0, py0, px1, py1, c, seed=5)
    outline(img, px0, py0, px1, py1, c['cloth_dark'])

    flap_bottom = py0 + 6                                   # 顶盖
    cloth(img, px0, py0, px1, flap_bottom, c, seed=9)
    for x in range(px0, px1 + 1):
        put(img, x, py0, c['cloth_light'])
        put(img, x, flap_bottom, c['cloth_dark'])

    for sx in (px0 + 2, px1 - 3):                           # 两条扣带，从顶盖压下来
        rect(img, sx, py0 + 1, sx + 1, flap_bottom + 4, c['cloth_dark'])
        put(img, sx, py0 + 1, c['cloth_light'])
        buckle(img, sx, flap_bottom + 3, c)

    pocket = (px0 + 2, flap_bottom + 7, px1 - 2, py1 - 1)   # 前袋
    cloth(img, *pocket, c, seed=13)
    outline(img, *pocket, c['cloth_dark'])
    for x in range(pocket[0], pocket[2] + 1):
        put(img, x, pocket[1], c['cloth_light'])
    buckle(img, (pocket[0] + pocket[2]) // 2, pocket[1] - 2, c)

    for y in range(py0 + 1, py1):                           # 两侧压条，让包体看着有厚度
        put(img, px0, y, c['cloth_dark'])
        put(img, px1, y, c['cloth_dark'])

    # ── 正面：两条肩带 + 一条胸带 ─────────────────────────────────
    fx0, fy0, fx1, fy1 = FRONT
    for sx in (fx0 + 2, fx1 - 3):
        cloth(img, sx, fy0, sx + 1, fy0 + 16, c, seed=17)
        put(img, sx, fy0, c['cloth_light'])
        put(img, sx + 1, fy0 + 16, c['cloth_dark'])
        buckle(img, sx, fy0 + 12, c)
    chest_y = fy0 + 7                                        # 胸带
    rect(img, fx0 + 2, chest_y, fx1 - 2, chest_y + 1, c['cloth'])
    for x in range(fx0 + 2, fx1 - 1):
        put(img, x, chest_y, c['cloth_light'])
        put(img, x, chest_y + 1, c['cloth_dark'])
    buckle(img, (fx0 + fx1) // 2, chest_y, c)

    # ── 侧面：后沿露出一点包体，上半截是肩带绕过来的部分 ──────────
    lx0, ly0, lx1, ly1 = SIDE_L
    cloth(img, lx1 - 1, ly0 + 1, lx1, ly1, c, seed=21)       # 左侧后沿（u 大的一头）
    cloth(img, lx0, ly0, lx1, ly0 + 3, c, seed=23)           # 肩带
    rx0, ry0, rx1, ry1 = SIDE_R
    cloth(img, rx0, ry0 + 1, rx0 + 1, ry1, c, seed=21)       # 右侧后沿（u 小的一头）
    cloth(img, rx0, ry0, rx1, ry0 + 3, c, seed=23)

    # ── 顶面：两个半边的中间朝后，画成肩上压过的带子 ──────────────
    tx0, ty0, tx1, ty1 = TOP
    cloth(img, tx0 + 3, ty0, tx1 - 3, ty1, c, seed=27)
    return img


def preview(images, scale=6):
    """把每档的四块 UV 区域裁出来横排：正面 / 背面 / 左侧 / 右侧。"""
    regions = [FRONT, BACK, SIDE_L, SIDE_R]
    gap = 6
    rows = []
    for name, img in images:
        parts = []
        for x0, y0, x1, y1 in regions:
            crop = [[img[y][x] for x in range(x0, x1 + 1)] for y in range(y0, y1 + 1)]
            parts.append(pngio.scale(crop, scale))
        h = max(len(p) for p in parts)
        w = sum(len(p[0]) for p in parts) + gap * (len(parts) - 1)
        band = pngio.blank(w, h, (26, 26, 32, 255))
        x = 0
        for p in parts:
            for y, row in enumerate(p):
                for dx, px in enumerate(row):
                    band[y][x + dx] = px if px[3] else (70, 70, 80, 255)
            x += len(p[0]) + gap
        rows.append(band)
    H = sum(len(r) for r in rows) + gap * (len(rows) - 1)
    W = max(len(r[0]) for r in rows)
    canvas = pngio.blank(W, H, (16, 16, 20, 255))
    y = 0
    for r in rows:
        for dy, row in enumerate(r):
            for dx, px in enumerate(row):
                canvas[y + dy][dx] = px
        y += len(r) + gap
    return canvas


if __name__ == '__main__':
    os.makedirs(OUT_DIR, exist_ok=True)
    images = []
    for name, palette in TIERS.items():
        img = build(palette)
        path = os.path.join(OUT_DIR, f'Backpack{name}.png')
        pngio.write(path, img)
        images.append((name, img))
        print('wrote', path)

    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    pngio.write(PREVIEW, preview(images))
    print('preview:', PREVIEW, '(每行一档，四块依次是 正面/背面/左侧/右侧)')
