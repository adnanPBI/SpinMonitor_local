using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using SpinMonitor.Services;

namespace SpinMonitor
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;
        private string? _originalPassword;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            _originalPassword = _settings.MySQL.Password;
            DataContext = _settings;

            // ✅ Show masked password if exists
            if (!string.IsNullOrEmpty(_settings.MySQL.Password))
            {
                MySqlPasswordBox.Password = _settings.MySQL.Password;
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            dlg.Description = "Select folder with MP3s";
            dlg.ShowNewFolderButton = true;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _settings.LibraryFolder = dlg.SelectedPath;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // ✅ Update MySQL password if changed
            if (!string.IsNullOrWhiteSpace(MySqlPasswordBox.Password))
            {
                _settings.MySQL.Password = MySqlPasswordBox.Password;
            }
            else if (string.IsNullOrWhiteSpace(_originalPassword))
            {
                // If no original password and field is empty, clear it
                _settings.MySQL.Password = "";
            }
            // Otherwise keep original password

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}