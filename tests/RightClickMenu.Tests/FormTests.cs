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

    [Theory]
    [InlineData("Right Click with Windows 10 Style", MenuStyle.Windows10,
        "add \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32\" /f /ve")]
    [InlineData("Right Click with Windows 11 Style", MenuStyle.Windows11,
        "delete \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\" /f")]
    public void ClickingStyleButtonRunsMatchingCommand(string text, MenuStyle style, string arguments) => RunSta(() =>
    {
        var runner = new RecordingRunner();
        var prompts = 0;
        using var form = new MainForm(runner, new NeverRestarter(), _ =>
        {
            prompts++;
            return DialogResult.Cancel;
        }, message => throw new InvalidOperationException(message));
        form.Show();
        form.Controls.OfType<Button>().Single(button => button.Text == text).PerformClick();
        var command = Assert.Single(runner.Commands);
        Assert.Equal(Path.Combine(Environment.SystemDirectory, "reg.exe"), command.FileName);
        Assert.Equal(arguments, command.Arguments);
        Assert.Equal(RegCommands.CreateStartInfo(style).Arguments, command.Arguments);
        Assert.Equal(1, prompts);
    });

    [Theory]
    [InlineData(DialogResult.OK, 1)]
    [InlineData(DialogResult.Cancel, 0)]
    [InlineData(DialogResult.None, 0)]
    [InlineData(DialogResult.Abort, 0)]
    [InlineData(DialogResult.Retry, 0)]
    [InlineData(DialogResult.Ignore, 0)]
    [InlineData(DialogResult.Yes, 0)]
    [InlineData(DialogResult.No, 0)]
    [InlineData(DialogResult.TryAgain, 0)]
    [InlineData(DialogResult.Continue, 0)]
    public void OnlyRestartButtonResultRestartsExplorer(DialogResult result, int expectedRestarts) => RunSta(() =>
    {
        using var prompt = new RestartPromptForm();
        Assert.Equal(DialogResult.OK, prompt.Controls.OfType<Button>().Single(button => button.Text == "เริ่มใหม่").DialogResult);
        var restarter = new RecordingRestarter();
        using var form = new MainForm(new RecordingRunner(), restarter, _ => result,
            message => throw new InvalidOperationException(message));
        form.Show();
        form.Controls.OfType<Button>().First().PerformClick();
        Assert.Equal(expectedRestarts, restarter.Calls);
    });

    [Fact]
    public void ClosingIsCanceledDuringCommandAndRestartThenAllowedAfterCompletion() => RunSta(() =>
    {
        var commandCompletion = new TaskCompletionSource<ProcessResult>();
        var restartCompletion = new TaskCompletionSource();
        var runner = new RecordingRunner { Completion = commandCompletion.Task };
        var restarter = new RecordingRestarter { Completion = restartCompletion.Task };
        using var form = new MainForm(runner, restarter, _ => DialogResult.OK,
            message => throw new InvalidOperationException(message));
        form.Show();
        var buttons = form.Controls.OfType<Button>().ToArray();
        buttons[0].PerformClick();
        Assert.All(buttons, button => Assert.False(button.Enabled));
        form.Close();
        Assert.False(form.IsDisposed);
        Assert.True(form.Visible);
        commandCompletion.SetResult(new ProcessResult(0, ""));
        PumpUntil(() => restarter.Calls == 1);
        form.Close();
        Assert.False(form.IsDisposed);
        Assert.True(form.Visible);
        Assert.All(buttons, button => Assert.False(button.Enabled));
        restartCompletion.SetResult();
        PumpUntil(() => buttons.All(button => button.Enabled));
        form.Close();
        Assert.True(form.IsDisposed);
    });

    [Fact]
    public void FailedButtonCommandShowsErrorWithoutPromptOrRestartAndAllowsClosing() => RunSta(() =>
    {
        var runner = new RecordingRunner { Completion = Task.FromResult(new ProcessResult(5, "denied")) };
        var errors = new List<string>();
        using var form = new MainForm(runner, new NeverRestarter(),
            _ => throw new InvalidOperationException("Failed commands must not prompt"), errors.Add);
        form.Show();
        form.Controls.OfType<Button>().Last().PerformClick();
        Assert.Contains("exit code 5", Assert.Single(errors));
        Assert.All(form.Controls.OfType<Button>(), button => Assert.True(button.Enabled));
        form.Close();
        Assert.True(form.IsDisposed);
    });

    private static void PumpUntil(Func<bool> completed)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!completed())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Fake operation did not complete");
            Application.DoEvents();
        }
    }

    private sealed class RecordingRunner : IProcessRunner
    {
        public List<System.Diagnostics.ProcessStartInfo> Commands { get; } = [];
        public Task<ProcessResult> Completion { get; init; } = Task.FromResult(new ProcessResult(0, ""));
        public Task<ProcessResult> RunAsync(System.Diagnostics.ProcessStartInfo startInfo)
        {
            Commands.Add(startInfo);
            return Completion;
        }
    }

    private sealed class RecordingRestarter : IExplorerRestarter
    {
        public int Calls { get; private set; }
        public Task Completion { get; init; } = Task.CompletedTask;
        public Task RestartAsync() { Calls++; return Completion; }
    }

    // UI events run on an STA thread with fake process, restart, and dialog boundaries.
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
