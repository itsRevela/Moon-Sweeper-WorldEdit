"""The full 12-line moon stone plant as one large SVG (and a PNG preview), nothing simplified.

Each line: 2 capsule catchers, chamber tree A1-A3, 4 pressure chambers, sand merge, sand tree S1-S8, dust tree D1-D8,
9 stone presses, one stone lane to its own sell box. Catchers, chambers and presses are numbered across the base.
Figures: game values (MoonWorld) and the capsule simulation (moonstone_sim.py, checked by moonstone_12.py).
"""
import math
from fractions import Fraction
from html import escape
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).parent

# ---- figures ---------------------------------------------------------------------------------------------------
LINES = 12
BELT = 1.1 / 0.75 * 60  # 88 items/min on a belt or sell lane
LINE_RATE = 86.8  # settled blocks/min per 4+9 line (simulation, minutes 120-150)
PRESS_RATE = [10.0] * 7 + [(LINE_RATE - 70.0) / 2] * 2  # P8/P9 of each line take the overflow
CHAMBER_KW, PRESS_KW, TRACKER_KW, TRACKER_KWH, CHARGE_EFF = 10, 18, 16, 10, 0.92
LINE_KW = 4 * CHAMBER_KW + 9 * PRESS_KW
PLANT_KW = LINES * LINE_KW
TRACKERS = math.ceil(PLANT_KW / TRACKER_KW)
PLANT_RATE = LINES * LINE_RATE
LUNAR_DAY_H = 28.53 * (24 + 50.47 / 60)  # MoonSolarSystem.EarthTurnsPerLunarDay x Earth's turn relative to the moon
DAYLIGHT_H = LUNAR_DAY_H / 2
BATTERY_MIN = TRACKERS * TRACKER_KWH * CHARGE_EFF / PLANT_KW * 60

C = dict(bg="#F6F7F9", dust="#A97B3F", sand="#D9A400", stone="#5B6472", ink="#1F2937", muted="#5B6472",
         chamber="#DCEAFE", chamber_edge="#2563EB", press="#FEF3C7", press_edge="#B45309", press_late="#FDE7C2",
         catcher="#E7E9EE", catcher_edge="#374151", sell="#DCFCE7", sell_edge="#15803D", split="#1D4ED8",
         merge="#7C3AED", panel="#FFFFFF", panel_edge="#CBD5E1", line_bg="#FFFFFF", line_edge="#D5DAE1",
         solar="#0EA5E9", solar_edge="#0369A1")

# ---- canvas: one drawing API, two outputs (SVG and a Pillow PNG preview) ------------------------------------------
CELL_W, CELL_H = 34.6, 21.4  # one line, in layout units
COLS, ROWS = 3, 4
HEADER_H, FOOTER_H, MARGIN = 5.0, 19.0, 0.8
TOTAL_W = MARGIN * 2 + COLS * CELL_W
TOTAL_H = HEADER_H + ROWS * CELL_H + FOOTER_H + MARGIN


