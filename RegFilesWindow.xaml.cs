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

public partial class RegFilesWindow : Window
{
    private static RegViewer mainWindow;

    public RegFilesWindow()
    {
        InitializeComponent();
        LoadRegFiles();
    }

    private void LoadRegFiles()
    {
        RegFilesStackPanel.Children.Clear();
        var regFilesFolder = Properties.Settings.Default.RegFilePath;
        if (Directory.Exists(regFilesFolder))
        {
            var regFiles = Directory.GetFiles(regFilesFolder, "*.reg");
            foreach (var regFile in regFiles)
            {
                AddRegFileContent(regFile);
            }
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
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 11
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
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 11,
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
                                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                                FontSize = 11,
                                Foreground = new SolidColorBrush(Colors.Black)
                            };

                            // Add a "fix" TextBlock to update the registry value
                            var fixTextBlock = new TextBlock
                            {
                                Text = "fix",
                                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                                FontSize = 11,
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
        var folderPickerWindow = new FolderPickerWindow
        {
            Title = "Select Folder",
            Width = 400,
            Height = 600
        };
        if (folderPickerWindow.ShowDialog() == true)
        {
            var selectedPath = folderPickerWindow.SelectedPath;
            Properties.Settings.Default.RegFilePath = selectedPath;
            Properties.Settings.Default.Save();
            LoadRegFiles();
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

}

