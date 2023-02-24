using System;
using System.Diagnostics;
using System.Diagnostics.Eventing;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
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

        public static WindowsApp WindowsApplication { get; } = new WindowsApp(AssemblyUtilities.GetTitle(), AssemblyUtilities.GetTitle(), AssemblyUtilities.GetDescription());

        [STAThread]
        static void Main()
        {
            WindowsApplication.PublisherName = AssemblyUtilities.GetCompany();
            WindowsApplication.FileExtensions.Add(".txt");
            WindowsApplication.FileExtensions.Add(".json");
            WindowsApplication.FileExtensions.Add(".xml");
            WindowsApplication.FileExtensions.Add(".csv");
            WindowsApplication.RegisterProcess();
            WindowsApplication.Register();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Main());
        }
    }
}
