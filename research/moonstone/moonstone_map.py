"""Draws the 2-catcher moon stone line (4 pressure chambers, 9 stone presses, 1 sell lane) as a PNG with Pillow."""
import math
from fractions import Fraction
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

S = 100  # pixels per layout unit
W, H = 36, 21
BG = "#F6F7F9"
C = dict(dust="#A97B3F", sand="#D9A400", stone="#5B6472", ink="#1F2937", muted="#5B6472",
         chamber="#DCEAFE", chamber_edge="#2563EB", press="#FEF3C7", press_edge="#B45309",
         press_late="#FDE7C2", catcher="#E7E9EE", catcher_edge="#374151", sell="#DCFCE7", sell_edge="#15803D",
         split="#1D4ED8", merge="#7C3AED", panel="#FFFFFF", panel_edge="#CBD5E1")
OUT = Path(__file__).with_name("moonstone_line.png")

img = Image.new("RGB", (W * S, H * S), BG)
d = ImageDraw.Draw(img)


def font(size, bold=False):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/" + ("segoeuib.ttf" if bold else "segoeui.ttf"), size)
    except OSError:
        return ImageFont.load_default()


def P(x, y):
    return round(x * S), round((H - y) * S)


def line(points, color, width=7, arrow=True, halo=False):
    px = [P(*p) for p in points]
    if halo:
        d.line(px, fill=BG, width=width + 16, joint="curve")
    d.line(px, fill=color, width=width, joint="curve")
    if arrow:
        (x1, y1), (x2, y2) = px[-2], px[-1]
        a = math.atan2(y2 - y1, x2 - x1)
        length, half = 24, 12
        back = (x2 - length * math.cos(a), y2 - length * math.sin(a))
        d.polygon([(x2, y2), (back[0] + half * math.sin(a), back[1] - half * math.cos(a)),
                   (back[0] - half * math.sin(a), back[1] + half * math.cos(a))], fill=color)


def text(x, y, s, size=20, bold=False, color=None, anchor="mm"):
    d.text(P(x, y), s, font=font(size, bold), fill=color or C["ink"], anchor=anchor)


def box(x, y, w, h, fill, edge, lines, radius=16, width=4):
    x0, y0 = P(x - w / 2, y + h / 2)
    x1, y1 = P(x + w / 2, y - h / 2)
    d.rounded_rectangle([x0, y0, x1, y1], radius=radius, fill=fill, outline=edge, width=width)
    heights = [size * 1.3 for _, size, _, _ in lines]
    top = (y0 + y1) / 2 - sum(heights) / 2
    for (s, size, bold, color), h_px in zip(lines, heights):
        d.text(((x0 + x1) / 2, top + h_px / 2), s, font=font(size, bold), fill=color or C["ink"], anchor="mm")
        top += h_px


def diamond(x, y, label, r=0.34, fill=None):
    cx, cy = P(x, y)
    rp = r * S
    d.polygon([(cx, cy - rp), (cx + rp, cy), (cx, cy + rp), (cx - rp, cy)], fill=fill or C["split"])
    d.text((cx, cy), label, font=font(17, True), fill="white", anchor="mm")


def merge_node(x, y, r=0.3):
    cx, cy = P(x, y)
    d.ellipse([cx - r * S, cy - r * S, cx + r * S, cy + r * S], fill=C["merge"])
    d.text((cx, cy), "M", font=font(20, True), fill="white", anchor="mm")


def frac_label(f):
    return {Fraction(1, 2): "½", Fraction(1, 4): "¼", Fraction(1, 8): "⅛"}.get(f, f"{f.numerator}/{f.denominator}")


# ---- layout -------------------------------------------------------------------------------------------------
PRESS_X = [9.9 + 2.15 * i for i in range(9)]
PRESS_Y, PRESS_W, PRESS_H = 9.0, 1.85, 1.7
PRESS_TOP, PRESS_BOTTOM = PRESS_Y + PRESS_H / 2, PRESS_Y - PRESS_H / 2
RATE = [10.0] * 7 + [8.4, 8.4]  # settled blocks/min per press, from moonstone_sim.py (2 h run: 86.8 total)
STONE_Y = 6.6
SELL_X, SELL_Y = 31.3, STONE_Y


