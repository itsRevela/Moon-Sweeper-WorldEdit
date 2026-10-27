"""Capsule-by-capsule simulation of the moon stone chain with 2-lane splitters.

Catcher A (always full) -> splitter tree -> chambers; chambers' sand -> merged sand belt -> splitter tree -> presses;
catcher B (always full) -> splitter tree -> presses' dust. Game figures: belts carry one 1 kg capsule per 60/88 s;
chamber: 20 kg dust store, 5 kg batch per 12 s -> 5 kg sand (assumed 1:1), 10 kg sand store, sends sand every 0.35 s;
press: 12 kg dust + 12 kg sand stores, 1 + 1 kg -> 1 block per 6 s. Splitters alternate lanes; when the next lane is
full they skip to the other (as in the game) or, for comparison, wait.
"""
from collections import deque

BELT_GAP = 60 / 88  # seconds between capsules on a full belt
DT = BELT_GAP / 40  # divides the belt gap, the press's 6 s and the chamber's 12 s exactly
LANE_HOLD = 6  # capsules a belt segment between two splitters holds (about 4.5 m)
RUN, WARMUP = 40 * 60, 10 * 60


class Lane:
    def __init__(self):
        self.count = 0

    def full(self):
        return self.count >= LANE_HOLD


class Splitter:
    def __init__(self, inlet, left, right, skip):
        self.inlet, self.outs, self.skip, self.next, self.ready = inlet, (left, right), skip, 0, 0.0

    def tick(self, t):
        if self.inlet.count == 0 or t < self.ready:
            return
        order = (self.next, 1 - self.next) if self.skip else (self.next,)
        for lane in order:
            if not self.outs[lane].full():
                self.inlet.count -= 1
                self.outs[lane].count += 1
                self.next, self.ready = 1 - lane, t + BELT_GAP
                return


def tree(leaves, skip, nodes):
    """A balanced tree of 2-lane splitters feeding `leaves`; returns its inlet lane."""
    if len(leaves) == 1:
        return leaves[0]
    half = len(leaves) // 2
    inlet = Lane()
    nodes.append(Splitter(inlet, tree(leaves[:half], skip, nodes), tree(leaves[half:], skip, nodes), skip))
    return inlet


class Chamber:
    def __init__(self):
        self.lane, self.dust, self.sand, self.busy_until, self.batch, self.next_out = Lane(), 0, 0, 0.0, 0, 0.0

    def tick(self, t, sand_queue):
        if self.lane.count and self.dust < 20:  # take dust off its belt
            self.lane.count -= 1
            self.dust += 1
        if self.batch and t >= self.busy_until and self.sand + 5 <= 10:  # finish a batch
            self.sand += 5
            self.batch = 0
        if not self.batch and self.dust >= 5:  # start a batch
            self.dust -= 5
            self.batch, self.busy_until = 1, t + 12
        if self.sand and t >= self.next_out and len(sand_queue) < LANE_HOLD:  # send sand out
            self.sand -= 1
            sand_queue.append(1)
            self.next_out = t + 0.35


class Press:
    def __init__(self):
        self.dust_lane, self.sand_lane = Lane(), Lane()
        self.dust, self.sand, self.busy_until, self.blocks = 0, 0, 0.0, 0

    def tick(self, t):
        for lane, attr in ((self.dust_lane, "dust"), (self.sand_lane, "sand")):
            if lane.count and getattr(self, attr) < 12:
                lane.count -= 1
                setattr(self, attr, getattr(self, attr) + 1)
        if t >= self.busy_until and self.dust >= 1 and self.sand >= 1:
            self.dust -= 1
            self.sand -= 1
            self.busy_until = t + 6
            if t >= WARMUP:
                self.blocks += 1


def split(items, groups):
    size, extra = divmod(len(items), groups)
    out, start = [], 0
    for g in range(groups):
        end = start + size + (1 if g < extra else 0)
        out.append(items[start:end])
        start = end
    return out


def simulate(chambers, presses, skip=True, sand_belts=1):
    nodes = []
    cs = [Chamber() for _ in range(chambers)]
    ps = [Press() for _ in range(presses)]
    dust_a = tree([c.lane for c in cs], skip, nodes)
    dust_b = tree([p.dust_lane for p in ps], skip, nodes)
    sand_queues = [deque() for _ in cs]
    # Each sand belt: some chambers' sand merged, then split among some presses.
    groups = [{"belt": tree([p.sand_lane for p in pg], skip, nodes), "queues": qg, "merge": 0.0, "rr": 0}
              for pg, qg in zip(split(ps, sand_belts), split(sand_queues, sand_belts))]
    feed_a = feed_b = 0.0
    t = 0.0
    while t < RUN:
        if t >= feed_a and not dust_a.full():  # catcher A, always full
            dust_a.count += 1
            feed_a = t + BELT_GAP
        if t >= feed_b and not dust_b.full():  # catcher B, always full
            dust_b.count += 1
            feed_b = t + BELT_GAP
        for g in groups:  # chambers' sand merged onto their sand belt, taken in turn
            if t < g["merge"] or g["belt"].full():
                continue
            qs = g["queues"]
            for i in range(len(qs)):
                q = qs[(g["rr"] + i) % len(qs)]
                if q:
                    q.popleft()
                    g["belt"].count += 1
                    g["rr"], g["merge"] = (g["rr"] + i + 1) % len(qs), t + BELT_GAP
                    break
        for node in nodes:
            node.tick(t)
        for c, q in zip(cs, sand_queues):
            c.tick(t, q)
        for p in ps:
            p.tick(t)
        t += DT
    minutes = (RUN - WARMUP) / 60
    per_press = [p.blocks / minutes for p in ps]
    return sum(per_press) / 1, per_press


if __name__ == "__main__":
    print("Steady state over 30 minutes after a 10-minute warm-up (blocks per minute):\n")
    print("  layout                    sand belts   skipping splitters   waiting splitters   per-press (skipping)")
    for chambers, presses, belts in ((4, 9, 1), (4, 9, 2), (4, 8, 1), (4, 8, 2), (3, 9, 2), (4, 10, 2), (5, 9, 2)):
        skip_total, per_press = simulate(chambers, presses, skip=True, sand_belts=belts)
        wait_total, _ = simulate(chambers, presses, skip=False, sand_belts=belts)
        spread = f"{min(per_press):.1f}-{max(per_press):.1f}"
        print(f"  {chambers} chambers + {presses:>2} presses  {belts:>10}   {skip_total:>18.1f}   {wait_total:>17.1f}   {spread:>12}")
    print(f"\n  ceiling: two full belts of dust = {2 * 88 / 2:.0f} blocks/min")
