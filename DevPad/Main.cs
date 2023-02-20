using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevPad.MonacoModel;
using Microsoft.Web.WebView2.Core;

namespace DevPad
{
    public partial class Main : Form
    {
        private readonly DevPadObject _dpo = new DevPadObject();
        private string _load;

        public Main()
        {
            InitializeComponent();
            Icon = Resources.Resources.DevPadIcon;
            _dpo.Load += DevPadOnLoad;
            _ = InitializeAsync();
        }

        public bool IsMonacoReady { get; private set; }

        private async Task InitializeAsync()
        {
            var udf = Settings.Current.UserDataFolder;
            var env = await CoreWebView2Environment.CreateAsync(null, udf);
            await WebView.EnsureCoreWebView2Async(env);
            WebView.CoreWebView2.AddHostObjectToScript("devPad", _dpo);
            WebView.Source = new Uri(Path.Combine(Application.StartupPath, @"Monaco\index.html"));
            IsMonacoReady = true;
        }

        private void DevPadOnLoad(object sender, DevPadLoadEventArgs e)
        {
            e.Load = _load;
        }

        private void NewWindowToolStripMenuItem_Click(object sender, EventArgs e) => Process.Start(Application.ExecutablePath);
        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void NewToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private async void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var languages = await WebView.GetLanguages();

            var sb = new StringBuilder();
            var index = 0;
            foreach (var kv in languages.OrderBy(k => k.Value.Name))
            {
                if (kv.Value.Extensions == null || kv.Value.Extensions.Length == 0)
                    continue;

                if (sb.Length > 0)
                {
                    sb.Append('|');
                }
                sb.Append(string.Format(Resources.Resources.OneFileFilter, kv.Value.Name, "*" + string.Join(";*", kv.Value.Extensions)));
                index++;
            }

            sb.Append(Resources.Resources.AllFilesFilter);

            var fd = new OpenFileDialog
            {
                RestoreDirectory = true,
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = sb.ToString(),
                FilterIndex = index + 1
            };
            if (fd.ShowDialog(this) != DialogResult.OK)
                return;

            var ext = Path.GetExtension(fd.FileName);
            var langs = await WebView.GetLanguagesByExtension();
            string lang;
            if (langs.TryGetValue(ext, out var langObject))
            {
                lang = langObject[0].Id;
            }
            else
            {
                lang = "plaintext";
            }

            _load = File.ReadAllText(fd.FileName);
            await WebView.ExecuteScriptAsync($"loadFromHost('{lang}')");
            Text = "DevPad - " + fd.FileName;
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
    }
}
