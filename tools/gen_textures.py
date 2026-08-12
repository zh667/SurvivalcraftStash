#!/usr/bin/env python3
"""生成 Stash 的方块图集。

    python3 tools/gen_textures.py

产物：shared/Assets/Textures/Stash/Blocks.png（512×512 = 16×16 格，每格 32×32，和原版图集同规格）
另外会在 refs/preview/ 下放一张放大的预览图，方便肉眼检查。

**为什么是脚本而不是画好的 PNG**：贴图要跟着档位配色、格子编号一起改，
手工画三档 × 三个面很容易改漏一处；写成脚本，改一个常量三档一起变，
而且 diff 里看得见改了什么。

风格参考：
  - 分级箱子照 IronChest 的做法——木箱本体不动，四边包一圈**该档金属的边框**加铆钉，
    一眼能看出等级，又不脱离 SC 的木箱语言。
  - 存储终端照 Tom's Simple Storage 的 terminal_front——深色机箱 + 一排物品槽 + 顶上一条屏幕。
  两边都只参考**设计**（形状怎么排、颜色怎么分），像素是这里自己画的，没有拷贝任何一方的贴图文件。

坐标约定：格号 = 行*16 + 列，和 Block.GetFaceTextureSlot 返回的值一致。
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pngio  # noqa: E402

TILE = 32
# **必须和原版一样是 16 列**。原版的 UV 是 `格号 % 列数 / 列数` 算的，列数来自
# Block.GetTextureSlotCount()，默认 16。我们一度做成 8 列并覆写 GetTextureSlotCount，
# 结果实机全黑——只要有任何一条路径没走到那个覆写（比如按 BlocksData 里的
# DefaultTextureSlot 取格），采样点就落到图集里没画东西的地方，
# 透明像素在不透明批次里就是纯黑。
# 现在列数跟原版一致，而且每个方块的 DefaultTextureSlot 直接指向自己的格子，
# 覆写没生效也只是少了正面/侧面的区分，不会变黑。
COLS = 16
ROWS = 16

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'shared', 'Assets', 'Textures', 'Stash', 'Blocks.png')
PREVIEW = os.path.join(ROOT, 'refs', 'preview', 'stash_blocks.png')

# ---------------------------------------------------------------- 调色板

WOOD = {
    'base': (146, 106, 62),
    'light': (170, 128, 78),
    'dark': (118, 84, 46),
    'seam': (82, 56, 30),
}

# 三档金属。色相跟 StashChestTiers.Tint 对齐，但这里是**贴图本身**的颜色，
# 不再乘 Tint（乘色调会把木头也一起染了，那正是之前"三个箱子长得像同一个"的原因）。
METALS = {
    'copper': {'base': (194, 116, 58), 'light': (232, 158, 96), 'dark': (132, 72, 32)},
    'iron': {'base': (172, 174, 180), 'light': (214, 216, 222), 'dark': (110, 112, 120)},
    'diamond': {'base': (78, 190, 210), 'light': (146, 232, 242), 'dark': (38, 130, 156)},
}

STEEL = {'base': (72, 76, 84), 'light': (104, 110, 120), 'dark': (44, 47, 54)}
SCREEN_ON = (96, 232, 236)
SCREEN_OFF = (150, 62, 62)
SLOT = (34, 36, 42)


def shade(color, amount):
    """amount > 0 提亮，< 0 压暗。"""
    return tuple(max(0, min(255, c + amount)) for c in color)


def noise(x, y, seed):
    """确定性噪点：同样的输入永远同样的输出，重跑脚本 diff 才干净。"""
    n = (x * 374761393 + y * 668265263 + seed * 1442695040888963407) & 0xFFFFFFFF
    n = (n ^ (n >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFF) / 255.0


# ---------------------------------------------------------------- 画笔

def new_tile(color=(0, 0, 0, 0)):
    return [[color] * TILE for _ in range(TILE)]


def put(tile, x, y, color):
    if 0 <= x < TILE and 0 <= y < TILE:
        tile[y][x] = color if len(color) == 4 else color + (255,)


def rect(tile, x0, y0, x1, y1, color):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(tile, x, y, color)


def outline(tile, x0, y0, x1, y1, color):
    for x in range(x0, x1 + 1):
        put(tile, x, y0, color)
        put(tile, x, y1, color)
    for y in range(y0, y1 + 1):
        put(tile, x0, y, color)
        put(tile, x1, y, color)


def planks(tile, vertical_seam=True, seed=1):
    """SC 风格的木板：4 行 8 像素高的板子，行间一条深缝，板面带一点点噪。"""
    for y in range(TILE):
        row = y // 8
        for x in range(TILE):
            n = noise(x, y, seed + row)
            c = WOOD['base']
            if n > 0.82:
                c = WOOD['light']
            elif n < 0.22:
                c = WOOD['dark']
            put(tile, x, y, c)

    for row in range(1, 4):                       # 横向板缝：一条深缝 + 下一块板的顶边提亮
        for x in range(TILE):
            put(tile, x, row * 8 - 1, WOOD['seam'])
            put(tile, x, row * 8, shade(WOOD['light'], 6))

    if vertical_seam:                             # 中间一条竖缝（原版箱子就是对开的）
        for y in range(TILE):
            put(tile, TILE // 2 - 1, y, WOOD['seam'])
            put(tile, TILE // 2, y, shade(WOOD['dark'], -6))


def metal_frame(tile, metal, thickness=2):
    """四边包一圈金属边框：左上提亮、右下压暗，做出一点厚度。"""
    for t in range(thickness):
        x0 = y0 = t
        x1 = y1 = TILE - 1 - t
        for x in range(x0, x1 + 1):
            put(tile, x, y0, metal['light'] if t == 0 else metal['base'])
            put(tile, x, y1, metal['dark'] if t == 0 else metal['base'])
        for y in range(y0, y1 + 1):
            put(tile, x0, y, metal['light'] if t == 0 else metal['base'])
            put(tile, x1, y, metal['dark'] if t == 0 else metal['base'])


def rivets(tile, metal, inset=4):
    """四角铆钉。2×2 一颗，左上一点高光。"""
    for cx, cy in ((inset, inset), (TILE - 1 - inset, inset),
                   (inset, TILE - 1 - inset), (TILE - 1 - inset, TILE - 1 - inset)):
        rect(tile, cx - 1, cy - 1, cx, cy, metal['base'])
        put(tile, cx - 1, cy - 1, metal['light'])
        put(tile, cx, cy, metal['dark'])


# ---------------------------------------------------------------- 各个面

def chest_front(metal):
    tile = new_tile()
    planks(tile, vertical_seam=True, seed=3)
    metal_frame(tile, metal)
    rivets(tile, metal)

    # 锁扣：正中一块金属板 + 钥匙孔，压在竖缝上
    cx = TILE // 2
    rect(tile, cx - 4, 11, cx + 3, 21, metal['base'])
    outline(tile, cx - 4, 11, cx + 3, 21, metal['dark'])
    for x in range(cx - 3, cx + 3):
        put(tile, x, 12, metal['light'])
    rect(tile, cx - 1, 14, cx, 15, shade(SLOT, 10))       # 钥匙孔（圆头）
    rect(tile, cx - 1, 16, cx, 18, shade(SLOT, 10))       # 钥匙孔（柄）
    return tile


def chest_side(metal):
    tile = new_tile()
    planks(tile, vertical_seam=False, seed=7)
    metal_frame(tile, metal)
    rivets(tile, metal)
    # 侧面中间加一条横向加强筋，跟正面区分开
    rect(tile, 2, 15, TILE - 3, 16, metal['base'])
    for x in range(2, TILE - 2):
        put(tile, x, 15, metal['light'])
        put(tile, x, 16, metal['dark'])
    return tile


def chest_top(metal):
    tile = new_tile()
    planks(tile, vertical_seam=False, seed=11)
    metal_frame(tile, metal)
    rivets(tile, metal)
    # 后沿两个合页
    for cx in (9, TILE - 10):
        rect(tile, cx - 2, 3, cx + 2, 8, metal['base'])
        outline(tile, cx - 2, 3, cx + 2, 8, metal['dark'])
        put(tile, cx - 1, 4, metal['light'])
    return tile


def hub_front():
    """存储终端正面：深色机箱 + 3×3 物品槽 + 顶上一条屏幕（照 Tom's 的排布）。"""
    tile = new_tile()
    for y in range(TILE):
        for x in range(TILE):
            n = noise(x, y, 21)
            c = STEEL['base']
            if n > 0.88:
                c = shade(STEEL['base'], 10)
            elif n < 0.18:
                c = shade(STEEL['base'], -8)
            put(tile, x, y, c)

    metal_frame(tile, METALS['iron'])

    rect(tile, 5, 5, TILE - 6, 10, shade(SCREEN_ON, -110))   # 屏幕
    outline(tile, 5, 5, TILE - 6, 10, STEEL['dark'])
    for x in range(6, TILE - 6, 2):                          # 屏幕上的字符行
        put(tile, x, 7, SCREEN_ON)
        put(tile, x, 8, shade(SCREEN_ON, -60))

    for gy in range(3):                                       # 3×3 物品槽
        for gx in range(3):
            x0 = 5 + gx * 8
            y0 = 13 + gy * 6
            rect(tile, x0, y0, x0 + 5, y0 + 3, SLOT)
            put(tile, x0, y0, shade(SLOT, -10))
            put(tile, x0 + 5, y0 + 3, shade(STEEL['light'], -20))
    return tile


