# CombatMeter

A SKADA-style combat meter for Core Keeper: damage dealt and damage received, broken
down by source, for the whole co-op party, with automatic boss-encounter tracking.

This folder mirrors what should become `Assets/CombatMeter/` inside the Core Keeper
Mod SDK Unity project. It isn't buildable on its own - it needs to be dropped into that
project and compiled/packaged from inside the Unity Editor.

## How this was built

Rather than guessing at APIs from the (mostly unwritten) official modding wiki, every
class/field/system referenced here was confirmed by decompiling the actual installed
game (`C:\Program Files (x86)\Steam\steamapps\common\Core Keeper`) with `ilspycmd`, and
by reading the official SDK's example mods (`Pugstorm/CoreKeeperModSDK` on GitHub) and a
community mod's proven `OnGUI` overlay pattern (`germanoeich/CoreKeeperMods`). See the
approved plan for the full research trail: it's saved separately, but the short version
is in the comments at the top of each file below.

## Setup (one-time, on your machine)

This has all been done already on this machine and verified to compile clean end to end
(Unity Hub + Editor 6000.0.58f2 installed, SDK cloned to `CoreKeeperModSDK/`, this mod
copied in, 0 compile errors on a full project rebuild). Steps below are for redoing it
elsewhere or after a game update.

1. Install **Unity Hub**, then add Editor **6000.0.58f2** with the **"Linux Build
   Support (Mono)"** module enabled (required to build mods).
2. Clone **`Pugstorm/CoreKeeperModSDK`** from GitHub, add it in Unity Hub as a project.
3. The git repo does **not** include the actual game assemblies (`Pug.ECS.Components.dll`,
   `Pug.Other.dll`, `PugMod.*.dll`, etc.) - Pugstorm can't publish their own compiled
   game DLLs to a public repo. Copy every DLL named in `CombatMeter.asmdef`'s
   `precompiledReferences` list from your installed game's
   `CoreKeeper_Data\Managed\` folder into the SDK project's
   `Assets/Plugins/CoreKeeperModSDK/` folder (skip any already present there - those
   ship with the repo and are already set up correctly). The 4 `*.Editor.dll` files
   that aren't in the game's Managed folder are bundled instead in
   `Assets/ModSDK/EditorAssemblies.zip` - extract those into the same folder.
4. **Critical**: for every DLL you just copied, its `.meta` file needs
   `isExplicitlyReferenced: 1` set under `PluginImporter:` (see "The isExplicitlyReferenced
   gotcha" below) - Unity's default auto-generated `.meta` leaves this unset, which
   breaks unrelated built-in Unity packages, not just this mod.
5. Open the project, let it import. It should compile with 0 errors.
6. Use the SDK's mod-packaging UI to build a mod asset for this - see the wiki's
   "Testing the Example Mods" page for the exact steps (Create Mod, point it at this
   assembly, package/export).

### The `isExplicitlyReferenced` gotcha

Copying the ~150 game DLLs in as plain Plugin assets (step 3) got them working for
`CombatMeter.asmdef`, but broke ~130 **unrelated** compile errors in built-in Unity
packages (`com.unity.2d.sprite`, `com.unity.render-pipelines.core`,
`com.unity.entities`) - confirmed by testing a 100%-vanilla SDK checkout with none of
these DLLs added, which compiled with 0 errors. Cause: Unity auto-references every
"Editor compatible" Plugin DLL into *every* assembly in the project by default,
including other packages' own loose (asmdef-less) Editor scripts - not just assemblies
that actually ask for it. Once ~150 new assemblies got auto-wired in that way, it broke
assembly resolution for those unrelated packages' Editor tooling.

The fix is the same one Pugstorm's own shipped `PugSprite.dll.meta` already uses:
```yaml
PluginImporter:
  ...
  isExplicitlyReferenced: 1
