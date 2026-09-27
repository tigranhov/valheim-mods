# Immersive Mapper — Plan

A set of Valheim mods for Immersive worlds (no map, no portals). Goal: make exploring,
finding traders and moving bases rich and possible without GPS-style shortcuts.

Build order: **1. Trader Beacons → 2. Cargo Lashing (+ Packing Crates) → 3. Cartographer's Kit**

---

## Design principles (apply to every mod)

1. **Information is earned, not given.** No coordinates, no live GPS, no auto-pins. Tools give
   *measurements relative to you* (bearing, distance, depth); the player turns them into knowledge.
2. **Diegetic first.** Things exist in the world (smoke, fireworks, items, cairns). HUD only
   while a tool is held/used, never permanent.
3. **Lore-friendly.** Reuse vanilla assets/VFX where possible, recipes fit biome progression.
4. **Server-authoritative & multiplayer-safe.** Server decides, clients display. Configs are
   server-synced (admin-only). Works on a dedicated server.
5. **Standalone mods.** Each mod installs on its own; shared code only if it clearly pays off.
6. **Configurable immersion.** Every "help" has a config knob so a group can tune how easy it is.

---

## Environment & decisions

| Item | Value |
|---|---|
| Game | Valheim 1.0.15 (network version 40), `D:\SteamLibrary\steamapps\common\Valheim` |
| Loader | BepInExPack Valheim 5.4.2350 |
| Library | Jötunn 2.30.1 |
| .NET SDKs | 6.0, 8.0, 9.0 installed; plugins target .NET Framework (net462, verify vs Jötunn stub) |
| Mod manager | Thunderstore Mod Manager |
| Dev/test profile | **New clean `Dev` profile** (BepInExPack + Jötunn + our mods only) |
| Multiplayer | Single player or a player-hosted world; **dedicated server not a focus** (may not run one) |
| Real play profile | Not decided — ask user before targeting any existing profile (Immers, Default, …) |

### Decision log
- 2026-09-26 — Order: Trader Beacons → Cargo Lashing → Cartographer's Kit.
- 2026-09-26 — Test in a new clean `Dev` profile; don't assume any existing profile.
- 2026-09-26 — Target a dedicated server the user controls (server-side logic is fine).
- 2026-09-26 — Trader Beacons: the mod never locks in camps itself. A camp signals only after vanilla
  has generated it (a player came within ~300 m), then keeps signalling to each player until they
  reach it (40 m / talk). SignalRadius (1200 m) is the max viewing distance; within it, far signals
  are drawn as impostors so fog doesn't hide them. Not unlimited range: that would work as a GPS.
- 2026-09-27 — Smoke only, day and night. Fireworks felt too non-immersive: kept as an optional
  config (off by default). Night visibility of smoke still to be judged in game.
- 2026-09-27 — SignalRadius default 500 m (1200 felt too far). Impostor worked but the user prefers
  real smoke at the camp → new default DrawMode=Real: per-renderer override of `unity_FogParams`
  so only the smoke gets thinner fog (as if 100 m away). Impostor kept as fallback.
- 2026-09-27 — Real mode still faded at ~150 m. Cause was not fog: the vanilla smoke material
  (Lux Lit Particles) has `_CamFadeDistance = (2, 150, 25, 0)`, a built-in fade-out ~150 m from the
  camera. Our material copy now sets its far distance to 100000. Fog is exponential, density
  0.006; camera far clip 20000 m.
