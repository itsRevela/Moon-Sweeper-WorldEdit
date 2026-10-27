"""Four always-full catchers: the plant is two independent lines (2 catchers each: one feeds the line's chambers,
one its presses' dust; the chambers' sand feeds the line's presses). Belts cap at 88 capsules/min, so merging
catcher belts adds nothing; lines share no belt or machine, so the plant's output is the sum of its lines.
Uses moonstone_sim (2-lane splitters that skip a full lane, game figures, sand 1:1 assumed)."""
import itertools
import sys

sys.path.insert(0, ".")
import moonstone_sim as m  # noqa: E402

CHAMBER_KW, PRESS_KW, TRACKER_KW = 10, 18, 16
CANDIDATES = [(4, 8), (4, 9), (4, 10), (5, 9), (5, 10), (3, 8)]


def run(chambers, presses, warm, end):
    m.WARMUP, m.RUN = warm * 60, end * 60
    return m.simulate(chambers, presses, skip=True)[0]


lines = {}
print("One line (2 catchers):  start (min 10-40)   settled (min 120-150)")
for c, p in CANDIDATES:
    lines[(c, p)] = (run(c, p, 10, 40), run(c, p, 120, 150))
    print(f"  {c} chambers + {p:>2} presses   {lines[(c, p)][0]:>8.1f}            {lines[(c, p)][1]:>8.1f}")

print("\nPlants of two lines (4 catchers), best settled output first, then fewest machines:")
print("  lines                 chambers presses  start  settled  of 176   kW  trackers  blocks/min per machine")
plants = []
for a, b in itertools.combinations_with_replacement(CANDIDATES, 2):
    chambers, presses = a[0] + b[0], a[1] + b[1]
    start, settled = lines[a][0] + lines[b][0], lines[a][1] + lines[b][1]
    kw = chambers * CHAMBER_KW + presses * PRESS_KW
    plants.append((round(settled, 1), -(chambers + presses), a, b, chambers, presses, start, kw))
for settled, _, a, b, chambers, presses, start, kw in sorted(plants, reverse=True)[:6]:
    print(f"  {a[0]}+{a[1]:<2} & {b[0]}+{b[1]:<2}         {chambers:>8} {presses:>7} {start:>6.1f} {settled:>8.1f}  "
          f"{settled / 176:>6.0%}  {kw:>4}  {-(-kw // TRACKER_KW):>8}  {settled / (chambers + presses):>10.2f}")
