"""Draws the SS12 lobby background (pixel art, SS13 vibes, with the controls and a few tips) as a 1920x1080 image.

Everything is drawn in code at 480x270 and scaled 4x with nearest-neighbour, so no third-party art is used.
Font: Boxfont Round from the SS14 repo (see Resources/Fonts/Boxfont-round/credits.txt).
Usage: python make_lobby_bg.py <ss14 repo root> [output.png|webp]
"""
import math, random, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter

W, H, S = 480, 270, 4
# The lobby shows only the left ~72% of the image (the rest is under its chat panel) and crops a little from the top and
# bottom, so everything that matters stays inside x < SAFE_R and between y = 14 and y = 256. The lobby's own buttons sit in
# the top left corner (x < 100, y < 34) and its credits in the bottom left (x < 72, y > 244).
CX, SAFE_R = 173, 346
root = sys.argv[1]
FONT = root + "/Resources/Fonts/Boxfont-round/Boxfont Round.ttf"
rnd = random.Random(1213)
img = Image.new("RGB", (W, H), (4, 4, 14))
d = ImageDraw.Draw(img)


def f(sz):
    return ImageFont.truetype(FONT, sz)


# ---- space: nebula + stars
neb = Image.new("RGB", (W, H), (4, 4, 14))
nd = ImageDraw.Draw(neb)
for cx, cy, r, col in [(90, 60, 120, (40, 10, 70)), (400, 40, 130, (70, 8, 30)), (240, 130, 160, (12, 20, 60))]:
    for i in range(r, 0, -3):
        a = (1 - i / r) ** 2
        c = tuple(int(4 + (col[k] - 4) * a * 0.9) for k in range(3))
        nd.ellipse([cx - i, cy - i * 0.6, cx + i, cy + i * 0.6], fill=c)
img.paste(neb)
d = ImageDraw.Draw(img)
for _ in range(420):
    x, y = rnd.randrange(W), rnd.randrange(H)
    b = rnd.choice([90, 130, 170, 220, 255])
    d.point((x, y), fill=(b, b, min(255, b + 20)))
for _ in range(14):
    x, y = rnd.randrange(W), rnd.randrange(150)
    d.point((x, y), fill=(255, 255, 255))
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        d.point((x + dx, y + dy), fill=(160, 160, 220))


# ---- the singularity (loose), small, top right
def singulo(cx, cy, r):
    for i in range(r + 22, 0, -1):
        t = i / (r + 22)
        if i > r:
            a = 1 - (i - r) / 22
            c = (int(70 * a), int(10 * a), int(120 * a))
        else:
            c = (0, 0, 0) if i < r * 0.8 else (int(30 * (1 - t)), 0, int(50 * (1 - t)))
        d.ellipse([cx - i, cy - i, cx + i, cy + i], fill=c)
    for k in range(5):
        rr = r + 6 + k * 5
        d.arc([cx - rr, cy - rr * 0.35, cx + rr, cy + rr * 0.35], 190, 350, fill=(150 + k * 20, 60, 220 - k * 20))
        d.arc([cx - rr, cy - rr * 0.35, cx + rr, cy + rr * 0.35], 10, 170, fill=(90, 30, 150))
    for _ in range(40):
        ang = rnd.uniform(0, math.tau)
        dist = rnd.uniform(r + 8, r + 70)
        d.point((cx + math.cos(ang) * dist, cy + math.sin(ang) * dist * 0.8),
                fill=rnd.choice([(200, 200, 210), (255, 190, 60), (120, 255, 160)]))


SX, SY = 328, 70
singulo(SX, SY, 14)
for i in range(7):
    ang = 2.9 + i * 0.09
    dist = 40 + i * 5
    x = SX + math.cos(ang) * dist
    y = SY + math.sin(ang) * dist * 0.7
    d.rectangle([x, y, x + 3, y + 2], fill=(120, 124, 135), outline=(60, 62, 72))
    d.point((x + 1, y + 1), fill=(255, 230, 120))

