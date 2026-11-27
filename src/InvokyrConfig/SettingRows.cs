using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace InvokyrConfig
{
    /// <summary>
    /// Draws one setting as a row: its name on the left, the control for its type, and Reset on the right, with the
    /// description underneath. Text and number fields keep a draft while focused and commit it when focus leaves
    /// (Enter, a click elsewhere, or closing); a draft that doesn't parse or isn't accepted is dropped.
    /// </summary>
    internal sealed class SettingRows
    {
        private const float NameWidth = 280f;
        private const float SliderWidth = 220f;
        private const float ValueWidth = 64f;
        private const float FieldWidth = 260f;
        private const float ChoiceWidth = 240f;
        private const float ToggleWidth = 110f;
        private const float ResetWidth = 90f;

        private readonly Action<string> _status;
        private readonly Dictionary<ConfigEntryBase, Draft> _drafts = new Dictionary<ConfigEntryBase, Draft>();

        private sealed class Draft
        {
            public Setting Setting;
            public string Text;
            public bool Focused;
        }

        public SettingRows(Action<string> status)
        {
            _status = status;
        }

        public void Draw(Pane pane, Setting s)
        {
            var kind = SettingValues.Choose(s);
            var entry = s.Entry;
            var line = pane.Card();

            if (kind != EditorKind.ReadOnly && line.ButtonRight("Reset", Theme.Button, ResetWidth))
            {
                _drafts.Remove(entry);
                MenuInput.Focus = -1;
                SettingValues.Reset(entry);
                _status($"{s.DisplayName} reset to {SettingValues.Format(entry.BoxedValue)}");
            }

            line.LabelLeft(s.DisplayName, Theme.Body, NameWidth);
            switch (kind)
            {
                case EditorKind.Toggle:
                    var on = entry.BoxedValue is bool b && b;
                    if (line.Button(on ? "On" : "Off", on ? Theme.TabOn : Theme.Button, ToggleWidth))
                        Set(s, !on);
                    break;
                case EditorKind.Choice:
                    if (line.Button(SettingValues.Format(entry.BoxedValue), Theme.Button, ChoiceWidth))
                        Set(s, SettingValues.Next(entry.BoxedValue, SettingValues.Choices(s)));
                    break;
                case EditorKind.Slider:
                    DrawSlider(line, s);
                    break;
                case EditorKind.Number:
                case EditorKind.Text:
                    DrawField(line, s);
                    break;
                default:
                    line.Label(ReadOnlyText(entry), Theme.MutedText);
                    break;
            }

            if (!string.IsNullOrEmpty(s.Description))
            {
                pane.Text(s.Description, Theme.MutedText);
                pane.Space(8f);
            }
        }

        private void DrawSlider(Line line, Setting s)
        {
            SettingValues.TryRange(s.Entry.Description?.AcceptableValues, out var min, out var max);
            float current;
            try
            {
                current = Convert.ToSingle(s.Entry.BoxedValue, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                current = min;
            }
            var raw = line.Slider(current, min, max, SliderWidth);
            // The slider hands back the value it was given unless it's being dragged.
            if (raw != current)
            {
                var value = SettingValues.FromSlider(s.Entry.SettingType, raw, min, max);
                if (!Equals(value, s.Entry.BoxedValue))
                    SettingValues.TrySet(s.Entry, value);
            }
            line.LabelLeft(SettingValues.Format(s.Entry.BoxedValue), Theme.Body, ValueWidth);
        }

        private void DrawField(Line line, Setting s)
        {
            var entry = s.Entry;
            var id = MenuInput.PeekId();
            var stored = Shown(entry);
            var shown = _drafts.TryGetValue(entry, out var draft) ? draft.Text : stored;
            var text = line.TextField(shown, 200, FieldWidth);
            if (MenuInput.Focus == id)
            {
                if (draft == null)
                {
                    draft = new Draft { Setting = s };
                    _drafts[entry] = draft;
                }
                draft.Text = text;
                draft.Focused = true;
            }
            else if (draft != null)
            {
                Commit(draft);
            }
        }

        /// <summary>Commits every open draft (the panel is closing or the list is changing).</summary>
        public void CommitAll()
        {
            foreach (var draft in new List<Draft>(_drafts.Values))
                Commit(draft);
            _drafts.Clear();
        }

        private void Commit(Draft draft)
        {
            var s = draft.Setting;
            _drafts.Remove(s.Entry);
            if (draft.Text == Shown(s.Entry))
                return;
            if (SettingValues.TryParse(s.Entry.SettingType, draft.Text, out var value) && Set(s, value))
                return;
            _status($"{s.DisplayName}: \"{draft.Text}\" isn't a value it accepts; kept {Shown(s.Entry)}");
        }

        /// <summary>Saving follows from the entry's SettingChanged (see ConfigWriter), so only changes are written.</summary>
        private static bool Set(Setting s, object value) => SettingValues.TrySet(s.Entry, value);

        private static string Shown(ConfigEntryBase entry) =>
            entry.BoxedValue is string text ? text : SettingValues.Format(entry.BoxedValue);

        private static string ReadOnlyText(ConfigEntryBase entry)
        {
            try
            {
                return entry.GetSerializedValue();
            }
            catch (Exception)
            {
                return entry.BoxedValue?.ToString() ?? "";
            }
        }
    }
}
