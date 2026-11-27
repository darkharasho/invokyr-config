using System.Linq;
using BepInEx.Configuration;
using Xunit;

namespace InvokyrConfig.Tests
{
    public class SettingValuesTests
    {
        private enum Mode { Low, Mid, High }

        private static Setting Wrap(ConfigEntryBase entry, bool readOnly = false) =>
            new Setting { Entry = entry, File = entry.ConfigFile, Key = entry.Definition.Key, ReadOnly = readOnly };

        [Fact]
        public void ChoosesEditorByType()
        {
            var c = TestConfig.New();
            Assert.Equal(EditorKind.Toggle, SettingValues.Choose(Wrap(c.Bind("S", "B", true, TestConfig.Desc("")))));
            Assert.Equal(EditorKind.Slider, SettingValues.Choose(Wrap(c.Bind("S", "F", 2f, TestConfig.Desc("", null, new AcceptableValueRange<float>(0.5f, 4f))))));
            Assert.Equal(EditorKind.Slider, SettingValues.Choose(Wrap(c.Bind("S", "I", 12, TestConfig.Desc("", null, new AcceptableValueRange<int>(6, 16))))));
            Assert.Equal(EditorKind.Number, SettingValues.Choose(Wrap(c.Bind("S", "N", 3, TestConfig.Desc("")))));
            Assert.Equal(EditorKind.Number, SettingValues.Choose(Wrap(c.Bind("S", "D", 3.0, TestConfig.Desc("")))));
            Assert.Equal(EditorKind.Choice, SettingValues.Choose(Wrap(c.Bind("S", "L", "a", TestConfig.Desc("", null, new AcceptableValueList<string>("a", "b"))))));
            Assert.Equal(EditorKind.Choice, SettingValues.Choose(Wrap(c.Bind("S", "E", Mode.Mid, TestConfig.Desc("")))));
            Assert.Equal(EditorKind.Text, SettingValues.Choose(Wrap(c.Bind("S", "T", "F1", TestConfig.Desc("")))));
            Assert.Equal(EditorKind.ReadOnly, SettingValues.Choose(Wrap(c.Bind("S", "R", true, TestConfig.Desc("")), readOnly: true)));
        }

        [Fact]
        public void ReadsRanges()
        {
            Assert.True(SettingValues.TryRange(new AcceptableValueRange<float>(0.5f, 4f), out var min, out var max));
            Assert.Equal((0.5f, 4f), (min, max));
            Assert.True(SettingValues.TryRange(new AcceptableValueRange<int>(6, 16), out min, out max));
            Assert.Equal((6f, 16f), (min, max));
            Assert.False(SettingValues.TryRange(new AcceptableValueList<string>("a"), out _, out _));
            Assert.False(SettingValues.TryRange(null, out _, out _));
        }

        [Fact]
        public void SliderRounds()
        {
            Assert.Equal(10, SettingValues.FromSlider(typeof(int), 9.6f, 6f, 16f));
            Assert.Equal(16, SettingValues.FromSlider(typeof(int), 99f, 6f, 16f));
            Assert.Equal(1.23f, SettingValues.FromSlider(typeof(float), 1.23456f, 0f, 4f));
            Assert.Equal(4f, SettingValues.FromSlider(typeof(float), 99f, 0.5f, 4f));
            Assert.Equal(0.5f, SettingValues.FromSlider(typeof(float), -3f, 0.5f, 4f));
        }

        [Fact]
        public void ParseRejectsGarbage()
        {
            Assert.False(SettingValues.TryParse(typeof(int), "abc", out _));
            Assert.False(SettingValues.TryParse(typeof(float), "1,5", out _));
            Assert.False(SettingValues.TryParse(typeof(int), "", out _));
            Assert.True(SettingValues.TryParse(typeof(float), " 2.5 ", out var f));
            Assert.Equal(2.5f, f);
            Assert.True(SettingValues.TryParse(typeof(int), "12", out var i));
            Assert.Equal(12, i);
            Assert.True(SettingValues.TryParse(typeof(string), " Tab ", out var s));
            Assert.Equal(" Tab ", s);
        }

        [Fact]
        public void TrySetOutOfRangeReportsFalse()
        {
            var entry = TestConfig.New().Bind("S", "F", 2f, TestConfig.Desc("", null, new AcceptableValueRange<float>(0.5f, 4f)));

            Assert.False(SettingValues.TrySet(entry, 99f));
            Assert.Equal(4f, entry.Value);
            Assert.False(SettingValues.TrySet(entry, "not a float"));
            Assert.Equal(4f, entry.Value);
        }

        [Fact]
        public void TrySetFiresSettingChanged()
        {
            var entry = TestConfig.New().Bind("S", "F", 2f, TestConfig.Desc(""));
            var fired = 0;
            entry.SettingChanged += (_, __) => fired++;

            Assert.True(SettingValues.TrySet(entry, 3f));
            Assert.Equal(1, fired);
            Assert.Equal(3f, entry.Value);
        }

        [Fact]
        public void ResetRestoresDefault()
        {
            var entry = TestConfig.New().Bind("S", "I", 12, TestConfig.Desc(""));
            entry.Value = 7;

            SettingValues.Reset(entry);

            Assert.Equal(12, entry.Value);
        }

        [Fact]
        public void ChoicesAndNextWrap()
        {
            var c = TestConfig.New();
            var list = Wrap(c.Bind("S", "L", "b", TestConfig.Desc("", null, new AcceptableValueList<string>("a", "b"))));
            var e = Wrap(c.Bind("S", "E", Mode.High, TestConfig.Desc("")));

            Assert.Equal(new object[] { "a", "b" }, SettingValues.Choices(list));
            Assert.Equal(new object[] { Mode.Low, Mode.Mid, Mode.High }, SettingValues.Choices(e));
            Assert.Equal("a", SettingValues.Next("b", SettingValues.Choices(list)));
            Assert.Equal(Mode.Low, SettingValues.Next(Mode.High, SettingValues.Choices(e)));
            Assert.Equal("a", SettingValues.Next("zzz", SettingValues.Choices(list)));
        }

        [Fact]
        public void Formats()
        {
            Assert.Equal("2", SettingValues.Format(2f));
            Assert.Equal("1.5", SettingValues.Format(1.5f));
            Assert.Equal("1.23", SettingValues.Format(1.2345f));
            Assert.Equal("On", SettingValues.Format(true));
            Assert.Equal("Off", SettingValues.Format(false));
            Assert.Equal("12", SettingValues.Format(12));
            Assert.Equal("", SettingValues.Format(null));
        }
    }
}
