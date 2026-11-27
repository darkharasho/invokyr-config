# InvokyrConfig: in-game settings for every Invokyr mod

## Why

Today the only way to change a mod setting (flashlight brightness, minimap size, ammo multiplier, max players, and
so on) is to quit, edit a `.cfg` file under `BepInEx/config`, and restart. InvokyrConfig adds an in-game panel that lists
every loaded BepInEx plugin's settings and edits them live, the way REPOConfig does for R.E.P.O. and
`stonewards-mod-settings` does for Stonewards.

Success: a player opens the panel from the title screen or the pause menu (or with a key), changes the flashlight
settings, and sees them take effect without restarting. Beam width and range change while the slider moves.
Brightness applies the next time the flashlight is switched on, and its setting description says so (agreed with
the user).

## Goals

- List the settings of **every** loaded plugin, not only ours. Discovery is generic, so third-party mods show up too.
- Two ways in, both opening the same panel:
  - a hotkey (default **F2**; F1 is the Admin Menu), configurable;
  - a **Mods** button in the title menu and the pause menu, next to the game's Options button.
- Edit the four setting types the mods use today: bool, float, int and string. Ranges come from each setting's
  `AcceptableValueRange`, and enums from `AcceptableValueList` or the enum type.
- Changes apply immediately (written through `ConfigEntryBase.BoxedValue`, so each mod's `SettingChanged` fires) and are
  saved to disk.
- Honour `ConfigurationManagerAttributes` (`DispName`, `Order`, `Browsable`, `ReadOnly`, `IsAdvanced`), read by
  field name through reflection, because every mod has its own copy of the class. Today the Invokyr mods only set
  `DispName` and `Order`.
- Show each setting's description in the panel, not only on hover. Notes such as "applies the next time it's
  switched on" must be visible.

## Non-goals

- Controller navigation. The panel works with the mouse only, like the Admin Menu.
- Matching the look of the game's own Options screens. The panel uses the Admin Menu's look.
- Syncing settings between players. Settings are local, the same as editing the `.cfg` file.
- Reworking the existing mods. They appear in the panel without changes, except for the Admin Menu follow-up
  below.

## Anti-goals

- **Crashing the game on launch.** More Players 0.1.2 showed that a Harmony patch on a game method can crash inside
  IL2CPP. InvokyrConfig patches **no game methods**. Every hook into the game is done by polling from its own
  MonoBehaviour.
- Breaking a menu if something goes wrong. Each failure is caught, logged once, and the feature switches itself off.
  The game's menus keep working without the Mods button.
- Saving a half-typed or out-of-range value.

## Constraints

- **Game and stack:** Unity 6000.3 IL2CPP, run under Proton and on Windows, with BepInEx 6.0.0-be.755 IL2CPP,
  Il2CppInterop and HarmonyX.
- **IMGUI is stripped:** `GUILayout`, `GUI.TextField`, sliders and `BeginGroup` throw "Method unstripping failed". Only
  `GUI.Label` and `GUI.DrawTexture` work. The panel reuses the Admin Menu's hand-built widget toolkit
  (`invokyr-admin-menu/src/InvokyrAdminMenu/Ui.cs`, `Theme.cs`, `Hotkey.cs`), copied into this repo rather than
  shared as a library.
- **Game menus are uGUI with TextMeshPro**, in the interop `vhs.dll`:
  - `_200_Dev.UI.StartMenu.Menus.MainMenu` has an `optionsButton` (`NavButton`).
  - `_200_Dev.UI.StartMenu.PauseMenu` has an `optionsButton` (`NavButton`) and a `contentLayoutGroupRebuilder`.
  - `VHSButton : Button` has `textMeshProUGUI` and `add_OnMouseClick(Il2CppSystem.Action)`.
- **Packaging and release:**
  - Build setup comes from `~/.cache/invokyr/scaffold.sh invokyr-config InvokyrConfig`.
  - Releases are GitHub releases plus the HML catalog only, with no Thunderstore. A release is a `v<version>` tag
    push, starting at 1.0.0.
- **Testing:** install with `hml install nexus-invokyr <zip> --profile Dev`. Never copy files into game folders.

## Approaches considered

1. **One overlay panel reached two ways (chosen).** The panel is drawn with the Admin Menu's IMGUI-label toolkit and
   opened by the hotkey or by a Mods button cloned from the game's Options button. It is the cheapest option and the
   lowest risk, because the toolkit is already proven in this game and nothing is patched. It works with the mouse only
   and looks like the Admin Menu, not the game.
2. **A native uGUI panel cloned from the game's Options screens.** This would match the game's look and support
   controller navigation. It means reverse-engineering how the game builds its slider and toggle rows and rebuilding
   them from code. It is several times the work, and a game update could break all of it.
3. **Hotkey only.** This is the simplest, but players wouldn't discover it. The user asked for both ways in.

The user picked approach 1.

## Design

### Parts

- **`Plugin`** (BasePlugin) has the following settings:
  - `General.ToggleKey`, default `"F2"`;
  - `General.ShowMenuButtons`, default `true`;
  - `General.ShowAdvanced`, default `false`.

  `Load()` adds the two MonoBehaviours below. It touches nothing else in the game.
- **`SettingsRegistry`** is rebuilt each time the panel opens. It walks `IL2CPPChainloader.Instance.Plugins`, takes
  each plugin's `Config`, and groups the entries by plugin (name and version), then by section. Entries with
  `Browsable = false`, and advanced entries unless `ShowAdvanced` is on, are hidden. Order follows `Order` when it is
  set, otherwise the `.cfg` order. InvokyrConfig lists its own settings too.
- **`SettingEditors`** chooses a control per entry:
  - **bool:** an on/off toggle button.
  - **float or int with an `AcceptableValueRange`:** a slider plus the value as text. Ints snap to whole numbers and
    floats round to 2 decimals.
  - **float or int without a range:** a text field. The value is committed on Enter or when the field loses focus, and
    only if it parses.
  - **enum, or a string with an `AcceptableValueList`:** a button that cycles through the allowed values.
  - **other strings:** a text field, committed on Enter or blur.
  - **`ReadOnly`:** the value is shown, but can't be edited.
  - Each row has a **Reset** button that sets the entry back to `DefaultValue`. Hovering a row shows its description.
  - Values are written through `BoxedValue`. A value the entry rejects (throws, or is clamped by its acceptable
    values) shows the stored value again.
- **`ConfigWriter`** marks a `ConfigFile` dirty on every change. While the panel is open it saves dirty files once a
  second, and it saves them again on close. Each file's `SaveOnConfigSet` is turned off while the panel is open, so a
  slider drag doesn't rewrite the file every frame, and restored on close.
- **`ConfigPanelController`** (MonoBehaviour) is modelled on `AdminMenuController`:
  - It opens and closes the panel on the hotkey, on the Mods button, on Escape and on its own Close button.
  - While the panel is open, the cursor is freed, the uGUI `EventSystem` is turned off, and `InputsManager` is
    disabled if one exists (there is none on the title screen). All three are restored on close.
  - The panel has a list of plugins on the left and the selected plugin's settings on the right, with scrolling.
  - Every frame is wrapped in try/catch with log-once. An exception closes the panel and restores input.
- **`MenuButtonInjector`** (MonoBehaviour) checks every 0.25 s for an active `MainMenu` or `PauseMenu`
  (`FindObjectOfType`). The first time it sees one in a scene, it:
  1. clones the `optionsButton` GameObject as a sibling placed directly after it;
  2. sets the clone's TMP text to "Mods";
  3. clears the clone's click listeners (`Button.onClick.RemoveAllListeners()`, plus the `VHSButton` events) and adds
     one that opens the panel;
  4. asks the pause menu's layout rebuilder (`contentLayoutGroupRebuilder`) to re-lay the menu, if one is there.

  The injector forgets a menu after a scene change, so the button is added again. If any step throws, it logs once and
  stops trying for that menu type. The hotkey still works.

### Opening from the pause menu

The pause menu stays open underneath the panel. Because the `EventSystem` is off, its buttons can't be clicked. Closing
the panel returns the player to the pause menu with its input restored. Escape closes only the panel. It must not also
close the pause menu. The Admin Menu has no code for this (it simply closes on the press; `Hotkey.EscapeHeld()` is
unused), so it is new work here. One way is to keep the game's input off until Escape is released, then restore it.
How to do it is the builder's call. Test it in the game.

### Admin Menu follow-up (Admin Menu 0.1.2, approved by the user)

The Admin Menu's `Spending.Players` entry is a machine-written list of SteamID64:allow/block pairs. The mod reads it
once at load and rewrites it from the menu. Editing it as text would be overwritten, and Reset would erase every
saved rule. Admin Menu 0.1.2 makes two changes:

- It adds `Browsable` (and the other standard fields) to its `ConfigurationManagerAttributes`.
- It marks `Spending.Players` as `Browsable = false`.

The Admin Menu's other settings are already read live, so they need no change. Team Upgrades' `Shared.*` entries
are real player choices, read on every use, so they are shown as they are.

### Living with the Admin Menu

Both overlays take over the cursor, the `EventSystem` and `InputsManager`, so only one may be open at a time:

- InvokyrConfig won't open while the Admin Menu is open.
- It closes itself the moment the Admin Menu opens.

It detects the Admin Menu without a compile-time reference: it finds the `InvokyrAdminMenu.AdminMenuController`
component and reads its private `_open` field by reflection, then caches the lookup. If the Admin Menu isn't
installed or the lookup fails, InvokyrConfig assumes it is closed.

## Builder's calls

- Panel size, spacing and colours (start from the Admin Menu's `Theme`).
- Exactly how the clone's listeners are cleared and its navigation links (`Selectable.navigation`) are fixed. The
  requirement is that clicking Mods never also triggers Options.
- Hotkey settings stored as strings (Admin Menu `ToggleKey`, Minimap `MapKey`) are edited as text in 1.0.0. A
  press-a-key capture can come later.
- Where to find the pause menu's rebuild call, if `contentLayoutGroupRebuilder` isn't enough.

## Tests

- **Pure logic, unit-tested without the game:** the registry's grouping, ordering and filtering over fake
  `ConfigFile`s, the editor choice per type, parse and clamp rules, and the writer's dirty and flush behaviour.
- **In the game, via HML Dev:**
  - The Mods button appears and works in the title and pause menus, and Options still opens Options.
  - The flashlight brightness change is visible live.
  - Values persist across a restart.
  - Escape and Close return the player to the pause menu with working input.
  - No errors appear in `ErrorLog.log`.
