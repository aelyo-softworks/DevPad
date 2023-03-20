using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using DevPad.Utilities;

namespace DevPad.Model
{
    public class TabGroup : DictionaryObject
    {
        private readonly ObservableCollection<MonacoTab> _tabs = new ObservableCollection<MonacoTab>();
        private readonly static ColorConverter _colorConverter = new ColorConverter();

        public TabGroup()
        {
            _tabs.Add(new MonacoAddTab());
        }

        public MonacoTab CurrentTab => Tabs.FirstOrDefault(t => t.IsSelected);
        public IEnumerable<MonacoTab> Tabs => _tabs.Where(t => !t.IsAdd);

        public bool IsDefault { get; internal set; }
        public bool IsNotDefault => !IsDefault;
        public bool IsAdd => this is AddTabGroup;
        public bool IsClosable => !IsAdd && !IsDefault;
        public string Key => Name + "\0" + ForeColor?.ToString() + "\0" + BackColor?.ToString();
        public virtual string FontFamily => string.Empty;
        public virtual string Name { get => DictionaryObjectGetPropertyValue<string>(); set => DictionaryObjectSetPropertyValue(value); }
        public virtual string ForeColor { get => DictionaryObjectGetNullifiedPropertyValue(); set => DictionaryObjectSetPropertyValue(value); }
        public virtual string BackColor { get => DictionaryObjectGetNullifiedPropertyValue(); set => DictionaryObjectSetPropertyValue(value); }
        public virtual string CloseButtonTooltip => Resources.Resources.CloseTabGroupTooltip;
        public virtual string AddButtonTooltip => string.Empty;

        public override string ToString() => Name;
        public override void CopyFrom(DictionaryObject from)
        {
            base.CopyFrom(from);
            IsDefault = ((TabGroup)from).IsDefault;
        }

        protected override IEnumerable DictionaryObjectGetErrors(string propertyName)
        {
            if (IsAdd)
                yield break;

            if (propertyName == null || propertyName == nameof(Name))
            {
                if (string.IsNullOrWhiteSpace(Name))
                    yield return Resources.Resources.NameError;

                if (!IsDefault && Name.EqualsIgnoreCase(Resources.Resources.DefaultGroupName))
                    yield return Resources.Resources.ReservedNameError;
            }

            if ((propertyName == null || propertyName == nameof(ForeColor)) && !string.IsNullOrWhiteSpace(ForeColor) && !_colorConverter.IsValid(ForeColor))
                yield return Resources.Resources.ColorError;

            if ((propertyName == null || propertyName == nameof(BackColor)) && !string.IsNullOrWhiteSpace(BackColor) && !_colorConverter.IsValid(BackColor))
                yield return Resources.Resources.ColorError;
        }

        public bool RemoveTab(MonacoTab tab)
        {
            if (tab == null)
                throw new ArgumentNullException(nameof(tab));

            return _tabs.Remove(tab);
        }

        public void SelectTab(MonacoTab tab)
        {
            if (tab == null)
                throw new ArgumentNullException(nameof(tab));

            foreach (var item in Tabs)
            {
                item.IsSelected = item == tab;
            }
        }

        public void ClearTabs()
        {
            _tabs.Clear();
        }
    }
}
