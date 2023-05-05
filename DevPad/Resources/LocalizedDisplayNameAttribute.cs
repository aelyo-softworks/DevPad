using System.ComponentModel;
using DevPad.Utilities;

namespace DevPad.Resources
{
    public class LocalizedDisplayNameAttribute : DisplayNameAttribute
    {
        public LocalizedDisplayNameAttribute(string displayName)
            : base(displayName)
        {
        }

        public override string DisplayName => Resources.ResourceManager.GetString(base.DisplayName).Nullify() ?? Conversions.Decamelize(base.DisplayName);
    }
}
