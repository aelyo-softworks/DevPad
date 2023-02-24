using System.ComponentModel;

namespace MonacoModel
{
    public class ThemeConverter : TypeConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection(new string[] { "vs", "vs-dark", "hc-light", "hc-black" });
        }
    }
}
