using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Puck.Diagnostics;
using PuckTools = Puck.Tools;

namespace Puck.Agent;

/// Anthropic Messages API를 부른다. 공식 C# SDK를 쓴다.
///
/// mac은 SDK 없이 HTTP를 직접 쓰는데, 원본 주석이 그 이유를 적어 뒀다 —
/// **Swift용 공식 SDK가 없어서**다. C#에는 있으므로 헤더·content block 조립·
/// tool_result가 user 메시지로 들어가는 규칙 같은 것을 우리가 계속 맞출 이유가 없다.
///
/// 우리 대화 모델 ↔ SDK 타입 변환은 이 파일에서만 일어난다.
public sealed class AnthropicAgentClient(Func<AgentConfiguration> configuration) : IAgentClient
{
    /// 스트리밍하지 않는 요청의 권장 기본값. thinking과 보이는 답이 이 안에서
    /// 함께 쓰이므로, 답 길이만 보고 잡으면 생각하다 잘린 턴이 돌아온다.
    private const int MaxTokens = 16000;

    public async Task<AgentTurn> SendAsync(
        string systemPrompt,
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<PuckTools.ToolSpec> tools,
        CancellationToken cancellation)
    {
        var config = configuration();

        // 클라이언트를 매번 만드는 이유는 설정의 키가 앱을 끄지 않고도
        // 적용돼야 하기 때문이다. 만드는 비용은 HTTP 한 번에 비해 없는 것과 같다.
        var client = new AnthropicClient { ApiKey = config.ApiKey };

        var parameters = new MessageCreateParams
        {
            Model = config.Model,
            MaxTokens = MaxTokens,
            // 도구를 고르고 여러 걸음을 잇는 일이라 생각을 켠다. Opus 5는
            // 생략해도 adaptive지만, 모델을 바꿔도 뜻이 유지되게 명시한다.
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig { Effort = EffortFrom(config.Effort) },
            System = systemPrompt,
            Messages = messages.Select(ToSdk).ToList(),
            // 도구가 없으면 빈 목록을 보낸다 — Tools는 init 전용이라 나중에
            // 채울 수 없고, 빈 목록은 "쓸 도구가 없다"와 같은 뜻이다.
            Tools = tools.Select(ToSdk).ToList(),
        };

        var response = await client.Messages.Create(parameters, cancellationToken: cancellation);

        // 안전 분류기가 거절하면 HTTP 200에 stop_reason=refusal로 온다.
        // content를 먼저 읽으면 빈 답을 정상으로 착각한다.
        if (response.StopReason == "refusal")
        {
            var why = response.StopDetails?.Explanation ?? "이유가 오지 않았습니다";
            AppLogger.Warning("agent", "모델이 요청을 거절했습니다",
                new Dictionary<string, object?> { ["category"] = response.StopDetails?.Category?.ToString() });
            return new AgentTurn([new AgentBlock.Text($"그건 답할 수 없어요. ({why})")], WantsToolUse: false);
        }

        return new AgentTurn(FromSdk(response.Content), response.StopReason == "tool_use");
    }

    private static Effort EffortFrom(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "medium" => Effort.Medium,
        "max" => Effort.Max,
        _ => Effort.High,
    };

    private static ToolUnion ToSdk(PuckTools.ToolSpec spec) => new Tool
    {
        Name = spec.Name,
        Description = spec.Description,
        InputSchema = new()
        {
            Properties = spec.Properties.ToDictionary(p => p.Key, p => p.Value),
            Required = spec.Required.ToList(),
        },
    };

    private static MessageParam ToSdk(AgentMessage message)
    {
        var blocks = new List<ContentBlockParam>();

        foreach (var block in message.Blocks)
        {
            switch (block)
            {
                case AgentBlock.Text text:
                    blocks.Add(new TextBlockParam { Text = text.Value });
                    break;

                case AgentBlock.Thinking thinking:
                    // 서명은 그대로 돌려보내야 한다 — 손대면 API가 거절한다.
                    blocks.Add(new ThinkingBlockParam
                    {
                        Thinking = thinking.Value,
                        Signature = thinking.Signature,
                    });
                    break;

                case AgentBlock.ToolUse toolUse:
                    blocks.Add(new ToolUseBlockParam
                    {
                        ID = toolUse.Id,
                        Name = toolUse.Name,
                        Input = toolUse.Input,
                    });
                    break;

                case AgentBlock.ToolResult result:
                    blocks.Add(new ToolResultBlockParam
                    {
                        ToolUseID = result.ToolUseId,
                        Content = result.Content,
                        IsError = result.IsError,
                    });
                    break;
            }
        }

        return new MessageParam
        {
            Role = message.Role == AgentRole.User ? Role.User : Role.Assistant,
            Content = blocks,
        };
    }

    private static IReadOnlyList<AgentBlock> FromSdk(IReadOnlyList<ContentBlock> content)
    {
        var blocks = new List<AgentBlock>(content.Count);

        foreach (var block in content)
        {
            if (block.TryPickText(out TextBlock? text))
            {
                blocks.Add(new AgentBlock.Text(text.Text));
            }
            else if (block.TryPickThinking(out ThinkingBlock? thinking))
            {
                blocks.Add(new AgentBlock.Thinking(thinking.Thinking, thinking.Signature));
            }
            else if (block.TryPickToolUse(out ToolUseBlock? toolUse))
            {
                blocks.Add(new AgentBlock.ToolUse(toolUse.ID, toolUse.Name, toolUse.Input));
            }
            // 그 밖(서버 도구 결과 등)은 이 Phase에서 쓰지 않으므로 흘려보낸다.
        }

        return blocks;
    }
}
