using System.Xml.Serialization;

namespace DevPad
{
    public class WindowSetting
    {
        private string _name;
        private int _top;
        private int _left;
        private int _height;
        private int _width;
        private bool _isMaximized;

        [XmlIgnore]
        public bool HasChanged { get; internal set; }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;

                _name = value;
                HasChanged = true;
            }
        }

        public int Top
        {
            get => _top;
            set
            {
                if (_top == value)
                    return;

                _top = value;
                HasChanged = true;
            }
        }

        public int Left
        {
            get => _left;
            set
            {
                if (_left == value)
                    return;

                _left = value;
                HasChanged = true;
            }
        }

        public int Height
        {
            get => _height;
            set
            {
                if (_height == value)
                    return;

                _height = value;
                HasChanged = true;
            }
        }

        public int Width
        {
            get => _width;
            set
            {
                if (_width == value)
                    return;

                _width = value;
                HasChanged = true;
            }
        }

        public bool IsMaximized
        {
            get => _isMaximized;
            set
            {
                if (_isMaximized == value)
                    return;

                _isMaximized = value;
                HasChanged = true;
            }
        }
    }
}
