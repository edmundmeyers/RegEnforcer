using System;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using System.Reflection;

namespace RegEnforcer
{
    public class TrayIconManager
    {
        private NotifyIcon notifyIcon;
        private RegFilesWindow? regFilesWindow; // Make regFilesWindow nullable

        public TrayIconManager()
        {
            SetupTrayIcon();
        }

        private void SetupTrayIcon()
        {
            //var resourceName = "RegEnforcer.Resources.regedit.ico";
            var resourceName = "RegEnforcer.Resources.Icon8.ico";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                // List all resources to help debug the issue
                var resourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                foreach (var name in resourceNames)
                {
                    Console.WriteLine(name);
                }
                throw new ArgumentNullException(nameof(stream), $"Resource '{resourceName}' not found.");
            }

            var icon = new System.Drawing.Icon(stream);

            notifyIcon = new NotifyIcon
            {
                Icon = icon,
                Visible = true,
                Text = "RegEnforcer"
            };

            notifyIcon.DoubleClick += (s, e) => ShowRegFilesWindow();

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open", null, (s, e) => ShowRegFilesWindow());
            contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

            notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void ShowRegFilesWindow()
        {
            if (regFilesWindow == null)
            {
                regFilesWindow = new RegFilesWindow();
                regFilesWindow.Closed += (s, e) => regFilesWindow = null;
            }

            regFilesWindow.Show();
            regFilesWindow.WindowState = WindowState.Normal;
            regFilesWindow.Activate();
        }

        private void ExitApplication()
        {
            notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }
    }
}
