using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DevPad.Utilities;

namespace DevPad.Ipc
{
    public static class SingleInstance
    {
        private static readonly Guid _id = new Guid("5feb27aa-1467-4919-b6b3-252299c4bf1d");

        private static CommandTarget _commandTarget = new CommandTarget(typeof(SingleInstance).FullName);

        public static event EventHandler<SingleInstanceCommandEventArgs> Command;

        public static IEnumerable<CommandResult> SendCommandLine(Guid desktopId, int targetPid = 0, IEnumerable<string> arguments = null) => Send(desktopId, targetPid, SingleInstanceCommandType.SendCommandLine, arguments?.ToArray());
        public static IEnumerable<CommandResult> Quit(Guid desktopId, int targetPid = -1) => Send(desktopId, targetPid, SingleInstanceCommandType.Quit);
        public static IEnumerable<CommandResult> Ping(Guid desktopId, int targetPid = -1) => Send(desktopId, targetPid, SingleInstanceCommandType.Ping);

        private static IEnumerable<CommandResult> Send(Guid desktopId, int targetPid, SingleInstanceCommandType type, params object[] arguments) => Send(desktopId, targetPid, (int)type, arguments);
        private static IEnumerable<CommandResult> Send(Guid desktopId, int targetPid, int type, params object[] arguments)
        {
            var input = new List<object>
            {
                WindowsUtilities.CurrentProcess.Id,
                Environment.CurrentDirectory,
                Environment.UserDomainName,
                Environment.UserName,
                desktopId.ToString("N"),
            };

            if (input.Count != _wellKnownArgs)
                throw new InvalidOperationException();

            if (arguments != null && arguments.Length > 0)
            {
                input.AddRange(arguments);
            }

            return CommandTarget.TryExec(targetPid, _commandTarget.Moniker, _id, type, input.ToArray());
        }

        [DllImport("user32")]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        public static void AllowSetForegroundWindow(Guid desktopId)
        {
            foreach (var result in Ping(desktopId))
            {
                if (result.HResult == 0 && result.Output is int pid && pid > 0)
                {
                    AllowSetForegroundWindow(pid);
                }
            }
        }

        public static void UnregisterCommandTarget() => Interlocked.Exchange(ref _commandTarget, null)?.Dispose();
        public static void RegisterCommandTarget()
        {
            _commandTarget.Command += OnCommand;
            _commandTarget.Register();
        }

        private class SingleInstanceCommand
        {
            public int ProcessId;
            public string CurrentDirectory;
            public string UserDomainName;
            public string UserName;
            public Guid DesktopId;
            public object[] Arguments;

            public static SingleInstanceCommand Parse(CommandTargetEventArgs e)
            {
                if (!(e.Input is object[] args) || args.Length < _wellKnownArgs)
                    return null;

                if (!int.TryParse(string.Format("{0}", args[0]), out var processId))
                    return null;

                if (!(args[1] is string currentDirectory))
                    return null;

                if (!(args[2] is string userDomainName))
                    return null;

                if (!(args[3] is string userName))
                    return null;

                if (!(args[4] is string did) || !Guid.TryParse(did, out var desktopId))
                    return null;

                args = args.Skip(_wellKnownArgs).ToArray();
                var cmd = new SingleInstanceCommand
                {
                    ProcessId = processId,
                    CurrentDirectory = currentDirectory,
                    UserDomainName = userDomainName,
                    UserName = userName,
                    DesktopId = desktopId,
                    Arguments = args
                };
                return cmd;
            }
        }

        private static void OnCommand(object sender, CommandTargetEventArgs e)
        {
            var cmd = SingleInstanceCommand.Parse(e);
            if (cmd == null)
                return;

            var ce = new SingleInstanceCommandEventArgs((SingleInstanceCommandType)e.Id,
                cmd.ProcessId,
                cmd.CurrentDirectory,
                cmd.UserDomainName,
                cmd.UserName,
                cmd.DesktopId,
                cmd.Arguments);
            Command?.Invoke(sender, ce);
            if (ce._outputSet)
            {
                e.Output = ce.Output;
            }

            if (ce.Handled)
            {
                e.HResult = S_OK;
            }
        }

        private const int _wellKnownArgs = 5;

        [DllImport("user32")]
        private static extern bool AllowSetForegroundWindow(int processId);

        internal const int S_OK = 0;
        internal const int E_NOTIMPL = unchecked((int)0x80004001);
        internal const int RPC_E_SERVERFAULT = unchecked((int)0x80010105);
        internal const int RPC_E_INVALID_OBJECT = unchecked((int)0x80010114);
        internal const int RPC_E_INVALID_IPID = unchecked((int)0x80010113);
    }
}
