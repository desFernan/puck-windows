namespace Puck.Agent;

/// 모델이 지금까지 들은 것.
///
/// 턴을 도는 일과 떼어 둔 이유는 다듬는 규칙 때문이다. 두 규칙 다 사람에게는
/// "잘 되던 대화가 갑자기 설명 없는 API 오류를 낸다"로 도착하는 실패를 막는
/// 것이고, 러너를 통해서는 예순 턴을 돌려야 닿는 자리라 따로 시험할 수 없다.
///
/// puck-mac의 `AgentConversations`를 옮긴 것이다. 저쪽은 채팅이 여럿이라
/// 채팅별로 나눠 들고 있지만, 여기는 대화가 하나뿐이라 목록 하나다.
public sealed class AgentConversation
{
    /// 들고 있는 메시지 수. 대화가 길어지면 매 요청이 그만큼 비싸지고
    /// 느려진다 — 지난 대화 전부를 매번 다시 올려 보내기 때문이다.
    public const int MaxMessages = 40;

    private readonly List<AgentMessage> _messages = [];

    public IReadOnlyList<AgentMessage> Messages => _messages;

    /// 지금까지의 대화에 이번 턴의 첫 마디를 얹은 **사본**. 턴은 이 위에서
    /// 자라다가 끝날 때 통째로 들어온다.
    ///
    /// 기록을 진행하며 조금씩 채우면, 도중에 네트워크가 끊기거나 사람이
    /// 취소했을 때 답 없는 user 메시지나 결과가 짝지어지지 않은 `tool_use`가
    /// 남는다. API는 그런 대화를 통째로 거절하므로, 한 번 실패한 펫은 앱을
    /// 껐다 켜기 전까지 다시는 말하지 못한다.
    public List<AgentMessage> Branch(AgentMessage opening) => [.. _messages, opening];

    /// 끝난 턴을 통째로 들여놓는다. 통째로 남거나 통째로 없던 일이 된다.
    public void Commit(IReadOnlyList<AgentMessage> turn)
    {
        _messages.Clear();
        _messages.AddRange(turn);
        Trim(_messages);
    }

    /// 오래된 것부터 버리되 **사람의 새 질문 자리에서만 자른다.**
    ///
    /// 아무 데서나 자르면 결과가 짝지어지지 않은 `tool_use`가 맨 앞에 남거나
    /// 같은 역할이 연달아 오는 기록이 되고, API는 그런 대화를 통째로 거절한다.
    /// 자를 자리를 못 찾으면 그냥 둔다 — 비싼 대화가 망가진 대화보다 낫다.
    public static void Trim(List<AgentMessage> messages)
    {
        if (messages.Count <= MaxMessages) return;

        for (var cut = messages.Count - MaxMessages; cut < messages.Count; cut++)
        {
            if (!IsFreshQuestion(messages[cut])) continue;
            messages.RemoveRange(0, cut);
            return;
        }
    }

    /// 도구 결과가 아니라 사람이 새로 꺼낸 말. 대화는 언제나 여기서 시작한다.
    private static bool IsFreshQuestion(AgentMessage message)
        => message.Role == AgentRole.User && !message.Blocks.OfType<AgentBlock.ToolResult>().Any();
}
