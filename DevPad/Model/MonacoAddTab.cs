using System;

namespace DevPad.Model
{
    public class MonacoAddTab : MonacoTab
    {
        public override string Name { get => "🞣"; set => throw new NotSupportedException(); }
        public override string FontFamily => "Segoe UI Symbol";
    }
}
