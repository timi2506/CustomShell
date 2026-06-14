using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;

namespace CustomShell
{
    public partial class SettingsWindow : Window
    {
        private ObservableCollection<ShortcutItem> _shortcuts;
        private bool _isUpdatingUI;
        private ShellConfig _config;

        public SettingsWindow()
        {
            InitializeComponent();
            _config = ConfigManager.LoadConfig();
            _shortcuts = new ObservableCollection<ShortcutItem>(_config.Shortcuts);

            GroupWindowsCheckBox.IsChecked = _config.GroupTaskbarWindows;
            if (_config.Theme == "Windows10")
                ThemeWin10Radio.IsChecked = true;
            else
                ThemeWin11Radio.IsChecked = true;
            
            ShortcutsList.ItemsSource = _shortcuts;
        }

        private void ShortcutsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = ShortcutsList.SelectedItem as ShortcutItem;
            if (selected != null)
            {
                EditPanel.IsEnabled = true;
                _isUpdatingUI = true;
                NameTextBox.Text = selected.Name;
                PathTextBox.Text = selected.FilePath;
                ArgsTextBox.Text = selected.Arguments;
                IconPathTextBox.Text = selected.IconPath;
                _isUpdatingUI = false;
            }
            else
            {
                EditPanel.IsEnabled = false;
                _isUpdatingUI = true;
                NameTextBox.Text = "";
                PathTextBox.Text = "";
                ArgsTextBox.Text = "";
                IconPathTextBox.Text = "";
                _isUpdatingUI = false;
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI) return;
            
            var selected = ShortcutsList.SelectedItem as ShortcutItem;
            if (selected != null)
            {
                selected.Name = NameTextBox.Text;
                selected.FilePath = PathTextBox.Text;
                selected.Arguments = ArgsTextBox.Text;
                selected.IconPath = IconPathTextBox.Text;
                ShortcutsList.Items.Refresh();
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var newItem = new ShortcutItem { Name = "New Shortcut" };
            _shortcuts.Add(newItem);
            ShortcutsList.SelectedItem = newItem;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = ShortcutsList.SelectedItem as ShortcutItem;
            if (selected != null)
            {
                _shortcuts.Remove(selected);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _config.Shortcuts = _shortcuts.ToList();
            _config.GroupTaskbarWindows = GroupWindowsCheckBox.IsChecked ?? false;
            _config.Theme = ThemeWin10Radio.IsChecked == true ? "Windows10" : "Windows11";
            ConfigManager.SaveConfig(_config);
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
