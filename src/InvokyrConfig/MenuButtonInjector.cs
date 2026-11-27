using System;
using System.Collections.Generic;
using _200_Dev.UI.StartMenu;
using _200_Dev.UI.StartMenu.Menus;
using _200_Dev.UI.UI_Navigation.NavElements;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace InvokyrConfig
{
    /// <summary>
    /// Adds a "Mods" button under Options in the title menu and the pause menu by cloning the game's Options button.
    /// Nothing in the game is patched: a quarter-second poll finds an open menu and adds the button once per menu
    /// instance. The clone loses the game's click handlers and its localization (which would turn "Mods" back into
    /// "Options"). If anything fails for a menu type, that type is left alone from then on; the hotkey still works.
    /// </summary>
    public sealed class MenuButtonInjector : MonoBehaviour
    {
        public MenuButtonInjector(IntPtr ptr) : base(ptr) { }

        internal const string ButtonName = "InvokyrConfig_ModsButton";
        private const float Interval = 0.25f;

        private float _next;
        private bool _mainDisabled, _pauseDisabled;
        private bool _mainLogged, _pauseLogged;
        private readonly List<GameObject> _clones = new List<GameObject>();

        private void Update()
        {
            if (Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + Interval;

            if (!Plugin.ShowMenuButtons.Value)
            {
                RemoveAll();
                return;
            }

            if (!_mainDisabled)
            {
                try
                {
                    var menu = Object.FindObjectOfType<MainMenu>();
                    if (menu != null && menu.isActiveAndEnabled && Inject(menu.optionsButton, null) && !_mainLogged)
                    {
                        _mainLogged = true;
                        Plugin.Log.LogInfo("Added the Mods button to the title menu");
                    }
                }
                catch (Exception e)
                {
                    _mainDisabled = true;
                    Plugin.Log.LogError($"Couldn't add the Mods button to the title menu; use {Plugin.ToggleKey.Value} instead. {e}");
                }
            }

            if (!_pauseDisabled)
            {
                try
                {
                    var menu = Object.FindObjectOfType<PauseMenu>();
                    if (menu != null && menu.isActiveAndEnabled && Inject(menu.optionsButton, menu.contentLayoutGroupRebuilder) && !_pauseLogged)
                    {
                        _pauseLogged = true;
                        Plugin.Log.LogInfo("Added the Mods button to the pause menu");
                    }
                }
                catch (Exception e)
                {
                    _pauseDisabled = true;
                    Plugin.Log.LogError($"Couldn't add the Mods button to the pause menu; use {Plugin.ToggleKey.Value} instead. {e}");
                }
            }
        }

        /// <returns>True if a button was added this time.</returns>
        private bool Inject(NavButton options, _200_Dev.UI.UI_Navigation.LayoutGroupRebuilder rebuilder)
        {
            if (options == null)
                return false;
            var source = options.gameObject;
            var parent = source.transform.parent;
            if (parent == null || parent.Find(ButtonName) != null)
                return false;

            var clone = Object.Instantiate(source, parent);
            try
            {
                clone.name = ButtonName;
                clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

                // Localization would re-translate the label back to "Options"; SimulatorText reapplies its term.
                foreach (var component in clone.GetComponentsInChildren<Component>(true))
                {
                    if (component == null)
                        continue;
                    var typeName = component.GetIl2CppType().Name;
                    if (typeName.Contains("Locali") || typeName == "SimulatorText")
                        Object.DestroyImmediate(component);
                }

                var open = DelegateSupport.ConvertDelegate<UnityAction>(new Action(OpenPanel));
                foreach (var button in clone.GetComponentsInChildren<Button>(true))
                {
                    button.onClick = new Button.ButtonClickedEvent();
                    button.onClick.AddListener(open);
                    var vhs = button.TryCast<VHSButton>();
                    if (vhs != null)
                        vhs.OnMouseClick = null;
                }

                foreach (var text in clone.GetComponentsInChildren<TextMeshProUGUI>(true))
                    text.text = "Mods";

                if (rebuilder != null)
                    rebuilder.ReloadLayout();
                var rect = parent.TryCast<RectTransform>();
                if (rect != null)
                    LayoutRebuilder.MarkLayoutForRebuild(rect);
                _clones.Add(clone);
                return true;
            }
            catch
            {
                Object.Destroy(clone);
                throw;
            }
        }

        private static void OpenPanel()
        {
            try
            {
                ConfigPanelController.Instance?.Open();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't open mod settings. {e}");
            }
        }

        private void RemoveAll()
        {
            foreach (var clone in _clones)
            {
                if (clone != null)
                    Object.Destroy(clone);
            }
            _clones.Clear();
        }
    }
}
