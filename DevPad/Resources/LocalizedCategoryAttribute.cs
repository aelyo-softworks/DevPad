using System.ComponentModel;

namespace Resources
{
    public class LocalizedCategoryAttribute : CategoryAttribute
    {
        public LocalizedCategoryAttribute(string name)
            : base(name)
        {
        }

        protected override string GetLocalizedString(string value) => Resources.ResourceManager.GetString(value);
    }
}
