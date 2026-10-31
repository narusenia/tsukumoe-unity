# usage: topng.py in.rgba WxH out.png [alpha]  -- premultiplied RGBA over mid-gray (or alpha as gray)
import sys, zlib, struct
src = open(sys.argv[1], 'rb').read()
w, h = map(int, sys.argv[2].split('x'))
rows = b''
for y in range(h):
    row = bytearray([0])
    for x in range(w):
        r, g, b, a = src[(y*w+x)*4:(y*w+x)*4+4]
        if len(sys.argv) > 4:
            row += bytes([a, a, a])
        else:
            row += bytes([min(255, r + 128*(255-a)//255), min(255, g + 128*(255-a)//255), min(255, b + 128*(255-a)//255)])
    rows += bytes(row)
def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d))
open(sys.argv[3], 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(rows)) + chunk(b'IEND', b''))
