using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;

namespace OpenPEData.SetupLauncher;

/// <summary>
/// Single-file setup entry that runs the embedded Velopack Setup.exe with --silent (reinstall, no dialogs),
/// then launches the installed application stub.
/// </summary>
internal static class Program
{
    private const string PayloadResourceSuffix = ".SetupPayload.exe";
    private const string PackId = "OpenPEData";
    private const string AppStubExe = "OpenPEData.Viewer.exe";
    private const string MainExeName = "OpenPEData.Viewer.exe";

    [STAThread]
    private static int Main(string[] args)
    {
        string payloadPath = null;
        var deletePayload = false;
        try
        {
            payloadPath = ExtractEmbeddedPayload(out deletePayload);
            var startInfo = new ProcessStartInfo
            {
                FileName = payloadPath,
                Arguments = BuildArguments(args),
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(payloadPath) ?? Environment.CurrentDirectory,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return 1;
            }

            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                TryStartInstalledApp(args);
            }
            else if (!HasSilentArgument(args))
            {
                ShowSetupFailed(process.ExitCode);
            }

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "OpenPEData Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return 1;
        }
        finally
        {
            if (deletePayload && !string.IsNullOrEmpty(payloadPath))
            {
                TryDelete(payloadPath);
            }
        }
    }

    private static bool HasSilentArgument(string[] args)
    {
        return args.Any(static a =>
            string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-s", StringComparison.OrdinalIgnoreCase));
    }

    private static void ShowSetupFailed(int exitCode)
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "velopack",
            "velopack.log");

        var message = new StringBuilder();
        message.AppendLine("The installation failed (exit code " + exitCode + ").");
        var lastError = TryReadLastLogError(logPath);
        if (!string.IsNullOrEmpty(lastError))
        {
            message.AppendLine();
            message.AppendLine(lastError);
        }

        message.AppendLine();
        message.AppendLine("Please close OpenPEData Viewer and any window or console opened in its installation folder, then run the setup again.");
        message.AppendLine();
        message.Append("Details: " + logPath);

        MessageBox.Show(
            message.ToString(),
            "OpenPEData Setup",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static string TryReadLastLogError(string logPath)
    {
        try
        {
            if (!File.Exists(logPath))
            {
                return null;
            }

            // Velopack keeps the log open while writing; allow shared read access.
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            string lastError = null;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                const string marker = "[ERROR] ";
                var index = line.IndexOf(marker, StringComparison.Ordinal);
                if (line.StartsWith("[setup:", StringComparison.Ordinal) && index >= 0)
                {
                    lastError = line.Substring(index + marker.Length).Trim();
                }
            }

            return lastError;
        }
        catch
        {
            // The log is only a hint for the user; ignore read failures.
            return null;
        }
    }

    private static string BuildArguments(string[] args)
    {
        var builder = new StringBuilder();
        if (!HasSilentArgument(args))
        {
            builder.Append("--silent");
        }

        foreach (var arg in args)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(QuoteIfNeeded(arg));
        }

        return builder.ToString();
    }

    private static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return value.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0
            ? "\"" + value.Replace("\"", "\\\"") + "\""
            : value;
    }

    private static string ExtractEmbeddedPayload(out bool deleteAfterUse)
    {
        deleteAfterUse = false;
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(PayloadResourceSuffix, StringComparison.OrdinalIgnoreCase));
        if (resourceName == null)
        {
            throw new InvalidOperationException("Embedded Velopack setup payload was not found.");
        }

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            "OpenPEData-setup-" + Guid.NewGuid().ToString("N") + ".exe");

        using (var input = assembly.GetManifestResourceStream(resourceName))
        {
            if (input == null)
            {
                throw new InvalidOperationException("Failed to open embedded Velopack setup payload.");
            }

            using var output = File.Create(tempPath);
            input.CopyTo(output);
        }

        deleteAfterUse = true;
        return tempPath;
    }

    private static void TryStartInstalledApp(string[] args)
    {
        var installDir = ResolveInstallDirectory(args);
        var stubPath = Path.Combine(installDir, AppStubExe);
        var appPath = File.Exists(stubPath)
            ? stubPath
            : Path.Combine(installDir, "current", MainExeName);

        if (!File.Exists(appPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = appPath,
            UseShellExecute = true,
            WorkingDirectory = installDir,
        });
    }

    private static string ResolveInstallDirectory(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--installto", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[i], "-t", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            PackId);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup of temp payload.
        }
    }
}
