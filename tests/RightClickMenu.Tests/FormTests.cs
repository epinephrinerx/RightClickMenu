using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Xunit;

namespace RightClickMenu.Tests;

public sealed class FormTests
{
    [Fact]
    public void MainWindowContainsExactlyTheTwoRequiredButtons() => RunSta(() =>
    {
        using var form = new MainForm(new NeverRunner(), new NeverRestarter());
        Assert.Equal(2, form.Controls.Count);
        Assert.Equal(new[] { "Right Click with Windows 10 Style", "Right Click with Windows 11 Style" },
            form.Controls.Cast<Control>().Select(control => Assert.IsType<Button>(control).Text));
    });

    [Fact]
    public void RestartPromptShowsWarningAndExactlyTwoThaiChoicesTogether() => RunSta(() =>
    {
        using var form = new RestartPromptForm();
        var label = Assert.Single(form.Controls.OfType<Label>());
        Assert.Contains("Explorer", label.Text);
        Assert.Contains("มีผล", label.Text);
        Assert.Contains("คำเตือน", label.Text);
        Assert.Contains("ปิดหน้าต่าง", label.Text);
        Assert.Contains("File Explorer", label.Text);
        var buttons = form.Controls.OfType<Button>().ToArray();
        Assert.Equal(new[] { "เริ่มใหม่", "ยกเลิก" }, buttons.Select(button => button.Text));
        Assert.Equal(DialogResult.OK, buttons[0].DialogResult);
        Assert.Equal(DialogResult.Cancel, buttons[1].DialogResult);
        Assert.Same(buttons[1], form.CancelButton);
        Assert.Same(buttons[1], form.AcceptButton);
        Assert.False(form.ControlBox);
        Assert.True(form.ClientRectangle.Contains(label.Bounds));
        Assert.All(buttons, button => Assert.True(form.ClientRectangle.Contains(button.Bounds)));
    });

    // Forms are constructed on an STA thread only; no window is shown or button invoked.
    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class NeverRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(System.Diagnostics.ProcessStartInfo startInfo) =>
            throw new InvalidOperationException("UI construction must not run a command");
    }
    private sealed class NeverRestarter : IExplorerRestarter
    {
        public Task RestartAsync() => throw new InvalidOperationException("UI construction must not restart Explorer");
    }
}
