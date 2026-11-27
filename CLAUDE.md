## Project Context

A BepInEx 6 (BepInEx.Unity.IL2CPP, be.755) mod for Invokyr (Unity 6000.3, IL2CPP): an in-game settings panel listing every loaded plugin's config entries (`IL2CPPChainloader.Instance.Plugins`). Opens on F2 or from a "Mods" button cloned from the game's Options button in `MainMenu` / `PauseMenu` (`vhs.dll`, `_200_Dev.UI.StartMenu`). Design: `docs/superpowers/specs/2026-11-26-invokyr-config-design.md`.

## Rules
- No Harmony patches on game methods (a patch on `VhsNetworkManager.Awake` crashed More Players). Hook the game by polling from our MonoBehaviours.
- IMGUI is stripped: only `GUI.Label`/`GUI.DrawTexture`; widgets are our own (`Ui.cs`, `Theme.cs`, copied from invokyr-admin-menu).
- Only one overlay at a time: `OverlayState.IsOpen` is read by Admin Menu 0.1.2+, and we read `InvokyrAdminMenu.AdminMenuController.IsOpen`, both by reflection (`ExternalFlag`).

## Building and testing
- `scripts/update-refs.sh` refreshes `~/.cache/invokyr` and `lib/refs` after a game update.
- `scripts/package.sh` builds `dist/darkharasho-InvokyrConfig-<version>.zip`; install with `hml install nexus-invokyr <zip> --profile Dev`. Run builds with `nice -n 19 taskset -c 0-3`.
- Tests (pure logic, real BepInEx.Core from `~/.cache/invokyr/core`): `dotnet test tests/InvokyrConfig.Tests`.
- Release: bump `Plugin.cs`, the csproj, `thunderstore/manifest.json`, `CHANGELOG.md`; push tag `v<version>`. GitHub + HML catalog (`hml-mod` topic) only.
