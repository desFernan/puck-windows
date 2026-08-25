using System.Text.Json;
using Puck.Agent;
using Puck.Tools;

namespace PuckTests.Agent;

public class ToolApprovalsTests
{
    private sealed class ScriptedPrompt(bool answer) : IApprovalPrompt
    {
        public int Asked { get; private set; }
        public Task<bool> RequestAsync(string toolName, IReadOnlyDictionary<string, JsonElement> arguments,
                                       CancellationToken cancellation)
        {
            Asked++;
            return Task.FromResult(answer);
        }
    }

    private static ToolSpec Spec(ToolApproval approval) => new()
    {
        Name = "t",
        Description = "d",
        Properties = new Dictionary<string, JsonElement>(),
        Approval = approval,
    };

    private static IReadOnlyDictionary<string, JsonElement> Args(string? command = null)
        => command is null
            ? new Dictionary<string, JsonElement>()
            : new Dictionary<string, JsonElement> { ["command"] = JsonSerializer.SerializeToElement(command) };

    // --- 허용 목록 ---

    [Theory]
    [InlineData("git status")]
    [InlineData("echo hello")]
    [InlineData("  dir  ")]
    [InlineData("Get-Process")]
    public void ReadOnlyCommandsGoThroughWithoutAsking(string command)
    {
        Assert.True(ToolApprovals.IsAllowlistedCommand(command));
    }

    [Theory]
    [InlineData("git push origin main")]
    [InlineData("git reset --hard HEAD~5")]
    [InlineData("git clean -fdx")]
    [InlineData("git -C C:/repo push")]
    [InlineData("git")]
    public void GitCommandsThatChangeTheRepositoryStillAsk(string command)
    {
        // `git`은 읽기도 쓰기도 한다. 첫 낱말만 보면 `git push`가 `git status`와
        // 구분되지 않는다.
        Assert.False(ToolApprovals.IsAllowlistedCommand(command));
    }

    [Theory]
    [InlineData("git log --oneline -20")]
    [InlineData("git diff HEAD~1")]
    [InlineData("git show abc123")]
    public void ReadOnlyGitStillGoesThrough(string command)
    {
        Assert.True(ToolApprovals.IsAllowlistedCommand(command));
    }

    [Theory]
    [InlineData("del *")]
    [InlineData("shutdown /s")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseIsNotAllowlisted(string? command)
    {
        Assert.False(ToolApprovals.IsAllowlistedCommand(command));
    }

    [Theory]
    [InlineData("git log && del *")]
    [InlineData("echo hi; shutdown /s")]
    [InlineData("dir | Remove-Item")]
    [InlineData("echo x > C:/important.txt")]
    [InlineData("echo `whoami`")]
    [InlineData("echo $(Remove-Item C:/x)")]
    [InlineData("git status $env:USERPROFILE")]
    public void ChainingDefeatsTheAllowlist(string command)
    {
        // 이어붙인 명령의 첫 낱말은 안전해 보인다. 그것만 보면 통과한다.
        Assert.False(ToolApprovals.IsAllowlistedCommand(command));
    }

    [Theory]
    [InlineData("git log\nRemove-Item -Recurse C:/important")]
    [InlineData("dir\r\nshutdown /s")]
    [InlineData("echo hi\n")]
    public void ANewLineIsAlsoAChain(string command)
    {
        // run_shell은 한 줄짜리 도구다. 첫 줄만 보고 통과시키면 그 아래에
        // 무엇이든 붙일 수 있다 — PowerShell은 줄바꿈을 명령 구분자로 읽는다.
        Assert.False(ToolApprovals.IsAllowlistedCommand(command));
    }

    // --- 승인 흐름 ---

    [Fact]
    public async Task AToolThatNeedsNoApprovalIsNeverAskedAbout()
    {
        var prompt = new ScriptedPrompt(false);
        var approvals = new ToolApprovals(prompt);

        Assert.True(await approvals.IsAllowedAsync(
            Spec(ToolApproval.NotRequired), Args(), AgentPermissionMode.ToolsOnly, default));
        Assert.Equal(0, prompt.Asked);
    }

    [Fact]
    public async Task AToolThatNeedsApprovalAsks()
    {
        var prompt = new ScriptedPrompt(true);
        var approvals = new ToolApprovals(prompt);

        Assert.True(await approvals.IsAllowedAsync(
            Spec(ToolApproval.Required), Args(), AgentPermissionMode.ToolsOnly, default));
        Assert.Equal(1, prompt.Asked);
    }

    [Fact]
    public async Task SayingNoStopsTheTool()
    {
        var approvals = new ToolApprovals(new ScriptedPrompt(false));
        Assert.False(await approvals.IsAllowedAsync(
            Spec(ToolApproval.Required), Args(), AgentPermissionMode.ToolsOnly, default));
    }

    [Fact]
    public async Task AnAllowlistedShellCommandSkipsThePrompt()
    {
        var prompt = new ScriptedPrompt(false);
        var approvals = new ToolApprovals(prompt);

        Assert.True(await approvals.IsAllowedAsync(
            Spec(ToolApproval.RequiredUnlessAllowlisted), Args("git status"),
            AgentPermissionMode.ToolsOnly, default));
        Assert.Equal(0, prompt.Asked);
    }

    [Fact]
    public async Task AShellCommandOutsideTheListStillAsks()
    {
        var prompt = new ScriptedPrompt(false);
        var approvals = new ToolApprovals(prompt);

        Assert.False(await approvals.IsAllowedAsync(
            Spec(ToolApproval.RequiredUnlessAllowlisted), Args("del *"),
            AgentPermissionMode.ToolsOnly, default));
        Assert.Equal(1, prompt.Asked);
    }

    [Fact]
    public async Task WithEverythingAllowedNothingIsAsked()
    {
        // 사람이 한 번 정한 것을 매 호출마다 다시 묻는 것은 그 설정을 없는 셈 치는 것이다.
        var prompt = new ScriptedPrompt(false);
        var approvals = new ToolApprovals(prompt);

        Assert.True(await approvals.IsAllowedAsync(
            Spec(ToolApproval.Required), Args(), AgentPermissionMode.Everything, default));
        Assert.Equal(0, prompt.Asked);
    }

    [Fact]
    public async Task WithNoUiToAskTheAnswerIsNo()
    {
        // 물어볼 수 없는 상황에서 "예"로 치는 것은 사람이 안 보는 사이에
        // 명령을 실행하는 것이다.
        var approvals = new ToolApprovals(new DenyingApprovalPrompt());
        Assert.False(await approvals.IsAllowedAsync(
            Spec(ToolApproval.Required), Args(), AgentPermissionMode.ToolsOnly, default));
    }
}
