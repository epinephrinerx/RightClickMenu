namespace RightClickMenu;

public interface IStyleDialogs
{
    bool ConfirmExplorerRestart();
    void ShowError(string message);
}

public interface IExplorerRestarter
{
    Task RestartAsync();
}

public sealed class StyleController(IProcessRunner runner, IExplorerRestarter restarter, IStyleDialogs dialogs)
{
    public async Task ApplyAsync(MenuStyle style)
    {
        ProcessResult result;
        try
        {
            result = await runner.RunAsync(RegCommands.CreateStartInfo(style));
        }
        catch (Exception ex)
        {
            dialogs.ShowError($"ไม่สามารถเรียก reg.exe ได้: {ex.Message}");
            return;
        }

        if (result.ExitCode != 0)
        {
            dialogs.ShowError($"reg.exe ล้มเหลว (exit code {result.ExitCode})\n{result.StandardError}");
            return;
        }

        if (!dialogs.ConfirmExplorerRestart())
            return;

        try
        {
            await restarter.RestartAsync();
        }
        catch (Exception ex)
        {
            dialogs.ShowError($"เปลี่ยน registry แล้ว แต่เริ่ม Explorer ใหม่ไม่สำเร็จ: {ex.Message}");
        }
    }
}
