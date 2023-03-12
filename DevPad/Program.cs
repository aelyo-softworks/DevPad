using System;
using System.Diagnostics;
using System.Diagnostics.Eventing;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
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
        public static bool InDebugMode { get; } = CommandLine.GetArgument("debug",
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

            WindowsApplication.PublisherName = AssemblyUtilities.GetCompany();
            WindowsApplication.RegisterProcess();

            var unregister = CommandLine.GetArgument("unregister", false);
            if (unregister)
            {
                WindowsApplication.Unregister();
                return;
            }

            var app = new App();
            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            app.InitializeComponent();
            app.Run();
        }

        private static void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Trace(e.Exception);
            e.Handled = true;
            using (var td = new TaskDialog())
            {
                const int sysInfoId = 1;
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
                        }
                    };
                };

                td.Flags |= TASKDIALOG_FLAGS.TDF_SIZE_TO_CONTENT | TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION;
                td.CommonButtonFlags |= TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CLOSE_BUTTON;
                td.CollapsedControlText = Resources.Resources.ShowDetails;
                td.ExpandedControlText = Resources.Resources.HideDetails;
                td.ExpandedInformation = e.Exception.ToString();
                td.MainIcon = TaskDialog.TD_ERROR_ICON;
                td.Title = WinformsUtilities.ApplicationTitle;
                td.MainInstruction = Resources.Resources.UnhandledException;
                td.CustomButtons.Add(sysInfoId, Resources.Resources.SystemInfo);
                var msg = e.Exception.GetAllMessages();
                td.Content = msg;
                td.Show(IntPtr.Zero);
                Application.Current.Shutdown();
            }
        }
    }
}