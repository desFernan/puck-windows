using System.Diagnostics;
using System.IO;
using System.Windows;
using Puck.Agent;
using Puck.Audio;
using Puck.Avatar;
using Puck.ClientWindow;
using Puck.Diagnostics;
using Puck.Input;
using Puck.Localization;
using Puck.Movement;
using Puck.Movement.States;
using Puck.Overlay;
using Puck.Pointing;
using Puck.Tools;
using Puck.Settings;
using Puck.WindowSensing;

namespace Puck.App;

/// 전부를 엮는 한 곳. 여기서 아바타를 고르고, 창을 띄우고, 프레임
/// 루프를 돌리고, 제스처를 상태 전이로 옮긴다.
public sealed class PetBootstrap : IDisposable, IWanderDelegate
{
    private readonly SettingsStore _settings;
    private readonly CompositionFrameClock _clock = new();
    private readonly PetGestureRecognizer _gestures = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private PetOverlayWindow? _window;
    private SpriteAvatar? _avatar;
    private CharacterBody? _body;
    private CharacterController? _controller;
    private ScreenSpace? _screens;

    /// 펫과 글로 말하는 창. 트레이에서 열거나, 승인을 물어야 할 때 저절로 뜬다.
    private ChatWindow? _chat;

    /// 한 번 정해 두고 잊는 설정들의 창. 트레이에서만 연다.
    private SettingsWindow? _settingsWindow;


    /// 지난 프레임에 펫이 걷던 세계의 크기. 이것이 바뀌면 딛고 있던 바닥이
    /// 사라졌을 수 있다.
    private Rect? _lastRoamableArea;

    /// 천장을 목적지로 뽑힌 배회가 벽까지 걸어가는 중이다. 도착하면 오른다.
    private bool _pendingCeilingClimb;
    private ReactDragState? _drag;
    private TrayIcon? _tray;
    private WindowListWatcher? _windows;
    private WalkState? _walk;
    private MoveToState? _moveTo;
    private HotkeyCoordinator? _hotkeys;
    private TextInputBubbleWindow? _bubble;
    private ClickDetector? _mouse;
    private SfxPlayer? _sfx;
    private SoundTable? _sounds;
    private AgentRunner? _agent;
    private readonly PendingPointTracker _pending = new();

    /// 에이전트의 진행 상황을 채팅 줄과 표정으로 옮긴다.
    private AgentProgressPresenter? _presenter;
    private Puck.Interop.WinEventHook? _foreground;

    public PetBootstrap(SettingsStore settings) => _settings = settings;

