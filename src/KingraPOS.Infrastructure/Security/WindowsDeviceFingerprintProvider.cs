using System.IO;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using KingraPOS.Application.Abstractions.Security;
using Microsoft.Win32;

namespace KingraPOS.Infrastructure.Security;

public sealed class WindowsDeviceFingerprintProvider : IDeviceFingerprintProvider
{
    private const string MachineGuidKeyPath = @"SOFTWARE\Microsoft\Cryptography";
    private const string MachineGuidValueName = "MachineGuid";

    public string GetFingerprint()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ResolveFingerprintSource()));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ResolveFingerprintSource()
    {
        if (OperatingSystem.IsWindows())
        {
            var machineGuid = ReadMachineGuid();
            if (!string.IsNullOrWhiteSpace(machineGuid))
                return machineGuid;
        }

        return $"{Environment.MachineName}|{Environment.OSVersion.Version}";
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(MachineGuidKeyPath);

            return key?.GetValue(MachineGuidValueName) as string;
        }
        catch (Exception exception) when (
            exception is SecurityException
            or UnauthorizedAccessException
            or PlatformNotSupportedException
            or IOException)
        {
            return null;
        }
    }
}
