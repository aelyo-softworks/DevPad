using System.ComponentModel;

namespace DevPad.Resources
{
    public class LocalizedDisplayNameAttribute : DisplayNameAttribute
    {
        public LocalizedDisplayNameAttribute(string displayName)
            : base(displayName)
        {
        }

        public override string DisplayName => Resources.ResourceManager.GetString(base.DisplayName);
    }
}
