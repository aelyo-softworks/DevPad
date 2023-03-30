using System.ComponentModel;

namespace DevPad.Resources
{
    public class LocalizedCategoryAttribute : CategoryAttribute
    {
        public LocalizedCategoryAttribute(string category)
            : base(category)
        {
        }

        protected override string GetLocalizedString(string value) => Resources.ResourceManager.GetString(value);
    }
}
