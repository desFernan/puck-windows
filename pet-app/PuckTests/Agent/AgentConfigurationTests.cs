using Puck.Agent;

namespace PuckTests.Agent;

public class DotEnvTests
{
    [Fact]
    public void APlainAssignmentIsRead()
    {
        var values = DotEnv.Parse("ANTHROPIC_API_KEY=sk-ant-123");
        Assert.Equal("sk-ant-123", values["ANTHROPIC_API_KEY"]);
    }

    [Fact]
    public void CommentsAndBlankLinesAreSkipped()
    {
        var values = DotEnv.Parse("# 주석\n\n  \nPUCK_MODEL=claude-opus-5\n");
        Assert.Single(values);
        Assert.Equal("claude-opus-5", values["PUCK_MODEL"]);
    }

    [Fact]
    public void AnExportPrefixIsAllowedBecausePeoplePasteFromTheirShell()
    {
        Assert.Equal("x", DotEnv.Parse("export ANTHROPIC_API_KEY=x")["ANTHROPIC_API_KEY"]);
    }

    [Fact]
    public void QuotesAreStrippedBecauseAQuotedKeyJustGets401()
    {
        Assert.Equal("x", DotEnv.Parse("ANTHROPIC_API_KEY=\"x\"")["ANTHROPIC_API_KEY"]);
        Assert.Equal("x", DotEnv.Parse("ANTHROPIC_API_KEY='x'")["ANTHROPIC_API_KEY"]);
    }

    [Fact]
    public void AValueMayContainEqualsSigns()
    {
        Assert.Equal("a=b=c", DotEnv.Parse("K=a=b=c")["K"]);
    }

    [Fact]
    public void ALineWithoutAnEqualsIsIgnoredRatherThanThrowing()
    {
        Assert.Empty(DotEnv.Parse("nonsense\n=novalue\n"));
    }
}

public class AgentConfigurationTests
{
    private static readonly Dictionary<string, string> Empty = new();

    [Fact]
    public void WithNothingSetTheDefaultsAreTheGoodOnes()
    {
        var config = AgentConfiguration.Resolve(Empty, _ => null);

        Assert.Equal("claude-opus-5", config.Model);
        Assert.Equal("high", config.Effort);
        Assert.Equal(AgentPermissionMode.ToolsOnly, config.Permissions);
        Assert.False(config.IsUsable);
    }

    [Fact]
    public void TheEnvironmentBeatsTheFile()
    {
        // 터미널에서 export 하고 띄운 사람의 의도가 파일에 적힌 것보다 명확하다.
        var file = new Dictionary<string, string> { ["ANTHROPIC_API_KEY"] = "from-file" };
        var config = AgentConfiguration.Resolve(file, k => k == "ANTHROPIC_API_KEY" ? "from-env" : null);

        Assert.Equal("from-env", config.ApiKey);
    }

    [Fact]
    public void TheFileIsUsedWhenTheEnvironmentIsEmpty()
    {
        var file = new Dictionary<string, string> { ["ANTHROPIC_API_KEY"] = "from-file" };
        Assert.Equal("from-file", AgentConfiguration.Resolve(file, _ => null).ApiKey);
    }

    [Fact]
    public void AnEmptyEnvironmentValueDoesNotShadowTheFile()
    {
        var file = new Dictionary<string, string> { ["ANTHROPIC_API_KEY"] = "from-file" };
        Assert.Equal("from-file", AgentConfiguration.Resolve(file, _ => "   ").ApiKey);
    }

    [Theory]
    [InlineData("edits", AgentPermissionMode.Edits)]
    [InlineData("all", AgentPermissionMode.Everything)]
    [InlineData("everything", AgentPermissionMode.Everything)]
    [InlineData("auto", AgentPermissionMode.Auto)]
    [InlineData("AUTO", AgentPermissionMode.Auto)]
    [InlineData("tools", AgentPermissionMode.ToolsOnly)]
    public void PermissionsParseFromTheirSettingValue(string raw, AgentPermissionMode expected)
    {
        Assert.Equal(expected, AgentConfiguration.ParsePermissions(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("EVERYTHNG")]
    public void AnUnknownPermissionFallsToTheNarrowestOne(string? raw)
    {
        // 설정 파일의 오타가 권한을 넓히는 방향으로 작용하면 안 된다.
        Assert.Equal(AgentPermissionMode.ToolsOnly, AgentConfiguration.ParsePermissions(raw));
    }

    [Fact]
    public void TheApiKeyNeverReachesTheLog()
    {
        var config = AgentConfiguration.Resolve(
            new Dictionary<string, string> { ["ANTHROPIC_API_KEY"] = "sk-ant-secret" }, _ => null);

        var fields = config.ToLogFields();
        Assert.DoesNotContain(fields.Values, v => v?.ToString()?.Contains("secret") == true);
        Assert.Equal(true, fields["hasKey"]);
    }
}
