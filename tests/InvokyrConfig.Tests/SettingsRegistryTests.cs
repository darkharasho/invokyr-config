using System.Linq;
using Xunit;

namespace InvokyrConfig.Tests
{
    public class SettingsRegistryTests
    {
        private static PluginSource Source(string name, BepInEx.Configuration.ConfigFile config) =>
            new PluginSource("guid." + name, name, "1.0.0", config);

        [Fact]
        public void GroupsByPluginThenSectionInFileOrder()
        {
            var b = TestConfig.New();
            b.Bind("Beam", "Width", 1f, TestConfig.Desc("w"));
            b.Bind("General", "Enabled", true, TestConfig.Desc("e"));
            b.Bind("Beam", "Range", 1f, TestConfig.Desc("r"));
            var a = TestConfig.New();
            a.Bind("General", "On", true, TestConfig.Desc("o"));

            var groups = SettingsRegistry.Build(new[] { Source("b", b), Source("A", a) }, false);

            Assert.Equal(new[] { "A", "b" }, groups.Select(g => g.Name));
            Assert.Equal(new[] { "Beam", "General" }, groups[1].Sections.Select(s => s.Name));
            Assert.Equal(new[] { "Width", "Range" }, groups[1].Sections[0].Settings.Select(s => s.Key));
            Assert.Same(b, groups[1].File);
            Assert.Equal("1.0.0", groups[1].Version);
        }

        [Fact]
        public void SortsByOrderDescendingThenFileOrder()
        {
            var c = TestConfig.New();
            c.Bind("S", "NullFirst", 0, TestConfig.Desc("", new FakeAttrs()));
            c.Bind("S", "Three", 0, TestConfig.Desc("", new FakeAttrs { Order = 3 }));
            c.Bind("S", "MinusOne", 0, TestConfig.Desc("", new FakeAttrs { Order = -1 }));
            c.Bind("S", "One", 0, TestConfig.Desc("", new FakeAttrs { Order = 1 }));
            c.Bind("S", "NullSecond", 0, TestConfig.Desc(""));

            var keys = SettingsRegistry.Build(new[] { Source("p", c) }, false)[0].Sections[0].Settings.Select(s => s.Key);

            Assert.Equal(new[] { "Three", "One", "NullFirst", "NullSecond", "MinusOne" }, keys);
        }

        [Fact]
        public void HidesBrowsableFalse()
        {
            var c = TestConfig.New();
            c.Bind("S", "Shown", 0, TestConfig.Desc(""));
            c.Bind("S", "Hidden", "", TestConfig.Desc("", new FakeAttrs { Browsable = false }));

            var keys = SettingsRegistry.Build(new[] { Source("p", c) }, true)[0].Sections[0].Settings.Select(s => s.Key);

            Assert.Equal(new[] { "Shown" }, keys);
        }

        [Fact]
        public void AdvancedOnlyWhenAsked()
        {
            var c = TestConfig.New();
            c.Bind("S", "Plain", 0, TestConfig.Desc(""));
            c.Bind("S", "Deep", 0, TestConfig.Desc("", new FakeAttrs { IsAdvanced = true }));

            Assert.Single(SettingsRegistry.Build(new[] { Source("p", c) }, false)[0].Sections[0].Settings);
            var shown = SettingsRegistry.Build(new[] { Source("p", c) }, true)[0].Sections[0].Settings;
            Assert.Equal(2, shown.Count);
            Assert.True(shown.Single(s => s.Key == "Deep").Advanced);
        }

        [Fact]
        public void DropsPluginWithNothingVisible()
        {
            var hidden = TestConfig.New();
            hidden.Bind("S", "Only", "", TestConfig.Desc("", new FakeAttrs { Browsable = false }));
            var empty = TestConfig.New();
            var shown = TestConfig.New();
            shown.Bind("S", "V", 1, TestConfig.Desc(""));

            var groups = SettingsRegistry.Build(new[] { Source("hidden", hidden), Source("empty", empty), Source("shown", shown) }, true);

            Assert.Equal(new[] { "shown" }, groups.Select(g => g.Name));
        }

        [Fact]
        public void UsesDispNameAndDescription()
        {
            var c = TestConfig.New();
            c.Bind("Beam", "BrightnessMultiplier", 2f, TestConfig.Desc("How bright. Applies the next time it's switched on.", new FakeAttrs { DispName = "Brightness" }));
            c.Bind("Beam", "Plain", 1f, TestConfig.Desc("p"));

            var settings = SettingsRegistry.Build(new[] { Source("p", c) }, false)[0].Sections[0].Settings;

            Assert.Equal("Brightness", settings[0].DisplayName);
            Assert.Equal("How bright. Applies the next time it's switched on.", settings[0].Description);
            Assert.Equal("Plain", settings[1].DisplayName);
            Assert.Same(c, settings[0].File);
            Assert.Equal("Beam", settings[0].Section);
        }

        [Fact]
        public void ReadOnlyAttributeAndUnsupportedTypesAreReadOnly()
        {
            var c = TestConfig.New();
            c.Bind("S", "Locked", 1, TestConfig.Desc("", new FakeAttrs { ReadOnly = true }));
            c.Bind("S", "Money", 1.5m, TestConfig.Desc(""));
            c.Bind("S", "Normal", 1, TestConfig.Desc(""));

            var settings = SettingsRegistry.Build(new[] { Source("p", c) }, false)[0].Sections[0].Settings;

            Assert.True(settings.Single(s => s.Key == "Locked").ReadOnly);
            Assert.True(settings.Single(s => s.Key == "Money").ReadOnly);
            Assert.False(settings.Single(s => s.Key == "Normal").ReadOnly);
        }

        [Fact]
        public void SkipsSourcesWithoutConfig()
        {
            var c = TestConfig.New();
            c.Bind("S", "V", 1, TestConfig.Desc(""));

            var groups = SettingsRegistry.Build(new[] { Source("none", null), Source("p", c), null }, false);

            Assert.Equal(new[] { "p" }, groups.Select(g => g.Name));
        }
    }
}
