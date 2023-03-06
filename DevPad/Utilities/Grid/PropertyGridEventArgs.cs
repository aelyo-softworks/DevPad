using System.ComponentModel;

namespace DevPad.Utilities.Grid
{
    public class PropertyGridEventArgs : CancelEventArgs
    {
        public PropertyGridEventArgs(PropertyGridProperty property)
            : this(property, null)
        {
        }

        public PropertyGridEventArgs(PropertyGridProperty property, object context)
        {
            Property = property;
            Context = context;
        }

        public PropertyGridProperty Property { get; }
        public object Context { get; set; }
    }
}