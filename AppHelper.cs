using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RegEnforcer;

public static class AppHelper
{
    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "RegEnforcer";

    public static bool IsApplicationSetToRunAtStartup()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: false))
            {
                if (key != null)
                {
                    var value = key.GetValue(AppName);
                    return value != null && value.ToString() == GetApplicationExecutablePath();
                }
            }
        }
        catch
        {
            // Log or handle exceptions as needed
        }

        return false;
    }

    public static void SetApplicationToRunAtStartup(bool enable)
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
                        key.SetValue(AppName, GetApplicationExecutablePath());
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

    public static bool IsSystemInDarkMode()
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

    public static void ToggleDarkMode(bool isDarkMode)
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

    public static string GetApplicationExecutablePath()
    {
        // Always return the EXE path, not a DLL
        return System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
    }
}
