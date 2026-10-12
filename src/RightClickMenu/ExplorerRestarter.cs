namespace RightClickMenu;

public interface IExplorerEnvironment
{
    IReadOnlyList<int> GetCurrentUserExplorerIds();
    Task KillAsync(int processId);
    Task DelayAsync(TimeSpan delay);
    void StartExplorer();
}

public sealed class ExplorerRestarter(IExplorerEnvironment environment) : IExplorerRestarter
{
    public async Task RestartAsync()
    {
        // Snapshot before killing: a replacement shell must not be killed in this pass.
        var processes = environment.GetCurrentUserExplorerIds();
        try
        {
            await Task.WhenAll(processes.Select(environment.KillAsync));
        }
        catch (OperationCanceledException ex)
        {
            // Kill may already have succeeded. Recover the shell before reporting the timeout.
            await EnsureExplorerRunningAsync();
            throw new InvalidOperationException("หมดเวลารอ Explorer ปิด แต่ตรวจสอบให้ Explorer ทำงานอีกครั้งแล้ว", ex);
        }
        await EnsureExplorerRunningAsync();
    }

    private async Task EnsureExplorerRunningAsync()
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await environment.DelayAsync(TimeSpan.FromMilliseconds(250));
            if (environment.GetCurrentUserExplorerIds().Count > 0)
                return;
        }
        environment.StartExplorer();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await environment.DelayAsync(TimeSpan.FromMilliseconds(250));
            if (environment.GetCurrentUserExplorerIds().Count > 0)
                return;
        }
        throw new InvalidOperationException("Explorer ยังไม่ทำงานหลังจากเริ่มใหม่ กรุณาเปิด explorer.exe ผ่าน Task Manager");
    }
}