# ---- corridor floor along the bottom (SS13 white tile checker, real perspective)
FLOOR_Y = 218
VPX, VPY = 200, 120
d.rectangle([0, FLOOR_Y - 4, W, FLOOR_Y], fill=(70, 74, 86))
d.rectangle([0, FLOOR_Y - 8, W, FLOOR_Y - 5], fill=(40, 42, 52))
rows = [FLOOR_Y + (H - FLOOR_Y) * (k / 7) ** 1.8 for k in range(8)]


def fx(x0, y):  # x on screen of the floor line that is x0 at the bottom edge
    return VPX + (x0 - VPX) * (y - VPY) / (H - VPY)


cols = list(range(-640, 1120, 80))
for r in range(7):
    y0, y1 = rows[r], rows[r + 1]
    for c in range(len(cols) - 1):
        light = (r + c) % 2 == 0
        t = r / 7
        base = 200 - int(t * 40)
        col = (base, base, base + 8) if light else (base - 34, base - 34, base - 22)
        d.polygon([(fx(cols[c], y0), y0), (fx(cols[c + 1], y0), y0), (fx(cols[c + 1], y1), y1), (fx(cols[c], y1), y1)], fill=col)
for y in rows:
    d.line([0, y, W, y], fill=(96, 100, 114))
for x0 in cols:
    d.line([fx(x0, FLOOR_Y), FLOOR_Y, fx(x0, H), H], fill=(96, 100, 114))


# ---- characters (drawn at 1x, standing on the strip of floor)
def shadow(cx, y, w):
    d.ellipse([cx - w, y - 2, cx + w, y + 2], fill=(60, 62, 74))


def head(cx, y, skin, hair=None):
    d.rectangle([cx - 5, y, cx + 5, y + 9], fill=skin, outline=(30, 20, 20))
    if hair:
        d.rectangle([cx - 5, y - 1, cx + 5, y + 2], fill=hair)
    d.point((cx - 2, y + 4), fill=(0, 0, 0))
    d.point((cx + 2, y + 4), fill=(0, 0, 0))


def assistant(cx, base):
    shadow(cx, base, 9)
    d.rectangle([cx - 5, base - 9, cx - 1, base], fill=(25, 25, 25))
    d.rectangle([cx + 1, base - 9, cx + 5, base], fill=(25, 25, 25))
    d.rectangle([cx - 5, base - 20, cx + 5, base - 8], fill=(120, 122, 130), outline=(70, 72, 80))
    d.rectangle([cx - 9, base - 19, cx - 6, base - 9], fill=(120, 122, 130))
    d.rectangle([cx + 6, base - 19, cx + 9, base - 9], fill=(120, 122, 130))
    d.rectangle([cx - 9, base - 11, cx - 6, base - 8], fill=(240, 200, 30))
    d.rectangle([cx + 6, base - 11, cx + 9, base - 8], fill=(240, 200, 30))
    head(cx, base - 31, (230, 180, 140), (80, 50, 30))
    d.rectangle([cx + 9, base - 12, cx + 20, base - 5], fill=(200, 30, 30), outline=(90, 10, 10))
    d.rectangle([cx + 12, base - 14, cx + 17, base - 12], outline=(160, 160, 170))
    d.line([cx + 9, base - 9, cx + 20, base - 9], fill=(120, 15, 15))