    public void Start()
    {
        PuckPaths.EnsureCreated();
        AppLogger.Configure(new JsonLinesFileAppender(PuckPaths.Logs));

        _tray = new TrayIcon(
            onOpenChat: () => Chat().ShowAndActivate(),
            onToggleVisible: ToggleVisible,
            onOpenCustomisationFolder: OpenCustomisationFolder,
            onReloadAvatar: ReloadAvatar,
            onOpenSettings: () => Settings().ShowAndActivate(),
            initiallyMuted: _settings.Muted,
            onMutedChanged: muted =>
            {
                _settings.Muted = muted;
                _settings.Save();
            },
            // 채팅 창을 먼저 닫는다. Shutdown이 닫는 순서에 기대면, 닫기를
            // 막는 창(우리는 X를 숨기기로 바꿔 뒀다)이 종료를 붙잡을 수 있다.
            onQuit: () =>
            {
                _chat?.CloseForGood();
                _settingsWindow?.CloseForGood();
                Application.Current.Shutdown();
            });

        // 창을 먼저 보기 시작한다 — 첫 프레임에 이미 목록이 있어야 펫이
        // 바닥에서 시작했다가 창 위로 튀어 오르지 않는다.
        _windows = WindowListWatcher.CreateDefault();
        // 쉬는 펫은 창 목록에 기대는 일을 하지 않는다. 치워 둔 펫도 마찬가지다 —
        // 보이지도 않는 펫을 위해 초당 열 번 창을 세는 것은 순전한 낭비다.
        _windows.PetIsResting = () =>
            _window?.IsVisible != true || _controller?.Current == StateKind.Idle;
        _windows.Start();
        _foreground = new Puck.Interop.WinEventHook(() => _windows?.NoteForegroundChanged());

        _window = new PetOverlayWindow();
        _window.Show();

        ReloadAvatar();

        _gestures.Clicked += () => { CancelWander(); _controller?.Request(StateKind.ReactClick); };
        _gestures.Dragged += position =>
        {
            if (_drag is null) return;
            CancelWander();
            _drag.DragPosition = position;
            _controller?.Request(StateKind.ReactDrag);
        };
        _gestures.Released += velocity =>
        {
            if (_body is null) return;
            _body.LaunchVelocity = velocity;
            _controller?.Request(StateKind.Fall);
        };

        var focus = new FocusAssistObserver();
        // 둘 중 하나라도 참이면 조용하다. 사람이 끈 것과 집중 지원이 끈 것을
        // 따로 두는 이유는, 집중 지원이 풀렸다고 사람이 끈 소리가 돌아오면
        // 안 되기 때문이다.
        _sfx = new SfxPlayer { IsMuted = () => _settings.Muted || focus.IsQuiet() };

        // 오버레이는 대부분의 시간 클릭스루라 마우스 이벤트를 받지 못한다.
        // Phase 1은 프레임마다 커서와 버튼을 물어봤는데(폴링), 그러면 프레임
        // 사이에 일어난 클릭을 통째로 놓친다. 저수준 훅이 그 자리를 대신한다.
        _mouse = new ClickDetector(Application.Current.Dispatcher)
        {
            Clock = () => _stopwatch.Elapsed.TotalSeconds,
        };
        _mouse.Pressed += OnMousePressed;
        _mouse.Moved += (p, t) => _gestures.OnMouseMove(p, t);
        _mouse.Released += OnMouseReleased;

        // 에이전트. 도구는 Phase 2의 감각을 그대로 물고, 설정은 매 요청마다
        // 다시 읽는다 — .env에 키를 넣은 사람이 앱을 끄지 않아도 되게.
        var registry = ToolRegistry.CreateDefault(
            windows: () => _windows?.Windows ?? [],
            // 도구는 스레드 풀에서 돈다. 화면 구성은 통째로 갈아 끼우는
            // 불변 기록이라, 갈아 끼운 것이 제때 보이기만 하면 된다.
            virtualScreen: () => Volatile.Read(ref _screens)?.Bounds ?? new Rect(0, 0, 1920, 1080),
            pointAt: PointPetAt);

        _agent = new AgentRunner(
            new AnthropicAgentClient(AgentConfiguration.FromDisk),
            registry,
            // 승인은 채팅 창이 묻는다. 창이 아직 없으면 그때 만들어 띄운다 —
            // 물어볼 곳이 없다는 이유로 거절하던 자리가 여기였다.
            new ToolApprovals(new ChatApprovalPrompt(Chat)),
            AgentConfiguration.FromDisk);

        _presenter = new AgentProgressPresenter(() => _chat);
        _agent.Progress += _presenter.OnProgress;

        _hotkeys = new HotkeyCoordinator(HotkeyBindings.Defaults, new Dictionary<string, Action>
        {
            [nameof(HotkeyBindings.SummonPet)] = SummonToCursor,
            [nameof(HotkeyBindings.TextInput)] = ShowInputBubble,
        });

        _clock.Tick += OnFrame;
        _clock.Start();
    }

