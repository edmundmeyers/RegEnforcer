using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace RegEnforcer;

public class RegistryKeyChangedEventArgs : EventArgs
{
    public string RegistryKeyPath { get; }

    public RegistryKeyChangedEventArgs(string registryKeyPath)
    {
        RegistryKeyPath = registryKeyPath;
    }
}

public class RegistryKeyWatcher
{
    private readonly IEnumerable<string> registryKeyPaths;
    private readonly List<Thread> monitoringThreads = new();
    private bool isRunning;

    public event EventHandler<RegistryKeyChangedEventArgs>? RegistryKeyChanged;

    public RegistryKeyWatcher(IEnumerable<string> registryKeyPaths)
    {
        this.registryKeyPaths = registryKeyPaths;
    }

    public void Start()
    {
        isRunning = true;

        foreach (var registryKeyPath in registryKeyPaths)
        {
            var thread = new Thread(() => MonitorRegistryKey(registryKeyPath));
            monitoringThreads.Add(thread);
            thread.Start();
        }
    }

    public void Stop()
    {
        isRunning = false;

        foreach (var thread in monitoringThreads)
        {
            thread.Join();
        }

        monitoringThreads.Clear();
    }

    private void MonitorRegistryKey(string registryKeyPath)
    {
        var parts = registryKeyPath.Split(new[] { '\\' }, 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid registry key path: {registryKeyPath}");
        }

        var rootKey = parts[0];
        var subKeyPath = parts[1];

        RegistryKey? rootRegistryKey = rootKey.ToUpper() switch
        {
            "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            "HKEY_USERS" => Registry.Users,
            "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            _ => throw new ArgumentException($"Unknown root key: {rootKey}")
        };

        using var subKey = rootRegistryKey.OpenSubKey(subKeyPath, writable: false);
        if (subKey == null)
        {
            throw new ArgumentException($"Registry key not found: {registryKeyPath}");
        }

        var keyHandle = subKey.Handle;

        while (isRunning)
        {
            // Wait for a change in the registry key
            var result = NativeMethods.RegNotifyChangeKeyValue(
                keyHandle,
                watchSubtree: true,
                NativeMethods.REG_NOTIFY_CHANGE_LAST_SET,
                IntPtr.Zero,
                async: false);

            if (result == 0) // ERROR_SUCCESS
            {
                RegistryKeyChanged?.Invoke(this, new RegistryKeyChangedEventArgs(registryKeyPath));
            }
            else
            {
                throw new InvalidOperationException($"Failed to monitor registry key. Error code: {result}");
            }
        }
    }
}

internal static class NativeMethods
{
    public const int REG_NOTIFY_CHANGE_LAST_SET = 0x00000004;

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern int RegNotifyChangeKeyValue(
        SafeHandle hKey,
        bool watchSubtree,
        int notifyFilter,
        IntPtr hEvent,
        bool async);
}
