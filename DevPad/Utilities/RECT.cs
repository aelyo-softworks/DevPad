using System.Drawing;
using System.Runtime.InteropServices;

namespace DevPad.Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;

        public RECT(int left, int top, int right, int bottom)
        {
            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
        }

        public int Width => right - left;
        public int Height => bottom - top;
        public Point LeftTop => new Point(left, top);
        public Point RightBottom => new Point(right, bottom);

        public override string ToString() => left + "," + top + "," + right + "," + bottom;
    }
}
