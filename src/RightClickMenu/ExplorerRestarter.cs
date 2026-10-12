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
        await Task.WhenAll(processes.Select(environment.KillAsync));
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
