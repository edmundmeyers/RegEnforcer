using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegEnforcer;

public static class RegHelper
{
    public static bool CompareRegistryValues(object regValue, string regFileValue)
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

        return regValue.ToString() == regFileValue;
    }

    public static string RegistryValueToString(object regValue)
    {
        if (regValue == null)
        {
            return string.Empty; // Return an empty string for null values
        }

        switch (regValue)
        {
            case int intValue:
                // Convert REG_DWORD to "dword:xxxxxxxx" format
                return $"dword:{intValue:x8}";

            case long longValue:
                // Convert REG_QWORD to "hex(b):" format
                return $"hex(b):{BitConverter.ToString(BitConverter.GetBytes(longValue)).Replace("-", ",").ToLower()}";

            case byte[] byteArrayValue:
                // Convert REG_BINARY to "hex:" format
                return $"hex:{BitConverter.ToString(byteArrayValue).Replace("-", ",").ToLower()}";

            case string[] stringArrayValue:
                // Convert REG_MULTI_SZ to "hex(7):" format
                var multiStringBytes = Encoding.Unicode.GetBytes(string.Join("\0", stringArrayValue) + "\0\0");
                return $"hex(7):{BitConverter.ToString(multiStringBytes).Replace("-", ",").ToLower()}";

            case string stringValue when IsExpandableString(stringValue):
                // Convert REG_EXPAND_SZ to "hex(2):" format
                var expandStringBytes = Encoding.Unicode.GetBytes(stringValue + "\0");
                return $"hex(2):{BitConverter.ToString(expandStringBytes).Replace("-", ",").ToLower()}";

            case string stringValue:
                // Convert REG_SZ to plain string
                return stringValue;

            default:
                // Fallback for unsupported types
                return regValue.ToString();
        }
    }

    private static bool IsExpandableString(string value)
    {
        // Add logic here if you need to differentiate between REG_SZ and REG_EXPAND_SZ
        // For now, assume all strings can be REG_EXPAND_SZ if needed
        return value.Contains("%"); // Example: Check for environment variables like %SystemRoot%
    }


    public static byte[] ParseRegFileBinaryValue(string regFileValue)
    {
        try
        {
            var hexValues = regFileValue.Split(',');
            var bytes = new byte[hexValues.Length];
            for (int i = 0; i < hexValues.Length; i++)
            {
                bytes[i] = Convert.ToByte(hexValues[i].Trim(), 16);
            }
            return bytes;
        }
        catch (FormatException ex)
        {
            Console.WriteLine($"Error parsing binary value: {ex.Message}");
            return Array.Empty<byte>();
        }
        catch (OverflowException ex)
        {
            Console.WriteLine($"Error parsing binary value: {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    public static string[] ParseRegFileMultiStringValue(string regFileValue)
    {
        // Split the comma-delimited string and convert it to a byte array
        string[] hexValues = regFileValue.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        byte[] hexData = new byte[hexValues.Length];

        for (int i = 0; i < hexValues.Length; i++)
        {
            hexData[i] = Convert.ToByte(hexValues[i].Trim(), 16); // Convert each hex string to a byte
        }

        // Decode the byte array into a Unicode string
        string decodedString = Encoding.Unicode.GetString(hexData);

        // Split the string by null characters ('\0') and return the result
        string[] result = decodedString.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);

        return result;
    }

    public static string ParseRegFileExpandableStringValue(string regFileValue)
    {
        // Split the comma-delimited string and convert it to a byte array
        string[] hexValues = regFileValue.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        byte[] hexData = new byte[hexValues.Length];

        for (int i = 0; i < hexValues.Length; i++)
        {
            hexData[i] = Convert.ToByte(hexValues[i].Trim(), 16); // Convert each hex string to a byte
        }

        // Decode the byte array into a Unicode string
        string decodedString = Encoding.Unicode.GetString(hexData);

        return decodedString.TrimEnd('\0');
    }


    public static object GetRegistryValue(string fullPath, string valueName)
    {
        try
        {
            // Split the full path into root key and subkey
            var parts = fullPath.Split(new[] { '\\' }, 2);
            if (parts.Length != 2)
            {
                Console.WriteLine($"Invalid registry path: {fullPath}");
                return null;
            }

            var rootKey = parts[0];
            var subKeyPath = parts[1];

            RegistryKey rootRegistryKey = null;

            // Determine the root key
            switch (rootKey.ToUpper())
            {
                case "HKEY_LOCAL_MACHINE":
                    rootRegistryKey = Registry.LocalMachine;
                    break;
                case "HKEY_CURRENT_USER":
                    rootRegistryKey = Registry.CurrentUser;
                    break;
                case "HKEY_CLASSES_ROOT":
                    rootRegistryKey = Registry.ClassesRoot;
                    break;
                case "HKEY_USERS":
                    rootRegistryKey = Registry.Users;
                    break;
                case "HKEY_CURRENT_CONFIG":
                    rootRegistryKey = Registry.CurrentConfig;
                    break;
                default:
                    Console.WriteLine($"Unknown root key: {rootKey}");
                    return null;
            }

            // Open the subkey and get the value without expanding environment variables
            using (var subKey = rootRegistryKey.OpenSubKey(subKeyPath))
            {
                if (subKey != null)
                {
                    if (valueName == "@") valueName = null;

                    return subKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                }
            }

            // Log the key path and value name if not found
            Console.WriteLine($"Registry key or value not found: {fullPath}\\{valueName}");
        }
        catch (Exception ex)
        {
            // Log the exception
            Console.WriteLine($"Error accessing registry: {ex.Message}");
        }

        return null;
    }


}
