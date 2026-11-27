# InvokyrConfig Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A BepInEx 6 IL2CPP plugin for Invokyr that lists every loaded plugin's config entries in an overlay panel
and edits them live. It opens with a hotkey (F2) or a "Mods" button in the title and pause menus. A small Admin Menu
release (0.1.2) goes with it.

**Architecture:**
- **Pure logic, unit-tested against real `BepInEx.Core`:** the registry, value rules, the batched writer and the
  cross-mod "open" flag.
- **Unity-facing code:** two MonoBehaviours, `ConfigPanelController` (the overlay) and `MenuButtonInjector` (polls for
  the game's menus and clones the Options button).
- **Drawing:** done with the Admin Menu's label-only IMGUI toolkit, copied in. The plugin patches no game methods.

**Tech Stack:** C# net6.0, BepInEx 6.0.0-be.755 IL2CPP, Il2CppInterop, Unity 6000.3 interop assemblies (`vhs.dll`,
`UnityEngine.UI`, `Unity.TextMeshPro`), and xunit on net8.0.

**Spec:** `docs/superpowers/specs/2026-11-26-invokyr-config-design.md`

## Global Constraints

- **No Harmony patches on game methods.** Everything hooks the game by polling from our own MonoBehaviours.
- **IMGUI:** use only `GUI.Label` and `GUI.DrawTexture`. Never use `GUILayout`, `GUI.Button`, `GUI.TextField`, sliders
  or `BeginGroup`. Use `new GUIStyle()`, never `new GUIStyle(GUIStyle)`.
- **Failures:** every Unity callback body is wrapped in try/catch with log-once. A failure closes the panel and
  restores input. A failing injector disables itself for that menu type.
- **Versions and ids:** the plugin GUID is `com.darkharasho.invokyr.config`, the name `InvokyrConfig`, and the first
  version `1.0.0`. The version must match in `Plugin.cs`, the csproj, `thunderstore/manifest.json` and `CHANGELOG.md`.
- **Settings:**
  - `General.ToggleKey` = `"F2"`
  - `General.ShowMenuButtons` = `true`
  - `General.ShowAdvanced` = `false`
- **Public text:** GitHub-facing text uses `darkharasho` only. Never include the owner's real name.
- **Heavy commands:** run builds and tests with `nice -n 19 taskset -c 0-3`.
- **Installing:** use `hml install nexus-invokyr <zip> --profile Dev`. Never copy files into game folders. Don't
  launch the game unless asked.
- **Releases:** GitHub plus the HML catalog only (the `hml-mod` topic), with no Thunderstore publishing.

## Review Focus

1. A mod whose plugin `Instance` or `Config` is null, or whose entry has an exotic type (`Vector3`, `Color`,
   `KeyboardShortcut`). Expected: the plugin or entry is listed read-only as `ToString()` text, and nothing throws.
   Covered by a test in Task 2.
2. A string typed into an int or float field that doesn't parse, or is out of range. Expected: the stored value is
   unchanged, the field goes back to it on blur, and nothing is saved. Covered by a test in Task 3.
3. Escape pressed in the pause menu while the panel is open. Expected: only the panel closes, and the game's input
   comes back after Escape is released. Checked in the game in Task 8.
4. A scene change (title to game, or game to title) while the panel is open, or after a button was injected.
   Expected: the panel closes cleanly and the button is injected into the new menu once. Checked in the game in
   Task 8.
5. The Admin Menu open and F2 pressed, or the reverse. Expected: the second overlay refuses to open. Covered by a
   test of `ExternalFlag` in Task 4, and checked in the game in Task 8.

---

### Task 1: Scaffold and empty plugin that builds and packages

**Files:**
- Create via `~/.cache/invokyr/scaffold.sh invokyr-config InvokyrConfig`: `.gitignore`, `scripts/*`, `.github/workflows/release.yml`,
  `src/InvokyrConfig/{InvokyrConfig.csproj,ConfigurationManagerAttributes.cs}`
- Create: `src/InvokyrConfig/Plugin.cs`, `thunderstore/{manifest.json,README.md,icon.svg,icon.png}`, `CHANGELOG.md`,
  `hml.json`, `CLAUDE.md`, `lib/refs/*` (via `scripts/update-refs.sh`)
- Modify: `src/InvokyrConfig/InvokyrConfig.csproj`, adding `<Reference>` for `Unity.TextMeshPro`, `UnityEngine.UIModule`
  and `UnityEngine.InputLegacyModule` (the last only if the build needs it). Drop `Mirror`.

