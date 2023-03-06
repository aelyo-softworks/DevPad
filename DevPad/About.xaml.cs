using System;
using System.Windows;
using System.Windows.Input;
using DevPad.Utilities;

namespace DevPad
{
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();

            Copyright.Text = "Aelyo DevPad V" + AssemblyUtilities.GetInformationalVersion() + " " + AssemblyUtilities.GetConfiguration() + Environment.NewLine +
                "Copyright (C) 2022-" + DateTime.Now.Year + " Aelyo Softworks." + Environment.NewLine +
                "All rights reserved.";
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e) => Close();
        private void Details_Click(object sender, RoutedEventArgs e) => MainWindow.ShowSystemInfo(this);
    }
}