class Canvas:
    """Drawing in layout units, y up. Records SVG and draws the PNG preview at the same time."""

    def __init__(self, scale, preview_scale):
        self.s, self.ps = scale, preview_scale
        self.svg = []
        self.img = Image.new("RGB", (round(TOTAL_W * preview_scale), round(TOTAL_H * preview_scale)), C["bg"])
        self.draw = ImageDraw.Draw(self.img)
        self.ox = self.oy = 0.0
        self.fonts = {}

    def at(self, x, y):
        self.ox, self.oy = x, y

    def _svg_pt(self, x, y):
        return (self.ox + x) * self.s, (TOTAL_H - (self.oy + y)) * self.s

    def _png_pt(self, x, y):
        return (self.ox + x) * self.ps, (TOTAL_H - (self.oy + y)) * self.ps

    def _font(self, size, bold):
        key = (size, bold)
        if key not in self.fonts:
            px = max(6, round(size * self.ps / 100))
            try:
                self.fonts[key] = ImageFont.truetype("C:/Windows/Fonts/" + ("segoeuib.ttf" if bold else "segoeui.ttf"), px)
            except OSError:
                self.fonts[key] = ImageFont.load_default()
        return self.fonts[key]

    def polyline(self, pts, color, width):
        svg = " ".join(f"{x:.1f},{y:.1f}" for x, y in (self._svg_pt(*p) for p in pts))
        self.svg.append(f'<polyline points="{svg}" fill="none" stroke="{color}" stroke-width="{width}" '
                        f'stroke-linejoin="round" stroke-linecap="round"/>')
        self.draw.line([self._png_pt(*p) for p in pts], fill=color, width=max(1, round(width * self.ps / 100)), joint="curve")

    def polygon(self, pts, fill):
        svg = " ".join(f"{x:.1f},{y:.1f}" for x, y in (self._svg_pt(*p) for p in pts))
        self.svg.append(f'<polygon points="{svg}" fill="{fill}"/>')
        self.draw.polygon([self._png_pt(*p) for p in pts], fill=fill)

    def rect(self, x, y, w, h, fill, edge, radius, width):
        sx, sy = self._svg_pt(x - w / 2, y + h / 2)
        self.svg.append(f'<rect x="{sx:.1f}" y="{sy:.1f}" width="{w * self.s:.1f}" height="{h * self.s:.1f}" '
                        f'rx="{radius}" fill="{fill}" stroke="{edge}" stroke-width="{width}"/>')
        x0, y0 = self._png_pt(x - w / 2, y + h / 2)
        x1, y1 = self._png_pt(x + w / 2, y - h / 2)
        self.draw.rounded_rectangle([x0, y0, x1, y1], radius=max(1, round(radius * self.ps / 100)), fill=fill,
                                    outline=edge, width=max(1, round(width * self.ps / 100)))

    def circle(self, x, y, r, fill):
        sx, sy = self._svg_pt(x, y)
        self.svg.append(f'<circle cx="{sx:.1f}" cy="{sy:.1f}" r="{r * self.s:.1f}" fill="{fill}"/>')
        px, py = self._png_pt(x, y)
        self.draw.ellipse([px - r * self.ps, py - r * self.ps, px + r * self.ps, py + r * self.ps], fill=fill)

    def text(self, x, y, s, size=20, bold=False, color=None, anchor="mm"):
        color = color or C["ink"]
        sx, sy = self._svg_pt(x, y)
        svg_anchor = {"m": "middle", "l": "start", "r": "end"}[anchor[0]]
        self.svg.append(f'<text x="{sx:.1f}" y="{sy:.1f}" font-size="{size}" font-weight="{"700" if bold else "400"}" '
                        f'fill="{color}" text-anchor="{svg_anchor}" dominant-baseline="central">{escape(s)}</text>')
        self.draw.text(self._png_pt(x, y), s, font=self._font(size, bold), fill=color, anchor=anchor)

    def save(self, svg_path, png_path):
        w, h = TOTAL_W * self.s, TOTAL_H * self.s
        head = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w:.0f}" height="{h:.0f}" viewBox="0 0 {w:.0f} {h:.0f}" '
                f'font-family="Segoe UI, Arial, sans-serif"><rect width="100%" height="100%" fill="{C["bg"]}"/>')
        svg_path.write_text(head + "\n".join(self.svg) + "</svg>", encoding="utf-8")
        self.img.save(png_path)


cv = Canvas(scale=100, preview_scale=40)


def line(pts, color, width=7, arrow=True, halo=False):
    if halo:
        cv.polyline(pts, C["bg"] if halo is True else halo, width + 16)
    cv.polyline(pts, color, width)
    if arrow:
        (x1, y1), (x2, y2) = pts[-2], pts[-1]
        a = math.atan2(y2 - y1, x2 - x1)
        length, half = 0.24, 0.12
        bx, by = x2 - length * math.cos(a), y2 - length * math.sin(a)
        cv.polygon([(x2, y2), (bx - half * math.sin(a), by + half * math.cos(a)),
                    (bx + half * math.sin(a), by - half * math.cos(a))], color)


