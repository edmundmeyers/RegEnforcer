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
    private static RegViewer mainWindow;

    private double ScreenFontSize = 11.0;

    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "RegEnforcer";

    public RegEnforcerWindow()
    {
        InitializeComponent();

        RunAtStartupMenuItem.IsChecked = IsApplicationSetToRunAtStartup();

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
        bool isDarkMode = IsSystemInDarkMode();  // false; // IsSystemInDarkMode();
        ToggleDarkMode(isDarkMode);

        // Subscribe to SystemEvents.UserPreferenceChanged
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

        LoadRegFiles();
    }

    public bool IsApplicationSetToRunAtStartup()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: false))
            {
                if (key != null)
                {
                    var value = key.GetValue(AppName);
                    return value != null && value.ToString() == System.Reflection.Assembly.GetExecutingAssembly().Location;
                }
            }
        }
        catch
        {
            // Log or handle exceptions as needed
        }

        return false;
    }

    private void SetApplicationToRunAtStartup(bool enable)
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: true))
            {
                if (key != null)
                {
                    if (enable)
                    {
                        // Add the application to the startup list
                        key.SetValue(AppName, System.Reflection.Assembly.GetExecutingAssembly().Location);
                    }
                    else
                    {
                        // Remove the application from the startup list
                        key.DeleteValue(AppName, throwOnMissingValue: false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to update startup setting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    private void RunAtStartupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (RunAtStartupMenuItem.IsChecked)
        {
            // Enable "Run at Startup"
            SetApplicationToRunAtStartup(true);
        }
        else
        {
            // Disable "Run at Startup"
            SetApplicationToRunAtStartup(false);
        }
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            // Check if the system's light/dark mode has changed
            bool isDarkMode = IsSystemInDarkMode();
            ToggleDarkMode(isDarkMode);
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

        // Unsubscribe from SystemEvents.UserPreferenceChanged
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
    }

    private bool IsSystemInDarkMode()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                if (key != null)
                {
                    var value = key.GetValue("AppsUseLightTheme");
                    if (value is int intValue)
                    {
                        return intValue == 0; // 0 = Dark Mode, 1 = Light Mode
                    }
                }
            }
        }
        catch
        {
            // Default to light mode if the registry key is not accessible
        }

        return false;
    }

    private void LoadRegFiles()
    {
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
                        var regValue = GetRegistryValue(currentKey, valueName);
                        var regFileValue = parts[1].Trim('"');

                        if (regValue != null && !CompareRegistryValues(regValue, regFileValue))
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
                                Tag = new RegistryFixInfo { Key = currentKey, ValueName = valueName, Value = regFileValue }
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
        if (mainWindow == null)
        {
            mainWindow = new RegViewer();
            mainWindow.Closed += (s, e) => mainWindow = null;

            // Center the MainWindow over the RegFilesWindow
            mainWindow.Left = this.Left + (this.Width - mainWindow.Width) / 2;
            mainWindow.Top = this.Top + (this.Height - mainWindow.Height) / 2;

            mainWindow.Show();
        }

        mainWindow.DrillToKey(keyPath);
    }

    private object GetRegistryValue(string fullPath, string valueName)
    {
        try
        {
            // Split the full path into root key and subkey
            var parts = fullPath.Split(new[] { '\\' }, 2);
            if (parts.Length != 2)
            {
                Console.WriteLine($"Invalid registry path: {fullPath}");
                return null;
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
                    Console.WriteLine($"Unknown root key: {rootKey}");
                    return null;
            }

            // Open the subkey and get the value without expanding environment variables
            using (var subKey = rootRegistryKey.OpenSubKey(subKeyPath))
            {
                if (subKey != null)
                {
                    return subKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                }
            }

            // Log the key path and value name if not found
            Console.WriteLine($"Registry key or value not found: {fullPath}\\{valueName}");
        }
        catch (Exception ex)
        {
            // Log the exception
            Console.WriteLine($"Error accessing registry: {ex.Message}");
        }

        return null;
    }

    private bool CompareRegistryValues(object regValue, string regFileValue)
    {
        if (regFileValue.StartsWith("hex:"))
        {
            regFileValue = regFileValue.Substring(4);
            var regFileBytes = ParseRegFileBinaryValue(regFileValue);
            return regValue is byte[] byteArrayValue && byteArrayValue.SequenceEqual(regFileBytes);
        }
        if (regFileValue.StartsWith("dword:"))
        {
            regFileValue = regFileValue.Substring(6);
            return regValue is int intValue && intValue.ToString("x8") == regFileValue;
        }
        if (regFileValue.StartsWith("hex(b):"))
        {
            regFileValue = regFileValue.Substring(7);
            var regFileBytes = ParseRegFileBinaryValue(regFileValue);
            return regValue is long longValue && BitConverter.GetBytes(longValue).SequenceEqual(regFileBytes);
        }
        if (regFileValue.StartsWith("hex(7):"))
        {
            regFileValue = regFileValue.Substring(7);
            var regFileStrings = ParseRegFileMultiStringValue(regFileValue);
            return regValue is string[] multiStringValue && multiStringValue.SequenceEqual(regFileStrings);
        }
        if (regFileValue.StartsWith("hex(2):"))
        {
            regFileValue = regFileValue.Substring(7);
            var regFileString = ParseRegFileExpandableStringValue(regFileValue);
            return regValue is string strValue && strValue == regFileString;
        }

        return regValue.ToString() == regFileValue;
    }

    private byte[] ParseRegFileBinaryValue(string regFileValue)
    {
        try
        {
            var hexValues = regFileValue.Split(',');
            var bytes = new byte[hexValues.Length];
            for (int i = 0; i < hexValues.Length; i++)
            {
                bytes[i] = Convert.ToByte(hexValues[i].Trim(), 16);
            }
            return bytes;
        }
        catch (FormatException ex)
        {
            Console.WriteLine($"Error parsing binary value: {ex.Message}");
            return Array.Empty<byte>();
        }
        catch (OverflowException ex)
        {
            Console.WriteLine($"Error parsing binary value: {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    private string[] ParseRegFileMultiStringValue(string regFileValue)
    {
        // Split the comma-delimited string and convert it to a byte array
        string[] hexValues = regFileValue.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        byte[] hexData = new byte[hexValues.Length];

        for (int i = 0; i < hexValues.Length; i++)
        {
            hexData[i] = Convert.ToByte(hexValues[i].Trim(), 16); // Convert each hex string to a byte
        }

        // Decode the byte array into a Unicode string
        string decodedString = Encoding.Unicode.GetString(hexData);

        // Split the string by null characters ('\0') and return the result
        string[] result = decodedString.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);

        return result;
    }

    private string ParseRegFileExpandableStringValue(string regFileValue)
    {
        // Split the comma-delimited string and convert it to a byte array
        string[] hexValues = regFileValue.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        byte[] hexData = new byte[hexValues.Length];

        for (int i = 0; i < hexValues.Length; i++)
        {
            hexData[i] = Convert.ToByte(hexValues[i].Trim(), 16); // Convert each hex string to a byte
        }

        // Decode the byte array into a Unicode string
        string decodedString = Encoding.Unicode.GetString(hexData);

        return decodedString.TrimEnd('\0');
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
        this.Close();
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
    private void ToggleDarkMode(bool isDarkMode)
    {
        var appResources = System.Windows.Application.Current.Resources;

        appResources.MergedDictionaries.Clear();

        if (isDarkMode)
        {
            // Load Dark Mode resources
            var darkModeResource = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/DarkMode.xaml")
            };
            appResources.MergedDictionaries.Add(darkModeResource);
        }
        else
        {
            // Load Light Mode resources
            var lightModeResource = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/LightMode.xaml")
            };
            appResources.MergedDictionaries.Add(lightModeResource);
        }
    }


}

