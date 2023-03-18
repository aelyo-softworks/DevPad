using DevPad.Utilities;

namespace DevPad.Model
{
    public class TabGroup : DictionaryObject
    {
        public bool IsDefault { get; internal set; }
        public bool IsAdd => this is AddTabGroup;
        public bool IsClosable => !IsAdd && !IsDefault;
        public virtual string FontFamily => string.Empty;
        public virtual string Name { get => DictionaryObjectGetNullifiedPropertyValue(); set => DictionaryObjectSetPropertyValue(value); }
        public virtual string CloseButtonTooltip => Resources.Resources.CloseTabGroupTooltip;
        public virtual string AddButtonTooltip => string.Empty;

        public override string ToString() => Name;
    }
}
