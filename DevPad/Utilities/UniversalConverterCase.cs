﻿using System;
using System.Globalization;

namespace DevPad.Utilities
{
    public class UniversalConverterCase
    {
        public virtual object Value { get; set; }
        public virtual object ConvertedValue { get; set; }
        public virtual object MinimumValue { get; set; }
        public virtual object MaximumValue { get; set; }
        public virtual UniversalConverterOptions Options { get; set; }
        public virtual UniversalConverterOperator Operator { get; set; }
        public virtual StringComparison StringComparison { get; set; } = StringComparison.CurrentCultureIgnoreCase;
        public virtual bool Reverse { get; set; }

        public virtual bool Matches(object value, object parameter, CultureInfo culture)
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
    }
}