using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DevPad.Utilities;

namespace DevPad.Model
{
    public class TabGroup : DictionaryObject, IKeyable
    {
        private readonly ObservableCollection<MonacoTab> _tabs = new ObservableCollection<MonacoTab>();

        public TabGroup()
        {
            _tabs.Add(new MonacoAddTab());
        }

        public ObservableCollection<MonacoTab> Tabs => _tabs;
        public IEnumerable<MonacoTab> FileViewTabs => Tabs.Where(t => t.IsFileView);
        public string ActiveTabKey { get; internal set; }
        public bool IsDefault { get; internal set; }
        public bool IsNotDefault => !IsDefault;
        public bool IsAdd => this is AddTabGroup;
        public bool IsClosable => !IsAdd && !IsDefault;
        public string Key => Name + "\0" + ForeColor?.ToString() + "\0" + BackColor?.ToString();
        public virtual string FontFamily => string.Empty;
        public virtual int SelectedTabIndex { get => DictionaryObjectGetPropertyValue(0); set => DictionaryObjectSetPropertyValue(value); }
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

            if ((propertyName == null || propertyName == nameof(ForeColor)) && !string.IsNullOrWhiteSpace(ForeColor) && !WpfUtilities.ColorConverter.IsValid(ForeColor))
                yield return Resources.Resources.ColorError;

            if ((propertyName == null || propertyName == nameof(BackColor)) && !string.IsNullOrWhiteSpace(BackColor) && !WpfUtilities.ColorConverter.IsValid(BackColor))
                yield return Resources.Resources.ColorError;
        }

        public void SelectTab(string tabKey)
        {
            if (tabKey == null)
                return;

            var index = Tabs.IndexOf(t => t.Key == tabKey);
            if (index < 0)
                return;

            SelectedTabIndex = index;
        }

        public void SelectTab(MonacoTab tab)
        {
            if (tab == null)
                throw new ArgumentNullException(nameof(tab));

            if (tab.IsAdd)
                return;

            var index = Tabs.IndexOf(tab);
            if (index < 0)
                return;

            SelectedTabIndex = index;
        }

        public void AddTab(MonacoTab tab)
        {
            if (tab == null)
                throw new ArgumentNullException(nameof(tab));

            var c = _tabs.Count - 1;
            tab.Index = c;
            _tabs.Insert(c, tab);
        }

        public bool RemoveTab(MonacoTab tab)
        {
            if (tab == null)
                throw new ArgumentNullException(nameof(tab));

            return _tabs.Remove(tab);
        }

        public void ClearTabs()
        {
            _tabs.Clear();
        }
    }
}
