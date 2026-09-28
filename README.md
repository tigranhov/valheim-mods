# Valheim mods

BepInEx mods for [Valheim](https://www.valheimgame.com/). Most of them are for worlds played
**without the map** (the Immersive preset): they make exploring, navigating and moving house richer
without GPS-style shortcuts. Information is earned, never given: no coordinates, no "you are here",
no automatic pins.

## Mods

| Mod | What it does | Needs | State |
|---|---|---|---|
| [Cartographer](ImmersiveMapper/src/Cartographer) | Draw your own maps. A **map case** holds draft sheets, a pace tally and a journal of legs; the **cartography table** becomes a master map that grows as you draw (zoom, colours learned from each biome, drafts laid over it like tracing paper, legs as measuring strings); a ship's **log line** counts the distance sailed. | Jötunn | In testing |
| [Ship Cargo](ImmersiveMapper/src/ShipCargo) | Cargo crates, built with the hammer. Set one down on a ship's deck and it rides along without sliding; carry it in your arms or pack it, contents and all. | Jötunn | Tested |
| [Cart Lashing](ImmersiveMapper/src/CartLashing) | Lash a cart standing on a ship's deck so it rides the ship instead of bumping off. Works with any cart built on the vanilla cart. | Jötunn | Tested |
| [Trader Beacons](ImmersiveMapper/src/TraderBeacons) | Traders signal travellers from afar with a column of smoke, once their camp exists, until you've found them. Something to walk towards, not a pin. | Jötunn | Works in single player |
| [Better Wheel](BetterWheel) | The item wheel opens straight on all your items (no category wheel), extra wheels on their own keys (food, gear, ...), and several picks per opening. Client side only. | — | In testing |
| [Valheim Tweaks](ValheimTweaks) | A configurable mining drop multiplier (iron scrap). | — | — |

Every mod needs [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/);
[Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) where noted. In multiplayer,
every player needs the mods with Jötunn; their gameplay settings are admin-only and synced from the
server (a player-hosted game counts).

## Building

You need:
- the .NET SDK (6 or newer) and the .NET Framework 4.8 targeting pack (it comes with Visual Studio);
- Valheim installed;
- Thunderstore Mod Manager or r2modman, with a profile that has BepInEx (and Jötunn).

Game assemblies are referenced from your Valheim install and are never part of this repository.

**Paths.** Set yours in the props files, or override them without touching tracked files:
- `Directory.Build.props`: `ValheimDir` (the game) and `ProfileDir` (the profile Valheim Tweaks
  deploys to).
- `ImmersiveMapper/Directory.Build.props`: `DevProfileDir`, the test profile the immersive mods
  deploy to (a profile called `Dev` by default). Put overrides in
  `ImmersiveMapper/Directory.Build.user.props` (git-ignored).

**Build.**

```bash
dotnet build ImmersiveMapper/ImmersiveMapper.sln -c Release
```

The four immersive mods, each copied into the Dev profile's `BepInEx/plugins` after the build.

```bash
dotnet build ValheimMods.sln -c Release
```

Everything, Valheim Tweaks included. Valheim Tweaks updates its profile only once its package has
been imported there.

Better Wheel builds and deploys to the Dev profile on its own (`dotnet build BetterWheel -c Release`).

Release builds of Cartographer, Better Wheel and Valheim Tweaks also make a Thunderstore package,
`bin/<team>-<name>-<version>.zip`, to import in the mod manager or upload.

## Layout

```
valheim-mods/
  Directory.Build.props      # machine paths for every mod
  ValheimMods.sln            # all projects
  BetterWheel/
  ValheimTweaks/
  ImmersiveMapper/
    PLAN.md                  # design, decisions and in-game test checklists for the immersive mods
    Directory.Build.props    # shared references (BepInEx, Jötunn, publicized game assemblies)
    ImmersiveMapper.sln
    src/Cartographer/
    src/ShipCargo/
    src/CartLashing/
    src/Shared/ShipRiding/   # riding ships, compiled into Ship Cargo and Cart Lashing
    src/TraderBeacons/
```

## License

[MIT](LICENSE)
