﻿using System.Diagnostics;
using System.Windows;

namespace Resource_Manager
{
    /// <summary>
    /// Логика взаимодействия для About.xaml
    /// </summary>
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            var psi = new ProcessStartInfo
            {
                FileName = e.Uri.ToString(),
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