    /// 설정에 저장된 아바타를, 없으면 첫 번째로 찾은 것을 불러온다.
    /// 다시 부르면 디스크에 있는 것으로 살아 있는 펫을 다시 만든다 —
    /// 그림을 고쳐 그리거나 매니페스트를 수정한 걸 앱을 끄지 않고
    /// 보는 방법이 이것이다.
    public void ReloadAvatar()
    {
        var catalogue = AvatarCatalogue.Scan(PuckPaths.Avatars);
        var entry = catalogue.FirstOrDefault(e => e.Name == _settings.AvatarName)
                    ?? catalogue.FirstOrDefault()
                    ?? BundledAvatar();

        if (entry is null)
        {
            AppLogger.Error("avatar", Strings.AvatarNoneInstalled);
            return;
        }

        SpriteAvatar avatar;
        try
        {
            avatar = SpriteAvatar.Load(entry.Directory);
        }
        catch (AvatarLoaderException ex)
        {
            AppLogger.Error("avatar", Strings.AvatarLoadFailed,
                new Dictionary<string, object?> { ["name"] = entry.Name, ["reason"] = ex.Message });
            return;
        }

        if (_screens is null) Volatile.Write(ref _screens, ScreenSpace.Current());
        if (_screens is null) return;

        var start = _body?.Position ?? StartPosition(_screens);

        _avatar = avatar;
        _sounds = SoundTable.From(avatar.Manifest, entry.Directory);
        _body = new CharacterBody(avatar, start,
            bounceIntensity: avatar.BounceIntensityOrDefault);
        _drag = new ReactDragState();

        var ledge = new ClimbLedgeState();
        _walk = new WalkState { Ledge = ledge };
        _moveTo = new MoveToState();
        var states = new Dictionary<StateKind, IStateHandler>
        {
            [StateKind.Idle] = new IdleState(new WanderScheduler()) { Wander = this },
            [StateKind.Walk] = _walk,
            [StateKind.Fall] = new FallState(),
            [StateKind.Land] = new LandState(),
            [StateKind.ClimbLedge] = ledge,
            [StateKind.Climb] = new ClimbState(),
            [StateKind.ClimbToCeiling] = new ClimbToCeilingState(),
            [StateKind.Ceiling] = new CeilingState(),
            [StateKind.MoveTo] = _moveTo,
            [StateKind.WalkOnTop] = new WalkOnTopState(),
            [StateKind.ReactClick] = new ReactClickState(),
            [StateKind.ReactDrag] = _drag,
        };

        _controller = new CharacterController(_body, states, StateKind.Idle, MakeContext);

        // 상태가 바뀌면 그 클립 이름으로 소리를 찾는다. 매니페스트의 sounds
        // 표가 clips와 같은 키 공간을 쓰기 때문에 이름이 하나로 통한다.
        _controller.Transitioned += (_, to) =>
        {
            if (states.TryGetValue(to, out var handler))
                _sfx?.Play(_sounds?.FilePath(handler.ClipKey));
        };
        _window!.Sprite.Avatar = avatar;
    }

    /// 커서가 있는 디스플레이의 바닥 한가운데.
    ///
    /// RoamableArea(모든 작업 영역의 합집합 **경계 상자**)의 바닥을 쓰면 안 된다.
    /// 디스플레이가 계단처럼 놓이면 그 경계 상자의 바닥은 어느 디스플레이에도
    /// 속하지 않는 빈 공간이고, 거기 세운 펫은 화면 밖에서 보이지 않는다.
    private static Point StartPosition(ScreenSpace screens)
    {
        var cursor = PetOverlayWindow.CursorPosition;
        var display = screens.ScreenContaining(cursor);
        return new Point(display.Left + display.Width / 2, screens.FloorY(cursor));
    }

    private StateContext MakeContext() => new()
    {
        Body = _body!,
        RoamableArea = _screens!.RoamableArea,
        AvatarHeight = _avatar!.Size.Height,
        VisualBounds = _body!.VisualBounds,
        WalkSpeed = MovementSolver.WalkSpeed * _settings.MovementSpeedMultiplier,
        LandingY = LandingY,
        HasGroundUnder = _screens.HasGroundUnder,
        SnapToGround = _screens.NearestStandablePoint,
        LedgeBeyond = _screens.LedgeBeyond,
        AreaAt = _screens.WorkingAreaContaining,
        Windows = _windows?.Windows ?? [],
        UnclimbableWindows = UnclimbableWindows(),
        RequestTransition = _ => { },   // CharacterController가 자기 것으로 갈아 끼운다
    };

    /// 전역 핫키. 지금은 "펫 부르기"만 물린다 — 음성(PTT)과 입력 버블은
    /// 각각 Phase 6과 이 Phase의 뒤쪽 태스크가 채운다.
    /// 펫 옆에 입력 버블을 띄운다. 받은 문장은 채팅 창으로 보낸 것과 같은
    /// 곳으로 간다.
    private void ShowInputBubble()
    {
        if (_body is null || _avatar is null || _screens is null) return;

        if (_bubble is null)
        {
            _bubble = new TextInputBubbleWindow();
            _bubble.Submitted += text => _ = AskAgentAsync(text);
        }

        _bubble.ShowAt(BubbleOrigin());
    }

