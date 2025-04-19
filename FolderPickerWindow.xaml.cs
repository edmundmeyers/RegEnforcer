using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace RegEnforcer;

public partial class FolderPickerWindow : Window
{
    public string SelectedPath { get; set; }

    public FolderPickerWindow()
    {
        InitializeComponent();
        LoadDriveLetters();
    }

    private void LoadDriveLetters()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                DriveComboBox.Items.Add(drive.Name);
            }
        }
        if (DriveComboBox.Items.Count > 0)
        {
            DriveComboBox.SelectedIndex = 0;
        }
    }

    private void DriveComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DriveComboBox.SelectedItem != null)
        {
            LoadFolders(DriveComboBox.SelectedItem.ToString());
        }
    }

    private void LoadFolders(string path)
    {
        FolderTreeView.Items.Clear();
        try
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                var item = CreateTreeViewItem(dir);
                item.Items.Add(null); // Placeholder for lazy loading
                item.Expanded += Folder_Expanded;
                FolderTreeView.Items.Add(item);
            }
        }
        catch (UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show("Access to the folder is denied.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Folder_Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem item && item.Items.Count == 1 && item.Items[0] == null)
        {
            item.Items.Clear();
            var path = item.Tag.ToString();
            try
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var subItem = CreateTreeViewItem(dir);
                    subItem.Items.Add(null); // Placeholder for lazy loading
                    subItem.Expanded += Folder_Expanded;
                    item.Items.Add(subItem);
                }
            }
            catch (UnauthorizedAccessException)
            {
                System.Windows.MessageBox.Show("Access to the folder is denied.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (FolderTreeView.SelectedItem is TreeViewItem selectedItem)
        {
            SelectedPathTextBox.Text = selectedItem.Tag.ToString();
        }
    }

    private TreeViewItem CreateTreeViewItem(string path)
    {
        var item = new TreeViewItem
        {
            Header = System.IO.Path.GetFileName(path),
            Tag = path
        };
        return item;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var path = SelectedPathTextBox.Text;
        if (Directory.Exists(path))
        {
            SelectedPath = path;
            DialogResult = true;
            Close();
        }
        else
        {
            System.Windows.MessageBox.Show("The specified path is not a valid directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(SelectedPath) && Directory.Exists(SelectedPath))
        {
            SelectedPathTextBox.Text = SelectedPath;
            var drive = Path.GetPathRoot(SelectedPath);
            DriveComboBox.SelectedItem = drive;
            LoadFolders(drive);
            ExpandPath(SelectedPath);
        }
    }

    private void ExpandPath(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar);
        TreeViewItem currentItem = null;

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue; // Skip empty parts (e.g., root drive)
            currentItem = FindAndExpandTreeViewItem(currentItem, part);
            if (currentItem == null)
            {
                break;
            }
        }

        if (currentItem != null)
        {
            currentItem.IsSelected = true;
            currentItem.BringIntoView();
        }
    }

    private TreeViewItem FindAndExpandTreeViewItem(TreeViewItem parent, string header)
    {
        ItemCollection items = parent == null ? FolderTreeView.Items : parent.Items;

        foreach (TreeViewItem item in items)
        {
            if (item.Header.ToString() == header)
            {
                item.IsExpanded = true;
                return item;
            }
        }

        return null;
    }
}