def box(x, y, w, h, fill, edge, rows, radius=16, width=4):
    cv.rect(x, y, w, h, fill, edge, radius, width)
    heights = [size * 1.3 / 100 for _, size, _, _ in rows]
    top = y + sum(heights) / 2
    for (s, size, bold, color), h_u in zip(rows, heights):
        cv.text(x, top - h_u / 2, s, size, bold, color)
        top -= h_u


def diamond(x, y, label, r=0.34):
    cv.polygon([(x, y + r), (x + r, y), (x, y - r), (x - r, y)], C["split"])
    cv.text(x, y, label, 17, True, "#FFFFFF")


def merge_node(x, y, r=0.3):
    cv.circle(x, y, r, C["merge"])
    cv.text(x, y, "M", 20, True, "#FFFFFF")


def frac_label(f):
    return {Fraction(1, 2): "½", Fraction(1, 4): "¼", Fraction(1, 8): "⅛"}.get(f, f"{f.numerator}/{f.denominator}")


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
    nodes = []

    def walk(node, share):
        y = root_y + sign * node["depth"]
        node["y"] = y
        nodes.append(node)
        if "children" not in node:
            return
        elbow = y + sign * 0.55
        for child in node["children"]:
            child_share = share / 2
            cy = root_y + sign * child["depth"]
            end = leaf_end_y if "leaf" in child else cy - sign * 0.34
            line([(node["x"], y + sign * 0.34), (node["x"], elbow), (child["x"], elbow), (child["x"], end)], color)
            tag = f"{frac_label(child_share)} · {float(BELT * child_share):g}"
            mid = (elbow + end) / 2 if "leaf" not in child else elbow + sign * 0.42
            if label_side > 0:
                cv.text(child["x"] + 0.1, mid, tag, 15, color=C["muted"], anchor="lm")
            else:
                cv.text(child["x"] - 0.1, mid, tag, 15, color=C["muted"], anchor="rm")
            walk(child, child_share)

    walk(root, Fraction(1))
    return nodes


# ---- one line, in its cell (local coordinates: x 0..CELL_W, y 0..CELL_H) ----------------------------------------
PRESS_X = [9.9 + 2.15 * i for i in range(9)]
PRESS_Y, PRESS_W, PRESS_H = 9.0, 1.85, 1.7
PRESS_TOP, PRESS_BOTTOM = PRESS_Y + PRESS_H / 2, PRESS_Y - PRESS_H / 2
STONE_Y, SELL_X = 6.6, 31.6
AY, A2_Y, A3_Y = 16.8, 17.9, 15.7
CHAMBER_X, CHAMBER_W, CHAMBER_H = 10.05, 3.6, 0.98
CHAMBER_Y = [18.45, 17.35, 16.25, 15.15]


