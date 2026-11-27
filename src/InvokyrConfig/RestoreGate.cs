namespace InvokyrConfig
{
    /// <summary>
    /// Holds back handing input to the game after the panel closes until Escape and the mouse button are both
    /// released, plus one more frame. Otherwise the press that closed the panel (Escape, or a click on Close) would
    /// also reach the menu underneath: Escape closing the pause menu, or a click landing on "Give up".
    /// </summary>
    internal sealed class RestoreGate
    {
        private bool _sawRelease;

        public bool Pending { get; private set; }

        public void Arm()
        {
            Pending = true;
            _sawRelease = false;
        }

        public void Clear()
        {
            Pending = false;
            _sawRelease = false;
        }

        /// <returns>True once, on the frame input may be handed back.</returns>
        public bool Tick(bool escapeHeld, bool mouseHeld)
        {
            if (!Pending)
                return false;
            if (escapeHeld || mouseHeld)
            {
                _sawRelease = false;
                return false;
            }
            if (!_sawRelease)
            {
                _sawRelease = true;
                return false;
            }
            Clear();
            return true;
        }
    }
}
