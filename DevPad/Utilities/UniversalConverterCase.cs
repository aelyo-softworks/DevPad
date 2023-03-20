using System;
using System.Globalization;

namespace DevPad.Utilities
{
    public class UniversalConverterCase
    {
        private object _convertedValue;
        private object _value;

        public virtual object MinimumValue { get; set; }
        public virtual object MaximumValue { get; set; }
        public virtual UniversalConverterOptions Options { get; set; }
        public virtual UniversalConverterOperator Operator { get; set; }
        public virtual StringComparison StringComparison { get; set; } = StringComparison.CurrentCultureIgnoreCase;
        public virtual bool Reverse { get; set; }
        public bool HasConvertedValue { get; private set; }
        public bool HasValue { get; private set; }

        public virtual object Value
        {
            get => _value;
            set
            {
                _value = value;
                HasValue = true;
            }
        }

        public virtual object ConvertedValue
        {
            get => _convertedValue;
            set
            {
                _convertedValue = value;
                HasConvertedValue = true;
            }
        }

        public virtual bool Matches(object value, object parameter, CultureInfo culture, Type targetType)
        {
            var input = new UniversalConverterInput();
            input.MaximumValue = MaximumValue;
            input.MinimumValue = MinimumValue;
            input.Operator = Operator;
            input.Options = Options;
            input.Value = Value;
            input.ValueToCompare = value;
            input.Reverse = Reverse;
            input.StringComparison = StringComparison;
            input.ConverterParameter = parameter;
            return input.Matches(culture);
        }

        public override string ToString() => Operator.ToString();
    }
}
