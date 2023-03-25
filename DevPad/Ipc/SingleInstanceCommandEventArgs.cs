using System;
using System.ComponentModel;
using System.Diagnostics;

namespace DevPad.Ipc
{
    public class SingleInstanceCommandEventArgs : HandledEventArgs
    {
        internal bool _outputSet;
        private object _output;
        private readonly Lazy<Process> _callingProcess;

        internal SingleInstanceCommandEventArgs(SingleInstanceCommandType type, int callingProcessId, string userDomainName, string userName, Guid callingDesktopId, object[] arguments)
        {
            Type = type;
            CallingProcessId = callingProcessId;
            UserDomainName = userDomainName;
            UserName = userName;
            CallingDesktopId = callingDesktopId;
            _callingProcess = new Lazy<Process>(() => LoadProcess(CallingProcessId));
            Arguments = arguments ?? Array.Empty<object>();
        }

        public SingleInstanceCommandType Type { get; }
        public string UserDomainName { get; }
        public string UserName { get; }
        public Guid CallingDesktopId { get; }
        public int CallingProcessId { get; }
        public object[] Arguments { get; }
        public Process CallingProcess => _callingProcess.Value;
        public string CallingProcessName => CallingProcess?.ProcessName ?? CallingProcessId.ToString();
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

        private static Process LoadProcess(int id)
        {
            if (id == 0)
                return null;

            try
            {
                return Process.GetProcessById(id);
            }
            catch
            {
                return null;
            }
        }
    }
}
