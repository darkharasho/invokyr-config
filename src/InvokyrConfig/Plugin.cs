using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace InvokyrConfig
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BasePlugin
    {
        public const string PluginGuid = "com.darkharasho.invokyr.config";
        public const string PluginName = "InvokyrConfig";
        public const string PluginVersion = "1.0.0";

        internal static new ManualLogSource Log;
        internal static ConfigEntry<string> ToggleKey;
        internal static ConfigEntry<bool> ShowMenuButtons;
        internal static ConfigEntry<bool> ShowAdvanced;

        public override void Load()
        {
            Log = base.Log;

            ToggleKey = Config.Bind("General", "ToggleKey", "F2", new ConfigDescription(
                "Opens and closes the mod settings panel (a Unity KeyCode, optionally with modifiers: F2 or F2 + LeftControl).",
                null, new ConfigurationManagerAttributes { DispName = "Toggle key", Order = 3 }));
            ShowMenuButtons = Config.Bind("General", "ShowMenuButtons", true, new ConfigDescription(
                "Adds a Mods button under Options in the title menu and the pause menu.",
                null, new ConfigurationManagerAttributes { DispName = "Mods button in menus", Order = 2 }));
            ShowAdvanced = Config.Bind("General", "ShowAdvanced", false, new ConfigDescription(
                "Also lists settings that mods mark as advanced.",
                null, new ConfigurationManagerAttributes { DispName = "Show advanced settings", Order = 1 }));

            AddComponent<ConfigPanelController>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded; press {ToggleKey.Value} or use Mods in the menu");
        }
    }
}
