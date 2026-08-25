using Puck.Agent;
using Puck.Avatar;
using Puck.ClientWindow;
using Puck.Localization;
using Puck.Movement;

namespace Puck.App;

/// 에이전트가 무엇을 하고 있는지를 **사람이 보는 두 가지**로 옮긴다:
/// 채팅 창에 쌓이는 줄과 펫이 짓는 표정.
///
/// PetBootstrap에서 떼어 낸 이유는 이것이 배선이 아니라 번역이기 때문이다 —
/// AgentEvent 하나가 늘 때 고쳐야 할 곳이 앱 전체를 엮는 파일이면 안 된다.
public sealed class AgentProgressPresenter(Func<ChatWindow?> chat)
{
    /// 표정 이름은 puck-linux가 브리지로 보내던 것 그대로다. 아바타 매니페스트의
    /// `emotions`에 없으면 idle로 떨어지므로(`AvatarLoader.ResolveClipStem`),
    /// 표정이 없는 아바타에서도 그림이 사라지지는 않는다.
    private const string ThinkingClip = "thinking";
    private const string HappyClip = "happy";
    private const string SadClip = "sad";

    private readonly EmotionOverride _emotion = new();

    /// 지금 표정이 상태의 클립을 덮고 있는가. 물러날 때 한 번만 되돌리려고 센다.
    private bool _showing;

    /// 사람이 한 말을 넘겼다. 한 턴이 얼마나 걸릴지는 아무도 모르므로
    /// (도구를 열두 번까지 부른다) 시간을 정해 두지 않고 붙잡고 있는다.
    public void TurnStarted(string userText)
    {
        chat()?.Append(TranscriptKind.User, userText);
        _emotion.Hold(ThinkingClip);
    }

    public void TurnFinished() => _emotion.Show(HappyClip);

    public void TurnFailed(string message)
    {
        chat()?.Append(TranscriptKind.Error, $"{Strings.ChatFailed}: {message}");
        _emotion.Show(SadClip);
    }

    /// 에이전트 루프는 스레드 풀을 오가므로 이것도 그럴 수 있다.
    /// `ChatWindow.Append`가 알아서 UI 스레드로 넘긴다.
    public void OnProgress(AgentEvent progress)
    {
        var window = chat();

        switch (progress)
        {
            case AgentEvent.UsingTool using_:
                window?.Append(TranscriptKind.Tool, string.Format(Strings.ChatUsingTool, using_.Name));
                break;

            // 성공한 도구는 이미 위에서 한 줄 나갔다. 실패만 덧붙인다 —
            // 도구마다 두 줄씩 쌓이면 사람이 답을 찾지 못한다.
            case AgentEvent.ToolDone { IsError: true } done:
                window?.Append(TranscriptKind.Error, string.Format(Strings.ChatToolFailed, done.Name));
                break;

            case AgentEvent.Refused refused:
                window?.Append(TranscriptKind.Notice, string.Format(Strings.ChatToolRefused, refused.Name));
                break;

            // 모델이 도구를 부르기 전에 하는 말("이거 볼게")도 답이다.
            case AgentEvent.Said said:
                window?.Append(TranscriptKind.Pet, said.Text);
                break;
        }
    }

    /// 표정이 있으면 상태가 고른 클립 위에 덮는다. **상태를 바꾸지는 않는다** —
    /// 생각하는 동안에도 펫은 걷고 떨어진다. 시간이 다 되면 그때 한 번
    /// 상태의 클립을 다시 걸어 원래대로 돌린다.
    public void Advance(double dt, CharacterBody body, CharacterController controller)
    {
        var clip = _emotion.Tick(dt);

        if (clip is not null)
        {
            body.Play(clip, loop: true);
            _showing = true;
            return;
        }

        if (!_showing) return;
        _showing = false;
        controller.ReplayCurrentClip();
    }
}
