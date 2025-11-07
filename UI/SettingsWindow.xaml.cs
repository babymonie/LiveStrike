using System;
using System.Windows;
using Models;

namespace UI
{
    public partial class SettingsWindow : Window
    {
        private AppSettings settings;
        
        public AppSettings Settings => settings;

        public SettingsWindow()
        {
            InitializeComponent();
            settings = AppSettings.Load();
            LoadSettings();
        }

        private void LoadSettings()
        {
            EnableAnimationsCheck.IsChecked = settings.EnableAnimations;
            HoverOpacitySlider.Value = settings.HoverOpacity;
            HoverOpacityLabel.Text = $"{(int)(settings.HoverOpacity * 100)}%";
            
            PollingIntervalSlider.Value = settings.PollingIntervalMs;
            PollingIntervalLabel.Text = $"{settings.PollingIntervalMs}ms";
            
            AutoStartServerCheck.IsChecked = settings.AutoStartNodeServer;
            StartOnLoginCheck.IsChecked = settings.StartOnLogin;
            MinimizeToTrayCheck.IsChecked = settings.MinimizeToTray;
            
            // Set theme selection
            foreach (System.Windows.Controls.ComboBoxItem item in ThemeComboBox.Items)
            {
                if (item.Tag?.ToString() == settings.Theme)
                {
                    ThemeComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void HoverOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (HoverOpacityLabel != null)
            {
                HoverOpacityLabel.Text = $"{(int)(e.NewValue * 100)}%";
            }
        }

        private void PollingIntervalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PollingIntervalLabel != null)
            {
                PollingIntervalLabel.Text = $"{(int)e.NewValue}ms";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save all settings
            settings.EnableAnimations = EnableAnimationsCheck.IsChecked ?? true;
            settings.HoverOpacity = HoverOpacitySlider.Value;
            settings.PollingIntervalMs = (int)PollingIntervalSlider.Value;
            settings.AutoStartNodeServer = AutoStartServerCheck.IsChecked ?? true;
            settings.StartOnLogin = StartOnLoginCheck.IsChecked ?? false;
            settings.MinimizeToTray = MinimizeToTrayCheck.IsChecked ?? true;
            
            if (ThemeComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedTheme)
            {
                settings.Theme = selectedTheme.Tag?.ToString() ?? "Dark";
            }

            settings.Save();
            DialogResult = true;
            Close();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            settings = new AppSettings(); // Reset to defaults
            LoadSettings();
        }
    }
}