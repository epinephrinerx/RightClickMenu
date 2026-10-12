namespace RightClickMenu;

public sealed class MainForm : Form, IStyleDialogs
{
    private readonly StyleController controller;
    private readonly Button windows10Button;
    private readonly Button windows11Button;
    private readonly Func<IWin32Window, DialogResult> showRestartPrompt;
    private readonly Action<string>? showError;
    private bool busy;

    public MainForm() : this(new ProcessRunner(), new ExplorerRestarter(new WindowsExplorerEnvironment())) { }

    public MainForm(IProcessRunner runner, IExplorerRestarter restarter,
        Func<IWin32Window, DialogResult>? showRestartPrompt = null, Action<string>? showError = null)
    {
        this.showRestartPrompt = showRestartPrompt ?? ShowRestartPrompt;
        this.showError = showError;
        Text = "Right Click Menu";
        ClientSize = new Size(420, 150);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        windows10Button = new Button
        {
            Text = "Right Click with Windows 10 Style",
            Location = new Point(20, 20), Size = new Size(380, 45), TabIndex = 0
        };
        windows11Button = new Button
        {
            Text = "Right Click with Windows 11 Style",
            Location = new Point(20, 85), Size = new Size(380, 45), TabIndex = 1
        };
        Controls.AddRange([windows10Button, windows11Button]);
        controller = new StyleController(runner, restarter, this);
        windows10Button.Click += async (_, _) => await ApplyAsync(MenuStyle.Windows10);
        windows11Button.Click += async (_, _) => await ApplyAsync(MenuStyle.Windows11);
    }

    private async Task ApplyAsync(MenuStyle style)
    {
        if (busy) return;
        busy = true;
        windows10Button.Enabled = windows11Button.Enabled = false;
        try { await controller.ApplyAsync(style); }
        finally
        {
            busy = false;
            windows10Button.Enabled = windows11Button.Enabled = true;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (busy) e.Cancel = true;
    }

    public bool ConfirmExplorerRestart() => showRestartPrompt(this) == DialogResult.OK;

    private static DialogResult ShowRestartPrompt(IWin32Window owner)
    {
        using var prompt = new RestartPromptForm();
        return prompt.ShowDialog(owner);
    }

    public void ShowError(string message)
    {
        if (showError is not null) showError(message);
        else MessageBox.Show(this, message, "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
