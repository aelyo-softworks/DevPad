using System.Windows.Forms;

namespace DevPad
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            Settings = Settings.Current.Clone();
            Settings.PropertyChanged += (s, e) =>
            {
                propertyGridSettings.Refresh();
            };

            InitializeComponent();
            Icon = Resources.Resources.DevPadIcon;

            MinimumSize = Size;
            propertyGridSettings.SelectedObject = Settings;
        }

        public Settings Settings { get; }
    }
}
