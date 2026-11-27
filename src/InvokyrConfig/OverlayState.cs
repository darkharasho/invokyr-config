using System;
using System.Reflection;

namespace InvokyrConfig
{
    /// <summary>
    /// Whether the settings panel is open. Public so other overlays (Admin Menu 0.1.2+) can read it by reflection and
    /// refuse to open on top of it: two overlays would fight over the game's input and the cursor.
    /// </summary>
    public static class OverlayState
    {
        public static bool IsOpen { get; internal set; }
    }

    /// <summary>
    /// A public static bool property on a type in another mod, read by reflection so neither mod needs the other.
    /// The lookup runs once (after every plugin has loaded); a missing type, property or any error reads as false.
    /// </summary>
    internal sealed class ExternalFlag
    {
        private readonly string _typeName;
        private readonly string _propertyName;
        private bool _looked;
        private PropertyInfo _property;

        public ExternalFlag(string typeName, string propertyName)
        {
            _typeName = typeName;
            _propertyName = propertyName;
        }

        public bool Get()
        {
            try
            {
                if (!_looked)
                {
                    _looked = true;
                    _property = Find();
                }
                return _property != null && _property.GetValue(null) is bool b && b;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private PropertyInfo Find()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try
                {
                    type = assembly.GetType(_typeName, false);
                }
                catch (Exception)
                {
                    continue;
                }
                var property = type?.GetProperty(_propertyName, BindingFlags.Public | BindingFlags.Static);
                if (property != null && property.PropertyType == typeof(bool))
                    return property;
            }
            return null;
        }
    }
}
