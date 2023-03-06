using System;

namespace DevPad.Utilities.Grid
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class PropertyGridAttribute : Attribute
    {
        public PropertyGridAttribute()
        {
            Type = typeof(object);
        }

        public object Value { get; set; }
        public string Name { get; set; }
        public Type Type { get; set; }
        public override object TypeId => Name;
    }
}