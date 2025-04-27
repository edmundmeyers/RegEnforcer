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
        LoadIcons();        // Icons toggle-- load em up
        SetupTrayIcon();

        // Check if the application is not set to run at startup
        ShowRegFilesWindow(!IsApplicationSetToRunAtStartup());

        RegistryFixes = regEnforcerWindow.RegistryFixes;

        // Show initial popup
        string badRegistryValues = BadRegistryValues();
        if (!string.IsNullOrEmpty(badRegistryValues))
            ShowBalloonAbout(badRegistryValues);

        // Initialize and start the timer
        InitializeRegistryCheckTimer();
    }
    private void InitializeRegistryCheckTimer()
    {
        registryCheckTimer = new System.Timers.Timer(2000); // 2 seconds in milliseconds
        registryCheckTimer.Elapsed += (sender, e) => CheckRegistryValues();
        registryCheckTimer.AutoReset = true; // Ensures the timer runs repeatedly
        registryCheckTimer.Start();
    }

    private void CheckRegistryValues()
    {
        bool anyBad = false;
        string changedValues = HaveRegistryValuesChanged(out anyBad);

        if (!string.IsNullOrEmpty(changedValues))
        {
            // Call LoadRegFiles on the UI thread
            regEnforcerWindow.Dispatcher.Invoke(() =>
            {
                regEnforcerWindow.LoadRegFiles();
            });

            ShowBalloonAbout(changedValues);
        }

        // Switch the tray icon to red if any are wrong
        SetTrayIcon(anyBad);
    }

    private void ShowBalloonAbout(string values)
    {
        // Display a notification if there are changes
        notifyIcon.ShowBalloonTip(
            5000, // Duration in milliseconds
            "Registry Changes Detected",
            $"{values}",
            ToolTipIcon.Warning
        );

    }


    private System.Drawing.Icon _blueIcon = null;
    private System.Drawing.Icon _redIcon = null;
    private void LoadIcons()
    {
        string blueIconResourceName = "RegEnforcer.Resources.Icon8.ico";
        string redIconResourceName = "RegEnforcer.Resources.Icon8red.ico";

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(blueIconResourceName);
        _blueIcon = new System.Drawing.Icon(stream);

        using var stream2 = Assembly.GetExecutingAssembly().GetManifestResourceStream(redIconResourceName);
        _redIcon = new System.Drawing.Icon(stream2);
    }

    private bool _lastAnyBad = false;
    private void SetTrayIcon(bool anyBad)
    {
        if (_lastAnyBad == anyBad) return;

        // Toggle
        notifyIcon.Icon = anyBad ? _redIcon : _blueIcon;
        _lastAnyBad = anyBad;
    }

    public string HaveRegistryValuesChanged(out bool anyBad)
    {
        string changedValues = string.Empty;
        anyBad = false;

        foreach (var fixInfo in RegistryFixes)
        {
            // Get the current registry value for the key and value name
            var currentValue = RegHelper.RegistryValueToString(RegHelper.GetRegistryValue(fixInfo.Key, fixInfo.ValueName));

            // Compare vs. what we expect-- it's not that it just changed, it's that it's not right
            anyBad = anyBad || currentValue != fixInfo.Value;

            // Compare the current value with the FoundValue to see if it changed. FoundValue is what we found last time.
            if (currentValue != fixInfo.FoundValue)
            {
                changedValues += $"{fixInfo.Key}\\{fixInfo.ValueName}: {currentValue} (Expected: {fixInfo.Value})\n";
            }
        }

        return changedValues; // No values have changed
    }

    public string BadRegistryValues()
    {
        string badValues = string.Empty;

        foreach (var fixInfo in RegistryFixes)
        {
            // Get the current registry value for the key and value name
            var currentValue = RegHelper.RegistryValueToString(RegHelper.GetRegistryValue(fixInfo.Key, fixInfo.ValueName));

            // Compare the current value with the FoundValue to see if it changed. FoundValue is what we found last time.
            if (currentValue != fixInfo.Value)
            {
                badValues += $"{fixInfo.Key}\\{fixInfo.ValueName}: {currentValue} (Expected: {fixInfo.Value})\n";
            }
        }

        return badValues; // No values have changed
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
