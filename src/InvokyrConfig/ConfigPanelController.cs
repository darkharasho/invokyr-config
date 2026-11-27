using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using User;
using Cursor = UnityEngine.Cursor;

namespace InvokyrConfig
{
    /// <summary>
    /// The settings panel: opens on the hotkey or from a Mods button, lists every plugin's settings, and edits them
    /// live. While open the game's input actions are off (<c>InputsManager.Disable</c>), the cursor is free and the uGUI
    /// event system is off so clicks don't reach the menu underneath. Closed with Escape, the game's input stays off
    /// until Escape is released, so the same press can't also close the pause menu.
    /// </summary>
    public sealed class ConfigPanelController : MonoBehaviour
    {
        public ConfigPanelController(IntPtr ptr) : base(ptr) { }

        internal static ConfigPanelController Instance;

        private const float ReferenceHeight = 1080f;
        private const float WindowWidth = 1100f;
        private const float WindowHeight = 760f;
        private const float ListWidth = 280f;
        private const string DefaultStatus = "Changes apply right away and are saved to the mod's file in BepInEx/config.";

        private readonly ExternalFlag _adminMenuOpen = new ExternalFlag("InvokyrAdminMenu.AdminMenuController", "IsOpen");
        private ConfigWriter _writer;
        private SettingRows _rows;

        private bool _open;
        private bool _restorePending;
        private int _scene;
        private InputsManager _inputs;
        private EventSystem _eventSystem;
        private bool _disabledEvents;
        private CursorLockMode _priorLock;
        private bool _priorVisible;
        private bool _loggedError;

        private List<PluginGroup> _groups = new List<PluginGroup>();
        private bool _builtAdvanced;
        private int _selected;
        private float _scroll, _contentHeight, _listScroll, _listHeight;
        private string _status = DefaultStatus;
        private string _toast;
        private float _toastUntil;

        private void Awake()
        {
            Instance = this;
            _writer = new ConfigWriter(onError: (file, e) => Plugin.Log.LogWarning($"Couldn't save {file.ConfigFilePath}: {e.Message}"));
            _rows = new SettingRows(_writer, message => _status = message);
        }

        private void Update()
        {
            try
            {
                if (_restorePending && !Hotkey.EscapeHeld())
                    RestoreInput(lost: false);
                if (_open)
                {
                    if (SceneManager.GetActiveScene().handle != _scene)
                    {
                        Close(escape: false, lost: true);
                        return;
                    }
                    MenuInput.Poll(Screen.height / ReferenceHeight);
                    if (Hotkey.EscapePressed())
                    {
                        Close(escape: true, lost: false);
                        return;
                    }
                    KeepCursorFree();
                    _writer.Tick(Time.unscaledTime);
                }
                if (Hotkey.WasPressed(Shortcut.Parse(Plugin.ToggleKey.Value)))
                {
                    if (_open)
                        Close(escape: false, lost: false);
                    else
                        Open();
                }
            }
            catch (Exception e)
            {
                LogOnce("update", e);
                if (_open)
                    Close(escape: false, lost: true);
            }
        }

        private void LateUpdate()
        {
            if (_open)
                KeepCursorFree();
        }

        private void OnDestroy()
        {
            if (_open)
                Close(escape: false, lost: true);
            else if (_restorePending)
                RestoreInput(lost: true);
            if (Instance == this)
                Instance = null;
        }

        internal void Open()
        {
            if (_open)
                return;
            if (_adminMenuOpen.Get())
            {
                ShowToast("Close the admin menu first");
                return;
            }
            if (_restorePending)
                RestoreInput(lost: false);

            Rebuild();
            _priorLock = Cursor.lockState;
            _priorVisible = Cursor.visible;
            _inputs = InputsManager.Instance;
            _inputs?.Disable();
            _eventSystem = EventSystem.current;
            if (_eventSystem != null && _eventSystem.enabled)
            {
                _eventSystem.enabled = false;
                _disabledEvents = true;
            }
            _scene = SceneManager.GetActiveScene().handle;
            MenuInput.Reset();
            _scroll = 0f;
            _status = DefaultStatus;
            _open = true;
            OverlayState.IsOpen = true;
            KeepCursorFree();
        }

