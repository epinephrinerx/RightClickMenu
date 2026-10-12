using System.Diagnostics;

namespace RightClickMenu;

public enum MenuStyle { Windows10, Windows11 }

public static class RegCommands
{
    public const string Windows10Arguments = "add \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32\" /f /ve";
    public const string Windows11Arguments = "delete \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\" /f";

    public static ProcessStartInfo CreateStartInfo(MenuStyle style) => new(Path.Combine(Environment.SystemDirectory, "reg.exe"), style switch
    {
        MenuStyle.Windows10 => Windows10Arguments,
        MenuStyle.Windows11 => Windows11Arguments,
        _ => throw new ArgumentOutOfRangeException(nameof(style))
    })
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardError = true
    };
}

public sealed record ProcessResult(int ExitCode, string StandardError);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessStartInfo startInfo);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("ไม่สามารถเริ่ม reg.exe ได้");
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, await stderr);
    }
}