    /// 사람이 적은 것을 에이전트에게 넘긴다. 입력 버블과 채팅 창이 같은 곳으로
    /// 들어온다 — 어느 쪽으로 말을 걸어도 대화는 하나다.
    ///
    /// 답이 올 때까지 기다리는 동안에도 펫은 계속 돌아다녀야 하므로 기다리지
    /// 않는다. 사람에게 보이는 것(줄과 표정)은 전부 presenter가 맡는다.
    private async Task AskAgentAsync(string text)
    {
        if (_agent is null || _presenter is null) return;

        _presenter.TurnStarted(text);

        try
        {
            var answer = await _agent.AskAsync(text);
            AppLogger.Log(LogLevel.Info, "agent", "펫이 답했습니다",
                new Dictionary<string, object?> { ["asked"] = text, ["answer"] = answer });

            // 마지막 답은 이미 Progress의 Said로 한 번 나갔다. 여기서 또
            // 붙이면 같은 말이 두 줄이 된다.
            _presenter.TurnFinished();
        }
        catch (Exception ex)
        {
            // 여기서 던지면 아무도 잡지 않는다(async void 자리) — 앱이 통째로 죽는다.
            AppLogger.Error("agent", "대화가 실패했습니다",
                new Dictionary<string, object?> { ["error"] = ex.Message });

            _presenter.TurnFailed(ex.Message);
        }
    }

    /// 채팅 창. 처음 필요할 때 만든다 — 한 번도 말을 안 거는 사람에게
    /// 창 하나를 미리 지어 둘 이유가 없다.
    /// 설정 창. 닫아도 버리지 않는다 — 여기에는 창이 닫힌 동안 낡아 갈
    /// 살아 있는 상태가 없다.
    private SettingsWindow Settings()
    {
        if (_settingsWindow is not null) return _settingsWindow;

        _settingsWindow = new SettingsWindow(_settings, ReloadAvatar);
        return _settingsWindow;
    }

    private ChatWindow Chat()
    {
        if (_chat is not null) return _chat;

        _chat = new ChatWindow();
        _chat.Submitted += text => _ = AskAgentAsync(text);
        return _chat;
    }