def draw_line(n):
    cat_a, cat_b = 2 * n - 1, 2 * n
    first_chamber, first_press = 4 * (n - 1) + 1, 9 * (n - 1) + 1
    cv.rect(CELL_W / 2, CELL_H / 2, CELL_W - 0.3, CELL_H - 0.3, C["line_bg"], C["line_edge"], 24, 5)
    cv.text(0.6, 20.55, f"LINE {n}", 40, True, anchor="lm")
    cv.text(3.6, 20.55, f"Catchers {cat_a} & {cat_b} → Chambers {first_chamber}–{first_chamber + 3} → "
            f"Presses {first_press}–{first_press + 8} → Sell box {n}   ·   ≈ {LINE_RATE} blocks/min   ·   {LINE_KW} kW",
            22, True, C["muted"], anchor="lm")

    # dust tree from below (drawn first so the stone lane passes over it)
    dust_root = build([(f"P{i}", x - 0.45) for i, x in enumerate(PRESS_X)])
    name_nodes(dust_root, "D")
    dust_nodes = draw_tree(dust_root, 2.0, +1, PRESS_BOTTOM, C["dust"], label_side=-1)

    # stone: every press onto one lane, to this line's sell box
    for x in PRESS_X:
        line([(x + 0.5, PRESS_BOTTOM), (x + 0.5, STONE_Y)], C["stone"], arrow=False, halo=C["line_bg"])
    line([(PRESS_X[0] + 0.5, STONE_Y), (SELL_X - 1.6, STONE_Y)], C["stone"], width=9, halo=C["line_bg"])
    cv.text(28.95, STONE_Y + 0.32, f"stone ≈ {LINE_RATE}/min", 16, True, C["stone"])

    # catcher B
    box(2.5, 2.0, 3.8, 2.0, C["catcher"], C["catcher_edge"], [
        (f"CAPSULE CATCHER {cat_b}", 23, True, None), ("always full · stores 100 kg", 17, False, C["muted"]),
        ("outfeed fills its belt", 17, False, C["muted"]), ("88 capsules/min · 1 kg dust each", 17, False, C["muted"])])
    line([(4.4, 2.0), (dust_root["x"] - 0.36, 2.0)], C["dust"], width=9)
    cv.text(9.5, 2.38, "DUST BELT · 88/min (full) → presses' dust inputs", 18, True, C["dust"])

    # catcher A, chamber tree, chambers
    box(2.5, AY, 3.8, 2.0, C["catcher"], C["catcher_edge"], [
        (f"CAPSULE CATCHER {cat_a}", 23, True, None), ("always full · stores 100 kg", 17, False, C["muted"]),
        ("outfeed fills its belt", 17, False, C["muted"]), ("88 capsules/min · 1 kg dust each", 17, False, C["muted"])])
    line([(4.4, AY), (5.06, AY)], C["dust"], width=9)
    cv.text(4.72, AY + 0.33, "88", 16, True, C["dust"])
    for y in (A2_Y, A3_Y):
        line([(5.74, AY), (6.25, AY), (6.25, y), (6.76, y)], C["dust"])
        cv.text(6.35, (AY + y) / 2, "½ · 44", 15, color=C["muted"], anchor="lm")
    for i, y in enumerate(CHAMBER_Y):
        parent = A2_Y if i < 2 else A3_Y
        line([(7.44, parent), (7.85, parent), (7.85, y), (CHAMBER_X - CHAMBER_W / 2, y)], C["dust"])
        cv.text(7.76, y + 0.18, "¼ · 22", 15, color=C["muted"], anchor="rm")
        line([(CHAMBER_X + CHAMBER_W / 2, y), (12.95, y), (12.95, AY)], C["sand"], arrow=False)
        cv.text(12.4, y + 0.2, "22", 15, True, C["sand"])
    diamond(5.4, AY, "A1")
    diamond(7.1, A2_Y, "A2")
    diamond(7.1, A3_Y, "A3")
    for i, y in enumerate(CHAMBER_Y):
        box(CHAMBER_X, y, CHAMBER_W, CHAMBER_H, C["chamber"], C["chamber_edge"], [
            (f"PRESSURE CHAMBER {first_chamber + i}", 19, True, None),
            ("22 dust → 22 sand /min · 88% busy", 15, False, C["muted"]),
            ("5 kg per 12 s · 10 kW · holds 20 dust, 10 sand", 13, False, C["muted"])], radius=12, width=3)

    # sand merge, sand belt, sand tree from above
    sand_root = build([(f"P{i}", x) for i, x in enumerate(PRESS_X)])
    name_nodes(sand_root, "S")
    line([(12.95, AY), (13.32, AY)], C["sand"])
    line([(13.9, AY), (sand_root["x"], AY), (sand_root["x"], 14.0 + 0.36)], C["sand"], width=9)
    cv.text(16.6, AY + 0.36, "SAND BELT · 4 × 22 = 88/min (full)", 18, True, C["sand"])
    merge_node(13.6, AY)
    sand_nodes = draw_tree(sand_root, 14.0, -1, PRESS_TOP, C["sand"], label_side=+1)

    for node in sand_nodes + dust_nodes:
        if "children" in node:
            diamond(node["x"], node["y"], node["name"])

    # presses
    for i, (x, rate) in enumerate(zip(PRESS_X, PRESS_RATE)):
        late = i >= 7
        box(x, PRESS_Y, PRESS_W, PRESS_H, C["press_late"] if late else C["press"], C["press_edge"], [
            (f"P{first_press + i}", 28, True, None), ("STONE PRESS", 14, True, C["press_edge"]),
            (f"{rate:.1f} blocks/min", 16, True, None),
            ("fills last" if late else "18 kW · 6 s/block", 13, False, C["muted"])], radius=12, width=3)
        cv.text(x - 0.9, PRESS_BOTTOM - 0.18, "dust", 12, color=C["dust"])
        cv.text(x + 0.92, PRESS_BOTTOM - 0.18, "stone", 12, color=C["stone"])

    # sell box
    box(SELL_X, STONE_Y, 3.2, 1.9, C["sell"], C["sell_edge"], [
        (f"SELL BOX {n}", 26, True, None), ("one input lane · max 88/min", 16, False, C["muted"]),
        (f"≈ {LINE_RATE} blocks/min sold", 17, True, C["sell_edge"]),
        (f"≈ {LINE_RATE * 60:,.0f} per daylight hour", 15, False, C["muted"])])

    # line figures (where the 2-catcher diagram had its notes)
    box(4.25, 9.6, 7.3, 4.6, C["panel"], C["panel_edge"], [
        (f"LINE {n} FLOWS", 29, True, None),
        (f"Catcher {cat_a} → chambers: 88 kg/min dust", 23, False, None),
        (f"Catcher {cat_b} → presses: 88 kg/min dust", 23, False, None),
        ("Chambers → presses: 88 kg/min sand (full belt)", 23, False, None),
        (f"Presses → sell box {n}: ≈ {LINE_RATE} blocks/min", 23, False, None),
        ("Splitters 19 (A1–A3, S1–S8, D1–D8) · merges 2", 23, False, None),
        (f"Power {LINE_KW} kW (4 × 10 + 9 × 18) while running", 23, False, None),
        ("P8/P9 positions fill last: full speed in ~1–2 h", 23, False, C["muted"])], radius=14, width=3)


