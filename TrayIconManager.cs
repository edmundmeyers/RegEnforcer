using RegEnforcer;
using System.Reflection;
using System.Windows;

public class TrayIconManager
{
    private NotifyIcon notifyIcon;
    private RegEnforcerWindow regEnforcerWindow; // Make regFilesWindow nullable

    private List<RegistryFixInfo> RegistryFixes;
    private System.Timers.Timer registryCheckTimer;

    public TrayIconManager()
    {
        SetupTrayIcon();

        // Check if the application is not set to run at startup
        ShowRegFilesWindow(!IsApplicationSetToRunAtStartup());

        RegistryFixes = regEnforcerWindow.RegistryFixes;

        // Initialize and start the timer
        InitializeRegistryCheckTimer();
    }
    private void InitializeRegistryCheckTimer()
    {
        registryCheckTimer = new System.Timers.Timer(1000); // 30 seconds in milliseconds
        registryCheckTimer.Elapsed += (sender, e) => CheckRegistryValues();
        registryCheckTimer.AutoReset = true; // Ensures the timer runs repeatedly
        registryCheckTimer.Start();
    }

    private void CheckRegistryValues()
    {
        string changedValues = HaveRegistryValuesChanged();

        if (!string.IsNullOrEmpty(changedValues))
        {
            // Call LoadRegFiles on the UI thread
            regEnforcerWindow.Dispatcher.Invoke(() =>
            {
                regEnforcerWindow.LoadRegFiles();
            });

            // Display a notification if there are changes
            notifyIcon.ShowBalloonTip(
                5000, // Duration in milliseconds
                "Registry Changes Detected",
                $"The following registry values have changed:\n{changedValues}",
                ToolTipIcon.Warning
            );
        }
    }

    public string HaveRegistryValuesChanged()
    {
        string changedValues = string.Empty;

        foreach (var fixInfo in RegistryFixes)
        {
            // Get the current registry value for the key and value name
            var currentValue = RegHelper.GetRegistryValue(fixInfo.Key, fixInfo.ValueName);

            // Compare the current value with the FoundValue
            if (!AreValuesEqual(currentValue, fixInfo.FoundValue))
            {
                changedValues += $"{fixInfo.Key}\\{fixInfo.ValueName}: {currentValue} (Expected: {fixInfo.FoundValue})\n";
            }
        }

        return changedValues; // No values have changed
    }


    private bool AreValuesEqual(object currentValue, object foundValue)
    {
        if (currentValue is byte[] currentBytes && foundValue is byte[] foundBytes)
        {
            // Compare byte arrays
            return currentBytes.SequenceEqual(foundBytes);
        }
        else if (currentValue is string[] currentStrings && foundValue is string[] foundStrings)
        {
            // Compare string arrays
            return currentStrings.SequenceEqual(foundStrings);
        }
        else
        {
            // Fallback to default equality check for other types
            return Equals(currentValue, foundValue);
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

    private void ShowRegFilesWindow(bool showWindow = true)
    {
        if (regEnforcerWindow == null)
        {
            regEnforcerWindow = new RegEnforcerWindow();
            regEnforcerWindow.Closed += (s, e) => regEnforcerWindow = null;
        }

        if (showWindow)
        {
            regEnforcerWindow.Show();
            regEnforcerWindow.WindowState = WindowState.Normal;
            regEnforcerWindow.Activate();
        }
    }

    private void ExitApplication()
    {
        notifyIcon.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private bool IsApplicationSetToRunAtStartup()
    {        
        // Use the RegEnforcerWindow's method to check if the application is set to run at startup
        return AppHelper.IsApplicationSetToRunAtStartup();
    }
}
