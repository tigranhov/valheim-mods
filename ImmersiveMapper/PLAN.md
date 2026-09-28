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
- 2026-09-27 — Ship Cargo performance pass before moving on: ship keys make crate network updates
  ~15–20× rarer; Shift-crafting crates is limited to the room at the station (see Milestone 2).
- 2026-09-27 — Cart lashing becomes its own mod (`CartLashing`), a lighter alternative to crates;
  the ship-riding code is shared (`src/Shared/ShipRiding`). See Milestone 2.

### Repo layout (planned)
```
immersive-mapper/
  PLAN.md
  Directory.Build.props      # game path, profile path, shared build settings
  ImmersiveMapper.sln
  src/TraderBeacons/
  src/ShipCargo/             # cargo crates
  src/CartLashing/           # lashing vanilla carts to ships
  src/Shared/ShipRiding/     # riding ships, compiled into ShipCargo and CartLashing
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
keep one link per target, to finding the ship by deck offset). Crates break like the shipwreck crates
(`Health`, 0 = vanilla) on land or afloat: contents spill out, crates stacked on top come loose and tumble down. Riding
a ship they are part of it and can't be broken: hits go to the ship. (First version: unbreakable.)
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
  ZDO along so the crate loads with the ship. Link = a key the mod keeps in the ship's ZDO
  (`IM_ShipKey`, survives reloads; ZDO ids don't), with the deck offset as fallback for older crates.
- Known limit: standing *on top of* a crate while sailing doesn't carry you (the deck does).
- Ship destroyed → each crate drops into the water as itself, contents and all, as a loose crate
  (`IM_CargoCrateAfloat`: the vanilla floating crate's physics, our slots; also what crates off a broken stack become). Shift+E fishes
  it out (from a deck or shallow water; not while swimming) → a placed crate again, lifted or packed.
  (First version spilled the contents + an empty crate item into vanilla floating crates.)

**Test checklist (Dev profile, test world):**
1. Build one with the hammer (Misc, near a workbench: 10 wood, 4 bronze nails), or `spawn IM_CargoCrateItem`.
2. Right-click the crate in the inventory → it's set down in front of you. E opens, fill it.
3. Shift+E picks it up → one heavy item; tooltip shows contents. Right-click again to set it down.
4. Try putting the packed crate in a chest / cart → "A packed crate can't go in there".
5. `spawn Karve` in water, board it, set a crate down on the deck, sail: it must not move or slide.
6. Log out and back in: the crate is still on the deck at the same spot.
7. Destroy the ship: cargo + the empty crate float away in vanilla floating crates.
8. Log: "CargoCrate components" and "collider layers" lines (layer decides player collision/stacking).

- Buildable crate (looks like the vanilla shipwreck crate), with its own slots (config).
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
Built with the hammer (see "Hammer-built crates" below); first version was crafted at the workbench.
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
  the water, where the crate floats as itself. Carrier gone / world reloaded → owner sets it down.
- Also: a crate with another crate on top can't be picked up or lifted (it would be left hanging).

### Hammer-built crates (✅ tested and merged into main 2026-09-27; was branch `experiment/hammer-crates`)
Crates were crafted at the workbench (and, when carried, appeared on it; Shift-craft limited to the
room there). They couldn't be repaired, so they became a **hammer piece** (Misc tab, workbench nearby,
10 wood + 4 bronze nails, config), like carts and chests:
- The placed crate is a building piece (`Piece` + `WearNTear`, replacing the vanilla crate's
  `Destructible` and keeping its sounds and toughness): repaired with the hammer, dismantled for its
  materials, a broken one drops them (and spills its contents). No support or rain wear, immune to ash
  and lava, nothing can be built on it (`m_supports` off). Wards protect it.
- The build ghost follows the crate rules, not the building rules (`CrateBuild`): same pose as setting
  a crate down (`CratePose`: snapping to crates, Shift for none, the hammer's rotation), `CrateFit`
  decides (the game refuses buildings on ships and on non-supporting pieces), and says why. The game's
  zone rules still count (no-build zones, wards, someone in the way). Built on a deck → rides the ship
  (`IPlaced.OnPlaced`).
- Riding a ship, hits on the crate go to the ship (patch moved from `Destructible` to `WearNTear`).
- The crate item stays only as the packed form (PickUpMode Inventory); it has no recipe any more.
- Removed: workbench crafting, crates appearing at the station, the Shift-craft limit, `CratesAtStation`.
- Loose crates (afloat/tumbled) keep the vanilla `Destructible`: breakable, not repairable; broken,
  they drop the crate's materials too, so no crate's materials are ever lost.
- Crates get a builder when they have none (set down from an item, fished out, or made before this):
  the game returns only a third of the materials of a piece nobody built.

### Performance pass (2026-09-27)
Single player was already light (per-crate work is a few pose updates per frame). Changes:
- **Network (the big one):** every ZDO update sends the crate's whole contents to each nearby
  player (~0.5 KB for 4 full slots). A riding crate used to update up to 10×/s while sailing (0.25 m /
  2° / 0.1 s). Now each ship gets a key and each crate remembers it, so after a reload the crate
  finds its ship by the key, not by an exact saved position. Its ZDO only has to stay in the ship's
  zone: at most every 2 s (1 m / 5°), plus right away when it crosses into a new zone. About 15–20×
  less traffic from crates on ships. Until the key is confirmed (older crates, or a ship someone else
  owns), the close sync is used. Carried crates: 4×/s → 2×/s.
- Each crate position update also made its container re-read its contents (the game reloads a
  container whenever its ZDO changes, checked once a second), so fewer updates also mean fewer reloads.
- `CrateItem.IsCrate` (runs whenever the game weighs or adds any item) compared `Object.name`, which
  allocates a string per call → now a prefab reference compare. Same for the crate recipe check.
- `CrateCarry.CarriedBy` (several calls per player per frame) scanned every crate, with a cache that
  only helped with one player → a player→crate dictionary.
- Crate weight followed a 0.5 s timer on every crate → now updated only when its contents change.

### Cart lashing — its own mod, `src/CartLashing` (✅ tested and merged into main 2026-09-27; was branch `feature/cart-lashing`)
**Decision (2026-09-27):** a separate mod, not part of Ship Cargo: crates (a new building, carrying)
change the classic game a lot; lashing only makes vanilla carts behave on ships. Install either or both.
- On a cart standing on a ship's deck, Shift+E → **"Lash to the ship"** / **"Untie"** (on the cart or
  its storage). A lashed cart rides the ship like a crate: the cart and its wheels frozen (kinematic),
  the game's `ZSyncTransform` paused, can't be pulled ("Untie the cart first"), storage still opens.
  Part of the ship: hits on it go to the ship. Standing on it (or a wheel) carries you like the deck.
- Works with anything using the vanilla cart script (`Vagon`, components added in `Vagon.Awake`),
  incl. CraftyCarts' carts. Free, no per-ship limit. Config: `Enabled` (admin-synced).
- Survives relog/zone reload (ship key). Ship destroyed → untied, an ordinary cart again.
- The owner does the lashing (RPC), checking again that it stands on a deck and isn't hitched.
- **Shared code:** riding ships moved out of Ship Cargo into `src/Shared/ShipRiding` (`ShipPassenger`,
  `ShipKey`, ship patches, "hits go to the ship", "standing on it = on the ship"), compiled into both
  mods, so each still installs on its own. With both installed, each patches for its own passengers,
  and they share the ship key (the key's RPC is registered once, by whichever loads first).

### Existing mods to recommend alongside (not rebuild)
LongshipUpgrades (bigger longship storage), BoatAdditions (knarr cargo ship), balrond shipyard
(cargo barrels/crates), BeastsOfBurden (lox/boar pull carts), LoxSaddleBags / Better Lox,
Adventure Backpacks / Valheim Backpack, ValheimRAFT (custom ships & land vehicles).

---

## Milestone 3 — Cartographer's Kit

**Problem:** no-map play means drawing your own map; nothing in-game supports that.
Existing mods either auto-generate maps (ZenMap, NomapPrinter, NoMapWayfinding) or draw on the
vanilla map (MapRoutes). Vegvisir is a companion web app (walk-time triangulation).
**Our mod does not touch the cartography table**, so it can live alongside those.

### 3a — Instruments (progression-gated)
| Tool | Tier | Gives you |
|---|---|---|
| Compass | Bronze | Bearing in degrees; drifts in storms / Mistlands |
| Pace counter / surveyor's wheel | Meadows–Black Forest | Meters walked since reset |
| Chip log | Longship era | Distance sailed (dead reckoning at sea) |
| Sighting staff | Iron | Bearing to an aimed landmark (for triangulation) |
| Sounding line | Ocean | Water depth (chart shallows & reefs) |
| Altimeter | Mountain | Height above sea level |
| Dvergr rangefinder | Mistlands | Distance to aimed point, with error |
- Optional **Cartography skill**: lowers instrument error as it levels.
- Readings shown only while the tool is held/used.

### 3b — Survey Journal
- Press a key to record a note: bearing + distance since last note, height, your own label.
- Journal UI; export to a local JSON file per world (feeds the companion app).

### 3c — Parchment canvas (in-game drawing)
- Parchment item / cartographer's desk opens a drawing UI: quill, charcoal, eraser, text,
  grid, scale bar, **protractor & ruler** (draw a line at bearing X), stamps
  (trader, dungeon, boss altar, base, danger…).
- **Plot journal:** draw recorded routes as dotted lines.
- Stored as **vector strokes** (small, syncable), compressed in item/ZDO data; cap stroke count.

### 3d — Sharing
- Join sheets into an atlas; hang a map on a wall piece for the group; copy at the desk
  (costs parchment + ink).

### 3e — Companion web app (optional)
- Reads the journal export, plots it, lets you draw with a tablet/stylus on a second screen.
- Export the drawing back into the game as a sheet.
- Idea to test early (zero-code): NomapPrinter accepts custom PNG layers — a world-aligned drawing
  could be shown in-game that way.

---

## Parked: other no-map gaps (discussed 2026-09-27, back to it when the user finds Hildir)
What the vanilla map did that Immersive takes away, checked in the game code.
**Vanilla no-map behavior:** a vegvisir or rune stone that reveals a place calls
`Game.DiscoverClosestLocation`; with no map open, the answer turns the camera toward the place for
3.5 s (`SetLookDir`, in `RPC_DiscoverLocationResponse`), once, with no distance and nothing kept.
- **Not needed (user's call):** finding your grave, finding home, finding other players.
- **Dyrnwyn fragments:** a chain of vegvisirs (Putrid Holes → Mysterious Locations → Lord Reto).
  The camera turn is enough, **but** the first vegvisir is inside a Putrid Hole, and dungeon
  interiors sit 5000 m above the world (`Location`): the camera points steeply down, and leaving the
  dungeon resets your facing. Fix idea: turn level toward it (no tilt), and point you again as you
  step out of the dungeon.
- **Hildir's dungeons** (Smouldering Tomb / Black Forest, Howling Cavern / Mountains, Sealed Tower /
  Plains): her camp's map table adds the pins. It seems to reveal several at once, so with no map
  you'd only end up facing the last one, not knowing which (to confirm). Ideas, lightest first:
  A) one location per use of the table, named ("The Smouldering Tomb lies this way");
  B) Hildir describes the way (bearing + rough distance in words, later into the Map case journal);
  C) once her quest is taken, each dungeon gives itself away until the chest is found: smoke from the
     Smouldering Tomb, howling from the Howling Cavern, a glow atop the Sealed Tower at night
     (reuses the Trader Beacons smoke);
  D) a treasure map: a sketch of the land around it, no coordinates (after Cartographer sheets).
  Leaning: A as the base, C to make it special.
- **Deep North** (1.0: boss Kall Fimbulbringer; Winding Tunnels, Mörkhalla, The Prison): its boss is
  likely found by vegvisir too; if that stone is inside a dungeon, same problem as the Putrid Holes.
- **First step when we get back to it:** a debug command listing every vegvisir and rune stone in
  the game's locations: what it points to, whether it's inside a dungeon, and whether it reveals
  several places at once. Confirms Hildir's table and Deep North without playing through them.

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