# ---- header ------------------------------------------------------------------------------------------------------
cv.at(0, 0)
cv.text(MARGIN, TOTAL_H - 1.6, f"Moon Stone Plant  ·  {LINES} Export Lines → {LINES} Sell Boxes  ·  "
        f"{2 * LINES} Capsule Catchers · {4 * LINES} Pressure Chambers · {9 * LINES} Stone Presses", 64, True, anchor="lm")
cv.text(MARGIN, TOTAL_H - 2.65, f"≈ {PLANT_RATE:,.1f} blocks/min  ·  ≈ {PLANT_RATE * 60:,.0f} blocks per daylight hour  ·  "
        f"{PLANT_KW:,} kW while running  ·  {TRACKERS} solar trackers  ·  {LINES * 19} splitters  ·  {LINES * 2} merges",
        36, True, C["sell_edge"], anchor="lm")
cv.text(MARGIN, TOTAL_H - 3.45, "Figures read from the game (MoonWorld specs) and a capsule-by-capsule simulation of one line "
        "(2-lane splitters alternate and skip a full lane). Lines share no belt, splitter or machine, so the plant is 12 × "
        "one line. Rates in kg (= capsules) per minute; ½ ¼ ⅛ = share a splitter offers each lane.", 32, color=C["muted"],
        anchor="lm")
cv.text(MARGIN, TOTAL_H - 4.2, "Assumed (not yet checked in game): 1 kg dust → 1 kg sand; a full catcher's outfeed fills its "
        "belt (88/min); stone blocks sit on a lane like capsules (0.75 m apart).", 32, color=C["muted"], anchor="lm")