def hub_side():
    tile = new_tile()
    for y in range(TILE):
        for x in range(TILE):
            n = noise(x, y, 23)
            c = STEEL['base']
            if n > 0.9:
                c = shade(STEEL['base'], 8)
            elif n < 0.2:
                c = shade(STEEL['base'], -8)
            put(tile, x, y, c)
    metal_frame(tile, METALS['iron'])
    for y in range(9, 24, 3):                                 # 散热栅
        rect(tile, 7, y, TILE - 8, y + 1, STEEL['dark'])
        for x in range(7, TILE - 7):
            put(tile, x, y + 1, shade(STEEL['light'], -30))
    return tile


def hub_top():
    tile = new_tile()
    for y in range(TILE):
        for x in range(TILE):
            n = noise(x, y, 29)
            put(tile, x, y, shade(STEEL['base'], 6) if n > 0.85 else STEEL['base'])
    metal_frame(tile, METALS['iron'])
    rect(tile, 12, 12, 19, 19, shade(SCREEN_ON, -120))        # 顶上一颗指示灯
    outline(tile, 12, 12, 19, 19, STEEL['dark'])
    rect(tile, 14, 14, 17, 17, SCREEN_ON)
    return tile


def upgrade_item(metal):
    """升级件：一块金属板，中间一个朝上的箭头——照 IronChest 升级件的"两种材料"意思，
    但 SC 的物品图标只有一格，用箭头比拼两种材质更认得出。"""
    tile = new_tile()
    rect(tile, 6, 6, TILE - 7, TILE - 7, metal['base'])
    outline(tile, 6, 6, TILE - 7, TILE - 7, metal['dark'])
    for x in range(7, TILE - 7):
        put(tile, x, 7, metal['light'])
    for y in range(7, TILE - 7):
        put(tile, 7, y, metal['light'])

    # 朝上的箭头。第一版把箭杆也描了边，看着像扇门；现在箭头实心、只在下沿压一道暗色。
    cx = TILE // 2
    head_top, head_bottom = 10, 17
    for i, y in enumerate(range(head_top, head_bottom + 1)):
        half = i + 1
        rect(tile, cx - half, y, cx - 1 + half, y, metal['light'])
        put(tile, cx - half, y, metal['dark'])                # 箭头两条斜边压暗，做出立体
        put(tile, cx - 1 + half, y, metal['dark'])
    rect(tile, cx - 2, head_bottom + 1, cx + 1, 24, metal['light'])
    for y in range(head_bottom + 1, 25):                      # 箭杆两侧的暗边
        put(tile, cx - 2, y, metal['dark'])
        put(tile, cx + 1, y, metal['dark'])
    rect(tile, cx - 2, 25, cx + 1, 25, metal['dark'])
    return tile


