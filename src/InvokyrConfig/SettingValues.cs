using System;

namespace InvokyrConfig
{
    /// <summary>Which control edits a setting, and the parse, format and set rules behind it.</summary>
    internal static class SettingValues
    {
        public static bool IsSupported(Type t) =>
            t == typeof(bool) || t == typeof(int) || t == typeof(long) || t == typeof(float) || t == typeof(double) ||
            t == typeof(string) || (t != null && t.IsEnum);
    }
}
