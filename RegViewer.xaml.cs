using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using MessageBox = System.Windows.Forms.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using Image = System.Windows.Controls.Image;

namespace RegEnforcer;

public partial class RegViewer : Window
{
    public RegViewer()
    {
        InitializeComponent();
        LoadRegistryKeys();
        LoadRegFilesFolder();
    }

    private void LoadRegistryKeys()
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine, Registry.ClassesRoot, Registry.Users, Registry.CurrentConfig })
        {
            var rootNode = CreateTreeViewItem(hive.Name, hive);
            rootNode.Items.Add(null); // Placeholder for lazy loading
            rootNode.Expanded += RootNode_Expanded;
            RegistryTreeView.Items.Add(rootNode);
        }
    }

    private void RootNode_Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem item && item.Items.Count == 1 && item.Items[0] == null)
        {
            item.Items.Clear();
            if (item.Tag is RegistryKey key)
            {
                try
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        var subKey = key.OpenSubKey(subKeyName);
                        var subItem = CreateTreeViewItem(subKeyName, subKey);
                        subItem.Items.Add(null); // Placeholder for lazy loading
                        subItem.Expanded += RootNode_Expanded;
                        item.Items.Add(subItem);
                    }
                }
                catch (System.Security.SecurityException)
                {
                    MessageBox.Show("Access to the registry key is denied.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    private void RegistryTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (RegistryTreeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is RegistryKey key)
        {
            var values = new List<RegistryValue>();
            try
            {
                foreach (var valueName in key.GetValueNames())
                {
                    values.Add(new RegistryValue { Name = valueName, Value = key.GetValue(valueName)?.ToString() });
                }
            }
            catch (System.Security.SecurityException)
            {
                MessageBox.Show("Access to the registry key is denied.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RegistryDataGrid.ItemsSource = values;

            // Update the window title with the full key path
            Title = $"Registry Viewer - {key.Name}";
        }
    }

    private TreeViewItem CreateTreeViewItem(string header, object tag)
    {
        var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var image = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/RegEnforcer;component/Resources/folder.png")),
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 5, 0)
        };
        var textBlock = new TextBlock { Text = header };
        stackPanel.Children.Add(image);
        stackPanel.Children.Add(textBlock);

        return new TreeViewItem
        {
            Header = stackPanel,
            Tag = tag
        };
    }

    public void DrillToKey(string keyPath)
    {
        var parts = keyPath.Split('\\');
        TreeViewItem currentItem = null;

        foreach (var part in parts)
        {
            currentItem = FindAndExpandTreeViewItem(currentItem, part);
            if (currentItem == null)
            {
                MessageBox.Show($"Key not found: {keyPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        currentItem.IsSelected = true;
        currentItem.BringIntoView();
    }

    private TreeViewItem FindAndExpandTreeViewItem(TreeViewItem parent, string header)
    {
        ItemCollection items = parent == null ? RegistryTreeView.Items : parent.Items;

        foreach (TreeViewItem item in items)
        {
            if (item.Header is StackPanel stackPanel && stackPanel.Children[1] is TextBlock textBlock && textBlock.Text == header)
            {
                item.IsExpanded = true;
                return item;
            }
        }

        return null;
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
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    private void LoadRegFilesFolder()
    {
        var regFilePath = Properties.Settings.Default.RegFilePath;
        if (string.IsNullOrEmpty(regFilePath) || !Directory.Exists(regFilePath))
        {
            regFilePath = AppDomain.CurrentDomain.BaseDirectory;
            Properties.Settings.Default.RegFilePath = regFilePath;
            Properties.Settings.Default.Save();
        }
        // Use regFilePath as needed
    }
}

public class RegistryValue
{
    public string Name { get; set; }
    public string Value { get; set; }
}