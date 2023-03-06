using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;

namespace DevPad.Utilities
{
    public class UniversalConverter : IValueConverter
    {
        public virtual object DefaultValue { get; set; }
        public virtual ObservableCollection<UniversalConverterCase> Switch { get; } = new ObservableCollection<UniversalConverterCase>();

        public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Conversions.ChangeType(parameter, targetType, null, culture);
        public virtual object Convert(object value, Type targetType, object parameter, string language) => Convert(value, targetType, parameter, CultureInfoFromName(language));
        public virtual object ConvertBack(object value, Type targetType, object parameter, string language) => ConvertBack(value, targetType, parameter, CultureInfoFromName(language));
        public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (Switch.Count == 0)
                return Conversions.ChangeType(value, targetType, culture);

            foreach (var sw in Switch)
            {
                if (sw.Matches(value, parameter, culture))
                {
                    if (sw.Options.HasFlag(UniversalConverterOptions.ConvertedValueIsConverterParameter))
                        return Conversions.ChangeType(parameter, targetType, culture);

                    return Conversions.ChangeType(sw.ConvertedValue, targetType, null, culture);
                }
            }

            return Conversions.ChangeType(DefaultValue, targetType, null, culture);
        }

        private static CultureInfo CultureInfoFromName(string language)
        {
            if (language != null)
            {
                try
                {
                    return new CultureInfo(language);
                }
                catch
                {
                    // continue
                }
            }
            return null;
        }
    }
}
