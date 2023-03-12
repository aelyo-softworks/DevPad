using System.ComponentModel;

namespace DevPad.Resources
{
    public class LocalizedDisplayNameAttribute : DisplayNameAttribute
    {
        public LocalizedDisplayNameAttribute(string name)
            : base(name)
        {
        }

        public override string DisplayName => Resources.ResourceManager.GetString(base.DisplayName);
    }
}