    /// point_at 도구가 부르는 곳. 펫이 실제로 거기로 걸어가 가리키고, 그 뒤의
    /// 클릭은 "가리킨 그것을 눌렀다"로 친다.
    private void PointPetAt(Point target)
    {
        if (_moveTo is null || _controller is null || _screens is null) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            CancelWander();
            _pending.Point(target, _stopwatch.Elapsed.TotalSeconds);
            _moveTo.Target = new Point(target.X, LandingY(target));
            _controller.Request(StateKind.MoveTo);
        });
    }

    /// 펫 머리 위. 프레임마다 다시 계산해서 펫이 걸으면 따라간다.
    private Point BubbleOrigin()
    {
        var position = _body!.Position;
        var display = _screens!.WorkingAreaContaining(position);
        return SpeechBubblePlacement.Origin(position, _avatar!.Size.Height, _bubble!.MeasuredSize, display);
    }

    /// 커서 쪽으로 오라고 한다. 커서가 창 위면 그 창의 윗변에 자리를 잡고,
    /// 아니면 커서 아래의 착지면으로 간다 — 부른 사람이 보고 있는 곳이 거기다.
    private void SummonToCursor()
    {
        if (_body is null || _controller is null || _moveTo is null || _avatar is null || _screens is null) return;

        var cursor = PetOverlayWindow.CursorPosition;
        var covering = _windows is null
            ? null
            : WindowSupport.CoveringWindow(cursor, _avatar.Size.Height, _windows.Windows);

        var perch = covering is null ? null : WindowSupport.PerchTarget(
            covering, cursor,
            roamableTop: _screens.CeilingY(cursor),
            avatarHeight: _avatar.Size.Height,
            petHalfWidth: _avatar.Size.Width / 2);

        CancelWander();
        _moveTo.Target = perch ?? new Point(cursor.X, LandingY(cursor));
        _controller.Request(StateKind.MoveTo);
    }

    /// 배회 타이머가 찼다. 창 목록을 아는 건 여기뿐이라 "저 창을 타고 오르자"는
    /// 결정이 여기서 나온다.
    public void WanderRequested(WanderOutcome outcome)
    {
        if (_body is null || _controller is null || _walk is null) return;

        // 움직임을 줄여 달라고 해 두었으면 배회는 일어나지 않는다. 배회만
        // 걸러지는 이유는 그것이 아무도 요청하지 않은 유일한 움직임이기
        // 때문이다 — 드래그도, 던지기도, 도구도, 불러서 오는 것도 이
        // 함수를 지나가지 않는다.
        outcome = ReducedMotion.Apply(outcome, ReducedMotion.IsOn);

        switch (outcome)
        {
            case WanderOutcome.ClimbNearestWindow when _windows is not null && _avatar is not null:
                var climbTarget = WindowSupport.NearestClimbTarget(
                    _body.Position, _windows.Windows,
                    roamableTop: _screens!.CeilingY(_body.Position),
                    avatarHeight: _avatar.Size.Height,
                    excluding: UnclimbableWindows());

                // 오를 것이 없으면 그냥 걷는다 — 오류가 아니라 흔한 경우다.
                _walk.TargetX = climbTarget?.X;
                _controller.Request(StateKind.Walk);
                break;

            case WanderOutcome.CrawlCeiling when _windows is not null && _avatar is not null:
                // 발밑에 벽이 있으면 바로 오른다. 없으면 벽까지 먼저 걸어간다 —
                // ClimbNearestWindow가 이미 하는 그 걸음이되, 목적지가 천장이었다는
                // 것을 기억한 채로.
                //
                // 여기서 포기하고 배회로 돌아가면 이 선택지는 사실상 닿을 수 없다.
                // 펫은 바닥이나 창 위에서 가만히 있다가 뽑히는데, 가만히 있는 동안
                // 벽에 붙어 있는 것은 우연이기 때문이다. mac에서 실측한 결과가
                // 3분에 두 번 뽑혀 두 번 다 오를 것이 없었다.
                var ceilingArea = _screens!.WorkingAreaContaining(_body.Position);
                if (WindowSupport.HasWall(_body.Position, _body.VisualBounds,
                                          _windows.Windows, ceilingArea, UnclimbableWindows()))
                {
                    _controller.Request(StateKind.ClimbToCeiling);
                    break;
                }

                _pendingCeilingClimb = true;
                _walk.TargetX = (WindowSupport.NearestClimbTarget(
                    _body.Position, _windows.Windows,
                    roamableTop: ceilingArea.Top,
                    avatarHeight: _avatar.Size.Height,
                    excluding: UnclimbableWindows())
                    ?? WindowSupport.NearestScreenEdge(_body.Position, _body.VisualBounds, ceilingArea)).X;
                _controller.Request(StateKind.Walk);
                break;

            case WanderOutcome.WalkToRandomPoint:
                _walk.TargetX = null;   // WalkState가 알아서 뽑는다
                _controller.Request(StateKind.Walk);
                break;

            case WanderOutcome.Stay:
                break;
        }
    }

    /// 서 있던 자리가 사라진 게 아니라 창 뒤로 갔다. 여기서 떨어뜨리면
    /// 숨어 있던 펫이 사람이 지금 쓰는 창 한가운데로 나온다 — 그래서
    /// 그냥 둔다. 다음 배회 때 알아서 걸어 나오고, 그 창이 치워지면
    /// 평소 판정이 다시 돈다.
    public void LostFootingBehind(WindowInfo window)
    {
        AppLogger.Log(LogLevel.Debug, "movement", "발밑이 창 뒤로 갔습니다",
            new Dictionary<string, object?> { ["window"] = window.OwnerName });
    }

    /// 설정의 "포커스된 창 위로는 올라가지 않기". 사람이 지금 쓰고 있는 창
    /// 위로 펫이 기어오르면 방해가 된다 — 그 창은 벽이 아니라 없는 것처럼 지나친다.
    private ISet<IntPtr>? UnclimbableWindows()
    {
        if (!_settings.AvoidFocusedWindow || _windows is null) return null;

        var foreground = Interop.Win32.GetForegroundWindow();
        Interop.Win32.GetWindowThreadProcessId(foreground, out var pid);
        var focused = WindowSupport.FocusedWindow((int)pid, _windows.Windows);
        return focused is null ? null : new HashSet<IntPtr> { focused.Handle };
    }

    /// 곧장 아래로 떨어지면 무엇에 닿는가. Phase 1에서는 언제나 화면 바닥이었고,
    /// 이제 창 윗면이 그 사이에 끼어든다 — StateContext.LandingY가 클로저인 덕분에
    /// 상태 코드는 한 줄도 바뀌지 않는다.
    private double LandingY(Point point)
    {
        var floor = _screens!.FloorY(point);
        if (_windows is null || _avatar is null) return floor;

        return LandingSurfaceResolver.LandingY(
            point.X, point.Y, _windows.Windows, floor,
            // 그 화면 자신의 위쪽 끝이어야 한다. 경계 상자의 top을 넘기면
            // 주 모니터에서 최대화된 창(윗변 0)까지 발판으로 판정돼, 거기 선
            // 펫의 몸이 화면 위로 넘어가 보이지 않는다.
            roamableTop: _screens.CeilingY(point),
            avatarHeight: _avatar.Size.Height);
    }

    private void OnFrame(double dt)
    {
        if (_window is null || _avatar is null || _body is null || _controller is null) return;

        // 숨어 있는 동안은 아무것도 하지 않는다 — 안 보이는 채로 돌아다니다가
        // 다시 켰을 때 엉뚱한 곳에 서 있으면 "숨겼다 켰다"로 읽히지 않는다.
        if (!_window.IsVisible) return;

        // 디스플레이 구성이 바뀌었을 수 있다. 목록이 비면(전부 잠듦)
        // 마지막으로 알던 것을 그대로 쓴다.
        Volatile.Write(ref _screens, ScreenSpace.Current() ?? _screens);
        PutDownAfterDisplayChange();

        _pending.Expire(_stopwatch.Elapsed.TotalSeconds);
        ClimbToCeilingWhenAtAWall();
        FollowCursorWhileDragged();
        _controller.Advance(dt);
        _presenter?.Advance(dt, _body, _controller);
        _body.UpdateBounce(_avatar.CurrentClipKey, _stopwatch.Elapsed);

        // 버블이 떠 있으면 펫을 따라온다. 한 번 놓고 두면 펫이 걸어 나간
        // 자리에 빈 상자만 남는다.
        if (_bubble is { IsVisible: true }) _bubble.MoveTo(BubbleOrigin());

        _window.MoveTo(_body.Position, _body.VisualBounds);
        _window.UpdateClickThrough(_avatar);
    }

    /// 끌려가는 동안에는 큐를 거치지 않고 지금 커서를 직접 본다.
    ///
    /// 마우스는 `WH_MOUSE_LL` 훅으로 들어오는데, 훅 콜백은 빨리 끝나야
    /// 하므로 좌표만 읽어 `DispatcherPriority.Input`으로 UI 스레드에
    /// 넘긴다. 그 우선순위는 `Render`보다 **낮아서**, 바쁠 때는 방금 온
    /// 마우스 이동이 이번 프레임의 그리기 뒤로 밀린다. 그러면 펫이 잡은
    /// 손에서 한 프레임 이상 뒤처진다.
    ///
    /// puck-linux는 같은 문제를 반대쪽에서 풀었다 — 16ms 타이머를 기다리지
    /// 말고 드래그 콜백에서 창을 바로 옮기는 것이다(`b08443c`). 여기서
    /// 그렇게 하면 창 위치를 쓰는 주체가 둘이 되고, 프레임 루프가 끝마다
    /// 지키는 "펫은 실제 화면 위에 있다" 불변식을 건너뛴다. 늦게 읽는
    /// 쪽이 같은 지연을 없애면서 그 둘을 다 지킨다.
    ///
    /// 던질 때의 속도는 여전히 이동 이벤트가 잰다 — 그건 사람이 손을
    /// 어떻게 움직였는가의 기록이지 지금 어디인가가 아니다.
    private void FollowCursorWhileDragged()
    {
        if (_drag is null || _controller?.Current != StateKind.ReactDrag) return;
        if (_drag.DragPosition is null) return;   // 아직 첫 이동 전이다

        _drag.DragPosition = PetOverlayWindow.CursorPosition;
    }

    /// 배회가 하려던 것을 놓는다.
    ///
    /// 천장으로 가려던 걸음은 그 배회의 일부라, 펫을 가로채는 것은 그
    /// 예약도 같이 떨어뜨려야 한다. 안 그러면 펫이 다른 이유로 어딘가에
    /// 보내졌다가 도착해서 아무도 시키지 않은 벽을 오른다.
    private void CancelWander() => _pendingCeilingClimb = false;

    /// 천장을 노린 배회의 걸음이 벽에 닿았으면 거기서부터 끝까지 오르게 한다.
    ///
    /// 매 프레임 불리고 나머지 시간에는 아무것도 하지 않는다 — 도착을
    /// 알려 주는 것이 없어서 그 순간을 이쪽에서 알아채야 한다.
    ///
    /// 두 순간을 잡는다. 벽이 두 종류이기 때문이다. **창의** 옆면으로
    /// 걸어 들어가는 것은 WalkState가 가로채 닿는 프레임에 Climb으로
    /// 넘기므로, 거기서 펫이 가만히 서 있는 순간은 없다. **화면의**
    /// 가장자리로 걸어가는 것은 아무것도 막지 않아서 그냥 도착해 선다.
    private void ClimbToCeilingWhenAtAWall()
    {
        if (!_pendingCeilingClimb || _controller is null || _body is null || _windows is null) return;

        var arrived = _controller.Current == StateKind.Idle;
        var alreadyClimbing = _controller.Current == StateKind.Climb;
        if (!arrived && !alreadyClimbing) return;
        _pendingCeilingClimb = false;

        // 뽑을 때의 판단을 믿지 않고 여기서 다시 묻는다. 걸어가는 데 시간이
        // 걸리고, 그동안 겨눴던 창은 닫히거나 옮겨질 수 있다. 잡을 것이
        // 없으면 펫은 걸어온 자리에 그대로 선다 — 어차피 했을 일이다.
        var area = _screens!.WorkingAreaContaining(_body.Position);
        if (!WindowSupport.HasWall(_body.Position, _body.VisualBounds,
                                   _windows.Windows, area, UnclimbableWindows()))
            return;

        _controller.Request(StateKind.ClimbToCeiling);
    }

    /// 세계가 다시 재어졌으면 서 있던 펫을 새 바닥에 내려놓는다.
    ///
    /// 화면이 짧아진 쪽은 가두기가 알아서 하지만, 길어진 쪽은 아무 일도
    /// 일어나지 않아 펫이 옛 바닥이 있던 선 위에 남는다. 다음 프레임에
    /// 떨어지기는 하지만, 해상도를 바꿨다고 펫이 화면 절반을 낙하하는 것은
    /// 물리가 아니라 고장으로 읽힌다.
    private void PutDownAfterDisplayChange()
    {
        if (_screens is null || _body is null || _avatar is null || _controller is null) return;

        var area = _screens.RoamableArea;
        var previous = _lastRoamableArea;
        _lastRoamableArea = area;

        // 첫 프레임은 "바뀐" 것이 아니다 — 시작 위치를 덮어쓰면 안 된다.
        if (previous is not { } was || was == area) return;
        if (!DisplayChangeRelocation.StandsOnGround(_controller.Current)) return;

        _body.Position = DisplayChangeRelocation.Standing(
            _body.Position, _body.VisualBounds, area, LandingY);
    }

    /// 사람이 눌렀다. 그림 위를 눌렀을 때만 제스처가 시작된다 — 여백을
    /// 누른 것은 아래 앱에 가야 한다.
    private void OnMousePressed(Point cursor, double now)
    {
        if (_avatar is null) return;

        // 가리켜 둔 것을 눌렀다면 그건 펫을 잡는 게 아니라 지시를 따른 것이다.
        if (_pending.Accepts(cursor, now)) return;

        var relative = new Point(cursor.X - _avatar.Position.X, cursor.Y - _avatar.Position.Y);
        if (_avatar.HitTest(relative, PetOverlayWindow.HitTolerance))
            _gestures.OnMouseDown(cursor, now);
    }

    private void OnMouseReleased(Point cursor, double now) => _gestures.OnMouseUp(cursor, now);

    private void ToggleVisible()
    {
        if (_window is null) return;
        if (_window.IsVisible) _window.Hide(); else _window.Show();
    }

    private static void OpenCustomisationFolder()
    {
        PuckPaths.EnsureCreated();
        Process.Start(new ProcessStartInfo(PuckPaths.Root) { UseShellExecute = true });
    }

    private static AvatarEntry? BundledAvatar()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Resources", "Avatars", "dummy");
        return Directory.Exists(directory) ? new AvatarEntry("dummy", directory) : null;
    }

    public void Dispose()
    {
        _clock.Stop();
        _mouse?.Dispose();
        _sfx?.Dispose();
        _hotkeys?.Dispose();
        _bubble?.Close();
        _foreground?.Dispose();
        _windows?.Dispose();
        _tray?.Dispose();
        // 숨기지 않고 정말로 닫는다 — 남겨 두면 프로세스가 안 끝난다.
        _chat?.CloseForGood();
        _window?.Close();
    }
}