        private void Rebuild()
        {
            var previous = _selected < _groups.Count ? _groups[_selected].Guid : null;
            _rows.CommitAll();
            _builtAdvanced = Plugin.ShowAdvanced.Value;
            _groups = SettingsRegistry.Build(ChainloaderSource.Plugins(), _builtAdvanced);
            _writer.Begin(_groups.Select(g => g.File));
            _selected = Math.Max(0, _groups.FindIndex(g => g.Guid == previous));
            MenuInput.Focus = -1;
        }

        /// <param name="escape">Closed with Escape: keep the game's input off until the key is released.</param>
        /// <param name="lost">The scene changed or something failed: hand input back now and leave the cursor to the game.</param>
        private void Close(bool escape, bool lost)
        {
            if (!_open)
                return;
            _open = false;
            OverlayState.IsOpen = false;
            try
            {
                _rows.CommitAll();
            }
            catch (Exception e)
            {
                LogOnce("commit", e);
            }
            _writer.End();
            MenuInput.Reset();
            if (escape && !lost)
                _restorePending = true;
            else
                RestoreInput(lost);
        }

        private void RestoreInput(bool lost)
        {
            _restorePending = false;
            // Another overlay opened in the meantime: it owns input now and hands it back itself.
            var other = _adminMenuOpen.Get();
            if (!other)
            {
                try
                {
                    if (_inputs != null)
                        _inputs.Enable();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Couldn't turn the game's input back on: {e.Message}");
                }
            }
            _inputs = null;
            if (_disabledEvents && _eventSystem != null)
                _eventSystem.enabled = true;
            _disabledEvents = false;
            _eventSystem = null;
            if (!lost && !other)
            {
                Cursor.lockState = _priorLock;
                Cursor.visible = _priorVisible;
            }
        }

        private static void KeepCursorFree()
        {
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (!_open && (_toast == null || Time.unscaledTime >= _toastUntil))
                return;
            try
            {
                var current = Event.current;
                var type = current.type;
                if (_open && type == EventType.KeyDown)
                {
                    MenuInput.Key(current);
                    return;
                }
                if (_open && type == EventType.Layout && Plugin.ShowAdvanced.Value != _builtAdvanced)
                    Rebuild();
                // Only Repaint draws and handles input, so a click is acted on exactly once per frame.
                if (type != EventType.Repaint)
                    return;

                Theme.Ensure();
                GUI.depth = -1000;
                var scale = Screen.height / ReferenceHeight;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                var width = Screen.width / scale;
                if (_open)
                {
                    MenuInput.BeginPass();
                    DrawPanel(width);
                    MenuInput.EndPass();
                }
                else
                {
                    DrawToast(width);
                }
            }
            catch (Exception e)
            {
                LogOnce("draw", e);
                if (_open)
                    Close(escape: false, lost: true);
            }
            finally
            {
                GUI.matrix = Matrix4x4.identity;
            }
        }

        private void DrawPanel(float width)
        {
            GUI.DrawTexture(new Rect(0f, 0f, width, ReferenceHeight), Theme.Dim);
            var window = new Rect((width - WindowWidth) / 2f, (ReferenceHeight - WindowHeight) / 2f, WindowWidth, WindowHeight);
            GUI.Label(window, "", Theme.WindowStyle);

            var x = window.x + 24f;
            var w = window.width - 48f;
            var top = window.y + 20f;
            const float headerHeight = 54f + 30f;
            const float statusHeight = 30f;
            var bodyTop = top + headerHeight;
            var bodyHeight = window.yMax - 20f - statusHeight - bodyTop;
            var list = new Rect(x, bodyTop, ListWidth, bodyHeight);
            var view = new Rect(x + ListWidth + 20f, bodyTop, w - ListWidth - 20f, bodyHeight);

            // Content first; the covers hide anything half outside the scrolled area.
            DrawList(list);
            DrawSettings(view);
            var inner = new Rect(window.x + 3f, window.y + 3f, window.width - 6f, window.height - 6f);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, bodyTop - inner.y), "", Theme.Cover);
            GUI.Label(new Rect(inner.x, bodyTop + bodyHeight, inner.width, inner.yMax - bodyTop - bodyHeight), "", Theme.Cover);

