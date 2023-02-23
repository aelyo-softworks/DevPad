using System;
using System.Reflection;
using System.Windows.Forms;

namespace DevPad
{
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();
            Icon = Resources.Resources.DevPadIcon;
            pictureBoxIcon.Image = Resources.Resources.DevPadIcon.ToBitmap();
            var asm = Assembly.GetEntryAssembly();
            var text = asm?.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description + " V" + asm?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            text += Environment.NewLine + asm?.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
            labelText.Text = text;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pictureBoxIcon.Image?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
