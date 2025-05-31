using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RegEnforcer;

public partial class RegEnforcerWindow : Window
{
    private double ScreenFontSize = 11.0;

    public List<RegistryFixInfo> RegistryFixes { get; } = new();

    public RegEnforcerWindow()
    {
        InitializeComponent();

        RunAtStartupMenuItem.IsChecked = AppHelper.IsApplicationSetToRunAtStartup();

        // Load window position and size
        this.Top = Properties.Settings.Default.RegFilesWindowTop;
        this.Left = Properties.Settings.Default.RegFilesWindowLeft;
        this.Width = Properties.Settings.Default.RegFilesWindowWidth;
        this.Height = Properties.Settings.Default.RegFilesWindowHeight;

        // Ensure the window is within screen bounds
        var screen = System.Windows.SystemParameters.WorkArea;
        if (this.Top < screen.Top || this.Top + this.Height > screen.Bottom)
            this.Top = screen.Top;
        if (this.Left < screen.Left || this.Left + this.Width > screen.Right)
            this.Left = screen.Left;

        // Load font size
        ScreenFontSize = Properties.Settings.Default.ScreenFontSize;

        // Load dark mode preference
        bool isDarkMode = AppHelper.IsSystemInDarkMode();
        AppHelper.ToggleDarkMode(isDarkMode);

        // Subscribe to SystemEvents.UserPreferenceChanged
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

        LoadRegFiles();
    }