```
This makes the DLL visible *only* to asmdefs that explicitly list it (like this one, via
`precompiledReferences` + `overrideReferences: true`), not auto-wired everywhere else.
The underlying `PluginImporter.isExplicitlyReferenced` C# property is `internal` (no
public API for it - confirmed against Unity's own `PluginImporter.bindings.cs` source),
and `-executeMethod` won't run while the project has *any* compile errors elsewhere, so
this can't be scripted through the normal Editor API when the project is already broken -
the `.meta` files have to be hand-edited (or written from scratch) directly. A
throwaway reflection-based Editor script for doing this via `-executeMethod`, for use
*after* the DLLs are already correctly referenced (e.g. a future SDK update), is kept at
`../SetupFixAutoReference.cs` (sibling to this folder, not part of the mod itself).

## What's implemented

- **`Server/DamageMeterCaptureSystem.cs`** - runs server-side only, one tick before the
  game's own `UpdateHealthFromBufferSystem` consumes and clears its `HealthChangeBuffer`
  singleton (the single queue every damage/heal event in the game funnels through).
  For each event, resolves the "true" source by walking `OwnerReferenceCD.owner` up to
  a player if the direct source was a minion/turret/projectile (mirrors the game's own
  kill-attribution logic), tags whether the victim is a `BossCD` entity, and broadcasts
  one `DamageMeterEventRpc` per event to all clients.
- **`Network/DamageMeterEventRpc.cs`** - the RPC payload. Uses `(ghostId, spawnTick)`
  pairs rather than raw `Entity` values, since entities aren't network-stable.
- **`Client/DamageMeterReceiveSystem.cs`** - runs on every client (including the host),
  resolves ghost ids back to local entities via `SpawnedGhostEntityMap`, resolves a
  display name (see caveat below), and feeds `CombatLogAggregator`.
- **`Client/CombatLogAggregator.cs` / `EncounterRecord.cs`** - plain C# aggregation:
  current encounter + last 20 in a history list, auto-starts on the first event
  involving a boss, auto-ends 10s after the last relevant event (or immediately on a
  tracked boss's death), per-source totals/hit-counts/DPS, and (for the "received" tab)
  a breakdown of who dealt each hit.
- **`UI/CombatMeterOverlay.cs`** - a draggable `OnGUI` window (Dealt / Received / Healing
  / History tabs), same technique as the community DebugMod's FPS overlay.
- **`UI/CombatMeterCommands.cs`** - chat commands `combatmeter.toggle` and
  `combatmeter.reset` (via Quantum Console, same as the SDK's `ModCommandsExample`).
  `CombatMeterMod.EarlyInit()` force-enables the console so these are reachable without
  needing a separate debug mod installed.

## Known open items (flagged in the plan, not blocking)

- **Player display names**: there's no clean public API for a player's chosen/Steam
  name, so players show up as "Player 1", "Player 2", etc. (from the stable
  `PlayerGhost.playerIndex`). If you want real names, the fix is one line in
  `DamageMeterReceiveSystem.ResolveName` - once the SDK project is open, use Unity
  Explorer or dnSpy on a live client to find where the game itself renders player
  nameplates, and read from whatever component that pulls from.
- **Per-ability breakdown** (melee vs ranged vs magic) isn't included - `HealthChange`
  doesn't carry that, only `DealDamageToEntityBuffer` does (a separate, earlier buffer
  in the pipeline). Capturing that too, correlating by entity/tick, is a reasonable v2
  addition if you want finer-grained breakdowns than "which entity/player."
- ~~This hasn't been compiled~~ Verified: compiles with 0 errors against the actual SDK
  project + installed game assemblies (Unity 6000.0.58f2). Not yet tested in an actual
  running game (packaging the mod and playing with it is the next step).

## Testing once it's packaged

1. Singleplayer: hit a normal enemy, confirm "Dealt" shows you; take a hit, confirm
   "Received" shows the enemy as the source.
2. Co-op (2 clients): confirm **both** players show up on every client's meter, not
   just the local one - this is the real test of the server-capture + RPC path.
3. Fight an actual boss: confirm the encounter auto-names itself after the boss and
   closes out when it dies.
