using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace InvokyrConfig
{
    /// <summary>A loaded plugin and its config, as handed to <see cref="SettingsRegistry.Build"/>.</summary>
    internal sealed class PluginSource
    {
        public readonly string Guid, Name, Version;
        public readonly ConfigFile Config;

        public PluginSource(string guid, string name, string version, ConfigFile config)
        {
            Guid = guid;
            Name = name;
            Version = version;
            Config = config;
        }
    }

    internal sealed class Setting
    {
        public ConfigEntryBase Entry;
        public ConfigFile File;
        public string Section, Key, DisplayName, Description;
        public int Order;
        public bool ReadOnly, Advanced;
    }

    internal sealed class SectionGroup
    {
        public string Name;
        public List<Setting> Settings = new List<Setting>();
    }

    internal sealed class PluginGroup
    {
        public string Guid, Name, Version;
        public ConfigFile File;
        public List<SectionGroup> Sections = new List<SectionGroup>();
    }

    /// <summary>Every plugin's visible settings, grouped by plugin and section, in the order the panel shows them.</summary>
    internal static class SettingsRegistry
    {
        public static List<PluginGroup> Build(IEnumerable<PluginSource> sources, bool showAdvanced)
        {
            var groups = new List<PluginGroup>();
            foreach (var source in sources)
            {
                if (source?.Config == null)
                    continue;
                var group = BuildOne(source, showAdvanced);
                if (group.Sections.Count > 0)
                    groups.Add(group);
            }
            return groups.OrderBy(g => g.Name ?? "", StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static PluginGroup BuildOne(PluginSource source, bool showAdvanced)
        {
            var group = new PluginGroup { Guid = source.Guid, Name = source.Name ?? source.Guid, Version = source.Version, File = source.Config };
            var sections = new Dictionary<string, SectionGroup>();
            var index = 0;
            var position = new Dictionary<Setting, int>();
            foreach (var entry in source.Config.Values)
            {
                if (entry?.Definition == null)
                    continue;
                var attrs = SettingAttributes.Read(entry.Description);
                if (attrs.Browsable == false)
                    continue;
                var advanced = attrs.IsAdvanced == true;
                if (advanced && !showAdvanced)
                    continue;

                var setting = new Setting
                {
                    Entry = entry,
                    File = source.Config,
                    Section = entry.Definition.Section ?? "",
                    Key = entry.Definition.Key,
                    DisplayName = string.IsNullOrEmpty(attrs.DispName) ? entry.Definition.Key : attrs.DispName,
                    Description = entry.Description?.Description ?? "",
                    Order = attrs.Order ?? 0,
                    ReadOnly = attrs.ReadOnly == true || !SettingValues.IsSupported(entry.SettingType),
                    Advanced = advanced,
                };
                position[setting] = index++;
                if (!sections.TryGetValue(setting.Section, out var section))
                {
                    section = new SectionGroup { Name = setting.Section };
                    sections[setting.Section] = section;
                    group.Sections.Add(section);
                }
                section.Settings.Add(setting);
            }
            foreach (var section in group.Sections)
                section.Settings = section.Settings.OrderByDescending(s => s.Order).ThenBy(s => position[s]).ToList();
            return group;
        }
    }
}
