using System;
using BepInEx.Configuration;

namespace ValheimTweaks
{
    /// <summary>
    /// Display hints for Configuration Manager. Pass an instance as a tag in
    /// <see cref="ConfigDescription"/>; the manager matches this class by name and reads its fields,
    /// so no reference to the manager is needed. Leave a field null to use the manager's default.
    /// </summary>
#pragma warning disable 0169, 0414, 0649
    internal sealed class ConfigurationManagerAttributes
    {
        /// <summary>Position within its category; higher values are listed first.</summary>
        public int? Order;

        /// <summary>Override the setting name shown in the window.</summary>
        public string DispName;

        /// <summary>Override the category (section) shown in the window.</summary>
        public string Category;

        /// <summary>Override the description tooltip.</summary>
        public string Description;

        /// <summary>Only shown when "Advanced settings" is enabled in the manager.</summary>
        public bool? IsAdvanced;

        /// <summary>Shown but not editable.</summary>
        public bool? ReadOnly;

        /// <summary>Set to false to hide the setting from the window entirely.</summary>
        public bool? Browsable;

        /// <summary>Value used by the "Reset" button instead of the entry's default.</summary>
        public object DefaultValue;

        /// <summary>Show a numeric range slider as a percentage.</summary>
        public bool? ShowRangeAsPercent;

        public bool? HideDefaultButton;
        public bool? HideSettingName;

        /// <summary>Replace the default editor with custom IMGUI drawing code.</summary>
        public Action<ConfigEntryBase> CustomDrawer;

        public delegate void CustomHotkeyDrawerFunc(ConfigEntryBase setting, ref bool isCurrentlyAcceptingInput);
        public CustomHotkeyDrawerFunc CustomHotkeyDrawer;

        /// <summary>Custom conversion for types the manager can't edit natively.</summary>
        public Func<object, string> ObjToStr;
        public Func<string, object> StrToObj;
    }
#pragma warning restore 0169, 0414, 0649
}
