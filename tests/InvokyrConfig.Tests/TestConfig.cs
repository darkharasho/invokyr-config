using System;
using System.IO;
using BepInEx.Configuration;

namespace InvokyrConfig.Tests
{
    /// <summary>Stands in for a mod's own copy of ConfigurationManagerAttributes (read by field name).</summary>
    internal sealed class FakeAttrs
    {
        public string DispName;
        public int? Order;
        public bool? Browsable;
        public bool? ReadOnly;
        public bool? IsAdvanced;
    }

    internal static class TestConfig
    {
        public static ConfigFile New() =>
            new ConfigFile(Path.Combine(Path.GetTempPath(), "invokyrconfig-tests", Guid.NewGuid() + ".cfg"), false);

        public static ConfigDescription Desc(string text, FakeAttrs attrs = null, AcceptableValueBase acceptable = null) =>
            attrs == null ? new ConfigDescription(text, acceptable) : new ConfigDescription(text, acceptable, attrs);
    }
}
