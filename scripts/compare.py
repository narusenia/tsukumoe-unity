# usage: compare.py a.rgba b.rgba WxH  -- max per-channel difference and pixels above 2/255
import sys
a, b = (open(p, 'rb').read() for p in sys.argv[1:3])
assert len(a) == len(b), (len(a), len(b))
w, h = map(int, sys.argv[3].split('x'))
d = [abs(x - y) for x, y in zip(a, b)]
bad = sum(1 for i in range(0, len(d), 4) if max(d[i:i + 4]) > 2)
print(f"max={max(d)}/255 pixels>2={bad} of {w*h} nonzero_a={sum(1 for i in range(3,len(a),4) if a[i])} nonzero_b={sum(1 for i in range(3,len(b),4) if b[i])}")
