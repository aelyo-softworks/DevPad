using System;
using System.Diagnostics;
using System.Diagnostics.Eventing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DevPad.Ipc;
using DevPad.Utilities;

namespace DevPad
{
    internal static class Program
    {
#if DEBUG
        private static readonly EventProvider _provider = new EventProvider(new Guid("964d4572-adb9-4f3a-8170-fcbecec27466"));
#endif
        [Conditional("DEBUG")]
        public static void Trace(object value = null, [CallerMemberName] string methodName = null)
        {
#if DEBUG
            _provider.WriteMessageEvent("#PAD(" + Thread.CurrentThread.ManagedThreadId + ")::" + methodName + " " + string.Format("{0}", value), 0, 0);
#endif
        }

        public static WindowsApp WindowsApplication { get; } = new WindowsApp("Aelyo.DevPad", AssemblyUtilities.GetTitle());
        public static bool IsNewInstance { get; } = CommandLine.Current.GetArgument<bool>("newinstance");
        public static bool InDebugMode { get; } = CommandLine.Current.GetArgument("debug",
#if DEBUG
            true
#else
            false
#endif
            );

        [STAThread]
        static void Main()
        {
            if (IntPtr.Size == 4)
            {
                WinformsUtilities.ShowError(null, Resources.Resources.Only64BitWindows);
                return;
            }

            if (CommandLine.Current.GetArgument<bool>("quit"))
            {
                var count = SingleInstance.Quit().Count();
                Environment.ExitCode = 1;
                return;
            }

            if (CommandLine.Current.GetArgument<bool>("ping"))
            {
                WindowsUtilities.AllocConsole();
                foreach (var result in SingleInstance.Ping())
                {
                    var txt = "Ping result: " + result.HResult + " Process id:" + result.Output;
                    Trace(txt);
                    Console.WriteLine(txt);
                }

                Console.WriteLine("Press ESC to terminate.");
                do
                {
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape)
                        break;

                }
                while (true);
                return;
            }

            // try to call already running process
            if (!IsNewInstance && SingleInstance.SendCommandLine(0, Environment.GetCommandLineArgs()).Any(r => r.HResult == 0))
                return;

            WindowsApplication.PublisherName = AssemblyUtilities.GetCompany();
            WindowsApplication.RegisterProcess();

            var unregister = CommandLine.Current.GetArgument("unregister", false);
            if (unregister)
            {
                WindowsApplication.Unregister();
                return;
            }

            SingleInstance.RegisterCommandTarget();
            try
            {
                var app = new App();
                Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
                app.InitializeComponent();
                app.Run();
            }
            finally
            {
                SingleInstance.UnregisterCommandTarget();
            }
        }

        private static bool _errorShown;

        public static void ShowError(Window window, Exception error, bool shutdownOnClose)
        {
            if (error == null || _errorShown)
                return;

            _errorShown = true;
            Trace(error);
            var shutdown = false;
            using (var td = new TaskDialog())
            {
                const int sysInfoId = 1;
                const int quitId = 2;
                td.Event += (s, e2) =>
                {
                    if (e2.Message == TASKDIALOG_NOTIFICATIONS.TDN_BUTTON_CLICKED)
                    {
                        var id = (int)(long)e2.WParam;
                        switch (id)
                        {
                            case sysInfoId:
                                MainWindow.ShowSystemInfo(null);
                                e2.HResult = 1; // S_FALSE => don't close
                                break;

                            case quitId:
                                shutdown = true;
                                break;
                        }
                    };
                };

                td.Flags |= TASKDIALOG_FLAGS.TDF_SIZE_TO_CONTENT | TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION;
                if (!shutdownOnClose)
                {
                    td.CommonButtonFlags |= TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CLOSE_BUTTON;
                }

                td.CollapsedControlText = Resources.Resources.ShowDetails;
                td.ExpandedControlText = Resources.Resources.HideDetails;
                td.ExpandedInformation = error.ToString();
                td.MainIcon = TaskDialog.TD_ERROR_ICON;
                td.Title = WinformsUtilities.ApplicationTitle;
                td.MainInstruction = Resources.Resources.UnhandledException;
                td.CustomButtons.Add(sysInfoId, Resources.Resources.SystemInfo);
                td.CustomButtons.Add(quitId, Resources.Resources.Quit);
                var msg = error.GetAllMessages();
                td.Content = msg;
                td.Show(window);
                if (shutdownOnClose || shutdown)
                {
                    Application.Current.Shutdown();
                }
            }

            _errorShown = false;
        }

        private static void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            ShowError(null, e.Exception, false);
        }
    }
}