def clown(cx, base):
    shadow(cx, base, 10)
    d.rectangle([cx - 7, base - 4, cx - 1, base], fill=(230, 50, 50))
    d.rectangle([cx + 1, base - 4, cx + 7, base], fill=(230, 50, 50))
    d.rectangle([cx - 4, base - 10, cx - 1, base - 4], fill=(240, 190, 20))
    d.rectangle([cx + 1, base - 10, cx + 4, base - 4], fill=(240, 190, 20))
    d.rectangle([cx - 6, base - 22, cx + 6, base - 9], fill=(40, 90, 230), outline=(15, 30, 110))
    d.rectangle([cx - 6, base - 17, cx + 6, base - 14], fill=(240, 190, 20))
    d.rectangle([cx - 10, base - 21, cx - 7, base - 11], fill=(40, 90, 230))
    d.rectangle([cx + 7, base - 21, cx + 10, base - 11], fill=(40, 90, 230))
    d.rectangle([cx - 10, base - 12, cx - 7, base - 9], fill=(250, 250, 250))
    d.rectangle([cx + 7, base - 12, cx + 10, base - 9], fill=(250, 250, 250))
    for i, c in enumerate([(230, 40, 40), (250, 150, 30), (250, 230, 40), (60, 200, 70), (50, 120, 240)]):
        d.rectangle([cx - 9 + i * 3 - 1, base - 36, cx - 7 + i * 3 + 1, base - 31], fill=c)
    d.rectangle([cx - 6, base - 32, cx + 6, base - 22], fill=(250, 245, 240), outline=(60, 40, 40))
    d.rectangle([cx - 1, base - 28, cx + 1, base - 26], fill=(240, 20, 20))
    d.point((cx - 3, base - 29), fill=(0, 0, 0))
    d.point((cx + 3, base - 29), fill=(0, 0, 0))
    d.line([cx - 3, base - 24, cx + 3, base - 24], fill=(220, 20, 20))
    d.rectangle([cx + 10, base - 15, cx + 14, base - 12], fill=(250, 210, 30), outline=(120, 90, 0))
    d.rectangle([cx + 14, base - 16, cx + 17, base - 11], fill=(30, 30, 30))


def security(cx, base):
    shadow(cx, base, 9)
    d.rectangle([cx - 5, base - 9, cx - 1, base], fill=(15, 15, 18))
    d.rectangle([cx + 1, base - 9, cx + 5, base], fill=(15, 15, 18))
    d.rectangle([cx - 6, base - 22, cx + 6, base - 8], fill=(150, 25, 30), outline=(70, 10, 14))
    d.rectangle([cx - 5, base - 20, cx + 5, base - 12], fill=(40, 40, 48))
    d.rectangle([cx - 10, base - 21, cx - 7, base - 10], fill=(150, 25, 30))
    d.rectangle([cx + 7, base - 21, cx + 10, base - 10], fill=(150, 25, 30))
    head(cx, base - 32, (200, 150, 115))
    d.rectangle([cx - 6, base - 35, cx + 6, base - 30], fill=(20, 20, 24))
    d.rectangle([cx - 5, base - 31, cx + 5, base - 29], fill=(80, 190, 255))
    d.line([cx + 10, base - 14, cx + 21, base - 24], fill=(30, 30, 34), width=2)
    d.point((cx + 21, base - 24), fill=(255, 240, 80))


assistant(112, 253)
clown(172, 254)
security(232, 253)
# a little speech bubble for the clown
ft = f(8)
d.rectangle([186, 214, 215, 224], fill=(250, 250, 250), outline=(10, 10, 10))
d.polygon([(190, 224), (195, 224), (191, 229)], fill=(250, 250, 250), outline=(10, 10, 10))
d.line([191, 224, 194, 224], fill=(250, 250, 250))
d.text((190, 215), "HONK", font=ft, fill=(10, 10, 10))


# ---- title with 3D-glasses (red/cyan) offset
def title(text, y, size, off):
    ft = f(size)
    x = CX - d.textlength(text, font=ft) / 2
    for ox in (-1, 0, 1):
        for oy in (-1, 0, 1):
            d.text((x - off + ox, y + oy), text, font=ft, fill=(30, 0, 10))
            d.text((x + off + ox, y + oy), text, font=ft, fill=(0, 12, 24))
    d.text((x - off, y), text, font=ft, fill=(255, 50, 70))
    d.text((x + off, y), text, font=ft, fill=(60, 230, 255))
    d.text((x, y), text, font=ft, fill=(255, 255, 255))


title("SPACE STATION 12", 36, 25, 2)
sub = "SS13, BUT NOW WITH A THIRD DIMENSION"
ft = f(10)
wid = d.textlength(sub, font=ft)
d.text((CX - wid / 2 + 1, 66), sub, font=ft, fill=(0, 0, 0))
d.text((CX - wid / 2, 65), sub, font=ft, fill=(255, 225, 90))
sub2 = "(please put on your 3D glasses)"
ft = f(8)
wid = d.textlength(sub2, font=ft)
d.text((CX - wid / 2, 79), sub2, font=ft, fill=(150, 160, 200))

