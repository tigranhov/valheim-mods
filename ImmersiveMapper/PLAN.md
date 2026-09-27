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
| Multiplayer | **Dedicated server the user controls** → our mods get installed on the server too |
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
  500: the smoke still reads well with that much haze. First git commit.

### Repo layout (planned)
```
immersive-mapper/
  PLAN.md
  Directory.Build.props      # game path, profile path, shared build settings
  ImmersiveMapper.sln
  src/TraderBeacons/
  src/CargoLashing/
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

**Status (2026-09-26):** v0.1 written and compiling (`src/TraderBeacons`). Not yet tested in game.
Next: create the `Dev` profile → test with `tb_test` → test against a real camp → tune → server.

**In-game test checklist (Dev profile, a throwaway test world):**
1. ✅ `tb_test` smoke visible from far away, drawn at the real spot.
2. `devcommands`, `tod 0.9`, `tb_test 800 smoke`: is the smoke readable at night at all?
3. `tb_camps` (spoiler) → go within ~300 m of Haldor (camp gets generated) → smoke appears →
   walk away: still visible up to 1200 m → walk in: stops within 40 m or on talking.
4. `tb_resetfound` to repeat. Check `BepInEx/LogOutput.log` for "Firework from…" and "Smoke material…".

**Console commands:** `tb_test [meters] [smoke|fireworks]` (reveals nothing),
`tb_camps` and `tb_resetfound` (need devcommands + server admin).

### v0.1 — Smoke & fireworks (MVP)
- Server checks player positions periodically (e.g. every 5 s).
- A camp starts signalling once the game has **generated** it (someone came within ~300 m, the
  same moment vanilla would show the map icon). From then on it signals to every player within
  the **signal radius** (max viewing distance, default 1200 m) who has **not yet found** it:
  - **Day:** tall smoke column above the camp, visible over the treeline.
  - **Dusk/Night:** firework volley (~1 min), repeated each night while someone is in range,
    until the trader is found. (Hildir sells fireworks in vanilla — fits the lore.)
- **Flash-to-bang:** firework sound is delayed by distance ÷ 343 m/s, so players can estimate
  distance by counting seconds (ties into the Cartographer's Kit).
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
      `unity_FogParams` override caps the smoke's fog at `FogAsIfMeters` (500). Drawn at the real
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

## Milestone 2 — Cargo Lashing (+ Packing Crates)

**Problem:** relocating bases a lot; carts on boats bump around and fall off; slot limits.

**Already in the user's Immers profile (don't duplicate, stay compatible):**
`Azumatt-HaulersHelper` (cart physics tuning), `OdinPlus-CraftyCartsRemake` (station carts with
storage), `MathiasDecrock-PlanBuild` (blueprints — useful for rebuilding a base).

### v0.1 — Lash carts to ships
- Interact with a cart standing on a ship deck → **"Lash to deck"**. The cart locks in place
  relative to the ship until **"Untie"**.
- Works with any object using the vanilla cart script (`Vagon`), incl. modded carts.
- Lashed state saved on the cart (ship ID + local offset/rotation), survives relog/zone reload.
- Auto-untie if the ship is destroyed; decide what happens if the ship sinks.
- Research: parent + kinematic vs. physics joint; ownership & sync jitter; how ValheimRAFT handles it.

### v0.2 — Packing Crates
- Craftable crate packs several stacks of **one** stackable material into **one slot**
  (e.g. "Crate of Wood ×250"). Weight = contents + crate. Unpack at destination.
- Only plain stackable materials (no quality/durability items). Keeps the non-teleportable flag
  of its contents.
- Maybe later: a "sealed chest" you can pick up with contents inside.

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
