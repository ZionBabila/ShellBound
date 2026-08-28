# Trace Forge

A self-contained drafting bench that generates **research-grounded line sheets in
true perspective** for tracing practice — the goal being the industrial-designer
skill of blocking in an idea quickly and confidently.

Open `index.html` in any browser. No build step, no dependencies, works offline.

## The idea

You answer four questions; the bench forges a fresh construction sheet. You export
it, drop it into Procreate on its own low-opacity layer, and **trace the
construction, not just the outline** — crate first, then the ellipse minor-axis,
then the contour. Repetition of the *construction moves* is what transfers; tracing
a finished silhouette does not.

## Why the lines are correct (not eyeballed)

The generator is a real **pinhole-camera projection**, so the geometry is right by
construction rather than approximated:

- **Boxes / crates** — every form starts inside an axis-aligned construction box,
  mirroring how ID sketching is taught.
- **Ellipses** are projections of actual 3-D circles, so their openness and
  minor-axis orientation are always geometrically true for the view.
- **2-point perspective** rotates the object about the vertical axis only, so
  verticals stay parallel (true 2-point). The object is dropped *below the horizon*
  by a pure vertical lift — this opens top faces into readable ~30° ellipses
  **without** introducing a third vanishing point.
- **3-point** adds a pitch (worm's- or bird's-eye), converging the verticals.
- **Fillets** are drawn the way designers draw them: seam edges pulled in by the
  radius plus quarter-circle arcs at each corner.

Everything is **seeded** — the same seed reproduces the same sheet, and re-rolling
gives a fresh variation on the same drill.

## The brief

| Question | Options |
|---|---|
| What are we drilling? | Fundamentals · Fillets · Product archetypes (multi-select) |
| Perspective | 2-point · 3-point · Mix |
| Difficulty | Warm-up · Builder · Challenge (angle severity + detail) |
| Tiles | 1 · 2 · 4 · 6 per sheet |
| Seed | reproducible; ↻ for a new one |

**Fundamentals** — crate + inscribed top-ellipse (with minor-axis guides) + cylinder.
**Fillets** — construction crate + soft-radius block with true corner arcs.
**Archetypes** — bottle (revolved surface), mug (cylinder + handle), audio pod
(filleted block + grille + parting line), each built on the same primitives.

## Export

- **Download PNG** rasterises the sheet at ~2× (~2080px wide) — crisp for Procreate.
- **SVG** exports the vector sheet with theme colours inlined.

Toggle *construction lines* and *labels* off if you want a cleaner reference.