def build(leaves, depth=0):
    if len(leaves) == 1:
        return {"leaf": leaves[0][0], "x": leaves[0][1], "depth": depth}
    half = len(leaves) // 2
    left, right = build(leaves[:half], depth + 1), build(leaves[half:], depth + 1)
    return {"children": [left, right], "x": (left["x"] + right["x"]) / 2, "depth": depth}


def name_nodes(root, prefix):
    queue, n = [root], 1
    while queue:
        node = queue.pop(0)
        if "children" in node:
            node["name"] = f"{prefix}{n}"
            n += 1
            queue.extend(node["children"])


def draw_tree(root, root_y, sign, leaf_end_y, color, label_side):
    """sign -1: grows downward (sand, from above); +1: grows upward (dust, from below)."""
    nodes = []

    def walk(node, share):
        y = root_y + sign * node["depth"] * 1.0
        node["y"] = y
        nodes.append(node)
        if "children" not in node:
            return
        elbow = y + sign * 0.55
        for child in node["children"]:
            child_share = share / 2
            cy = root_y + sign * child["depth"] * 1.0
            end = leaf_end_y if "leaf" in child else cy - sign * 0.34
            line([(node["x"], y + sign * 0.34), (node["x"], elbow), (child["x"], elbow), (child["x"], end)], color)
            offered = 88 * child_share
            tag = f"{frac_label(child_share)} · {offered:g}"
            mid = (elbow + end) / 2 if "leaf" not in child else elbow + sign * 0.42
            if label_side > 0:
                text(child["x"] + 0.1, mid, tag, 15, color=C["muted"], anchor="lm")
            else:
                text(child["x"] - 0.1, mid, tag, 15, color=C["muted"], anchor="rm")
            walk(child, child_share)

    walk(root, Fraction(1))
    return nodes


# ---- title --------------------------------------------------------------------------------------------------
text(0.5, 20.45, "Moon Stone Line  ·  2 Capsule Catchers → 4 Pressure Chambers → 9 Stone Presses → 1 Sell Lane",
     40, True, anchor="lm")
text(0.5, 19.85, "Figures read from the game (MoonWorld specs). 2-lane splitters alternate capsules and skip a full lane. "
     "Rates in kg (= capsules) per minute; ½ ¼ ⅛ = share a splitter offers each lane.", 20, color=C["muted"], anchor="lm")

# ---- dust tree (from below): drawn first so the stone lane passes over it -----------------------------------
dust_root = build([(f"P{i + 1}", x - 0.45) for i, x in enumerate(PRESS_X)])
name_nodes(dust_root, "D")
dust_nodes = draw_tree(dust_root, 2.0, +1, PRESS_BOTTOM, C["dust"], label_side=-1)

# ---- stone: each press down to one lane, on to the sell box --------------------------------------------------
for x in PRESS_X:
    line([(x + 0.5, PRESS_BOTTOM), (x + 0.5, STONE_Y)], C["stone"], arrow=False, halo=True)
line([(PRESS_X[0] + 0.5, STONE_Y), (SELL_X - 1.6, STONE_Y)], C["stone"], width=9, halo=True)
text(28.75, STONE_Y + 0.32, "stone ≈ 86.8/min", 16, True, C["stone"])

# ---- catcher B and its belt ---------------------------------------------------------------------------------
box(2.5, 2.0, 3.8, 2.0, C["catcher"], C["catcher_edge"], [
    ("CAPSULE CATCHER B", 24, True, None), ("always full · stores 100 kg", 17, False, C["muted"]),
    ("outfeed fills its belt", 17, False, C["muted"]), ("88 capsules/min · 1 kg dust each", 17, False, C["muted"])])
line([(4.4, 2.0), (dust_root["x"] - 0.36, 2.0)], C["dust"], width=9)
text(9.5, 2.38, "DUST BELT · 88/min (full) → presses' dust inputs", 18, True, C["dust"])

