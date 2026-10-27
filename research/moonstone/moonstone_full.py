"""Moon stone with both catchers always full: each catcher's outfeed fills its belt.

Game figures (read from the running game): belt 1.1 m/s at 0.75 m spacing; capsule 1 kg; chamber 5 kg dust per 12 s
(25 kg/min of sand, assuming 1:1 dust to sand), 10 kW; press 1 kg dust + 1 kg sand -> 1 block per 6 s (10/min), 18 kW;
solar tracker 16 kW peak. ASSUMPTION: a catcher's outfeed sends capsules as fast as the game's other outputs
(every 0.35 s = 171/min), so its belt, not the catcher, is the limit.
"""
BELT_PER_MIN = 1.1 / 0.75 * 60  # 88 items a minute
SUPPLY = 2 * min(BELT_PER_MIN, 60 / 0.35)  # kg of dust a minute from the two catchers
CHAMBER, PRESS = 25.0, 10.0  # kg sand / min, blocks / min
CHAMBER_KW, PRESS_KW, TRACKER_KW = 10, 18, 16


def blocks(chambers, presses, dust=SUPPLY):
    # 2 kg of dust per block (1 straight in, 1 through a chamber), capped by the chambers' sand and the presses.
    return min(dust / 2, chambers * CHAMBER, presses * PRESS)


print(f"Supply: 2 catchers x {min(BELT_PER_MIN, 60 / 0.35):.1f} capsules/min (belt-limited) = {SUPPLY:.1f} kg/min of dust")
print(f"Ceiling: {SUPPLY / 2:.1f} blocks/min = {SUPPLY / 2 * 60:,.0f} blocks/hour, whatever you build\n")

# Brute force every layout up to 8 chambers and 15 presses: the fewest machines reaching each output level.
best = {}
for chambers in range(1, 9):
    for presses in range(1, 16):
        out = round(blocks(chambers, presses), 2)
        kw = chambers * CHAMBER_KW + presses * PRESS_KW
        if out not in best or kw < best[out][2]:
            best[out] = (chambers, presses, kw)
top = sorted(best.items(), reverse=True)[:5]
print("Most blocks first (fewest kW for each):")
print("  blocks/min  chambers  presses   kW  trackers  dust used  chamber use  press use")
for out, (chambers, presses, kw) in top:
    print(f"  {out:>10.1f}  {chambers:>8}  {presses:>7}  {kw:>4}  {-(-kw // TRACKER_KW):>8}  {2 * out:>6.1f} kg  "
          f"{out / (chambers * CHAMBER):>10.0%}  {out / (presses * PRESS):>8.0%}")

c, p, kw = best[max(best)][0], best[max(best)][1], best[max(best)][2]
print(f"\nBest: {c} chambers + {p} presses -> {max(best):.1f} blocks/min; one fewer of either drops output:")
print(f"  {c - 1} chambers: {blocks(c - 1, p):.1f}/min   {p - 1} presses: {blocks(c, p - 1):.1f}/min")
print(f"Dust split: {SUPPLY / 2:.1f} kg/min to the chambers, {SUPPLY / 2:.1f} kg/min straight to the presses (exactly half)")
