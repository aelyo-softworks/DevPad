using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;

namespace DevPad.Ipc
{
    public sealed class CommandTarget : CommandTarget.IOleCommandTarget, IDisposable
    {
        private int _cookie;
        public const string Delimiter = "!";

        public event EventHandler<CommandTargetEventArgs> Command;

        public CommandTarget(string moniker)
        {
            if (moniker == null)
                throw new ArgumentNullException(nameof(moniker));

            Moniker = moniker;
        }

        public string Moniker { get; }

        public void Register() => _cookie = RunningObjectTable.Register(this);
        public void Revoke()
        {
            var cookie = Interlocked.Exchange(ref _cookie, 0);
            if (cookie != 0)
            {
                RunningObjectTable.Revoke(cookie);
            }
        }

        public void Dispose() => Revoke();
        public static bool CanRetryOnError(int error) => error == SingleInstance.RPC_E_INVALID_OBJECT || error == SingleInstance.RPC_E_SERVERFAULT || error == SingleInstance.RPC_E_INVALID_IPID;

        // targetPid = 0 => first
        // targetPid = -1 => all
        // targetPid = X => process id X
        public static IEnumerable<CommandResult> TryExec(int targetPid, string moniker, Guid commandGroup, int commandId, object input)
        {
            if (moniker == null)
                throw new ArgumentNullException(nameof(moniker));

            foreach (var obj in GetObjects(targetPid, moniker))
            {
                if (!(obj is IOleCommandTarget target))
                {
                    yield return new CommandResult(SingleInstance.RPC_E_INVALID_IPID, null);
                    continue;
                }

                yield return getResult();

                CommandResult getResult()
                {
                    try
                    {
                        object output = null;
                        var hr = target.Exec(commandGroup, commandId, 0, ref input, ref output);
                        if (hr < 0)
                            return new CommandResult(hr, null);

                        return new CommandResult(hr, output);
                    }
                    catch
                    {
                        return new CommandResult(SingleInstance.RPC_E_SERVERFAULT, null);
                    }
                }
            }
        }

        private static IEnumerable<object> GetObjects(int targetPid, string moniker)
        {
            if (targetPid > 0)
            {
                var obj = RunningObjectTable.GetObject(moniker + ":" + targetPid, throwOnError: false);
                if (obj != null)
                    yield return obj;

                yield break;
            }

            CreateBindCtx(0, out var ctx);
            foreach (var mk in RunningObjectTable.Enumerate(false))
            {
                var name = getName();
                if (name == null)
                    continue;

                if (name.StartsWith(Delimiter + moniker))
                {
                    var robj = RunningObjectTable.GetObject(mk, false);
                    if (robj != null)
                    {
                        yield return robj;
                        if (targetPid == 0)
                            break;
                    }
                }

                string getName()
                {
                    try
                    {
                        mk.GetDisplayName(ctx, null, out var dn);
                        return dn;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        }

        int IOleCommandTarget.QueryStatus(Guid commandGroup, int count, IntPtr commands, IntPtr text) => SingleInstance.E_NOTIMPL;
        int IOleCommandTarget.Exec(Guid commandGroup, int commandId, int options, ref object input, ref object output)
        {
            //Program.Trace("commandGroup: " + commandGroup + " commandId:" + commandId + "options:" + options + " input:" + input);
            var e = new CommandTargetEventArgs(commandGroup, commandId, input);
            Command?.Invoke(this, e);

            if (e._outputSet)
            {
                output = e.Output;
            }
            return e.HResult;
        }

        [ComImport, Guid("b722bccb-4e68-101b-a2bc-00aa00404770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IOleCommandTarget
        {
            [PreserveSig]
            int QueryStatus([MarshalAs(UnmanagedType.LPStruct)] Guid commandGroup, int count, IntPtr commands, IntPtr text);

            [PreserveSig]
            int Exec([MarshalAs(UnmanagedType.LPStruct)] Guid commandGroup, int commandId, int commandOptions, ref object input, ref object output);
        }

        [DllImport("ole32")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);
    }
}