# ---- panels
panels = Image.new("RGBA", (W, H), (0, 0, 0, 0))
pd = ImageDraw.Draw(panels)
pd.rectangle([10, 92, 336, 164], fill=(8, 10, 24, 205), outline=(90, 100, 140, 255))
pd.rectangle([10, 168, 336, 215], fill=(8, 10, 24, 205), outline=(90, 100, 140, 255))
img.paste(panels, (0, 0), panels)
d = ImageDraw.Draw(img)

HEAD = (255, 225, 90)
TEXT = (232, 234, 245)
CAP_FACE, CAP_EDGE, CAP_LOW = (66, 72, 100), (160, 168, 205), (24, 26, 42)


HOT_FACE, HOT_EDGE, HOT_LOW = (235, 150, 20), (255, 225, 120), (120, 60, 0)


def keycap(x, y, label, hot=False):
    """A small key (an amber one when it is highlighted), returns the x where it ends."""
    ft = f(8)
    w = int(d.textlength(label, font=ft)) + 6
    face, edge, low = (HOT_FACE, HOT_EDGE, HOT_LOW) if hot else (CAP_FACE, CAP_EDGE, CAP_LOW)
    d.rectangle([x, y + 1, x + w, y + 11], fill=low)
    d.rectangle([x, y, x + w, y + 9], fill=face, outline=edge)
    d.text((x + 3, y + 1), label, font=ft, fill=(25, 12, 0) if hot else (255, 255, 255))
    return x + w + 2


def control(x, y, keys, label, hot=False):
    for k in keys:
        x = keycap(x, y, k, hot)
    d.text((x + 3, y + 1), label, font=f(8), fill=(255, 225, 90) if hot else TEXT)


d.text((16, 95), "CONTROLS", font=f(8), fill=HEAD)
LX, RX = 16, 178
control(LX, 108, ["W", "A", "S", "D"], "walk")
control(LX, 122, ["MOUSE"], "look around")
control(LX, 136, ["N"], "first / third person")
# the one thing a new player gets stuck on: the mouse turns the camera, so menus need Alt
hl = Image.new("RGBA", (W, H), (0, 0, 0, 0))
hd = ImageDraw.Draw(hl)
hd.rectangle([13, 148, 173, 162], fill=(235, 150, 20, 70), outline=(255, 210, 80, 255))
img.paste(hl, (0, 0), hl)
d = ImageDraw.Draw(img)
control(LX, 150, ["ALT"], "HOLD: FREE THE MOUSE", hot=True)
control(RX, 108, ["M"], "minimap on / off")
control(RX, 122, ["-"], "minimap small / large")
control(RX, 136, ["F11"], "3D settings")
control(RX, 150, ["F12"], "3D / flat view")

d.text((16, 171), "TIPS", font=f(8), fill=HEAD)
tips = [
    "Aim with the cross; it turns green when it is in reach.",
    "Slow? F11, Quality preset: Low (it also lowers itself).",
    "Windows are real glass: you can look through them.",
]
for i, t in enumerate(tips):
    d.text((16, 182 + i * 10), t, font=f(8), fill=TEXT)

tag = "ss12.org"
ft = f(10)
d.text((SAFE_R - d.textlength(tag, font=ft), 16), tag, font=ft, fill=(120, 130, 170))

# ---- upscale, vignette and scanlines
out = img.resize((W * S, H * S), Image.NEAREST)
vg = Image.new("L", out.size, 0)
ImageDraw.Draw(vg).ellipse([-out.width * 0.15, -out.height * 0.25, out.width * 1.15, out.height * 1.25], fill=255)
vg = vg.filter(ImageFilter.GaussianBlur(120))
out = Image.composite(out, Image.new("RGB", out.size, (0, 0, 0)), vg.point(lambda v: min(255, int(v * 1.15 + 40))))
sl = ImageDraw.Draw(out)
for y in range(0, out.height, S):
    sl.line([0, y + S - 1, out.width, y + S - 1], fill=(0, 0, 0))
path = sys.argv[2] if len(sys.argv) > 2 else "lobby_preview.png"
if path.lower().endswith(".webp"):
    out.save(path, quality=90, method=6)
else:
    out.save(path)
print("ok", out.size)
