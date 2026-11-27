using System.Collections.Generic;
using BepInEx.Unity.IL2CPP;

namespace InvokyrConfig
{
    /// <summary>The loaded plugins from BepInEx's IL2CPP chainloader (kept out of SettingsRegistry so tests don't need it).</summary>
    internal static class ChainloaderSource
    {
        public static IEnumerable<PluginSource> Plugins()
        {
            var list = new List<PluginSource>();
            var chainloader = IL2CPPChainloader.Instance;
            if (chainloader?.Plugins == null)
                return list;
            foreach (var info in chainloader.Plugins.Values)
            {
                if (!(info?.Instance is BasePlugin plugin) || plugin.Config == null)
                    continue;
                var meta = info.Metadata;
                list.Add(new PluginSource(meta?.GUID, meta?.Name, meta?.Version?.ToString(), plugin.Config));
            }
            return list;
        }
    }
}
