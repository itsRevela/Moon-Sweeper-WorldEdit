"""Moon stone chain: capsule catchers -> pressure chamber -> stone press, with the game's own figures.

Figures read from the running game (MoonWorld / ProcessorSpec statics, 2026-10-26):
  capsule                1 kg dust (CapsuleCapacityKg), catcher store 100 kg (CatcherStoreKg)
  pressure chamber       5 kg dust per batch (ChamberBatchKg), 12 s cycle, 10 kW;
                         buffers: dust 20 kg, pressing 5 kg, sand out 10 kg
  stone press            1 kg dust + 1 kg sand -> 1 block of 2 kg (Press*PerBlockKg, StoneBlockKg), 6 s cycle, 18 kW;
                         buffers: dust 12 kg, sand 12 kg, stone out 10 blocks
  belts                  1.1 m/s, 0.75 m spacing -> 88 items/min, products emitted every 0.35 s (never the limit here)
  solar                  post 3 kW peak; twin 7.5 kW + 4 kWh; tracker 16 kW + 10 kWh (follows the sun); charge eff. 0.92
  moon clock             real time (MoonSolarSystem.Clock == Unix time): ~29.5-day lunar day, ~14.8 days of night
ASSUMPTION (check in game): the chamber turns dust into sand 1:1 by mass (5 kg dust -> 5 kg sand per cycle).
"""
import itertools
import math

CHAMBER_SAND_PER_MIN = 5 * 60 / 12  # 25 kg/min of sand per chamber
PRESS_BLOCKS_PER_MIN = 60 / 6  # 10 blocks/min per press
DUST_PER_BLOCK = 1.0  # kg straight into the press
SAND_PER_BLOCK = 1.0  # kg, made from 1 kg of dust in a chamber
SAND_PER_DUST = 1.0  # the assumption above
CHAMBER_KW, PRESS_KW = 10, 18


def blocks_per_min(dust, to_chamber, chambers, presses):
    """Blocks a minute for `dust` kg/min caught, `to_chamber` kg/min of it sent to the chambers."""
    sand = min(to_chamber, chambers * CHAMBER_SAND_PER_MIN / SAND_PER_DUST) * SAND_PER_DUST
    direct = dust - to_chamber
    return min(direct / DUST_PER_BLOCK, sand / SAND_PER_BLOCK, presses * PRESS_BLOCKS_PER_MIN)


def best_split(dust, chambers, presses, steps=10_000):
    """Brute force over every split of the dust (the proof): the best split and its output."""
    return max(((blocks_per_min(dust, dust * i / steps, chambers, presses), dust * i / steps) for i in range(steps + 1)))


def closed_form(dust, chambers, presses):
    # Each block costs 1 kg of dust directly plus 1 kg of dust turned into sand: 2 kg per block at best.
    return min(dust / (DUST_PER_BLOCK + SAND_PER_BLOCK / SAND_PER_DUST),
               chambers * CHAMBER_SAND_PER_MIN * SAND_PER_DUST / SAND_PER_BLOCK, presses * PRESS_BLOCKS_PER_MIN)


def machines_for(dust):
    blocks = dust / 2
    return math.ceil(blocks * SAND_PER_BLOCK / SAND_PER_DUST / CHAMBER_SAND_PER_MIN), math.ceil(blocks / PRESS_BLOCKS_PER_MIN)


print("1. Proof the best split is half the dust to the chambers (brute force over 10,001 splits, 2 chambers, 5 presses)")
for dust in (10, 20, 40, 100):
    out, split = best_split(dust, 2, 5)
    assert abs(out - closed_form(dust, 2, 5)) < 1e-9
    print(f"   {dust:>5} kg/min dust: best {out:6.2f} blocks/min with {split:6.2f} kg/min ({split / dust:.0%}) to the chambers")

print("\n2. Any other split wastes dust (20 kg/min caught, 1 chamber, 1 press)")
for share in (0.3, 0.4, 0.5, 0.6, 0.7):
    out = blocks_per_min(20, 20 * share, 1, 1)
    print(f"   {share:.0%} to chamber: {out:5.2f} blocks/min, {20 - 2 * out:5.2f} kg/min of dust piling up unused")

print("\n3. Machines at full use: one chamber's sand feeds this many presses:",
      CHAMBER_SAND_PER_MIN * SAND_PER_DUST / SAND_PER_BLOCK / PRESS_BLOCKS_PER_MIN, "-> 5 presses : 2 chambers")
for chambers, presses in itertools.product((1, 2), (1, 2, 3, 5)):
    if (chambers, presses) in ((1, 1), (1, 2), (2, 5)):
        cap = closed_form(10_000, chambers, presses)
        print(f"   {chambers} chamber(s) + {presses} press(es): up to {cap:5.1f} blocks/min, takes {2 * cap:5.1f} kg/min of dust, "
              f"{CHAMBER_KW * chambers + PRESS_KW * presses} kW while running")

print("\n4. What your two catchers' combined intake needs (measure it: watch a catcher's kg over a minute)")
print("   dust kg/min   blocks/min   blocks/hour   chambers  presses   kW   solar trackers (16 kW peak)")
for dust in (5, 10, 13.3, 20, 30, 50, 100):
    chambers, presses = machines_for(dust)
    kw = CHAMBER_KW * chambers + PRESS_KW * presses
    print(f"   {dust:>10}   {dust / 2:>10.2f}   {dust / 2 * 60:>11.0f}   {chambers:>8}  {presses:>7}  {kw:>4}   {math.ceil(kw / 16):>3}")

night_hours = 29.53 / 2 * 24
print(f"\n5. Night: ~{night_hours:.0f} h of darkness. Keeping 1 chamber + 1 press running needs "
      f"{28 * night_hours / 0.92:,.0f} kWh = {28 * night_hours / 0.92 / 10:,.0f} trackers' batteries: not practical.")
print("   At night the catchers fill (100 kg each) and stop, so ~200 kg plus the machines' buffers is what a night saves.")
