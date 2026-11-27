# InvokyrConfig

Change any mod's settings inside Invokyr, without editing config files or restarting.

- **Mods** button under Options in the title menu and the pause menu, or press **F2** anywhere.
- Lists every installed BepInEx mod with its settings: switches for on/off settings, sliders for numbers with a range, text boxes for the rest, and a Reset button on each.
- Changes apply right away (for mods that read their settings live) and are saved to the mod's file in `BepInEx/config`.
- Some settings say in their description when they take effect, for example flashlight brightness applies the next time the flashlight is switched on.

## Settings
- **Toggle key** (F2): opens and closes the panel.
- **Mods button in menus** (on): the Mods button in the title and pause menus.
- **Show advanced settings** (off): also lists settings a mod marks as advanced.

Requires BepInEx 6 for IL2CPP. Works with the mouse; Esc or Close returns you to where you were. It won't open while the Admin Menu is open, and vice versa.