            GUI.Label(new Rect(x, top, w - 130f, 48f), "MOD SETTINGS", Theme.Title);
            if (Header(new Rect(x + w - 110f, top + 4f, 110f, 40f), "Close", Theme.Button))
                Close(escape: false, lost: false);
            var subtitle = _groups.Count == 0
                ? "No mods with settings are loaded"
                : $"{_groups[_selected].Name} {_groups[_selected].Version} · {_groups.Count} mods";
            GUI.Label(new Rect(x, top + 54f, w, 26f), subtitle, Theme.MutedText);
            GUI.Label(new Rect(x, window.yMax - 20f - statusHeight + 4f, w, statusHeight), _status, Theme.MutedText);
        }

        private void DrawList(Rect view)
        {
            Scroll(view, ref _listScroll, _listHeight);
            var pane = new Pane(view, view.width - 14f, _listScroll);
            for (var i = 0; i < _groups.Count; i++)
            {
                var row = pane.Row(42f);
                if (row.Button(Short(_groups[i].Name, 22), i == _selected ? Theme.TabOn : Theme.Button, pane.Width) && i != _selected)
                {
                    _rows.CommitAll();
                    MenuInput.Focus = -1;
                    _selected = i;
                    _scroll = 0f;
                }
            }
            _listHeight = pane.Y + _listScroll;
            ScrollBar(view, _listScroll, _listHeight);
        }

        private void DrawSettings(Rect view)
        {
            Scroll(view, ref _scroll, _contentHeight);
            var pane = new Pane(view, view.width - 20f, _scroll);
            if (_selected < _groups.Count)
            {
                foreach (var section in _groups[_selected].Sections)
                {
                    pane.Heading(string.IsNullOrEmpty(section.Name) ? "SETTINGS" : section.Name.ToUpperInvariant());
                    foreach (var setting in section.Settings)
                        _rows.Draw(pane, setting);
                }
            }
            _contentHeight = pane.Y + _scroll;
            ScrollBar(view, _scroll, _contentHeight);
        }

        private static void Scroll(Rect view, ref float scroll, float contentHeight)
        {
            var maxScroll = Mathf.Max(0f, contentHeight - view.height);
            if (MenuInput.Wheel != 0f && view.Contains(MenuInput.Pos))
            {
                scroll += MenuInput.Wheel * 60f;
                MenuInput.Wheel = 0f;
            }
            scroll = Mathf.Clamp(scroll, 0f, maxScroll);
        }

        private static void ScrollBar(Rect view, float scroll, float contentHeight)
        {
            const float bar = 6f;
            var maxScroll = Mathf.Max(0f, contentHeight - view.height);
            if (maxScroll <= 0f)
                return;
            GUI.Label(new Rect(view.xMax - bar, view.y, bar, view.height), "", Theme.Track);
            var thumbHeight = Mathf.Max(30f, view.height * view.height / contentHeight);
            var thumbY = view.y + (view.height - thumbHeight) * (scroll / maxScroll);
            GUI.Label(new Rect(view.xMax - bar, thumbY, bar, thumbHeight), "", Theme.Fill);
        }

        private static string Short(string text, int max) =>
            string.IsNullOrEmpty(text) || text.Length <= max ? text ?? "" : text.Substring(0, max - 1) + "…";

        private static bool Header(Rect rect, string text, Btn style)
        {
            var hot = rect.Contains(MenuInput.Pos);
            GUI.Label(rect, text, hot ? style.Hover : style.Normal);
            if (!hot || !MenuInput.Click)
                return false;
            MenuInput.Click = false;
            return true;
        }

        private void DrawToast(float width)
        {
            var w = Theme.TextWidth(_toast, Theme.Toast) + 40f;
            var h = Theme.Toast.fontSize * 1.35f + 24f;
            GUI.Label(new Rect((width - w) / 2f, ReferenceHeight * 0.12f, w, h), _toast, Theme.Toast);
        }

        private void ShowToast(string message)
        {
            _toast = message;
            _toastUntil = Time.unscaledTime + 3f;
        }

        private void LogOnce(string where, Exception e)
        {
            if (_loggedError)
                return;
            _loggedError = true;
            Plugin.Log.LogError($"Mod settings {where} failed. {e}");
        }
    }
}
