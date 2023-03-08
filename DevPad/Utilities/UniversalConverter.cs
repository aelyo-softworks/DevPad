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
                    object converted;
                    if (sw.Options.HasFlag(UniversalConverterOptions.ConvertedValueIsConverterParameter))
                    {
                        converted = Conversions.ChangeType(parameter, targetType, culture);
                    }
                    else
                    {
                        if (!sw.HasConvertedValue)
                        {
                            converted = Conversions.ChangeType(value, targetType, null, culture);
                        }
                        else
                        {
                            converted = Conversions.ChangeType(sw.ConvertedValue, targetType, null, culture);
                        }
                    }

                    if (sw.Operator == UniversalConverterOperator.Negate)
                    {
                        converted = Negate(converted);
                    }
                    return converted;
                }
            }

            return Conversions.ChangeType(DefaultValue, targetType, null, culture);
        }

        private static object Negate(object value)
        {
            if (value == null)
                return value;

            var type = value.GetType();
            var tc = Type.GetTypeCode(type);
            switch (tc)
            {
                case TypeCode.Boolean:
                    return !(bool)value;

                case TypeCode.Char:
                    return -(char)value;

                case TypeCode.SByte:
                    return -(sbyte)value;

                case TypeCode.Byte:
                    return -(byte)value;

                case TypeCode.Int16:
                    return -(short)value;

                case TypeCode.UInt16:
                    return -(ushort)value;

                case TypeCode.Int32:
                    return -(int)value;

                case TypeCode.UInt32:
                    return -(uint)value;

                case TypeCode.Int64:
                    return -(long)value;

                case TypeCode.UInt64:
                    return (ulong)-(long)(ulong)value;

                case TypeCode.Single:
                    return -(float)value;

                case TypeCode.Double:
                    return -(double)value;

                case TypeCode.Decimal:
                    return -(decimal)value;

                case TypeCode.String:
                    return "-" + value;

                case TypeCode.Object:
                    if (value is TimeSpan ts)
                        return -ts;
                    break;
            }
            return value;
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
