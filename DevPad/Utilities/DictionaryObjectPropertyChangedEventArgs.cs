﻿using System;
using System.ComponentModel;

namespace DevPad.Utilities
{
    public class DictionaryObjectPropertyChangedEventArgs : PropertyChangedEventArgs
    {
        public DictionaryObjectPropertyChangedEventArgs(string propertyName, DictionaryObjectProperty existingProperty, DictionaryObjectProperty newProperty)
            : base(propertyName)
        {
            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));

            if (newProperty == null)
                throw new ArgumentNullException(nameof(newProperty));

            // existingProperty may be null

            ExistingProperty = existingProperty;
            NewProperty = newProperty;
        }

        public DictionaryObjectProperty ExistingProperty { get; }
        public DictionaryObjectProperty NewProperty { get; }
    }
}
