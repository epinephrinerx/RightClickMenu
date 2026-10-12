using System.Diagnostics;
using Xunit;

namespace RightClickMenu.Tests;

public sealed class BehaviorTests
{
    [Theory]
    [InlineData(MenuStyle.Windows10, "add \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32\" /f /ve")]
    [InlineData(MenuStyle.Windows11, "delete \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\" /f")]
    public async Task SuccessfulCommandUsesExactArgumentsAndCancelDoesNotRestart(MenuStyle style, string arguments)
    {
        var runner = new FakeRunner();
        var restarter = new FakeRestarter();
        var dialogs = new FakeDialogs();
        await new StyleController(runner, restarter, dialogs).ApplyAsync(style);
        Assert.NotNull(runner.Command);
        Assert.Equal(Path.Combine(Environment.SystemDirectory, "reg.exe"), runner.Command.FileName);
        Assert.Equal(arguments, runner.Command.Arguments);
        Assert.False(runner.Command.UseShellExecute);
        Assert.True(runner.Command.CreateNoWindow);
        Assert.True(runner.Command.RedirectStandardError);
        Assert.Equal(1, runner.Calls);
        Assert.Equal(1, dialogs.Prompts);
        Assert.Equal(0, restarter.Calls);
        Assert.Empty(dialogs.Errors);
    }

