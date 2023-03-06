using System;

namespace DevPad.Utilities
{
    public class ValueChangedEventArgs<T> : EventArgs
    {
        public ValueChangedEventArgs(T value)
        {
            Value = value;
        }

        public T Value { get; }

        public override string ToString() => Value?.ToString();
    }
}
