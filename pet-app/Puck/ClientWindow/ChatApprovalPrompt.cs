using System.Text.Json;
using System.Windows;
using Puck.Agent;

namespace Puck.ClientWindow;

/// 승인을 채팅 창에 물어본다. `DenyingApprovalPrompt`가 있던 자리다 —
/// 물어볼 UI가 없어서 거절하던 것이, 이제 물어볼 곳이 생겼다.
///
/// puck-linux의 `GtkApprover`와 같은 일을 한다. 저쪽은 워커 스레드를 막고
/// 답을 기다리지만 여기서는 기다리는 것이 `Task`라 아무 스레드도 잡지 않는다.
public sealed class ChatApprovalPrompt(Func<ChatWindow?> window) : IApprovalPrompt
{
    /// 인자를 사람에게 보여 줄 때의 길이 상한. 스크립트 한 편이 통째로
    /// 들어오면 창이 그것으로 가득 차 "허용" 단추가 화면 밖으로 밀린다.
    private const int ArgumentLimit = 600;

    public async Task<bool> RequestAsync(string toolName, IReadOnlyDictionary<string, JsonElement> arguments,
                                         CancellationToken cancellation)
    {
        var dispatcher = Application.Current?.Dispatcher;

        // 물어볼 수 없으면 거절한다. 물을 수 없을 때 "예"로 치는 것은
        // 사람이 안 보는 사이에 명령을 실행하는 것이다.
        if (dispatcher is null) return false;

        var asking = await dispatcher.InvokeAsync(() =>
        {
            var chat = window();
            return chat is null
                ? Task.FromResult(false)
                : chat.RequestApprovalAsync(toolName, Describe(arguments), cancellation);
        });

        return await asking;
    }

    /// 무엇을 실행하는지 사람이 읽을 수 있게. 인자가 하나뿐인 도구(대부분)는
    /// 값만 보여 준다 — `{"command": "git status"}`보다 `git status`가 낫다.
    public static string Describe(IReadOnlyDictionary<string, JsonElement> arguments)
    {
        var text = arguments.Count switch
        {
            0 => "",
            1 => Value(arguments.Single().Value),
            _ => string.Join("\n", arguments.Select(a => $"{a.Key}: {Value(a.Value)}")),
        };

        return text.Length <= ArgumentLimit ? text : text[..ArgumentLimit] + "…";
    }

    private static string Value(JsonElement element)
        => element.ValueKind == JsonValueKind.String ? element.GetString() ?? "" : element.ToString();
}
