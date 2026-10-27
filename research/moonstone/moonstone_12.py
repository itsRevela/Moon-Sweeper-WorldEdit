"""12 sell boxes (one lane each): every figure derived from the game's values and the capsule simulation.

Game values (read from MoonWorld in the running game): belt 1.1 m/s, capsules 0.75 m apart; capsule 1 kg; chamber
5 kg per 12 s, 10 kW; press 1 kg dust + 1 kg sand -> 1 block per 6 s, 18 kW; solar tracker 16 kW peak;
MoonSolarSystem.EarthTurnsPerLunarDay 28.53 with the clock at real-time speed (clockSpeed 1).
Assumptions: 1 kg dust -> 1 kg sand; a full catcher's outfeed fills its belt; blocks sit on a lane like capsules.
"""
import math
import sys

sys.path.insert(0, ".")
import moonstone_sim as sim  # noqa: E402

LANES = 12
BELT = 1.1 / 0.75 * 60  # items per minute on one belt / sell lane
CHAMBER = 5 * 60 / 12  # kg of sand per minute
PRESS = 60 / 6  # blocks per minute
CHAMBER_KW, PRESS_KW, TRACKER_KW = 10, 18, 16
results = []


def check(claim, value, ok):
    results.append((claim, value, ok))


def settled(chambers, presses, start_min, end_min):
    sim.WARMUP, sim.RUN = start_min * 60, end_min * 60
    return sim.simulate(chambers, presses, skip=True)[0]


# 1. Belt and sell-lane limit.
check("one belt / sell lane carries 88 items per minute", f"{BELT:.1f}", round(BELT, 6) == 88)

# 2. One line, by simulation (capsule by capsule, 2-lane splitters that skip a full lane).
line9 = settled(4, 9, 120, 150)
line8 = settled(4, 8, 120, 150)
line3 = settled(3, 9, 10, 40)
check("4 chambers + 9 presses settle near 86.8 blocks/min", f"{line9:.1f}", abs(line9 - 86.8) < 0.5)
check("4 chambers + 8 presses give 80 blocks/min", f"{line8:.1f}", abs(line8 - 80) < 0.1)
check("3 chambers on one dust belt cap the line at 75 (3 x 25)", f"{line3:.1f}", abs(line3 - 75) < 0.5)

# 3. Twelve independent lines (no belt, splitter or machine is shared, so outputs add).
total9, total8 = LANES * line9, LANES * line8
check("12 lines of 4+9 sell ~1,042 blocks/min", f"{total9:.1f}", abs(total9 - 1041.6) < 6)
check("... which is ~62,500 blocks per hour", f"{total9 * 60:,.0f}", abs(total9 * 60 - 62_500) < 400)
check("12 lines of 4+8 sell 960 blocks/min", f"{total8:.1f}", abs(total8 - 960) < 1)
check("4+8 is 8% less than 4+9", f"{1 - total8 / total9:.1%}", abs((1 - total8 / total9) - 0.08) < 0.01)

# 4. Machine, splitter and dust counts.
catchers, chambers, presses = 2 * LANES, 4 * LANES, 9 * LANES
splitters9 = LANES * (3 + 8 + 8)  # chamber tree A1-A3, sand tree S1-S8, dust tree D1-D8
splitters8 = LANES * (3 + 7 + 7)  # 8 presses: each tree needs 7 splitters
check("24 catchers, 48 chambers, 108 presses", f"{catchers}, {chambers}, {presses}", (catchers, chambers, presses) == (24, 48, 108))
check("228 splitters (4+9), 204 (4+8), 24 merges", f"{splitters9}, {splitters8}, {2 * LANES}", (splitters9, splitters8) == (228, 204))
check("dust used: 2,112 kg/min", f"{catchers * BELT:,.0f}", catchers * BELT == 2112)

# 5. Power.
kw9 = LANES * (4 * CHAMBER_KW + 9 * PRESS_KW)
kw8 = LANES * (4 * CHAMBER_KW + 8 * PRESS_KW)
trackers9, trackers8 = math.ceil(kw9 / TRACKER_KW), math.ceil(kw8 / TRACKER_KW)
check("4+9 plant: 2,424 kW, 152 trackers at peak", f"{kw9} kW, {trackers9}", (kw9, trackers9) == (2424, 152))
check("4+8 plant: 2,208 kW, 138 trackers at peak", f"{kw8} kW, {trackers8}", (kw8, trackers8) == (2208, 138))
per9, per8 = total9 / trackers9, total8 / trackers8
check("blocks/min per tracker: 4+9 ~6.85, 4+8 ~6.96", f"{per9:.2f}, {per8:.2f}", abs(per9 - 6.85) < 0.05 and abs(per8 - 6.96) < 0.01)

# 6. What sharing between lines could save: the absolute minimums if belts could be merged across lines freely.
sold_max = LANES * BELT  # every lane full
min_chambers = math.ceil(sold_max / CHAMBER)  # sand needed / sand per chamber
min_presses = math.ceil(sold_max / PRESS)
per_belt_chambers = math.ceil(BELT / CHAMBER)  # without sharing, each dust belt needs this many
saved_kw = (chambers - min_chambers) * CHAMBER_KW + (presses - min_presses) * PRESS_KW
check("without sharing, each dust belt needs 4 chambers (88 / 25 rounded up)", f"{per_belt_chambers}", per_belt_chambers == 4)
check("with free sharing: at least 43 chambers and 106 presses", f"{min_chambers}, {min_presses}", (min_chambers, min_presses) == (43, 106))
check("sharing saves at most 5 chambers + 2 presses = 86 kW (3.5%)", f"{chambers - min_chambers} + {presses - min_presses}, "
      f"{saved_kw} kW = {saved_kw / kw9:.1%}", saved_kw == 86)

# 7. Lunar day: the moon clock runs in real time; Earth turns 28.53 times (as seen from the moon) per lunar day,
# and one such turn (Earth's rotation relative to the moon) lasts 24 h 50.47 min.
lunar_day_h = 28.53 * (24 + 50.47 / 60)
daylight_h = lunar_day_h / 2
per_daylight = total9 * 60 * daylight_h
check("lunar day ~29.5 days, daylight ~354 h", f"{lunar_day_h / 24:.2f} d, {daylight_h:.0f} h", abs(lunar_day_h / 24 - 29.53) < 0.05)
check("one daylight period at full output: ~22 million blocks", f"{per_daylight / 1e6:.1f} M", abs(per_daylight / 1e6 - 22) < 0.5)

width = max(len(c) for c, _, _ in results)
for claim, value, ok in results:
    print(f"{'OK ' if ok else 'OFF'}  {claim:<{width}}  {value}")
print(f"\n{sum(ok for _, _, ok in results)} of {len(results)} claims hold")