# ---- catcher A, chamber tree, chambers -----------------------------------------------------------------------
AY = 16.8
box(2.5, AY, 3.8, 2.0, C["catcher"], C["catcher_edge"], [
    ("CAPSULE CATCHER A", 24, True, None), ("always full · stores 100 kg", 17, False, C["muted"]),
    ("outfeed fills its belt", 17, False, C["muted"]), ("88 capsules/min · 1 kg dust each", 17, False, C["muted"])])
line([(4.4, AY), (5.06, AY)], C["dust"], width=9)
text(4.72, AY + 0.33, "88", 16, True, C["dust"])
A2_Y, A3_Y = 17.9, 15.7
for y in (A2_Y, A3_Y):
    line([(5.74, AY), (6.25, AY), (6.25, y), (6.76, y)], C["dust"])
    text(6.35, (AY + y) / 2, "½ · 44", 15, color=C["muted"], anchor="lm")
CHAMBER_X, CHAMBER_W, CHAMBER_H = 10.05, 3.6, 0.98
CHAMBER_Y = [18.45, 17.35, 16.25, 15.15]
for i, y in enumerate(CHAMBER_Y):
    parent = A2_Y if i < 2 else A3_Y
    line([(7.44, parent), (7.85, parent), (7.85, y), (CHAMBER_X - CHAMBER_W / 2, y)], C["dust"])
    text(7.76, y + 0.18, "¼ · 22", 15, color=C["muted"], anchor="rm")
    line([(CHAMBER_X + CHAMBER_W / 2, y), (12.95, y), (12.95, AY)], C["sand"], arrow=False)
    text(12.4, y + 0.2, "22", 15, True, C["sand"])
diamond(5.4, AY, "A1")
diamond(7.1, A2_Y, "A2")
diamond(7.1, A3_Y, "A3")
for i, y in enumerate(CHAMBER_Y):
    box(CHAMBER_X, y, CHAMBER_W, CHAMBER_H, C["chamber"], C["chamber_edge"], [
        (f"PRESSURE CHAMBER {i + 1}", 19, True, None),
        ("22 dust → 22 sand /min · 88% busy", 15, False, C["muted"]),
        ("5 kg per 12 s · 10 kW · holds 20 dust, 10 sand", 13, False, C["muted"])], radius=12, width=3)

# ---- sand merge, sand belt, sand tree (from above) ----------------------------------------------------------
sand_root = build([(f"P{i + 1}", x) for i, x in enumerate(PRESS_X)])
name_nodes(sand_root, "S")
line([(12.95, AY), (13.32, AY)], C["sand"])
line([(13.9, AY), (sand_root["x"], AY), (sand_root["x"], 14.0 + 0.36)], C["sand"], width=9)
text(16.6, AY + 0.36, "SAND BELT · 4 × 22 = 88/min (full)", 18, True, C["sand"])
merge_node(13.6, AY)
sand_nodes = draw_tree(sand_root, 14.0, -1, PRESS_TOP, C["sand"], label_side=+1)

# ---- splitters on top of their lines ------------------------------------------------------------------------
for node in sand_nodes + dust_nodes:
    if "children" in node:
        diamond(node["x"], node["y"], node["name"])

# ---- presses ------------------------------------------------------------------------------------------------
for i, (x, rate) in enumerate(zip(PRESS_X, RATE)):
    late = i >= 7
    box(x, PRESS_Y, PRESS_W, PRESS_H, C["press_late"] if late else C["press"], C["press_edge"], [
        (f"P{i + 1}", 30, True, None), ("STONE PRESS", 14, True, C["press_edge"]),
        (f"{rate:.1f} blocks/min", 16, True, None), ("fills last" if late else "18 kW · 6 s/block", 13, False, C["muted"])],
        radius=12, width=3)
    text(x - 0.9, PRESS_BOTTOM - 0.18, "dust", 12, color=C["dust"], anchor="mm")
    text(x + 0.92, PRESS_BOTTOM - 0.18, "stone", 12, color=C["stone"], anchor="mm")