    private void RunAtStartupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AppHelper.SetApplicationToRunAtStartup(RunAtStartupMenuItem.IsChecked);
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            // Check if the system's light/dark mode has changed
            bool isDarkMode = AppHelper.IsSystemInDarkMode();
            AppHelper.ToggleDarkMode(isDarkMode);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);

        // Save window position and size
        Properties.Settings.Default.RegFilesWindowTop = this.Top;
        Properties.Settings.Default.RegFilesWindowLeft = this.Left;
        Properties.Settings.Default.RegFilesWindowWidth = this.Width;
        Properties.Settings.Default.RegFilesWindowHeight = this.Height;

        // Save font size
        Properties.Settings.Default.ScreenFontSize = ScreenFontSize;

        // Persist settings
        Properties.Settings.Default.Save();

        e.Cancel = true;
        this.Visibility = Visibility.Hidden;

        // Unsubscribe from SystemEvents.UserPreferenceChanged
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
    }

     public void LoadRegFiles()
    {
        RegistryFixes.Clear();

        RegFilesStackPanel.Children.Clear();
        var regFilesFolder = Properties.Settings.Default.RegFilePath;
        if (Directory.Exists(regFilesFolder))
        {
            Title = $"RegEnforcer - {regFilesFolder}";

            var regFiles = Directory.GetFiles(regFilesFolder, "*.reg");
            foreach (var regFile in regFiles)
            {
                AddRegFileContent(regFile);
            }
        }
        else
        {
            Title = "RegEnforcer";
        }
    }

    private void AddRegFileContent(string filePath)
    {
        var header = new TextBlock
        {
            Text = $"{Path.GetFileName(filePath)}",
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(Colors.Silver),
            Margin = new Thickness(0, 10, 0, 5),
            Padding = new Thickness(5),
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono"),
            FontSize = ScreenFontSize
        };
        RegFilesStackPanel.Children.Add(header);

        var content = File.ReadAllLines(filePath);
        string currentKey = null;
        for (int i = 0; i < content.Length; i++)
        {
            var line = content[i];
            if (i == 1 && string.IsNullOrWhiteSpace(line))
            {
                continue; // Skip the second line if it is blank
            }
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentKey = line.Trim('[', ']');
            }
            if (!line.StartsWith("Windows Registry Editor Version 5.00") && !string.IsNullOrEmpty(line) && currentKey != null)
            {
                // Handle multi-line values
                while (line.EndsWith("\\"))
                {
                    line = line.TrimEnd('\\') + content[++i].Trim();
                }

                var stackPanel = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Margin = new Thickness(0, 5, 0, 5) // Add margin around the keys and values
                };

                var textBlock = new TextBlock
                {
                    Text = line,
                    Foreground = (SolidColorBrush)System.Windows.Application.Current.FindResource("TextColor"),
                    FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono"),
                    FontSize = ScreenFontSize,
                    FontWeight = line.EndsWith("]") ? FontWeights.Bold : FontWeights.Normal,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                textBlock.MouseEnter += (sender, e) => textBlock.TextDecorations = TextDecorations.Underline;
                textBlock.MouseLeave += (sender, e) => textBlock.TextDecorations = null;
                textBlock.MouseLeftButtonUp += (sender, e) => OpenMainWindow(currentKey);

                if (line.StartsWith(";"))
                {
                    textBlock.Foreground = new SolidColorBrush(Colors.Green);
                }
                else if (!line.EndsWith("]"))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        var valueName = parts[0].Trim('"');
                        var regValue = RegHelper.RegistryValueToString(RegHelper.GetRegistryValue(currentKey, valueName));
                        var regFileValue = parts[1].Trim('"');
                        var registryFixInfo = new RegistryFixInfo { Key = currentKey, ValueName = valueName, Value = regFileValue, FoundValue = regValue };
                        RegistryFixes.Add(registryFixInfo);

                        //if (regValue != null && !RegHelper.CompareRegistryValues(regValue, regFileValue))
                        if (regValue != regFileValue)
                        {
                            textBlock.Foreground = new SolidColorBrush(Colors.Red);

                            // Add a new TextBlock to display the current registry value
                            var registryValueTextBlock = new TextBlock
                            {
                                Text = $" (Current: \"{regValue}\")",
                                FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono"),
                                FontSize = ScreenFontSize,
                                Foreground = (SolidColorBrush)System.Windows.Application.Current.FindResource("TextColor")
                            };

                            // Add a "fix" TextBlock to update the registry value
                            var fixTextBlock = new TextBlock
                            {
                                Text = "fix",
                                FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono"),
                                FontSize = ScreenFontSize,
                                Margin = new Thickness(6, 0, 0, 0),
                                Foreground = new SolidColorBrush(Colors.Blue),
                                TextDecorations = TextDecorations.Underline,
                                Cursor = System.Windows.Input.Cursors.Hand,
                                Tag = registryFixInfo
                            };
                            fixTextBlock.MouseLeftButtonUp += FixTextBlock_MouseLeftButtonUp;

                            stackPanel.Children.Add(textBlock);
                            stackPanel.Children.Add(registryValueTextBlock);
                            stackPanel.Children.Add(fixTextBlock);
                        }
                        else
                        {
                            stackPanel.Children.Add(textBlock);
                        }
                    }
                }
                else
                {
                    stackPanel.Children.Add(textBlock);
                }

                RegFilesStackPanel.Children.Add(stackPanel);
            }
        }
    }

    private void FixTextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBlock textBlock && textBlock.Tag is RegistryFixInfo fixInfo)
        {
            var key = fixInfo.Key;
            var valueName = fixInfo.ValueName;
            var value = fixInfo.Value;

            try
            {
                var parts = key.Split(new[] { '\\' }, 2);
                if (parts.Length != 2)
                {
                    System.Windows.MessageBox.Show($"Invalid registry path: {key}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var rootKey = parts[0];
                var subKeyPath = parts[1];

                RegistryKey rootRegistryKey = null;

                // Determine the root key
                switch (rootKey.ToUpper())
                {
                    case "HKEY_LOCAL_MACHINE":
                        rootRegistryKey = Registry.LocalMachine;
                        break;
                    case "HKEY_CURRENT_USER":
                        rootRegistryKey = Registry.CurrentUser;
                        break;
                    case "HKEY_CLASSES_ROOT":
                        rootRegistryKey = Registry.ClassesRoot;
                        break;
                    case "HKEY_USERS":
                        rootRegistryKey = Registry.Users;
                        break;
                    case "HKEY_CURRENT_CONFIG":
                        rootRegistryKey = Registry.CurrentConfig;
                        break;
                    default:
                        System.Windows.MessageBox.Show($"Unknown root key: {rootKey}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                }

                // Open the subkey and set the value
                using (var subKey = rootRegistryKey.OpenSubKey(subKeyPath, writable: true))
                {
                    if (subKey != null)
                    {
                        subKey.SetValue(valueName, value);
                        System.Windows.MessageBox.Show($"Registry value updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadRegFiles(); // Refresh the screen
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"Registry key not found: {key}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error updating registry: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OpenMainWindow(string keyPath)
    {
        try
        {
            // Set the LastKey value for RegEdit
            using (var regeditKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit", true))
            {
                if (regeditKey != null)
                {
                    regeditKey.SetValue("LastKey", keyPath, RegistryValueKind.String);
                }
            }

            // Launch RegEdit
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "regedit.exe",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to open RegEdit: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    private void SelectFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = FolderPicker.ShowDialog("Select Folder of .Reg Files");
        if (!string.IsNullOrEmpty(selectedPath))
        {
            Properties.Settings.Default.RegFilePath = selectedPath;
            Properties.Settings.Default.Save();
            LoadRegFiles();
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        this.Visibility = Visibility.Hidden;
    }



    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Adjust the vertical offset based on the mouse wheel delta
            ScreenFontSize += e.Delta * 0.01;
            if (ScreenFontSize < 1) ScreenFontSize = 1;
            UpdateFontSize(RegFilesStackPanel);

            // Mark the event as handled to prevent further propagation
            e.Handled = true;
        }
    }

    private void UpdateFontSize(StackPanel stackPanel)
    {
        foreach (UIElement ui in stackPanel.Children)
        {

            if (ui is TextBlock textBlock)
            {
                textBlock.FontSize = ScreenFontSize;
            } 
            else if (ui is StackPanel childStackPanel)
            {
                UpdateFontSize(childStackPanel);
            }
        }
    }
  
}