- [ ] Run the scaffold, then `scripts/update-refs.sh`. Expected: `lib/refs` contains `Unity.TextMeshPro.dll`.
- [ ] `Plugin : BasePlugin`:
  - `PluginGuid`, `PluginName` and `PluginVersion = "1.0.0"`;
  - static `Log`, `ToggleKey` (string), `ShowMenuButtons` (bool) and `ShowAdvanced` (bool), bound with
    `ConfigurationManagerAttributes { DispName, Order }`;
  - `Load()` logs "InvokyrConfig 1.0.0 loaded; press F2 or use Mods in the menu". The components are added in Task 5.
- [ ] Write the manifest (depends on `BepInEx-BepInExPack_IL2CPP-6.0.788`), README, CHANGELOG `## 1.0.0`, and
  `hml.json` (id `config`, game `nexus-invokyr`, dependency `nexus:invokyr/6/13`). Copy the shapes from
  invokyr-admin-menu.
- [ ] Draw a 256×256 icon (gear on the Admin Menu's dark and gold palette) as `icon.svg` and render it to `icon.png`.
- [ ] Run `nice -n 19 taskset -c 0-3 scripts/package.sh`. Expected: `dist/darkharasho-InvokyrConfig-1.0.0.zip`.
- [ ] Commit: "chore: scaffold InvokyrConfig plugin".

### Task 2: SettingsRegistry

**Files:**
- Create: `src/InvokyrConfig/SettingsRegistry.cs`, `src/InvokyrConfig/SettingAttributes.cs`
- Test: `tests/InvokyrConfig.Tests/InvokyrConfig.Tests.csproj` (net8.0, xunit, `RollForward=Major`, compiling the
  pure `src` files with `<Compile Include … Link>`, and referencing `BepInEx.Core` from
  `$(HOME)/.cache/invokyr/core/BepInEx.Core.dll`), `tests/InvokyrConfig.Tests/SettingsRegistryTests.cs`

**Interfaces (Produces):**
```csharp
internal sealed class PluginSource { string Guid; string Name; string Version; ConfigFile Config; } // ctor with all four
internal sealed class Setting {
    ConfigEntryBase Entry; ConfigFile File; string Section; string Key;
    string DisplayName;   // DispName ?? Key
    string Description;   // Entry.Description?.Description ?? ""
    int Order;            // attribute Order ?? 0
    bool ReadOnly;        // attribute ReadOnly == true || type unsupported (SettingValues.IsSupported false)
    bool Advanced; }
internal sealed class SectionGroup { string Name; List<Setting> Settings; }
internal sealed class PluginGroup  { string Guid; string Name; string Version; ConfigFile File; List<SectionGroup> Sections; }
internal static class SettingsRegistry {
    List<PluginGroup> Build(IEnumerable<PluginSource> sources, bool showAdvanced);
    IEnumerable<PluginSource> FromChainloader(); // IL2CPPChainloader.Instance.Plugins; skips null Instance/Config; not unit-tested
}
internal readonly struct SettingAttributes {
    string DispName; int? Order; bool? Browsable; bool? ReadOnly; bool? IsAdvanced;
    static SettingAttributes Read(ConfigDescription d);
}
```
- **Attribute reading:** `SettingAttributes.Read` scans `d.Tags` for any object with public fields or properties of
  those names, by reflection, because each mod has its own copy of the class.
- **Ordering:**
  - Plugins are sorted by Name, ignoring case. Plugins with no visible entries are dropped.
  - Sections appear in the order they are first seen in the file.
  - Within a section, entries are sorted by `Order` descending, then by file order.
- **Filtering:** `Browsable == false` is always hidden. `IsAdvanced == true` is hidden unless `showAdvanced` is on.

- [ ] Write the tests. Each builds a real `ConfigFile(tempPath, false)`.
  - `GroupsByPluginThenSectionInFileOrder`: two sources named "b" and "A" give `["A","b"]`. Sections "Beam" then
    "General" stay in that order.
  - `SortsByOrderDescendingThenFileOrder`: Orders 3, null, 1, null give 3, 1, then the two nulls in file order. A null
    `Order` counts as 0, so it sorts between 1 and -1. Assert that exact order.
  - `HidesBrowsableFalse`: an entry tagged with an anonymous-class-like object `new FakeAttrs { Browsable = false }`
    is missing.
  - `AdvancedOnlyWhenAsked`: an entry is hidden when `showAdvanced` is false and shown when it is true.
  - `DropsPluginWithNothingVisible`.
  - `UsesDispNameAndDescription`: the entry gets `DisplayName == "Brightness"` and the description text.
  - `UnsupportedTypeIsReadOnly`: a `ConfigFile.Bind("S","V", new System.Version(1,2))` entry, or another type
    BepInEx's TomlTypeConverter accepts that the editors don't, is `ReadOnly`.
- [ ] Run `nice -n 19 taskset -c 0-3 dotnet test tests/InvokyrConfig.Tests`. Expected: FAIL, because the types aren't
  defined yet.
- [ ] Implement it. `ReadOnly` uses `SettingValues.IsSupported` from Task 3. Implement Task 3's `IsSupported` first, or
  stub it as `bool IsSupported(Type t)` returning true for bool, int, float, double, long, string and enums, and
  finish the rest in Task 3.
- [ ] Run the tests. Expected: PASS. Commit: "feat: settings registry".

### Task 3: SettingValues (editor choice, parse, format, set)

**Files:** Create `src/InvokyrConfig/SettingValues.cs`. Test: `tests/InvokyrConfig.Tests/SettingValuesTests.cs`

**Interfaces (Produces):**
```csharp
internal enum EditorKind { Toggle, Slider, Number, Choice, Text, ReadOnly }
internal static class SettingValues {
    bool IsSupported(Type t);
    EditorKind Choose(Setting s);              // ReadOnly if s.ReadOnly
    bool TryRange(AcceptableValueBase a, out float min, out float max); // AcceptableValueRange<int|float|double|long> via reflection on MinValue/MaxValue
    object[] Choices(Setting s);               // enum values, or AcceptableValueList.AcceptableValues
    object Next(object current, object[] choices); // wraps; unknown current -> choices[0]
    string Format(object value);               // float/double "0.##" invariant; bool "On"/"Off"; else ToString()
    bool TryParse(Type t, string text, out object value); // invariant culture, trims
    object FromSlider(Type t, float raw, float min, float max); // clamp; int/long Math.Round; float/double round 2dp
    bool TrySet(ConfigEntryBase e, object value); // BoxedValue = value inside try; true iff no throw and Equals(e.BoxedValue, value)
    void Reset(ConfigEntryBase e);             // BoxedValue = DefaultValue
}
```
`Choose` rules:
- bool → Toggle;
- int, long, float or double with a range → Slider; without a range → Number;
- an enum, or any type with an `AcceptableValueList` → Choice;
- string → Text;
- anything else → ReadOnly.

- [ ] Write the tests:
  - `BoolIsToggle`, `RangedFloatIsSlider` (`AcceptableValueRange<float>(0.5f,4f)` gives `TryRange` = (0.5, 4)),
    `RangedIntIsSlider`, `UnrangedIntIsNumber`, `StringListIsChoice`, `EnumIsChoice`, `PlainStringIsText`.
  - `SliderRounds`: for an int range of 6 to 16, raw 9.6 gives 10. For a float, 1.23456 gives 1.23. Raw 99 gives the
    max.
  - `ParseRejectsGarbage`: `TryParse(typeof(int),"abc")` is false, `TryParse(typeof(float),"1,5")` is false (invariant
    culture), and `" 2.5 "` gives 2.5f.
  - `TrySetOutOfRangeReportsFalse`: setting 99f on an entry with range (0.5, 4) leaves the entry clamped to 4 and
    returns false.
  - `TrySetFiresSettingChanged`: a handler count of 1 after a valid set.
  - `ResetRestoresDefault`.
  - `NextWraps`.
  - `FormatFloat`: 2f gives "2" and 1.5f gives "1.5".
- [ ] Run. Expected: FAIL. Implement. Run. Expected: PASS. Commit: "feat: setting value rules".

### Task 4: ConfigWriter and ExternalFlag

**Files:** Create `src/InvokyrConfig/ConfigWriter.cs`, `src/InvokyrConfig/OverlayState.cs`. Test: `tests/InvokyrConfig.Tests/ConfigWriterTests.cs`, `ExternalFlagTests.cs`

**Interfaces (Produces):**
```csharp
internal sealed class ConfigWriter {
    ConfigWriter(Action<ConfigFile> save = null); // default f => f.Save()
    void Begin(IEnumerable<ConfigFile> files);    // remember each SaveOnConfigSet, set false
    void MarkDirty(ConfigFile f);
    void Tick(float now);                         // save dirty files if now - lastFlush >= 1f
    void End();                                   // save dirty, restore SaveOnConfigSet; safe to call twice
}
public static class OverlayState { public static bool IsOpen { get; internal set; } } // public: Admin Menu reads it by reflection
internal sealed class ExternalFlag {
    ExternalFlag(string typeFullName, string propertyName); // public static bool property
    bool Get(); // first call searches AppDomain assemblies and caches PropertyInfo (or the miss); any failure -> false
}
```
- [ ] Write the tests:
  - `NoSaveUntilASecond`: `Tick(0.5f)` saves nothing, and `Tick(1.0f)` saves once.
  - `EndFlushesAndRestores`: `SaveOnConfigSet` is back to true and the dirty file is saved once.
  - `CleanFilesNotSaved`.
  - `EndTwiceIsSafe`.
  - `ExternalFlagReadsStatic`: a test-local `public static class FakeOverlay { public static bool IsOpen; }` property is
    read as true or false.
  - `ExternalFlagMissingTypeIsFalse`.
- [ ] Run. Expected: FAIL. Implement. Run. Expected: PASS. Commit: "feat: batched config writer and cross-mod overlay flag".

### Task 5: ConfigPanelController (the overlay)

**Files:**
- Create: `src/InvokyrConfig/ConfigPanelController.cs`, `src/InvokyrConfig/SettingRows.cs`
- Copy from invokyr-admin-menu, changing only the namespace: `Ui.cs`, `Theme.cs`, `Hotkey.cs`, `Shortcut.cs`
  (`Shortcut.Parse` fallback F2)
- Modify: `Plugin.cs` (`ClassInjector` registration is automatic via `AddComponent<ConfigPanelController>()`)

**Interfaces:**
- Consumes: `SettingsRegistry.Build/FromChainloader`, `SettingValues.*`, `ConfigWriter`, `OverlayState`, and
  `ExternalFlag("InvokyrAdminMenu.AdminMenuController","IsOpen")`.
- Produces: `ConfigPanelController.Instance` (static) and `void Open()`. `MenuButtonInjector` calls these.

Behaviour, modelled on `AdminMenuController`:
- **Update:** `MenuInput.Poll` while the panel is open, then the hotkey toggle.
- **Escape:** pressing Escape closes the panel. Restoring input is deferred until `!Hotkey.EscapeHeld()`
  (`_restorePending`).
- **Open():**
  - It refuses if the Admin Menu flag is set, and shows a 3 s toast "Close the admin menu first".
  - It rebuilds the registry and calls `ConfigWriter.Begin` on all plugin files.
  - It sets `OverlayState.IsOpen = true`, disables `InputsManager.Instance` (null-safe), disables `EventSystem.current`
    and frees the cursor.
- **Close():**
  - It calls `ConfigWriter.End()` and sets `OverlayState.IsOpen = false`.
  - It restores input now, or once Escape is released.
- **OnDestroy, or a scene change while open:** close and restore immediately.
- **OnGUI:**
  - On `EventType.KeyDown`, call `MenuInput.Key(Event.current)`.
  - On Layout, take a snapshot.
  - On Repaint, draw at a 1080p reference scale. The window is 1100×760. On the left is a 280-wide list of plugin
    buttons (`Theme.TabOn` for the selected one). On the right is a scrolling `Pane` with a `Heading` for each section
    and a `Card` row for each setting, then the description as `MutedText` underneath.
  - The header has "MOD SETTINGS", a Close button, and a muted line with the plugin name and version.
- **`SettingRows.Draw(Pane, Setting, ConfigWriter)`:** draws one row by `EditorKind`. Reset is right-aligned at 90 wide.
  - **Toggle:** a 110-wide "On"/"Off" button.
  - **Slider:** 300 wide, plus a 70-wide value label.
  - **Number and Text:** a 260-wide `TextField` with a draft string per entry. A commit happens when focus leaves the
    field's id (Enter, or a click elsewhere). If `TryParse` or `TrySet` fails, the draft is dropped and the row shows
    the stored value again.
  - **Choice:** a 240-wide button that cycles with `Next`.
  - **ReadOnly:** a right-aligned value label.
  - Every successful set calls `writer.MarkDirty(setting.File)`.
- **Writer:** call `ConfigWriter.Tick(Time.unscaledTime)` from Update while the panel is open.

- [ ] Implement it. Build with `nice -n 19 taskset -c 0-3 dotnet build src/InvokyrConfig -c Release -m:2`. Expected:
  0 errors. Existing tests still pass.
- [ ] Commit: "feat: settings overlay panel".

### Task 6: MenuButtonInjector

**Files:** Create `src/InvokyrConfig/MenuButtonInjector.cs`. Modify `Plugin.cs` (`AddComponent<MenuButtonInjector>()`).

**Interfaces:** Consumes `ConfigPanelController.Instance.Open()`, `Plugin.ShowMenuButtons`.

The class runs a check every 0.25 s (unscaled time) while `ShowMenuButtons` is on. It handles two menu types: `MainMenu`
(`_200_Dev.UI.StartMenu.Menus`) and `PauseMenu` (`_200_Dev.UI.StartMenu`). For each one:
1. **Find the menu:** `Object.FindObjectOfType<T>()`. If it's null or not `isActiveAndEnabled`, skip it.
2. **Check for an existing button:** if a child named `InvokyrConfig_ModsButton` already sits under
   `optionsButton.transform.parent`, skip.
3. **Clone:** `Object.Instantiate(optionsButton.gameObject, parent)`, rename it to `InvokyrConfig_ModsButton`, and
   `SetSiblingIndex(options.GetSiblingIndex() + 1)`.
4. **Remove localization:** destroy (`DestroyImmediate`) every component in the clone's children whose
   `GetIl2CppType().Name` contains "Locali" (I2 Localization). Otherwise "Mods" gets re-translated back to "Options".
5. **Rewire clicks:** for every `Button` in the children, assign a new `Button.ButtonClickedEvent()` to `onClick` (this
   drops serialized listeners), then `AddListener(DelegateSupport.ConvertDelegate<UnityAction>(new Action(OpenPanel)))`.
   On each `VHSButton`, also set `OnMouseClick = null`.
6. **Set the text:** set every `TextMeshProUGUI` in the children to "Mods".
7. **Re-lay the menu:** for `PauseMenu`, call `contentLayoutGroupRebuilder?.ReloadLayout()`. For both, call
   `LayoutRebuilder.MarkLayoutForRebuild(parent as RectTransform)`.

On failure, log once per type with the exception, set that type's `disabled = true`, and destroy the half-built clone.
When `ShowMenuButtons` is off, destroy any existing clones.

- [ ] Implement it. Build. Expected: 0 errors. Commit: "feat: Mods button in title and pause menus".

### Task 7: Admin Menu 0.1.2 (in `~/Documents/GitHub/invokyr-admin-menu`)

**Files:** Modify:
- `src/InvokyrAdminMenu/ConfigurationManagerAttributes.cs`: add `public bool? Browsable; public bool? ReadOnly; public bool? IsAdvanced;`.
- `Plugin.cs`: `Spending.Players` gets `Browsable = false`. Version 0.1.2.
- `AdminMenuController.cs`: add `public static bool IsOpen { get; private set; }`, set in Open and Close. `Open()`
  refuses if `ExternalFlag("InvokyrConfig.OverlayState","IsOpen")` is true, and shows the toast "Close mod settings
  first".
- Create `src/InvokyrAdminMenu/ExternalFlag.cs` (a copy of Task 4's).
- Also bump the csproj, `thunderstore/manifest.json` and `CHANGELOG.md` (`## 0.1.2`).

- [ ] Run `dotnet test tests/InvokyrAdminMenu.Tests` and `scripts/package.sh`. Expected: PASS, and the 0.1.2 zip is built.
- [ ] Commit in that repo: "feat: hide the spending list from settings menus; don't open over InvokyrConfig (0.1.2)".

### Task 8: Install and verify in the game

- [ ] `hml install nexus-invokyr dist/darkharasho-InvokyrConfig-1.0.0.zip --profile Dev`, and the same for the Admin
  Menu 0.1.2 zip. Check with `hml mods nexus-invokyr --profile Dev --json`.
- [ ] Ask the owner to launch, or launch if they asked, then check:
  - **Title menu:** Mods appears under Options. Options still opens Options. Mods opens the panel.
  - **Hotkey:** F2 toggles the panel on the title screen and in a game.
  - **Pause menu:** Mods appears. Escape closes only the panel, and the pause menu then works with the mouse.
  - **Live changes:** flashlight width and range change while dragging. Brightness applies when the flashlight is
    next switched on. Minimap size changes live.
  - **Persistence:** after a restart, the values are still there.
  - **Admin Menu clash:** with the Admin Menu open (F1, as host), F2 shows a toast. With the panel open, F1 shows a
    toast.
  - **Scene change:** going from the title to a game and back re-injects the button once.
  - **Hidden settings:** the Admin Menu's spending list doesn't appear.
  - **Clean log:** `BepInEx/ErrorLog.log` has no InvokyrConfig errors.
- [ ] Release only when the owner approves:
  - create `darkharasho/invokyr-config` with the `hml-mod` topic, push, and tag `v1.0.0`;
  - tag Admin Menu `v0.1.2`;
  - show `sai_watch_github_run` for each.
