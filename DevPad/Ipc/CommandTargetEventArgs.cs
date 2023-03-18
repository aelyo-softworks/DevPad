using System;

namespace DevPad.Ipc
{
    public sealed class CommandTargetEventArgs : EventArgs
    {
        internal bool _outputSet;
        private object _output;

        public CommandTargetEventArgs(Guid commandGroup, int id, object input)
        {
            Group = commandGroup;
            Id = id;
            Input = input;
            HResult = SingleInstance.E_NOTIMPL;
        }

        public Guid Group { get; }
        public int Id { get; }
        public object Input { get; }

        public int HResult { get; set; }
        public object Output
        {
            get => _output;
            set
            {
                if (value == _output)
                    return;

                _output = value;
                _outputSet = true;
            }
        }
    }
}
