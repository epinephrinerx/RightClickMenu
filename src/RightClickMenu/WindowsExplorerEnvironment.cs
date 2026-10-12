using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace RightClickMenu;

public sealed class WindowsExplorerEnvironment : IExplorerEnvironment
{
    private readonly string currentUserSid;
    private readonly int currentSessionId;

    public WindowsExplorerEnvironment()
    {
        using var currentProcess = Process.GetCurrentProcess();
        currentSessionId = currentProcess.SessionId;
        using var identity = WindowsIdentity.GetCurrent();
        currentUserSid = identity.User?.Value
            ?? throw new InvalidOperationException("ไม่สามารถระบุผู้ใช้ปัจจุบันได้");
    }

    public IReadOnlyList<int> GetCurrentUserExplorerIds()
    {
        var ids = new List<int>();
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            using (process)
            using (var handle = OpenProcess(0x1000, false, process.Id)) // QUERY_LIMITED_INFORMATION
            {
                if (!handle.IsInvalid && process.SessionId == currentSessionId && IsCurrentUser(handle))
                    ids.Add(process.Id);
            }
        }
        return ids;
    }

    public async Task KillAsync(int processId)
    {
        Process process;
        try { process = Process.GetProcessById(processId); }
        catch (ArgumentException) { return; } // Already exited.
        using (process)
        {
            try
            {
                // Recheck ownership and image on the same process handle used by Kill.
                // Never kill an unrelated process if a PID was reused.
                using var handle = new SafeProcessHandle(process.Handle, ownsHandle: false);
                var imagePath = new StringBuilder(32768);
                var length = imagePath.Capacity;
                if (!QueryFullProcessImageName(handle, 0, imagePath, ref length))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!string.Equals(Path.GetFileName(imagePath.ToString()), "explorer.exe", StringComparison.OrdinalIgnoreCase)
                    || process.SessionId != currentSessionId
                    || !IsCurrentUser(handle))
                    return;
                process.Kill();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (InvalidOperationException) when (process.HasExited) { }
            catch (Win32Exception) when (process.HasExited) { }
        }
    }

    public Task DelayAsync(TimeSpan delay) => Task.Delay(delay);

    public void StartExplorer()
    {
        using var process = Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
        {
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("ไม่สามารถเริ่ม explorer.exe ได้");
    }

    private bool IsCurrentUser(SafeProcessHandle process)
    {
        if (!OpenProcessToken(process, 0x0008, out var token)) // TOKEN_QUERY; fail closed.
            return false;
        using (token)
        using (var identity = new WindowsIdentity(token.DangerousGetHandle()))
            return identity.User?.Value == currentUserSid;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder imagePath, ref int size);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
}