# ---- the 12 lines --------------------------------------------------------------------------------------------------
for n in range(1, LINES + 1):
    row, col = divmod(n - 1, COLS)
    cv.at(MARGIN + col * CELL_W, FOOTER_H + (ROWS - 1 - row) * CELL_H)
    draw_line(n)

# ---- footer: plant summary, solar field, legend and notes -----------------------------------------------------------
cv.at(MARGIN, 0.6)
panel_h = FOOTER_H - 1.4
box(CELL_W / 2, panel_h / 2, CELL_W - 0.3, panel_h, C["panel"], C["panel_edge"], [("", 16, False, None)], 24, 5)
summary = [
    ("PLANT SUMMARY", 62, True, None),
    (f"≈ {PLANT_RATE:,.1f} blocks/min sold ({LINES} lanes × {LINE_RATE}; lane max {BELT:.0f})", 40, True, C["sell_edge"]),
    (f"≈ {PLANT_RATE * 60:,.0f} blocks per hour · ≈ {PLANT_RATE * 60 * DAYLIGHT_H / 1e6:.1f} million per lunar daylight "
     f"({DAYLIGHT_H:.0f} h)", 37, False, None),
    ("", 34, False, None),
    (f"Capsule catchers: {2 * LINES} (always full) · dust {2 * LINES * BELT:,.0f} kg/min", 37, False, None),
    (f"Pressure chambers: {4 * LINES} · sand {4 * LINES * 22:,} kg/min (each 22 of 25, 88% busy)", 37, False, None),
    (f"Stone presses: {9 * LINES} ({7 * LINES} at 10.0/min, {2 * LINES} at {PRESS_RATE[-1]:.1f}/min)", 37, False, None),
    (f"Splitters: {LINES * 19} (A1–A3, S1–S8, D1–D8 per line) · merges: {LINES * 2}", 37, False, None),
    (f"Belts per line: 2 dust + 1 sand (all full, 88/min) + 1 stone lane", 37, False, None),
    ("", 34, False, None),
    (f"Smaller build: {LINES} × (4 chambers + 8 presses) ≈ 958.4 blocks/min, {LINES * (4 * CHAMBER_KW + 8 * PRESS_KW):,} kW,", 34, False, C["muted"]),
    (f"{math.ceil(LINES * (4 * CHAMBER_KW + 8 * PRESS_KW) / TRACKER_KW)} trackers, every press at full speed from the start "
     "(8% less stone, ~same per tracker)", 34, False, C["muted"]),
    ("Sharing belts between lines could at best save 5 chambers + 2 presses (86 kW, 3.5%):", 34, False, C["muted"]),
    ("floor of 43 chambers and 106 presses, at the cost of belts crossing lines.", 34, False, C["muted"]),
]
heights = [size * 1.3 / 100 for _, size, _, _ in summary]
top = panel_h - 0.5
for (s, size, bold, color), h in zip(summary, heights):
    cv.text(0.6, top - h / 2, s, size, bold, color, anchor="lm")
    top -= h

# solar field: every tracker drawn
cv.at(MARGIN + CELL_W, 0.6)
box(CELL_W / 2, panel_h / 2, CELL_W - 0.3, panel_h, C["panel"], C["panel_edge"], [("", 16, False, None)], 24, 5)
cv.text(0.6, panel_h - 0.7, f"SOLAR FIELD · {TRACKERS} SOLAR TRACKERS (shared grid)", 62, True, anchor="lm")
cv.text(0.6, panel_h - 1.45, f"{TRACKERS} × {TRACKER_KW} kW peak = {TRACKERS * TRACKER_KW:,} kW for a {PLANT_KW:,} kW load "
        f"({LINES} lines × {LINE_KW} kW) · trackers follow the sun", 37, True, C["solar_edge"], anchor="lm")
