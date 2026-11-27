using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;

namespace InvokyrConfig
{
    internal enum EditorKind { Toggle, Slider, Number, Choice, Text, ReadOnly }

    /// <summary>Which control edits a setting, and the parse, format and set rules behind it.</summary>
    internal static class SettingValues
    {
        public static bool IsSupported(Type t) =>
            t == typeof(bool) || IsNumber(t) || t == typeof(string) || (t != null && t.IsEnum);

        private static bool IsNumber(Type t) => t == typeof(int) || t == typeof(long) || t == typeof(float) || t == typeof(double);

        private static bool IsWhole(Type t) => t == typeof(int) || t == typeof(long);

        public static EditorKind Choose(Setting s)
        {
            if (s.ReadOnly)
                return EditorKind.ReadOnly;
            var t = s.Entry.SettingType;
            var acceptable = s.Entry.Description?.AcceptableValues;
            if (t == typeof(bool))
                return EditorKind.Toggle;
            if (Choices(s).Length > 0)
                return EditorKind.Choice;
            if (IsNumber(t))
                return TryRange(acceptable, out _, out _) ? EditorKind.Slider : EditorKind.Number;
            if (t == typeof(string))
                return EditorKind.Text;
            return EditorKind.ReadOnly;
        }

        /// <summary>Bounds of an AcceptableValueRange of any numeric type.</summary>
        public static bool TryRange(AcceptableValueBase acceptable, out float min, out float max)
        {
            min = max = 0f;
            if (acceptable == null)
                return false;
            var type = acceptable.GetType();
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(AcceptableValueRange<>))
                return false;
            try
            {
                min = Convert.ToSingle(type.GetProperty("MinValue")?.GetValue(acceptable), CultureInfo.InvariantCulture);
                max = Convert.ToSingle(type.GetProperty("MaxValue")?.GetValue(acceptable), CultureInfo.InvariantCulture);
                return max > min;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The values a Choice cycles through: an enum's values, or an AcceptableValueList's. Empty if neither.</summary>
        public static object[] Choices(Setting s)
        {
            var t = s.Entry.SettingType;
            var acceptable = s.Entry.Description?.AcceptableValues;
            if (acceptable != null)
            {
                var type = acceptable.GetType();
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AcceptableValueList<>) &&
                    type.GetProperty("AcceptableValues")?.GetValue(acceptable) is IEnumerable values)
                    return values.Cast<object>().ToArray();
            }
            if (t != null && t.IsEnum)
                return Enum.GetValues(t).Cast<object>().ToArray();
            return Array.Empty<object>();
        }

        public static object Next(object current, object[] choices)
        {
            if (choices == null || choices.Length == 0)
                return current;
            var i = Array.IndexOf(choices, current);
            return choices[(i + 1) % choices.Length];
        }

        public static string Format(object value)
        {
            switch (value)
            {
                case null: return "";
                case bool b: return b ? "On" : "Off";
                case float f: return f.ToString("0.##", CultureInfo.InvariantCulture);
                case double d: return d.ToString("0.##", CultureInfo.InvariantCulture);
                case IFormattable n: return n.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }

        /// <summary>Text from a field, as the setting's type. Numbers are invariant culture and trimmed; strings are kept as typed.</summary>
        public static bool TryParse(Type t, string text, out object value)
        {
            value = null;
            if (text == null)
                return false;
            if (t == typeof(string))
            {
                value = text;
                return true;
            }
            var trimmed = text.Trim();
            const NumberStyles number = NumberStyles.Float;
            var culture = CultureInfo.InvariantCulture;
            if (t == typeof(int) && int.TryParse(trimmed, NumberStyles.Integer, culture, out var i)) { value = i; return true; }
            if (t == typeof(long) && long.TryParse(trimmed, NumberStyles.Integer, culture, out var l)) { value = l; return true; }
            if (t == typeof(float) && float.TryParse(trimmed, number, culture, out var f) && !float.IsNaN(f) && !float.IsInfinity(f)) { value = f; return true; }
            if (t == typeof(double) && double.TryParse(trimmed, number, culture, out var d) && !double.IsNaN(d) && !double.IsInfinity(d)) { value = d; return true; }
            if (t == typeof(bool) && bool.TryParse(trimmed, out var b)) { value = b; return true; }
            if (t != null && t.IsEnum)
            {
                try
                {
                    value = Enum.Parse(t, trimmed, true);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>A slider position as the setting's type: clamped, whole numbers rounded, decimals to 2 places.</summary>
        public static object FromSlider(Type t, float raw, float min, float max)
        {
            var v = Math.Min(max, Math.Max(min, raw));
            if (t == typeof(int))
                return (int)Math.Round(v, MidpointRounding.AwayFromZero);
            if (t == typeof(long))
                return (long)Math.Round(v, MidpointRounding.AwayFromZero);
            var rounded = Math.Round((double)v, 2, MidpointRounding.AwayFromZero);
            if (t == typeof(double))
                return rounded;
            return (float)rounded;
        }

        /// <summary>
        /// Writes through BoxedValue so the mod's SettingChanged fires. False if the entry threw or stored something
        /// else (it clamped the value to its acceptable range).
        /// </summary>
        public static bool TrySet(ConfigEntryBase entry, object value)
        {
            try
            {
                entry.BoxedValue = value;
                return Equals(entry.BoxedValue, value);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Reset(ConfigEntryBase entry) => entry.BoxedValue = entry.DefaultValue;
    }
}
