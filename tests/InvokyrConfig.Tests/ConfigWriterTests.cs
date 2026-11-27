using System.Collections.Generic;
using BepInEx.Configuration;
using Xunit;

namespace InvokyrConfig.Tests
{
    public class ConfigWriterTests
    {
        private readonly List<ConfigFile> _saved = new List<ConfigFile>();

        private ConfigWriter Writer() => new ConfigWriter(f => _saved.Add(f));

        [Fact]
        public void BeginTurnsOffSaveOnSet()
        {
            var a = TestConfig.New();
            var writer = Writer();

            writer.Begin(new[] { a });

            Assert.False(a.SaveOnConfigSet);
        }

        [Fact]
        public void NoSaveUntilASecond()
        {
            var a = TestConfig.New();
            var writer = Writer();
            writer.Begin(new[] { a });
            writer.Tick(0f);
            writer.MarkDirty(a);

            writer.Tick(0.5f);
            Assert.Empty(_saved);
            writer.Tick(1.0f);
            Assert.Equal(new[] { a }, _saved);
            writer.Tick(2.5f);
            Assert.Single(_saved);
        }

        [Fact]
        public void EndFlushesAndRestores()
        {
            var a = TestConfig.New();
            var b = TestConfig.New();
            b.SaveOnConfigSet = false;
            var writer = Writer();
            writer.Begin(new[] { a, b });
            writer.MarkDirty(a);

            writer.End();

            Assert.Equal(new[] { a }, _saved);
            Assert.True(a.SaveOnConfigSet);
            Assert.False(b.SaveOnConfigSet);
        }

        [Fact]
        public void CleanFilesNotSaved()
        {
            var a = TestConfig.New();
            var writer = Writer();
            writer.Begin(new[] { a });

            writer.Tick(5f);
            writer.End();

            Assert.Empty(_saved);
        }

        [Fact]
        public void EndTwiceIsSafe()
        {
            var a = TestConfig.New();
            var writer = Writer();
            writer.Begin(new[] { a });
            writer.MarkDirty(a);

            writer.End();
            writer.End();

            Assert.Single(_saved);
            Assert.True(a.SaveOnConfigSet);
        }

        [Fact]
        public void ChangesMadeByOtherCodeAreSaved()
        {
            var a = TestConfig.New();
            var entry = a.Bind("Spending", "Players", "", TestConfig.Desc(""));
            var writer = Writer();
            writer.Begin(new[] { a });

            entry.Value = "123:block";
            writer.End();

            Assert.Equal(new[] { a }, _saved);
        }

        [Fact]
        public void StopsListeningAfterEnd()
        {
            var a = TestConfig.New();
            var entry = a.Bind("S", "V", 1, TestConfig.Desc(""));
            var writer = Writer();
            writer.Begin(new[] { a });
            writer.End();

            entry.Value = 2;
            writer.Begin(System.Array.Empty<ConfigFile>());
            writer.End();

            Assert.Empty(_saved);
        }

        [Fact]
        public void SaveFailureDoesNotStopOthers()
        {
            var a = TestConfig.New();
            var b = TestConfig.New();
            var writer = new ConfigWriter(f =>
            {
                if (f == a)
                    throw new System.IO.IOException("locked");
                _saved.Add(f);
            });
            writer.Begin(new[] { a, b });
            writer.MarkDirty(a);
            writer.MarkDirty(b);

            writer.End();

            Assert.Equal(new[] { b }, _saved);
            Assert.True(a.SaveOnConfigSet);
        }
    }

    public static class FakeOverlay
    {
        public static bool IsOpen { get; set; }
    }

    public class ExternalFlagTests
    {
        [Fact]
        public void ReadsStaticProperty()
        {
            var flag = new ExternalFlag("InvokyrConfig.Tests.FakeOverlay", "IsOpen");
            FakeOverlay.IsOpen = true;
            Assert.True(flag.Get());
            FakeOverlay.IsOpen = false;
            Assert.False(flag.Get());
        }

        [Fact]
        public void MissingTypeIsFalse()
        {
            Assert.False(new ExternalFlag("Nope.Missing", "IsOpen").Get());
            Assert.False(new ExternalFlag("InvokyrConfig.Tests.FakeOverlay", "Missing").Get());
        }

        [Fact]
        public void OwnStateIsReadable()
        {
            var flag = new ExternalFlag("InvokyrConfig.OverlayState", "IsOpen");
            OverlayState.IsOpen = true;
            Assert.True(flag.Get());
            OverlayState.IsOpen = false;
            Assert.False(flag.Get());
        }
    }
}