def wireless(bound):
    """无线终端：一台手持机。屏幕亮青 = 已绑定，暗红 = 未绑定。"""
    tile = new_tile()
    body = STEEL
    rect(tile, 8, 4, TILE - 9, TILE - 5, body['base'])
    outline(tile, 8, 4, TILE - 9, TILE - 5, body['dark'])
    for x in range(9, TILE - 9):
        put(tile, x, 5, body['light'])
    for y in range(5, TILE - 5):
        put(tile, 9, y, body['light'])

    screen = SCREEN_ON if bound else SCREEN_OFF
    rect(tile, 11, 8, TILE - 12, 17, shade(screen, -90))
    outline(tile, 11, 8, TILE - 12, 17, body['dark'])
    for y in range(10, 16, 2):
        for x in range(12, TILE - 12, 2):
            put(tile, x, y, screen)

    for gy in range(2):                                       # 底下两排按键
        for gx in range(3):
            x0 = 11 + gx * 4
            y0 = 20 + gy * 4
            rect(tile, x0, y0, x0 + 2, y0 + 2, body['light'])
            put(tile, x0 + 2, y0 + 2, body['dark'])

    put(tile, TILE // 2, 2, screen)                           # 天线
    put(tile, TILE // 2, 3, body['light'])
    return tile


# ---------------------------------------------------------------- 拼图集

# 格号 = 行*16 + 列，和原版图集同一套坐标。
# **每一档的第一格就是它在 StashBlocksData.csv 里的 DefaultTextureSlot**，
# 这样即使 GetFaceTextureSlot 的覆写因故没生效，也只是六个面都用正面那格，不会采到空白。
# 改这里要同步改 StashBlockTextures.cs 的常量和 CSV 的 DefaultTextureSlot 列。
LAYOUT = {
    0: lambda: chest_front(METALS['copper']),
    1: lambda: chest_side(METALS['copper']),
    2: lambda: chest_top(METALS['copper']),
    3: lambda: chest_front(METALS['iron']),
    4: lambda: chest_side(METALS['iron']),
    5: lambda: chest_top(METALS['iron']),
    6: lambda: chest_front(METALS['diamond']),
    7: lambda: chest_side(METALS['diamond']),
    8: lambda: chest_top(METALS['diamond']),
    9: hub_front,
    10: hub_side,
    11: hub_top,
    12: lambda: upgrade_item(METALS['copper']),
    13: lambda: upgrade_item(METALS['iron']),
    14: lambda: upgrade_item(METALS['diamond']),
    15: lambda: wireless(False),
    16: lambda: wireless(True),
}

NAMES = {
    0: 'copper front', 1: 'copper side', 2: 'copper top',
    3: 'iron front', 4: 'iron side', 5: 'iron top',
    6: 'diamond front', 7: 'diamond side', 8: 'diamond top',
    9: 'hub front', 10: 'hub side', 11: 'hub top',
    12: 'upgrade copper', 13: 'upgrade iron', 14: 'upgrade diamond',
    15: 'wireless unbound', 16: 'wireless bound',
}


def unused_tile():
    """没用到的格子填暗品红棋盘格。

    以前这里是透明的，一旦采样点算错就是一片纯黑——看不出是"UV 算错"还是"贴图没加载"。
    填成一眼认得出的错误色，下次再采错立刻知道是哪一类问题。"""
    tile = new_tile()
    for y in range(TILE):
        for x in range(TILE):
            put(tile, x, y, (70, 20, 60) if (x // 4 + y // 4) % 2 == 0 else (46, 12, 40))
    return tile


def build():
    atlas = pngio.blank(COLS * TILE, ROWS * TILE)
    marker = unused_tile()
    for slot in range(COLS * ROWS):
        if slot in LAYOUT:
            continue
        ox, oy = (slot % COLS) * TILE, (slot // COLS) * TILE
        for y in range(TILE):
            for x in range(TILE):
                atlas[oy + y][ox + x] = marker[y][x]
    for slot, painter in LAYOUT.items():
        tile = painter()
        ox, oy = (slot % COLS) * TILE, (slot // COLS) * TILE
        for y in range(TILE):
            for x in range(TILE):
                atlas[oy + y][ox + x] = tile[y][x]
    return atlas


def preview(atlas, scale=5):
    """把用到的格子排成一行放大，空格填深灰，方便一眼看全。"""
    slots = sorted(LAYOUT)
    gap = 4
    big = [pngio.scale([[atlas[(s // COLS) * TILE + y][(s % COLS) * TILE + x]
                         for x in range(TILE)] for y in range(TILE)], scale)
           for s in slots]
    h = len(big[0])
    w = sum(len(b[0]) for b in big) + gap * (len(big) - 1)
    canvas = pngio.blank(w, h, (26, 26, 32, 255))
    x = 0
    for b in big:
        for y, row in enumerate(b):
            for dx, p in enumerate(row):
                canvas[y][x + dx] = p if p[3] else (70, 70, 80, 255)
        x += len(b[0]) + gap
    return canvas


if __name__ == '__main__':
    atlas = build()
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    pngio.write(OUT, atlas)
    print(f'wrote {OUT} ({COLS * TILE}x{ROWS * TILE})')

    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    pngio.write(PREVIEW, preview(atlas))
    print('preview:', PREVIEW)
    print('slots:', ', '.join(f'{s}={NAMES[s]}' for s in sorted(LAYOUT)))
