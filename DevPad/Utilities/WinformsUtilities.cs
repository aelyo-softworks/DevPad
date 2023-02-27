using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

#if SETUP
using Resources = DevPad.Setup.Resources;
#endif

namespace DevPad.Utilities
{
    public static class WinformsUtilities
    {
        public static string ApplicationName => AssemblyUtilities.GetTitle();
        public static string ApplicationVersion => AssemblyUtilities.GetFileVersion();
        public static string ApplicationTitle => ApplicationName + " V" + ApplicationVersion;

        public static void ShowMessage(this IWin32Window owner, string text) => MessageBox.Show(owner, text, ApplicationTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        public static DialogResult ShowConfirm(this IWin32Window owner, string text) => MessageBox.Show(owner, text, ApplicationTitle + " - " + Resources.Resources.Confirmation, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        public static DialogResult ShowQuestion(this IWin32Window owner, string text) => MessageBox.Show(owner, text, ApplicationTitle + " - " + Resources.Resources.Confirmation, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        public static void ShowError(this IWin32Window owner, string text) => MessageBox.Show(owner, text, ApplicationTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        public static void ShowWarning(this IWin32Window owner, string text) => MessageBox.Show(owner, text, ApplicationTitle + " - " + Resources.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);

        public static void HideDropDowns(this MenuStrip menu)
        {
            if (menu == null)
                return;

            foreach (var item in menu.Items.OfType<ToolStripDropDownItem>())
            {
                item.HideDropDown();
            }
        }

        public static void SafeBeginInvoke(this Control control, Action action)
        {
            if (control == null)
                throw new ArgumentNullException(nameof(control));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (control.InvokeRequired)
            {
                control.BeginInvoke(action);
                return;
            }

            action();
        }

        public static IAsyncResult BeginInvoke(this Control control, Action action)
        {
            if (control == null)
                throw new ArgumentNullException(nameof(control));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            return control.BeginInvoke(action);
        }

        public static Task<T> BeginInvoke<T>(this Control control, Func<T> action)
        {
            if (control == null)
                throw new ArgumentNullException(nameof(control));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (!control.IsHandleCreated)
                return Task.FromResult<T>(default);

            return Task.Factory.FromAsync(control.BeginInvoke(action), r => (T)control.EndInvoke(r));
        }
    }
}
