using Xunit;

namespace InvokyrConfig.Tests
{
    public class RestoreGateTests
    {
        [Fact]
        public void WaitsForEscapeReleaseThenOneFrame()
        {
            var gate = new RestoreGate();
            gate.Arm();

            Assert.True(gate.Pending);
            Assert.False(gate.Tick(escapeHeld: true, mouseHeld: false));
            Assert.False(gate.Tick(escapeHeld: false, mouseHeld: false));
            Assert.True(gate.Tick(escapeHeld: false, mouseHeld: false));
            Assert.False(gate.Pending);
        }

        [Fact]
        public void WaitsForMouseRelease()
        {
            var gate = new RestoreGate();
            gate.Arm();

            Assert.False(gate.Tick(false, true));
            Assert.False(gate.Tick(false, true));
            Assert.False(gate.Tick(false, false));
            Assert.True(gate.Tick(false, false));
        }

        [Fact]
        public void PressAgainRestartsTheWait()
        {
            var gate = new RestoreGate();
            gate.Arm();

            Assert.False(gate.Tick(false, false));
            Assert.False(gate.Tick(false, true));
            Assert.False(gate.Tick(false, false));
            Assert.True(gate.Tick(false, false));
        }

        [Fact]
        public void IdleGateNeverFires()
        {
            var gate = new RestoreGate();
            Assert.False(gate.Pending);
            Assert.False(gate.Tick(false, false));
        }
    }
}
