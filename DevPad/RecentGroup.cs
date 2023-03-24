using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using DevPad.Model;
using DevPad.Utilities;

namespace DevPad
{
    public class RecentGroup : IKeyable
    {
        public virtual string Name { get; set; }
        public virtual string ForeColor { get; set; }
        public virtual string BackColor { get; set; }

        [Browsable(false)]
        [JsonIgnore]
        public string Key => Name + "\0" + ForeColor?.ToString() + "\0" + BackColor?.ToString();

        [Browsable(false)]
        public string ActiveTabKey { get; set; }

        public TabGroup ToTabGroup()
        {
            if (Name.EqualsIgnoreCase(Resources.Resources.DefaultGroupName))
                throw new InvalidOperationException();

            return new TabGroup { Name = Name, ForeColor = ForeColor, BackColor = BackColor, ActiveTabKey = ActiveTabKey };
        }
        public static RecentGroup FromTabGroup(TabGroup tabGroup)
        {
            if (tabGroup == null)
                throw new ArgumentNullException(nameof(tabGroup));

            return new RecentGroup { Name = tabGroup.Name, ForeColor = tabGroup.ForeColor, BackColor = tabGroup.BackColor, ActiveTabKey = tabGroup.ActiveTabKey };
        }

        public override string ToString() => Name + " (" + ForeColor + "/" + BackColor + ")";
    }
}