cols, size, gap = 19, 1.28, 0.4
for i in range(TRACKERS):
    r, c = divmod(i, cols)
    x, y = 1.2 + c * (size + gap) + size / 2, panel_h - 2.6 - r * (size + gap) - size / 2
    cv.rect(x, y, size, size, C["solar"], C["solar_edge"], 8, 3)
    cv.text(x, y, f"T{i + 1}", 23, True, "#FFFFFF")
cv.text(0.6, 1.75, f"Batteries: {TRACKERS} × {TRACKER_KWH} kWh × {CHARGE_EFF} charge efficiency = "
        f"{TRACKERS * TRACKER_KWH * CHARGE_EFF:,.0f} kWh ≈ {BATTERY_MIN:.0f} min of full load", 34, False, C["muted"], anchor="lm")
cv.text(0.6, 0.9, f"Moon clock runs in real time: lunar day ≈ {LUNAR_DAY_H / 24:.2f} days, ≈ {DAYLIGHT_H:.0f} h of daylight; "
        "production stops in the lunar night.", 34, False, C["muted"], anchor="lm")

# legend and build notes
cv.at(MARGIN + 2 * CELL_W, 0.6)
box(CELL_W / 2, panel_h / 2, CELL_W - 0.3, panel_h, C["panel"], C["panel_edge"], [("", 16, False, None)], 24, 5)
cv.text(0.6, panel_h - 0.7, "LEGEND", 62, True, anchor="lm")
ly = panel_h - 1.7
for i, (name, color) in enumerate((("dust belt (capsules, 1 kg each)", C["dust"]), ("sand belt", C["sand"]),
                                   ("stone lane (blocks, 2 kg each)", C["stone"]))):
    y = ly - i * 1.0
    line([(0.8, y), (2.6, y)], color, width=11)
    cv.text(3.0, y, name, 37, anchor="lm")
diamond(1.7, ly - 3.2, "S", r=0.32)
cv.text(3.0, ly - 3.2, "2-lane splitter: alternates capsules, skips a full lane", 37, anchor="lm")
merge_node(1.7, ly - 4.2, r=0.3)
cv.text(3.0, ly - 4.2, "merge: several belts onto one", 37, anchor="lm")
cv.rect(1.7, ly - 5.2, 1.2, 0.6, C["press"], C["press_edge"], 8, 3)
cv.text(3.0, ly - 5.2, "stone press (shaded: P8/P9 positions, fill last)", 37, anchor="lm")
cv.rect(1.7, ly - 6.2, 1.2, 0.6, C["chamber"], C["chamber_edge"], 8, 3)
cv.text(3.0, ly - 6.2, "pressure chamber", 37, anchor="lm")
cv.text(0.8, ly - 7.2, "½ · 44  =  share a splitter offers that lane · kg/min offered", 34, color=C["muted"], anchor="lm")
notes = [
    ("BUILD NOTES", 46, True, None),
    ("Every line is identical; build one, check it, then copy it 11 times.", 34, False, None),
    ("Sand tree (S1–S8, above the presses) and dust tree (D1–D8, below) have the same", 34, False, None),
    ("shape, so every press gets one sand and one dust input.", 34, False, None),
    ("P1–P7 positions are offered 11/min and take 10; the overflow skips on to the", 34, False, None),
    ("P8/P9 positions (1/16 each + overflow), which fill last: ~1–2 h to full speed.", 34, False, None),
    ("Machine stores (12 dust, 12 sand, 10 blocks per press) stay full overnight.", 34, False, None),
]
top = ly - 8.1
for s, size, bold, color in notes:
    h = size * 1.3 / 100
    cv.text(0.8, top - h / 2, s, size, bold, color, anchor="lm")
    top -= h

cv.save(HERE / "moonstone_plant_12.svg", HERE / "moonstone_plant_12_preview.png")
print(f"SVG {TOTAL_W * 100:.0f} x {TOTAL_H * 100:.0f} px, preview {cv.img.size}; {len(cv.svg):,} SVG elements")
