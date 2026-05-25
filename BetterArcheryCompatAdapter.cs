using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class BetterArcheryQuiverApi
    {
        private readonly FieldInfo _quiverEnabledField;
        private readonly FieldInfo _quiverRowIndexField;
        private ConfigEntry<bool>? _quiverEnabledConfig;
        private bool _quiverRowIndexResolved;
        private int _quiverRowIndex;

        private BetterArcheryQuiverApi(FieldInfo quiverEnabledField, FieldInfo quiverRowIndexField)
        {
            _quiverEnabledField = quiverEnabledField;
            _quiverRowIndexField = quiverRowIndexField;
        }

        public static bool TryCreate(Assembly assembly, out BetterArcheryQuiverApi? api, out string detail)
        {
            api = null;
            Type? type = assembly.GetTypes().FirstOrDefault(candidate => candidate.IsClass && candidate.Name == "BetterArchery");
            if (type == null)
            {
                detail = "BetterArchery type was not found";
                return false;
            }

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
            FieldInfo? quiverEnabledField = fields.FirstOrDefault(field => string.Equals(field.Name, "ConfigQuiverEnabled", StringComparison.OrdinalIgnoreCase));
            FieldInfo? quiverRowIndexField = fields.FirstOrDefault(field => string.Equals(field.Name, "QuiverRowIndex", StringComparison.OrdinalIgnoreCase));
            if (quiverEnabledField == null || quiverRowIndexField == null)
            {
                detail = "quiver config fields were not found";
                return false;
            }

            api = new BetterArcheryQuiverApi(quiverEnabledField, quiverRowIndexField);
            detail = "";
            return true;
        }

        public bool IsQuiverCell(Vector2i pos, bool includeRestockableSlots)
        {
            try
            {
                _quiverEnabledConfig ??= _quiverEnabledField.GetValue(null) as ConfigEntry<bool>;
            }
            catch
            {
                _quiverEnabledConfig = null;
            }

            if (_quiverEnabledConfig == null || !_quiverEnabledConfig.Value)
            {
                return false;
            }

            if (!_quiverRowIndexResolved || _quiverRowIndex == 0)
            {
                try
                {
                    _quiverRowIndex = _quiverRowIndexField.GetValue(null) is int rowIndex ? rowIndex : 0;
                }
                catch
                {
                    _quiverRowIndex = 0;
                }

                _quiverRowIndexResolved = _quiverRowIndex != 0;
            }

            if (_quiverRowIndex == 0)
            {
                return false;
            }

            if (pos.y == _quiverRowIndex - 1)
            {
                return true;
            }

            return pos.y == _quiverRowIndex && (includeRestockableSlots || pos.x > 2);
        }
    }
}
