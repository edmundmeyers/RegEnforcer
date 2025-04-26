using RegEnforcer;
using System.Reflection;
using System.Windows;

public class TrayIconManager
{
    private NotifyIcon notifyIcon;
    private RegEnforcerWindow? regEnforcerWindow; // Make regFilesWindow nullable

    private List<RegistryFixInfo> fixes;

    public TrayIconManager()
    {
        SetupTrayIcon();

        // Check if the application is not set to run at startup
        if (!IsApplicationSetToRunAtStartup())
        {
            ShowRegFilesWindow();
        }
    }

    private void SetupTrayIcon()
    {
        var resourceName = "RegEnforcer.Resources.Icon8.ico";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

        if (stream == null)
        {
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
        if (regEnforcerWindow == null)
        {
            regEnforcerWindow = new RegEnforcerWindow();
            regEnforcerWindow.Closed += (s, e) => regEnforcerWindow = null;
        }

        regEnforcerWindow.Show();
        regEnforcerWindow.WindowState = WindowState.Normal;
        regEnforcerWindow.Activate();
    }

    private void ExitApplication()
    {
        notifyIcon.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private bool IsApplicationSetToRunAtStartup()
    {        
        // Use the RegEnforcerWindow's method to check if the application is set to run at startup
        return new RegEnforcerWindow().IsApplicationSetToRunAtStartup();
    }
}
