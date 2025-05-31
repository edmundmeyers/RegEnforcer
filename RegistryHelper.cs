using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RegEnforcer;

public class RegistryHelper
{
    public string RegFilesFolder { get; set; }

    public RegistryHelper(string regFilesFolder)
    {
        RegFilesFolder = regFilesFolder;
    }

    public IEnumerable<string> GetRegFiles()
    {
        if (Directory.Exists(RegFilesFolder))
        {
            return Directory.GetFiles(RegFilesFolder, "*.reg");
        }
        return Enumerable.Empty<string>();
    }

    public IEnumerable<string> ReadRegFileContent(string filePath)
    {
        return File.Exists(filePath) ? File.ReadAllLines(filePath) : Enumerable.Empty<string>();
    }

    public object GetRegistryValue(string fullPath, string valueName)
    {
        try
        {
            var parts = fullPath.Split(new[] { '\\' }, 2);
            if (parts.Length != 2) return null;

            var rootKey = parts[0];
            var subKeyPath = parts[1];

            RegistryKey rootRegistryKey = GetRootRegistryKey(rootKey);
            if (rootRegistryKey == null) return null;

            using (var subKey = rootRegistryKey.OpenSubKey(subKeyPath))
            {
                return subKey?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            }
        }
        catch
        {
            return null;
        }
    }

    public bool UpdateRegistryValue(string key, string valueName, string value)
    {
        try
        {
            var parts = key.Split(new[] { '\\' }, 2);
            if (parts.Length != 2) return false;

            var rootKey = parts[0];
            var subKeyPath = parts[1];

            RegistryKey rootRegistryKey = GetRootRegistryKey(rootKey);
            if (rootRegistryKey == null) return false;

            using (var subKey = rootRegistryKey.OpenSubKey(subKeyPath, writable: true))
            {
                if (subKey != null)
                {
                    subKey.SetValue(valueName, value);
                    return true;
                }
            }
        }
        catch
        {
            // Log or handle exceptions as needed
        }
        return false;
    }

    public bool CompareRegistryValues(object regValue, string regFileValue)
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

        return regValue?.ToString() == regFileValue;
    }

    private RegistryKey GetRootRegistryKey(string rootKey)
    {
        return rootKey.ToUpper() switch
        {
            "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            "HKEY_USERS" => Registry.Users,
            "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            _ => null,
        };
    }

    private byte[] ParseRegFileBinaryValue(string regFileValue)
    {
        try
        {
            var hexValues = regFileValue.Split(',');
            return hexValues.Select(hex => Convert.ToByte(hex.Trim(), 16)).ToArray();
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private string[] ParseRegFileMultiStringValue(string regFileValue)
    {
        var hexValues = regFileValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var hexData = hexValues.Select(hex => Convert.ToByte(hex.Trim(), 16)).ToArray();
        var decodedString = Encoding.Unicode.GetString(hexData);
        return decodedString.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private string ParseRegFileExpandableStringValue(string regFileValue)
    {
        var hexValues = regFileValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var hexData = hexValues.Select(hex => Convert.ToByte(hex.Trim(), 16)).ToArray();
        return Encoding.Unicode.GetString(hexData).TrimEnd('\0');
    }

    public static void AddApplicationToStartup(string appName)
    {
        // Use the EXE path instead of the DLL path
        string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", true))
        {
            key.SetValue(appName, $"\"{exePath}\"");
        }
    }
}
