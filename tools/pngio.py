"""极小 PNG 读写：只支持 8bit 的灰度/RGB/调色板/带 alpha，够我们用了。

标准库 zlib 就能做，不用装 Pillow——开发机上装不了第三方包，
而且贴图是**用脚本生成**的，不能依赖一个装不上的库来重现。
"""
import struct
import zlib


def _paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    return b if pb <= pc else c


def read(path):
    """-> (width, height, pixels)，pixels[y][x] = (r,g,b,a)"""
    data = open(path, 'rb').read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n', path
    pos = 8
    idat = bytearray()
    plte = trns = None
    w = h = depth = color = None
    while pos < len(data):
        length, ctype = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if ctype == b'IHDR':
            w, h, depth, color = struct.unpack('>IIBB', body[:10])
        elif ctype == b'PLTE':
            plte = body
        elif ctype == b'tRNS':
            trns = body
        elif ctype == b'IDAT':
            idat += body
        elif ctype == b'IEND':
            break
    assert depth == 8, f'{path}: 只支持 8bit，实际 {depth}'
    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[color]
    raw = zlib.decompress(bytes(idat))
    stride = w * channels
    out = []
    prev = bytearray(stride)
    p = 0
    for _ in range(h):
        f = raw[p]
        p += 1
        line = bytearray(raw[p:p + stride])
        p += stride
        for i in range(stride):
            a = line[i - channels] if i >= channels else 0
            b = prev[i]
            c = prev[i - channels] if i >= channels else 0
            if f == 1:
                line[i] = (line[i] + a) & 255
            elif f == 2:
                line[i] = (line[i] + b) & 255
            elif f == 3:
                line[i] = (line[i] + (a + b) // 2) & 255
            elif f == 4:
                line[i] = (line[i] + _paeth(a, b, c)) & 255
        row = []
        for x in range(w):
            px = line[x * channels:(x + 1) * channels]
            if color == 6:
                row.append(tuple(px))
            elif color == 2:
                row.append((px[0], px[1], px[2], 255))
            elif color == 0:
                row.append((px[0], px[0], px[0], 255))
            elif color == 4:
                row.append((px[0], px[0], px[0], px[1]))
            else:
                i = px[0]
                r, g, b = plte[i * 3:i * 3 + 3]
                row.append((r, g, b, trns[i] if trns and i < len(trns) else 255))
        out.append(row)
        prev = line
    return w, h, out


def write(path, pixels):
    h = len(pixels)
    w = len(pixels[0])
    raw = bytearray()
    for row in pixels:
        raw.append(0)
        for r, g, b, a in row:
            raw += bytes((r & 255, g & 255, b & 255, a & 255))

    def chunk(tag, body):
        return (struct.pack('>I', len(body)) + tag + body
                + struct.pack('>I', zlib.crc32(tag + body) & 0xffffffff))

    out = b'\x89PNG\r\n\x1a\n'
    out += chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
    out += chunk(b'IDAT', zlib.compress(bytes(raw), 9))
    out += chunk(b'IEND', b'')
    open(path, 'wb').write(out)


def blank(w, h, color=(0, 0, 0, 0)):
    return [[color] * w for _ in range(h)]


def scale(pixels, n):
    return [[px for px in row for _ in range(n)] for row in pixels for _ in range(n)]