    [Theory]
    [InlineData(MenuStyle.Windows10)]
    [InlineData(MenuStyle.Windows11)]
    public async Task ExplicitRestartChoiceRestartsOnceAfterSuccessfulCommand(MenuStyle style)
    {
        var runner = new FakeRunner();
        var restarter = new FakeRestarter();
        var dialogs = new FakeDialogs { Restart = true };
        await new StyleController(runner, restarter, dialogs).ApplyAsync(style);
        Assert.Equal(1, dialogs.Prompts);
        Assert.Equal(1, restarter.Calls);
        Assert.Empty(dialogs.Errors);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task FailedCommandReportsExitCodeAndStderrWithoutPrompt(int code)
    {
        var runner = new FakeRunner { Result = new(code, "Access denied") };
        var restarter = new FakeRestarter();
        var dialogs = new FakeDialogs { Restart = true };
        await new StyleController(runner, restarter, dialogs).ApplyAsync(MenuStyle.Windows11);
        Assert.Contains($"exit code {code}", Assert.Single(dialogs.Errors));
        Assert.Contains("Access denied", dialogs.Errors[0]);
        Assert.Equal(0, dialogs.Prompts);
        Assert.Equal(0, restarter.Calls);
    }

    [Fact]
    public async Task ProcessStartFailureReportsErrorWithoutPrompt()
    {
        var runner = new FakeRunner { Failure = new InvalidOperationException("start failed") };
        var restarter = new FakeRestarter();
        var dialogs = new FakeDialogs { Restart = true };
        await new StyleController(runner, restarter, dialogs).ApplyAsync(MenuStyle.Windows10);
        Assert.Contains("start failed", Assert.Single(dialogs.Errors));
        Assert.Equal(0, dialogs.Prompts);
        Assert.Equal(0, restarter.Calls);
    }

    [Fact]
    public async Task RestartFailureReportsErrorAfterSinglePrompt()
    {
        var restarter = new FakeRestarter { Failure = new InvalidOperationException("restart failed") };
        var dialogs = new FakeDialogs { Restart = true };
        await new StyleController(new FakeRunner(), restarter, dialogs).ApplyAsync(MenuStyle.Windows10);
        Assert.Equal(1, dialogs.Prompts);
        Assert.Contains("restart failed", Assert.Single(dialogs.Errors));
    }

    [Fact]
    public async Task RestartKillsSnapshotAndStartsExplorerAfterTwoSecondsWithoutRespawn()
    {
        var environment = new FakeExplorerEnvironment();
        await new ExplorerRestarter(environment).RestartAsync();
        Assert.Equal(new[] { 10, 20 }, environment.Killed);
        Assert.Equal(TimeSpan.FromSeconds(2), environment.Delays.Take(8).Aggregate(TimeSpan.Zero, (sum, delay) => sum + delay));
        Assert.Equal(1, environment.Starts);
        Assert.Equal("start", environment.Events[10]);
        Assert.Equal(9, environment.Delays.Count);
        Assert.Equal(new[] { "kill 10", "kill 20" }, environment.Events.Take(2));
    }

    [Fact]
    public async Task RespawnedShellIsNeitherKilledNorStartedAgain()
    {
        var environment = new FakeExplorerEnvironment { Respawn = true };
        await new ExplorerRestarter(environment).RestartAsync();
        Assert.Equal(new[] { 10, 20 }, environment.Killed);
        Assert.Single(environment.Delays);
        Assert.Equal(0, environment.Starts);
    }

    [Fact]
    public async Task KillFailureDoesNotStartAdditionalExplorer()
    {
        var environment = new FakeExplorerEnvironment { KillFailure = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ExplorerRestarter(environment).RestartAsync());
        Assert.Equal(0, environment.Starts);
        Assert.Empty(environment.Delays);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KillWaitTimeoutRecoversShellBeforeReportingThaiError(bool respawn)
    {
        var environment = new FakeExplorerEnvironment { KillTimeout = true, Respawn = respawn };
        var dialogs = new FakeDialogs { Restart = true };
        await new StyleController(new FakeRunner(), new ExplorerRestarter(environment), dialogs)
            .ApplyAsync(MenuStyle.Windows10);
        Assert.Equal(new[] { 10, 20 }, environment.Killed);
        Assert.Equal(respawn ? 0 : 1, environment.Starts);
        Assert.True(environment.GetCurrentUserExplorerIds().Count > 0);
        var error = Assert.Single(dialogs.Errors);
        Assert.Contains("หมดเวลารอ Explorer ปิด", error);
        Assert.DoesNotContain("A task was canceled", error);
    }

    [Fact]
    public async Task KillWaitTimeoutAndFailedRecoveryReportsThaiRecoveryError()
    {
        var environment = new FakeExplorerEnvironment { KillTimeout = true, StartFails = true };
        var dialogs = new FakeDialogs { Restart = true };
        await new StyleController(new FakeRunner(), new ExplorerRestarter(environment), dialogs)
            .ApplyAsync(MenuStyle.Windows10);
        Assert.Equal(1, environment.Starts);
        Assert.Contains("Explorer ยังไม่ทำงาน", Assert.Single(dialogs.Errors));
        Assert.DoesNotContain("A task was canceled", dialogs.Errors[0]);
    }

    [Fact]
    public async Task ExplorerThatFailsToStartIsReportedAfterBoundedWait()
    {
        var environment = new FakeExplorerEnvironment { StartFails = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ExplorerRestarter(environment).RestartAsync());
        Assert.Equal(1, environment.Starts);
        Assert.Equal(16, environment.Delays.Count);
    }

    private sealed class FakeRunner : IProcessRunner
    {
        public ProcessResult Result { get; init; } = new(0, "");
        public Exception? Failure { get; init; }
        public ProcessStartInfo? Command { get; private set; }
        public int Calls { get; private set; }
        public Task<ProcessResult> RunAsync(ProcessStartInfo startInfo)
        {
            Calls++;
            Command = startInfo;
            return Failure is null ? Task.FromResult(Result) : Task.FromException<ProcessResult>(Failure);
        }
    }

    private sealed class FakeRestarter : IExplorerRestarter
    {
        public int Calls { get; private set; }
        public Exception? Failure { get; init; }
        public Task RestartAsync()
        {
            Calls++;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private sealed class FakeDialogs : IStyleDialogs
    {
        public bool Restart { get; init; }
        public int Prompts { get; private set; }
        public List<string> Errors { get; } = [];
        public bool ConfirmExplorerRestart() { Prompts++; return Restart; }
        public void ShowError(string message) => Errors.Add(message);
    }

    private sealed class FakeExplorerEnvironment : IExplorerEnvironment
    {
        private int reads;
        public bool Respawn { get; init; }
        public bool KillFailure { get; init; }
        public bool KillTimeout { get; init; }
        public bool StartFails { get; init; }
        public List<int> Killed { get; } = [];
        public List<TimeSpan> Delays { get; } = [];
        public List<string> Events { get; } = [];
        public int Starts { get; private set; }
        public IReadOnlyList<int> GetCurrentUserExplorerIds() =>
            reads++ == 0 ? [10, 20] : Respawn || (Starts > 0 && !StartFails) ? [30] : [];
        public Task KillAsync(int processId)
        {
            Killed.Add(processId);
            Events.Add($"kill {processId}");
            if (KillTimeout) return Task.FromException(new TaskCanceledException());
            return KillFailure ? Task.FromException(new InvalidOperationException("kill failed")) : Task.CompletedTask;
        }
        public Task DelayAsync(TimeSpan delay)
        {
            Delays.Add(delay);
            Events.Add("delay");
            return Task.CompletedTask;
        }
        public void StartExplorer() { Starts++; Events.Add("start"); }
    }
}