# ---- sell box -----------------------------------------------------------------------------------------------
box(SELL_X, SELL_Y, 3.2, 1.9, C["sell"], C["sell_edge"], [
    ("SELL BOX", 28, True, None), ("one input lane · max 88/min", 17, False, C["muted"]),
    ("≈ 86.8 blocks/min sold", 18, True, C["sell_edge"]), ("≈ 5,200 blocks per daylight hour", 15, False, C["muted"])])

# ---- notes (left middle) ------------------------------------------------------------------------------------
box(4.25, 9.6, 7.5, 4.9, C["panel"], C["panel_edge"], [
    ("BUILD NOTES", 28, True, None),
    ("Sand tree S1–S8 (above) and dust tree D1–D8 (below)", 21, False, None),
    ("have the same shape: every press gets one of each.", 21, False, None),
    ("Splitters halve the belt: ½ → ¼ → ⅛. P1–P7 are offered", 21, False, None),
    ("11/min and take 10; the rest skips on to P8 and P9", 21, False, None),
    ("(1/16 each plus the overflow), so they fill last.", 21, False, None),
    ("Full speed after ~1–2 h; stores stay full overnight.", 21, False, None),
    ("Each press: 1 kg dust + 1 kg sand → 1 block (2 kg).", 21, False, None),
    ("Holds 12 dust, 12 sand, 10 blocks.", 21, False, None)], radius=14, width=3)

# ---- summary panel ------------------------------------------------------------------------------------------
summary = [
    ("LINE SUMMARY", 32, True, None),
    ("≈ 86.8 blocks/min settled (99% of the sell lane)", 24, True, C["sell_edge"]),
    ("Catcher A → chambers: 88 kg/min dust", 22, False, None),
    ("Catcher B → presses: 88 kg/min dust", 22, False, None),
    ("Chambers → presses: 88 kg/min sand (one full belt)", 22, False, None),
    ("", 14, False, None),
    ("Machines: 2 catchers · 4 chambers · 9 presses", 22, False, None),
    ("Splitters: 19 (A1–A3, S1–S8, D1–D8) · Merges: 2", 22, False, None),
    ("Power: 4 × 10 + 9 × 18 = 202 kW while running", 22, False, None),
    ("≈ 13 solar trackers (16 kW peak each)", 22, False, None),
    ("", 14, False, None),
    ("Smaller build: drop P9 and S8/D8 → 4 + 8 presses,", 22, False, C["muted"]),
    ("80 blocks/min, even ⅛ split, full speed at once", 22, False, C["muted"]),
    ("", 14, False, None),
    ("Assumed: 1 kg dust → 1 kg sand; a full catcher's", 19, False, C["muted"]),
    ("outfeed fills its belt; blocks space like capsules.", 19, False, C["muted"]),
    ("Production stops in the ~2-week lunar night.", 19, False, C["muted"]),
]
box(32.3, 14.15, 6.9, 9.6, C["panel"], C["panel_edge"], summary, radius=14, width=3)

# ---- legend -------------------------------------------------------------------------------------------------
lx, ly = 29.2, 4.3
box(32.3, 2.55, 6.9, 4.1, C["panel"], C["panel_edge"], [("", 10, False, None)], radius=14, width=3)
text(lx, ly, "LEGEND", 22, True, anchor="lm")
for i, (name, color) in enumerate((("dust belt", C["dust"]), ("sand belt", C["sand"]), ("stone lane", C["stone"]))):
    y = ly - 0.6 - i * 0.5
    line([(lx, y), (lx + 1.2, y)], color, width=9)
    text(lx + 1.45, y, name, 17, anchor="lm")
diamond(lx + 0.6, ly - 2.15, "S", r=0.26)
text(lx + 1.45, ly - 2.15, "2-lane splitter: alternates, skips a full lane", 16, anchor="lm")
merge_node(lx + 0.6, ly - 2.75, r=0.24)
text(lx + 1.45, ly - 2.75, "merge: several belts onto one", 16, anchor="lm")
text(lx, ly - 3.35, "½ · 44  =  share offered · kg/min offered", 16, color=C["muted"], anchor="lm")

img.save(OUT)
print(OUT, img.size)
