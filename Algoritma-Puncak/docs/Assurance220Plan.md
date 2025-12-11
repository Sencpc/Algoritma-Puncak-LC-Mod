# Assurance 220 Encounter Blueprint

## Map + Facility Baseline

- Moon: 220 (Assurance)
- Facility: Small Factory (doors, fire exit, pressurized door, spike traps, landmines, steam valves)
- Spawn caps: 5-7 inside the facility, 3-5 outside the facility.
- Priority behaviors: territorial (IDs 1, 5, 7), pack communication (IDs 5, 7), limited omniscience (IDs 4, 6 in enraged state), selective hearing (IDs 5, 8 only react to ground-movement noise).

## Spawn-Weight Table

### Inside Pool (IDs 1-6)

| ID  | Enemy                              | Min | Max | Weight | Notes                                                                 |
| --- | ---------------------------------- | --- | --- | ------ | --------------------------------------------------------------------- |
| 1   | Bunker Spider                      | 1   | 1   | 0.15   | Always reserve a vent junction; locks off alternate path.             |
| 2   | Thumper                            | 1   | 1   | 0.20   | Anchors the long sewer hall; enters omniscient state after detection. |
| 3   | Hoarding Bug                       | 1   | 2   | 0.15   | Migrates nests deeper, forcing players past Spider/Thumper territory. |
| 4   | Flowerman                          | 1   | 1   | 0.20   | Supplies the stalk/checkmate pressure.                                |
| 5   | Baboon Hawk                        | 1   | 2   | 0.20   | Territorial pack inside cafeteria; shares alerts with outside flock.  |
| 6   | Flex slot (Sand Spider / Coilhead) | 0   | 1   | 0.10   | Optional disruptor; also gains temporary omniscience when enraged.    |

Ensure total concurrent count stays between 5 and 7 by clamping spawns once weight sum produces 7.

### Outside Pool (IDs 7-9)

| ID  | Enemy                                | Min | Max | Weight | Notes                                                                  |
| --- | ------------------------------------ | --- | --- | ------ | ---------------------------------------------------------------------- |
| 7   | Eyeless Dog                          | 2   | 3   | 0.50   | Owns fire exit + ship deck; reacts only to high-priority ground noise. |
| 8   | Baboon Hawk                          | 1   | 2   | 0.35   | Edge patrols; relays sightings to inside pack.                         |
| 9   | Roamer (Sand Spider / Hoarder scout) | 0   | 1   | 0.15   | Light pressure near steam valves/landmines.                            |

Keep maximum simultaneous outdoor bodies to 5 by disabling further spawns once Eyeless dog pack reaches 3 and hawks reach 2.

## NavMesh Heatmap & Territory Plan

- **Zone A – Vent Wing (Spider):** mark heat cells at each web trap; discourage other enemies by raising traversal cost inside 8 m radius. Bunker Spider’s heat stays red, so spawn logic never places Thumper there.
- **Zone B – Sewer Hall (Thumper):** paint a high-speed lane along the long corridor; on high-noise events, flood the lane with "charge" heat so Thumper snaps to straight-line paths.
- **Zone C – Nest Interior (Hoarders):** keep medium heat near loot clusters; when the nest migrates deeper, drop cold spots behind it so players sense abandonment.
- **Zone D – Cafeteria / Common Room (Baboon Hawks):** two concentric rings: hot inner ring for leaders, warm outer ring for flock drift. When hawks broadcast alerts, temporarily boost outer ring heat so the pack tightens.
- **Zone E – Fire Exit / Ship Ramp (Eyeless Dogs):** perma-hot strip from fire exit to ship doorway. Any sprint-generated high-priority noise injects a spike that propagates along this strip, forcing the closest dog to reset its NavMesh path instantly.
- **Zone F – Peripheral Yard (Baboon Hawks outside + roamer):** moderate heat, but drop "cold" void near ship dock to signal territorial no-go unless hawks are chasing.

For visualization, export NavMesh cells with color mapping (cold = 0, hot = 1) and refresh every 2 seconds so interrupts appear as bright pulses.

## Encounter Flow (“Checkmate”)

1. **Ingress:** Players pick either spider-blocked wing or sewer hall. Any detour loops back through Bunker Spider territory unless they cut wiring.
2. **Pressure Build:** Flowerman stalks a specific player. The targeted player must keep eyes on him, locking them in place.
3. **Sewer Threat:** Another player tries to flank; Thumper hears moving footsteps, enters state 2, and rushes down the hall.
4. **Panic Response:** Team starts sprinting to help. Sprint noise hits Eyeless Dog high-priority channel near fire exit; dogs reset nav paths and charge inside.
5. **Outside Collapse:** Baboon Hawks near the perimeter receive both Flowerman broadcast and Eyeless dog howl. They swoop toward the ship ramp, cutting escape.
6. **Resolution:** Players must choose between keeping eye contact (and getting slammed by Thumper) or breaking contact to flee (triggering Eyeless dogs/baboon pack). Either choice risks cascading aggro, producing the desired "checkmate" feel.

## Implementation Notes

- Keep IDs 5 and 8 strictly ground-noise reactive by filtering voice/sprint cues in `AISensorSuite` (already wired for MouthDogs; reuse same envelope but new thresholds).
- Let Flowerman and Thumper set `AlwaysKnowPosition` flags only after direct sighting; decay back to false after 5 seconds without reconfirmation.
- Sync Baboon hawk flocks via existing pack broadcast; extend it to include outside units so a single alert radiates across the yard.
- Eyeless dogs should immediately `ResetPath()` whenever a high-priority noise is registered to guarantee fast direction changes.
- `AssuranceSpawnDirector` clamps Assurance to 5-7 indoor / 3-5 outdoor enemies and rewrites the spawn table every time the moon loads.
