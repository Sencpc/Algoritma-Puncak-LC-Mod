# Algoritma Puncak – Lethal Company AI Overhaul

## Proposal Overview

This repository accompanies the **AI for Games** proposal that reimagines the cooperative horror-economy title **Lethal Company** by Zeekerss. In the base game, crews descend onto abandoned industrial exomoons to salvage scrap, evade anomalies, and satisfy the ever-rising quotas of “The Company.” Our project investigates how smarter AI behaviours, dynamic encounters, and informed design trade-offs can heighten both the survival tension and the economic decision space.

## Authors

- Kelun Kaka Santoso — 222117039
- Yoga Pramana Syahputra Teguh — 222117068
- Go, Gregory Aaron Gosal — 222117051
- Sean Cornellius Putra Chrisyanto — 222117067

**Program Studi Informatika, Fakultas Sains dan Teknologi, Institut Sains dan Teknologi Terpadu Surabaya (ISTTS), 2025.**

## Game Description

Lethal Company is a first-person, four-player cooperative survival horror experience. Players act as contract workers for a faceless corporation, travelling to derelict moons to gather valuable scrap while traps, environmental hazards, and hostile fauna attempt to end the mission. Meeting the quota extends the cycle with tougher profit targets; failure ejects the crew into space and resets progress.

## Gameplay Overview

- Up to four players coordinate via proximity voice and text chat.
- Procedurally assembled facilities mix loot rooms, traps, and creature spawns; weather states such as fog, sandstorms, floods, or eclipses alter visibility and threat levels.
- Players balance inventory limits (four slots, with some two-handed items) against mobility and utility.
- Scrap is sold on the Company moon 71-Gordion; funds unlock new equipment, ship cosmetics, and access to deadlier but more lucrative moons.
- If no one boards the ship before midnight, autopilot departs and all scrap is lost.

### Core Gameplay Loop

1. Deploy to a selected moon based on desired risk/reward.
2. Sweep the facility, collecting scrap while monitoring stamina, sound, light, and inventory.
3. Relay intel between field teams and the shipboard navigator (radar/terminal control).
4. Avoid, kite, or outsmart anomalies such as Thumpers, Brackens, and the infamous Jester.
5. Extract before the nightly deadline or escalating hazards overwhelm the crew.
6. Sell haul, satisfy The Company quota, and receive the next three-day target.
7. Reinvest credits into gear, plan the next moon, and repeat.

## Feature Highlights

1. **Quota Pressure** – Rising corporate demands force deeper, riskier dives.
2. **Diverse Moons** – Each moon mixes unique biomes, loot density, and monster tables.
3. **Semi-Procedural Facilities** – Layouts, loot nodes, and spawns shuffle every run.
4. **Scrap Economy** – Slot management and item weight drive grab-and-go triage decisions.
5. **Signal-Driven AI** – Enemies react to light, sound, proximity, and line-of-sight cues.
6. **Environmental Hazards** – Darkness, traps, locked doors, vents, and weather amplify dread.
7. **Time Pressure** – Midnight deadlines escalate danger and force hard retreats.
8. **Ship Hub** – Safe room for planning, radar support, and purchase terminals.
9. **Equipment Investment** – Credits fund lights, scanners, tools, and novelty ship upgrades.
10. **Skill-Based Progression** – Success hinges on coordination, knowledge, and loadout choices rather than character XP.
11. **Proximity Comms** – Diegetic voice occlusion fuels emergent storytelling.
12. **Meaningful Loss** – Death risks losing expensive gear, raising tension on every run.
13. **Field Intel Mini-Games** – Clues such as footprints or flickering lights hint at lurking threats.
14. **Stealth vs. Chaos** – Players choose between quiet infiltration or time-saving hustle.
15. **Self-Tuned Difficulty** – Selecting moons effectively sets the challenge curve.
16. **Dynamic Audio Atmosphere** – Sound cues double as warning systems without heavy HUD reliance.
17. **Dark Corporate Humor** – Satirical messaging reinforces the narrative tone.
18. **Drop-In Accessibility** – Sessions remain friendly to quick, repeatable co-op runs.
19. **High Replayability** – Randomized layouts plus quota pressure keep loops fresh.
20. **Expansion Hooks** – Future work can add elite enemy events, drone tools, or a living intel codex.

## Risks & Mitigations

| Risk                                          | Mitigation                                                            |
| --------------------------------------------- | --------------------------------------------------------------------- |
| Repetition after long play sessions           | Introduce dynamic events, elite monsters, and rotating modifiers.     |
| Solo players feel overwhelmed                 | Offer adaptive difficulty tuning or solo-friendly compensation perks. |
| Steep learning curve for enemy identification | Gradually unlock an intel log or codex without spoiling encounters.   |
| Gear loss snowballs into failure streaks      | Provide baseline "corporate relief" packages after consecutive wipes. |

## Future Enhancements

- Advanced sensor suites (bio-scanners, deployable drones, remote turrets).
- Rare lunar events (eclipse nights, swarm incursions, rogue AI outbreaks).
- Corporate reputation systems that affect pricing, penalties, and mission unlocks.
- Shared bestiary/log that evolves as teams document anomalies.

## Repository Usage

This repo houses the ongoing AI-overhaul experimentation for **Lethal Company** and supporting proposal documentation. Use it to:

- Iterate on BepInEx/Harmony mods that extend NavMesh-driven enemy intelligence.
- Track design decisions, feature prioritization, and academic deliverables.
- Communicate goals with collaborators, advisors, and future contributors.

Feel free to extend the document with implementation details, screenshots, or benchmarks as the project matures.
