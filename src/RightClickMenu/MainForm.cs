namespace RightClickMenu;

public sealed class MainForm : Form, IStyleDialogs
{
    private readonly StyleController controller;
    private readonly Button windows10Button;
    private readonly Button windows11Button;

    public MainForm() : this(new ProcessRunner(), new ExplorerRestarter(new WindowsExplorerEnvironment())) { }

    public MainForm(IProcessRunner runner, IExplorerRestarter restarter)
    {
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
        windows10Button.Enabled = windows11Button.Enabled = false;
        try { await controller.ApplyAsync(style); }
        finally { windows10Button.Enabled = windows11Button.Enabled = true; }
    }

    public bool ConfirmExplorerRestart()
    {
        using var prompt = new RestartPromptForm();
        return prompt.ShowDialog(this) == DialogResult.OK;
    }

    public void ShowError(string message) =>
        MessageBox.Show(this, message, "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
