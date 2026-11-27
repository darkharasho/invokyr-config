using System;
using System.Reflection;
using BepInEx.Configuration;

namespace InvokyrConfig
{
    /// <summary>
    /// The ConfigurationManagerAttributes fields a setting is tagged with. Every mod ships its own copy of that class,
    /// so the tags are read by field or property name rather than by type.
    /// </summary>
    internal readonly struct SettingAttributes
    {
        public readonly string DispName;
        public readonly int? Order;
        public readonly bool? Browsable;
        public readonly bool? ReadOnly;
        public readonly bool? IsAdvanced;

        private SettingAttributes(string dispName, int? order, bool? browsable, bool? readOnly, bool? isAdvanced)
        {
            DispName = dispName;
            Order = order;
            Browsable = browsable;
            ReadOnly = readOnly;
            IsAdvanced = isAdvanced;
        }

        public static SettingAttributes Read(ConfigDescription description)
        {
            string dispName = null;
            int? order = null;
            bool? browsable = null, readOnly = null, isAdvanced = null;
            var tags = description?.Tags;
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    if (tag == null || tag is AcceptableValueBase)
                        continue;
                    dispName = dispName ?? Get(tag, "DispName") as string;
                    order = order ?? AsInt(Get(tag, "Order"));
                    browsable = browsable ?? Get(tag, "Browsable") as bool?;
                    readOnly = readOnly ?? Get(tag, "ReadOnly") as bool?;
                    isAdvanced = isAdvanced ?? Get(tag, "IsAdvanced") as bool?;
                }
            }
            return new SettingAttributes(dispName, order, browsable, readOnly, isAdvanced);
        }

        private static object Get(object tag, string name)
        {
            try
            {
                var type = tag.GetType();
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                var field = type.GetField(name, flags);
                if (field != null)
                    return field.GetValue(tag);
                var property = type.GetProperty(name, flags);
                return property != null && property.GetIndexParameters().Length == 0 ? property.GetValue(tag) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static int? AsInt(object value)
        {
            switch (value)
            {
                case int i: return i;
                case long l: return (int)l;
                case short s: return s;
                default: return null;
            }
        }
    }
}
