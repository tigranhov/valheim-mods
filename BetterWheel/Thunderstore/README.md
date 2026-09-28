# Better Wheel

A quicker item wheel for Valheim.

- **The wheel key (G) opens straight on all your items**: no category wheel first. With many items
  the wheel spirals through them as you keep turning. Looking at a fermenter, item stand, offering
  bowl and the like still opens just the items it takes, and the emote key still opens emotes.
- **Extra wheels on their own keys**, up to four: each shows the item types you choose. Three come
  ready: *Food & gear*, *Food* and *Gear*; they start without a key, bind the ones you want in
  [Configuration Manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/).
  Press the key again to close; another wheel's key switches to that wheel. Side mouse buttons work.
- **Several picks per opening**: eat three foods, swap your whole kit, then close the wheel. (The
  game only keeps it open for gear, and for food while you can still eat.)
- **Release to use**, per wheel: hold the key, point at something, let go, and it's used. A quick
  tap still opens the wheel for clicking.

## Settings

| Setting | Default | What it does |
|---|---|---|
| General / WheelKeyOpens | AllItems | `Categories` brings back the game's category wheel. |
| General / AfterUse | UntilClosed | `Vanilla`: the game decides when the wheel closes. |
| General / ReleaseToUse | On | For G and T: letting go of the held key uses what you point at (`Off`: just closes; `Game`: the game's own setting). |
| General / ReleaseHoldTime | 0.2 | Seconds a key must be held before letting go counts as a release; a shorter press is a tap. |
| Wheel 1–4 / ReleaseToUse | On | The same, for that wheel's key. |
| Wheel 1–4 / Key | none | Opens that wheel. |
| Wheel 1–4 / Name | Food & gear, Food, Gear | Shown in the middle of the wheel. |
| Wheel 1–4 / ItemTypes | | Item types on the wheel, e.g. `Consumable, Helmet, Chest, Legs`. |
| Debug / Log | false | Writes what the wheel does to the BepInEx log, for bug reports. |

The game's other wheel settings (size, hover select, animation, ...) still apply; its Release to use
setting is replaced by the ReleaseToUse settings above (set them to `Game` to follow it).

## Multiplayer

Client side only: the wheel is your own menu, nothing is sent to other players, and they don't
need the mod.
