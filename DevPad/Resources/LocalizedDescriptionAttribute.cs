using System.ComponentModel;

namespace DevPad.Resources
{
    public class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        public LocalizedDescriptionAttribute(string description)
            : base(description)
        {
        }

        public override string Description => Resources.ResourceManager.GetString(DescriptionValue);
    }
}
