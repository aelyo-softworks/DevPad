using System;
using System.Diagnostics;
using System.Diagnostics.Eventing;
using System.Runtime.CompilerServices;
using System.Threading;
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

        [STAThread]
        static void Main()
        {
            if (IntPtr.Size == 4)
            {
                WinformsUtilities.ShowError(null, Resources.Resources.Only64BitWindows);
                return;
            }

            WindowsApplication.PublisherName = AssemblyUtilities.GetCompany();

            // for some reason, if I add these, they will not appear as recent items in TaskBar's JumpList...
            //WindowsApplication.FileExtensions.Add(".txt");
            //WindowsApplication.FileExtensions.Add(".json");
            //WindowsApplication.FileExtensions.Add(".xml");
            //WindowsApplication.FileExtensions.Add(".csv");
            WindowsApplication.RegisterProcess();

            var unregister = CommandLine.GetArgument("unregister", false);
            if (unregister)
            {
                WindowsApplication.Unregister();
                return;
            }

            WindowsApplication.Register();
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
