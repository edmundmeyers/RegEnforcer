using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RegEnforcer;

public static class FolderPicker
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO lpbi);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct BROWSEINFO
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public IntPtr pszDisplayName;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    private const uint BIF_RETURNONLYFSDIRS = 0x0001; // Only return file system directories
    private const uint BIF_NEWDIALOGSTYLE = 0x0040;   // Use the new style dialog (if available)


    public static string ShowDialog(string title = "Select Folder")
    {
        var browseInfo = new BROWSEINFO
        {
            hwndOwner = IntPtr.Zero,
            pidlRoot = IntPtr.Zero,
            pszDisplayName = Marshal.AllocHGlobal(260), // Allocate memory for the display name
            lpszTitle = title,
            ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE,
            lpfn = IntPtr.Zero,
            lParam = IntPtr.Zero,
            iImage = 0
        };

        try
        {
            IntPtr pidl = SHBrowseForFolder(ref browseInfo);
            if (pidl == IntPtr.Zero)
            {
                return null; // User canceled the dialog
            }

            var path = new StringBuilder(260);
            if (SHGetPathFromIDList(pidl, path))
            {
                return path.ToString();
            }

            return null; // Failed to get the folder path
        }
        finally
        {
            Marshal.FreeHGlobal(browseInfo.pszDisplayName); // Free allocated memory
        }
    }

}
