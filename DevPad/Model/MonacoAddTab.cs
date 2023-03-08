using System;

namespace DevPad.Model
{
    public class MonacoAddTab : MonacoTab
    {
        public override string Name { get => string.Empty; set => throw new NotSupportedException(); }
    }
}