- 2026-09-27 — ✅ Real mode works: visible far away, and the per-renderer `unity_FogParams`
  override works too. `FogAsIfMeters` = 100 (user's pick): closer than that, normal fog; farther,
  the haze stays as if 100 m away. The impostor mode is no longer needed (candidate for removal).
- 2026-09-27 — Impostor removed (DistantView, DrawMode, ImpostorDistance). FogAsIfMeters default
  500 at first, then 300 (user's pick). First git commit.
- 2026-09-27 — Dedicated server is not a focus (user may not run one). Mods stay multiplayer-safe
  (server logic also runs on a player-hosted game), but no server deployment work.
- 2026-09-27 — Milestone 2 becomes "Ship Cargo": cargo crates (user's idea, based on the crates a
  broken ship leaves behind) + cart lashing, each switchable. See Milestone 2 for details.
- 2026-09-27 — Milestone 3 design agreed: no compass (the sky is the compass); one Map case item
  (workbench) holds drafts, a master copy, the pace tally and the journal; sea instruments are ship
  fittings; field notes in the case, fair copy at the vanilla cartography table (taken over, one
  master per table, the case's copy refreshes only at the table); upgrades are parts fitted into the
  case. Answers the open question on instruments: held, like the hammer.
- 2026-09-27 — Cartographer step 2 (table) decided: either action spot opens the table, one player at
  a time, the master grows as you draw, drafts are laid over it and aligned (two-point fit), copied as
  is or traced (both built to compare), then wiped; copying is free; colours only at the table.

### Repo layout (planned)
```
immersive-mapper/
  PLAN.md
  Directory.Build.props      # game path, profile path, shared build settings
  ImmersiveMapper.sln
  src/TraderBeacons/
  src/ShipCargo/             # cargo crates + cart lashing
  src/Cartographer/
  companion/                 # optional web app (milestone 3e)
```
Decompiled game code is reference only — kept in a scratch folder, **never committed**.

### Testing approach
1. Single-player test world (Immersive preset) in the `Dev` profile — the host runs server logic.
2. Admin-only debug commands in each mod (off by default) to teleport/print positions for testing.
3. Then the user's dedicated server (server + all clients need the mod; Jötunn version check).
4. Before release: compatibility pass against the user's real mod list.

---

## Milestone 1 — Trader Beacons

**Problem:** without a map, finding Haldor (Black Forest), Hildir (Meadows, ~3–5 km from center)
and the Bog Witch (Swamp, ~3–8 km from center) is pure luck. Vanilla only reveals the icon within
~320–450 m, and that icon doesn't exist in no-map mode.

**Idea:** traders notice travellers from afar and signal them — in-world, visible, not a pin.

**Status (2026-09-27):** v0.1 works in single player (Dev profile): smoke visible far away, drawn at
the real spot. Remaining: real-camp test, night look.

**In-game test checklist (Dev profile, a throwaway test world):**
1. ✅ `tb_test` smoke visible from far away, drawn at the real spot.
2. `devcommands`, `tod 0.9`, `tb_test 400 smoke`: is the smoke readable at night at all?
3. `tb_camps` (spoiler) → go within ~300 m of Haldor (camp gets generated) → smoke appears →
   walk away: still visible up to 500 m → walk in: stops within 40 m or on talking.
4. `tb_resetfound` to repeat. Check `BepInEx/LogOutput.log` for errors.

**Console commands:** `tb_test [meters] [smoke|fireworks]` (reveals nothing),
`tb_camps` and `tb_resetfound` (need devcommands + server admin).

### v0.1 — Smoke & fireworks (MVP)
- Server checks player positions periodically (e.g. every 5 s).
- A camp starts signalling once the game has **generated** it (someone came within ~300 m, the
  same moment vanilla would show the map icon). From then on it signals to every player within
  the **signal radius** (max viewing distance, default 500 m) who has **not yet found** it:
  - **Day and night:** tall smoke column above the camp, visible over the treeline.
  - **Optional, off by default:** firework volleys at night instead (judged too non-immersive),
    with flash-to-bang sound delay (distance ÷ 343 m/s).
- "Found" = player came within discovery range (≈ vanilla reveal radius, config) or talked to the
  trader. Stored server-side per player per world.
- Everyone in the signal radius sees the signal (it's a real thing in the sky), not just one player.
- Trader list is config-driven (location prefab names), so modded traders can be added.

**Config (server-synced):** enable per trader, signal radius, discovery radius, day/night modes,
volley duration & interval, smoke on/off, sound delay on/off, who sees it (in-range / all).

**Research (decompiled `assembly_valheim.dll` 1.0.15, 2026-09-26):**
- [x] Camp location names: `Vendor_BlackForest` (Haldor), `Hildir_camp`, `BogWitch_Camp`
      (found in the game's asset bundles).
- [x] Candidate camps: `ZoneSystem.m_locationInstances` holds every candidate spot
      (`LocationInstance { m_location, m_position, m_placed }`). When a zone with a candidate is
      generated, `PlaceLocations` places it and, if `ZoneLocation.m_unique`, calls
      `RemoveUnplacedLocations` → all other candidates are deleted. So "first approached wins"
      is real, and happens at zone-generation range (~300–450 m). On a dedicated server the
      server generates ("ghost") zones around every player, so `m_placed` is known server-side.
      **Our approach:** only placed camps signal; we never touch candidates or the world save.
- [x] Server-only data: clients don't have `m_locationInstances` in multiplayer (they only
      get icon RPCs). → Server decides, sends signal RPCs to clients.
- [x] Discovery: vanilla has no per-player "found trader" flag we can use (the map icon
      is sent to all when the camp is placed). → Our own "found" flag, stored server-side
      per player per world (`BepInEx/config/TraderBeacons/`), set when within found radius or
      when the player talks to the trader (`Trader.Interact`).
- [x] Fireworks: vanilla fireworks are thrown into a fire; `Fireplace.m_fireworkItemList[i]
      .m_fireworksEffects` (EffectList) holds the visuals + sounds → reuse from the
      `fire_pit` prefab. Instantiate locally with `ZNetView.m_forceDisableInit` so nothing is
      networked.
- [x] Day/night on client: `EnvMan.IsNight()` = day fraction ≤0.25 or ≥0.75,
      `EnvMan.IsDaylight()` (false in always-dark envs).
- [x] Ground height anywhere (zone not loaded): `WorldGenerator.instance.GetHeight(x, z)`.
- [x] Player identity on server: `peer.m_refPos`, player ZDO `ZDOVars.s_playerID`.
- [x] Far VFX: smoke faded out by ~150 m. Cause: the vanilla smoke material's built-in camera fade
      (`_CamFadeDistance`), not fog. Fix: our material copy disables the far fade, and a per-renderer
      `unity_FogParams` override caps the smoke's fog at `FogAsIfMeters` (300). Drawn at the real
      camp, so terrain and trees hide it naturally. (An impostor approach worked too, but was removed.)
- [x] Smoke material: `smoke` / `Lux Lit Particles/ Bumped`, borrowed from a vanilla fire.
- Note for existing worlds: players who found a trader before the mod was installed will see its
  smoke until they visit the camp again (the mod can't know about earlier visits).

### v0.2 — Signal Horn (call & response)
- Craftable horn item. Blow it → any unfound trader within horn range (e.g. 2.5 km) answers
  after a delay with a horn sound from its direction (+ a single firework at night).
- Repeat from another spot to triangulate. Cooldown (config).

### v0.3 — Rumors
- Once you've met one trader, they sell a "rumor" (coins) about another trader: coarse direction
  (8 compass points) and fuzzy distance (±25%), plus a landmark hint (e.g. "by a great lake").

### v0.4 — Waymark cairns
- Stone cairns with carved arrows placed along rings (e.g. 400/800/1200 m) around each camp,
  pointing toward it. Spawned when those zones first generate/load.

### v0.5 — Circling ravens
- A flock of birds circling above the camp, visible from far away in daytime.

---

## Milestone 2 — Ship Cargo (cargo crates + cart lashing)

**Problem:** relocating bases a lot; carts on boats bump around and fall off; slot limits.

**Already in the user's Immers profile (don't duplicate, stay compatible):**
`Azumatt-HaulersHelper` (cart physics tuning), `OdinPlus-CraftyCartsRemake` (station carts with
storage), `MathiasDecrock-PlanBuild` (blueprints — useful for rebuilding a base).

**Decisions (2026-09-27):** build both features in one mod (`src/ShipCargo`), **each switchable in
the config**. Crates first, then lashing. Lashing is free (no material cost), cargo doesn't slow
the ship (vanilla ships ignore their hold's weight too), and there's no per-ship limit: whatever
physically fits. A packed crate can't go into carts, ship holds or chests.

**Key research (decompiled 1.0.15):**
- Carts (`Vagon`) are physics bodies pulled by a `ConfigurableJoint`; ships (`Ship`) are physics
  bodies too. Each is simulated by its owner, often a different player → bumping, falling off.
- `ZSyncTransform.m_characterParentSync`: the mechanism that keeps players steady on moving ships.
  It syncs an object's position **relative to its parent** when the parent has a `ZNetView`,
  and every client rebuilds it the same way. → Shared "ship passenger" code for crates and carts:
  parent to the ship, freeze physics, relative sync; release on untie/pick-up/ship destroyed.
- Ships' holds drop floating crates when a ship breaks (`Container.m_destroyedLootPrefab`) — the
  look to reuse for our crate.

### v0.1 — Cargo crates
**Status (2026-09-27):** in game: pick up → set down on a deck → rides the ship ✅. Reload with 4
crates on one ship: all 4 back in place and riding ✅ (after switching from saved ZDO links, which
keep one link per target, to finding the ship by deck offset). Crates are unbreakable.
Placement preview ✅ (ghost, snapping beside/on top, red where it clips), shimmer at sea fixed ✅
(world-space noise/triplanar off in the crate material), karve walls count ✅ (a bit strict: the hull
planks are convex shapes → now 0.15 m overlap allowed with the hull only).
✅ Tested 2026-09-27: stack limits per ship type (raft 1, karve 1, longship 2, drakkar 3, other
ships 2, ground unlimited); cargo weight trims the ship (at full capacity the crates press down with
10% of the ship's weight: even load sits lower, one-sided load tips); standing on a crate that rides
a ship carries you (the crate's body counts as the ship in `Character.UpdateGroundContact`).
Weight can't sink a ship (buoyancy tops out around 7.5× the ship's weight); an optional "overload
damages the ship" rule was offered and declined.
Leftover cosmetic: the crate *item* borrows the wood item's model (only visible if it's ever dropped). A crate item
spawned with `spawn` couldn't be set down (item recognized by shared-data reference; spawned items
carry their own copy) → fixed to match by prefab name, needs re-test. Vanilla crate: Default layer
(players collide, stacking works), has Destructible (can be smashed open).
Design notes from the code research:
- The crate's contents travel in the item's custom data as the exact bytes the placed crate saved
  (`ZDOVars.s_items`); weight/count summaries alongside.
- Riding a ship is **not** Unity parenting (a networked child dies with its parent when the area
  unloads, leaving a dead instance in ZNetScene). Every client places the crate at a fixed offset
  from its own copy of the ship each frame; crate↔ship collisions are ignored; the owner moves the
  ZDO along so the crate loads with the ship. Link = `SyncTransform` connection (survives reloads).
- Known limit: standing *on top of* a crate while sailing doesn't carry you (the deck does).
- Ship destroyed → crate contents + the empty crate go into vanilla floating `CargoCrate`s.

**Test checklist (Dev profile, test world):**
1. `devcommands`, `spawn IM_CargoCrateItem` (or craft at a workbench: 10 wood, 4 bronze nails).
2. Right-click the crate in the inventory → it's set down in front of you. E opens, fill it.
3. Shift+E picks it up → one heavy item; tooltip shows contents. Right-click again to set it down.
4. Try putting the packed crate in a chest / cart → "A packed crate can't go in there".
5. `spawn Karve` in water, board it, set a crate down on the deck, sail: it must not move or slide.
6. Log out and back in: the crate is still on the deck at the same spot.
7. Destroy the ship: cargo + the empty crate float away in vanilla floating crates.
8. Log: "CargoCrate components" and "collider layers" lines (layer decides player collision/stacking).

- Craftable crate (looks like the vanilla shipwreck crate), with its own slots (config).
- Place it on the ground or on a ship deck. **On a deck it rides the ship**: never slides, never
  falls off, synced for every player.
- **Pick it up with its contents** → one heavy inventory item (weight = crate + contents). Place it
  again at the destination.
- Packed crates only live in a player inventory (not in carts, holds, chests or other crates).
- If the ship is destroyed, crates on it float free (like the vanilla crates).
- Research: vanilla crate prefab name; placement surface rules on ships; storing an inventory in
  item data; per-item weight; blocking a packed crate from containers; death/tombstone.

### Carrying crates (✅ tested and merged into main 2026-09-27; was branch `experiment/carry-crates`)
Config `4 - Carrying / PickUpMode`: **Front** (default: carry it in your arms) | Inventory (pack into
an item). Crate defaults changed at the same time: **4 slots** (4×1, so crates don't replace chests;
lowering the size never hides items, they move to extra rows), **empty weight 20**, **ship trim off**.
Crafted at the workbench: 10 wood + 4 bronze nails (config). In Front mode a crafted crate appears at
the station instead of in the inventory: side by side on its top along the long edge, else on the
ground on the crafter's side; at most `CratesAtStation` (3) wait there, otherwise crafting is refused
before anything is used up. In Inventory mode crafting is vanilla (untested yet).
- Shift+E lifts the crate itself; it stays a world object and every client holds it at the same pose
  relative to its copy of the carrier (like riding a ship). Colliders off while carried.
- While carrying: the placement ghost is always on; click = set down, right-click = put down beside
  you, Shift = no snapping. Attack/block blocked. Front hides the weapon and puts both hands on the
  crate's sides with hand IK (`CharacterAnimEvent.OnAnimatorIK`).
- `Encumbrance`: Weight (default: inventory + crate + contents vs. your carry limit) | Always | Never.
  Encumbered → the game's heavy walk + its rules; otherwise the normal walk. Never any sprinting.
- The carried crate is drawn smaller (60%). `5 - Carry tuning` has live sliders for scale, height,
  distance, tilt, hand grip (position + wrist angle), elbows, and `BodyFollow`. Back carrying removed.
- Motion: the crate and hands follow the torso's walk motion (`Animator.bodyPosition` in the IK pass),
  keeping shoulder-to-hand distance steady so elbows don't pump with each step. (Tried first: a
  speed/stride bob, too fast at Valheim's jog; then a footstep-event bob; both dropped.)
- First try: all crates made you encumbered (flat rule) and the crate looked comically large.
- Put down automatically when sitting/steering, dying, taking out the hammer; swimming drops it in
  the water (floats away in a vanilla crate). Carrier gone / world reloaded → owner sets it down.
- Also: a crate with another crate on top can't be picked up or lifted (it would be left hanging).

### v0.2 — Cart lashing
- On a cart standing on a ship deck, Shift+E → **"Lash to ship"** / **"Untie"**. A lashed cart
  rides the ship like a crate (body + wheels frozen, relative sync).
- Works with anything using the vanilla cart script (`Vagon`), incl. CraftyCarts' carts.
- Survives relog/zone reload. Ship destroyed → the cart is released with its contents.

### Existing mods to recommend alongside (not rebuild)
LongshipUpgrades (bigger longship storage), BoatAdditions (knarr cargo ship), balrond shipyard
(cargo barrels/crates), BeastsOfBurden (lox/boar pull carts), LoxSaddleBags / Better Lox,
Adventure Backpacks / Valheim Backpack, ValheimRAFT (custom ships & land vehicles).

---

## Milestone 3 — Cartographer's Kit

**Problem:** no-map play means drawing your own map; nothing in-game supports that.
Existing mods either auto-generate maps (ZenMap, NomapPrinter, NoMapWayfinding) or draw on the
vanilla map (MapRoutes). Vegvisir is a companion web app (walk-time triangulation).

**Design (agreed with the user 2026-09-27):**
- **No compass.** The sky is the compass (the Yggdrasil branch; the sun rises due east, stands in
  the south at midday about 60° up and sets due west, per `EnvMan`). Finding your place stays one
  of the big challenges. The kit covers what the sky can't tell you: distance, depth, angles.
  A sunstone (finds the sun when the sky is hidden) is not planned for now.
- **Few inventory slots.** Everything you use on foot lives in **one item, the Map case**.
  Sea instruments are **ship fittings**: attached once, then part of the ship.
- **Field notes and fair copy.** In the field you make rough marks and collect distances; at the
  cartography table you get a sense of true size and refine positions and sizes.
- **Nothing knows where anything is.** No "you are here", no world positions on a sheet; every
  helper works only from numbers you measured.
- Readings and the map show only while the case is held. Configs admin-only and server-synced.

**Build order:** 1. Map case → 2. Cartography table → 3. Ship fittings → then case upgrades,
sharing, companion app.

### 3a — Map case (one item)
- Crafted at the **workbench** (Meadows), so pace counting and sketching start early.
- **Held like the hammer**, in both hands. **M** (the map key, unused in no-map worlds) takes it
  out / puts it away.
- **Reading while walking:** the sheet shows while held; you can walk but not run (config to allow
  running). A key switches to drawing (cursor, field tools); **drawing stops you** (input blocked).
- Holds **draft sheets** (field sketches, count in config) and **a copy of one table's master**
  (read-only in the field).
- **Tally:** counts your paces while the case is anywhere in the inventory (on foot only; not
  swimming, riding or sailing). Read it on the case's edge while held. Two modes (config, user's
  pick 2026-09-27): **Distance** (default: ground covered, shown as paces of `PaceLength`, a random
  stride error per leg) or **Footsteps** (every footfall; running strides are longer).
- **Journal:** end a leg with a key → saves the tally count with your own label ("Leg 3: 412 paces,
  to the river crossing") and starts a new count. No rune-stick items.
- **Field tools:** charcoal (one thick, rough line), stamps, short text notes, eraser, undo.
  Config: field drawing allows everything / stamps and charcoal (default) / stamps only / nothing.
- Lose the case (death) → drafts and the master copy go with it (tombstone as usual). A new case
  gets the master again at a table.
- Strokes stored as **vector strokes**, compressed in the item's custom data; stroke/point caps.

**Status (2026-09-27):** steps 1 and 2 built (`src/Cartographer`), neither tested in game yet.
The drawing format (v2) and fill triangulation were unit-tested outside the game (a scratch test,
not committed): round trip, step-1 sheets still readable, self-crossing outlines still fill.
Keys: M take out / put away (no-map worlds), right-click draw, F next sheet, J end leg; in the
drawing view Esc or right-click closes, Ctrl+Z undoes. The case is a copy of the hammer with a
leather tube model made in code; the parchment and pen textures are made in code too (no assets).
Stamps borrow the game's map pin icons, trader icons and a few item/piece icons.

**Test checklist (Dev profile, test world with no map):**
1. `devcommands`, `spawn IM_MapCase` (or craft at the workbench: 2 deer hide, 4 leather scraps).
2. M takes it out: the sheet shows at the bottom, sheet name left, tally right. No attack/block,
   slow walk only. M again puts it away.
3. Walk about 100 m: the tally counts (≈125 paces at 0.8 m). Swimming, riding, sitting: no count.
4. J → label → "Leg 1 noted: N paces", the tally starts again from 0.
5. Right-click: the drawing view. Charcoal lines, a stamp, a note, the eraser, Ctrl+Z, sheet tabs,
   the Journal tab (End leg button). Esc / right-click / Done closes; the game menu must not open.
6. F flips sheets while reading. Put the case in a chest and back, log out and in: all still there.
7. Config: `FieldDrawing = Everything` → ink + colours; `Tally Mode = Footsteps`; `ReadingPace`.
8. Hold pose: `9 - Hold tuning` sliders (take it out again to apply). Log: "Hammer children".

**Step 2 test checklist (cartography table):**
1. `spawn` or build a cartography table in the no-map world; hover: "Work at the master map".
   E on either side opens the table view (HUD hidden; Esc, right-click or Done leaves).
2. Draw with every tool: charcoal, quill + colours, wash (under the lines), fill (draw an outline),
   stamps, text, eraser, sizes Fine/Medium/Broad, Straight and Dotted; Ctrl+Z. Wheel zooms around
   the cursor, middle mouse or Space + drag pans; draw past the first sheet's area (it grows).
3. Grid on/off; Scale button (paces per square); Fit.
4. With a map case holding a sketch: bottom bar Sheet N → the draft lies over the master
   see-through. Drag / Shift+drag / Ctrl+wheel. Two-point fit. Then **Copy as is** on one draft and
   **Trace** on another (compare); Clear guides. The copied draft is blank afterwards; Ctrl+Z
   brings back both.
5. End two legs in the field (J), then at the table: Leg buttons → press where it began, drag the
   direction, let go → a dotted string of true length; the next leg chains from its end.
6. Leave the table: the case's Master tab (and F while reading) shows the master copy.
   A second table (empty): "Lay your copy here".
7. Two players: the second one gets "Someone is working at the table".

### 3b — Cartography table (taken over)
- The vanilla table is useless in no-map worlds, so we take over its two action spots (read and
  write `Switch`es). Our data goes under our own ZDO key; the vanilla map data is untouched.
  (Conflicts with NomapPrinter, which uses the table: acceptable, user's call.)
- **One master map per table** (outposts can have their own). The case's master copy refreshes
  **only at the table**: a friend's edits reach you when you come back.
- Full toolset: ink colours, fine lines, text, stamps.
- **Helpers for true size:** sheet scale (e.g. one square = 100 paces); **measuring string**
  (a journal leg becomes a string of true length: pin one end, swing it to where you remember
  going); **two-point fix** (mark two places, enter the real distance → rescale the sketch);
  **trace and copy** a draft onto the master (costs parchment/ink).
- **Decided 2026-09-27 (step 2):**
  - E on either action spot opens the table view (master, your case's drafts, all tools). Ward
    access as vanilla. **One player at a time** ("Someone is working at the table").
  - The master **grows as you draw** (no fixed size); zoom and pan (also on drafts in the field).
  - **Drafts onto the master:** lay a draft over the master like tracing paper (see-through), move /
    rotate / resize it, or **two-point fit** (two spots on the draft, the same two on the master).
    Then **copy as is** or **trace** (lines come over as faint guides to ink over, then clear); both
    get built so the user can compare in game. A copied draft is **wiped**. Copying is **free**.
  - Journal legs become **measuring strings** at the master's scale (one square = N paces).
  - Tools: charcoal, quill (ink colours), **wash brush** (wide, see-through, under the lines) and
    **area fill** (draw an outline, it fills) with named biome swatches (Meadows light green, Black
    Forest dark green, Swamp olive brown, Mountain chalk white, Plains ochre, Mistlands grey-violet,
    Ashlands red-black, Deep North ice blue, Ocean woad blue); dotted and straight lines; text and
    stamps in two sizes; brush sizes fine / medium / broad. Nothing is ever coloured for you.
  - **Drafts stay rough:** colours only at the table (FieldDrawing=Everything allows all in the field).
  - The case's master copy refreshes when you leave a table that has a master; at an empty table
    you can lay your case's copy onto it (to start an outpost's map).

### 3c — Ship fittings
- **Log line:** attach to a ship's stern (the item is used up). A rope trails behind the ship.
  Hover the reel to read the distance run; E hauls in and resets. Stored on the ship, so it works
  for everyone aboard.
- **Sounding line:** fitted at the bow. E drops the lead → depth ("Seven fathoms", "No bottom at
  fifty").
- To settle later: destroyed ship → fittings drop or are lost; units at sea.

### 3d — Case upgrades
- **Craft a part at its station, then right-click it to fit it into the case** (the part is used
  up). Lets upgrades come from any station (vanilla quality upgrades only use the item's own
  station). First draft, to tune:
  | Part | Station | Adds |
  |---|---|---|
  | Extra pages | Workbench | More draft sheets |
  | Sighting vane | Forge | Aim at landmark A, then B → the angle between them (find yourself on your map from 2–3 landmarks) |
  | Dvergr lens | Black forge / Galdr table | Rangefinder: distance to an aimed point, with error (config, maybe off) |
- Dropped: compass, altimeter. Optional later: a Cartography skill that lowers measurement error.

### 3e — Sharing (later)
- Hand a sheet to a friend; join sheets into an atlas; hang a map on a wall for the group.

### 3f — Companion web app (optional, later)
- Reads a journal export, plots it, lets you draw with a tablet/stylus on a second screen.
- Export the drawing back into the game as a sheet.

---

## Reference: existing mods (researched 2026-09-26)
- Maps: [ZenMap](https://thunderstore.io/c/valheim/p/ZenDragon/ZenMap/),
  [NomapPrinter](https://thunderstore.io/c/valheim/p/shudnal/NomapPrinter/),
  [NoMapWayfinding](https://thunderstore.io/c/valheim/p/IronTree/NoMapWayfinding/),
  [ZenCompass](https://thunderstore.io/c/valheim/p/ZenDragon/ZenCompass/),
  [MapRoutes](https://old.thunderstore.io/c/valheim/p/SOPMEHUA/MapRoutes/),
  [Vegvisir (web app)](https://github.com/Ludrietz/Vegvisir),
  [Jötunn map API](https://valheim-modding.github.io/Jotunn/tutorials/map.html)
- Traders: [Locator](https://valheim.thunderstore.io/package/purpledxd/Locator/) (GPS-style, not immersive)
- Transport: [ValheimRAFT](https://thunderstore.io/c/valheim/p/zolantris/ValheimRAFT/),
  [LongshipUpgrades](https://thunderstore.io/c/valheim/p/shudnal/LongshipUpgrades/),
  [BoatAdditions](https://thunderstore.io/c/valheim/p/blacks7ar/BoatAdditions/v/1.4.1/),
  [balrond shipyard](https://thunderstore.io/c/valheim/p/Balrond/balrond_shipyard/),
  [BeastsOfBurden](https://thunderstore.io/c/valheim/p/clevel/BeastsOfBurden/),
  [LoxSaddleBags](https://old.thunderstore.io/c/valheim/p/Moddermother/LoxSaddleBags/),
  [Better Lox](https://www.nexusmods.com/valheim/mods/1967),
  [Valheim Backpack](https://www.nexusmods.com/valheim/mods/3456),
  [Adventure Backpacks](https://github.com/Vapok/AdventureBackpacks)

## Open questions
- Should the signal be visible to all players in range, or only to the approaching player?
- Which real profile will the mods be played in (and compatibility pass against it)?
- Cartographer: are instruments equipment (utility slot) or held tools (hand slot)?
