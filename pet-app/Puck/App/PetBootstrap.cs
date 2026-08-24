using System.Diagnostics;
using System.IO;
using System.Windows;
using Puck.Avatar;
using Puck.Diagnostics;
using Puck.Input;
using Puck.Localization;
using Puck.Movement;
using Puck.Movement.States;
using Puck.Overlay;
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
    private ReactDragState? _drag;
    private TrayIcon? _tray;
    private WindowListWatcher? _windows;
    private WalkState? _walk;
    private MoveToState? _moveTo;
    private GlobalHotkeyManager? _hotkeys;
    private TextInputBubbleWindow? _bubble;
    private Puck.Interop.WinEventHook? _foreground;
    private bool _wasPressed;

    public PetBootstrap(SettingsStore settings) => _settings = settings;

    public void Start()
    {
        PuckPaths.EnsureCreated();
        AppLogger.Configure(new JsonLinesFileAppender(PuckPaths.Logs));

        _tray = new TrayIcon(
            onToggleVisible: ToggleVisible,
            onOpenCustomisationFolder: OpenCustomisationFolder,
            onReloadAvatar: ReloadAvatar,
            onQuit: () => Application.Current.Shutdown());

        // 창을 먼저 보기 시작한다 — 첫 프레임에 이미 목록이 있어야 펫이
        // 바닥에서 시작했다가 창 위로 튀어 오르지 않는다.
        _windows = WindowListWatcher.CreateDefault();
        _windows.Start();
        _foreground = new Puck.Interop.WinEventHook(() => _windows?.NoteForegroundChanged());

        _window = new PetOverlayWindow();
        _window.Show();

        ReloadAvatar();

        _gestures.Clicked += () => _controller?.Request(StateKind.ReactClick);
        _gestures.Dragged += position =>
        {
            if (_drag is null) return;
            _drag.DragPosition = position;
            _controller?.Request(StateKind.ReactDrag);
        };
        _gestures.Released += velocity =>
        {
            if (_body is null) return;
            _body.LaunchVelocity = velocity;
            _controller?.Request(StateKind.Fall);
        };

        RegisterHotkeys();

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

        _screens ??= ScreenSpace.Current();
        if (_screens is null) return;

        var start = _body?.Position ?? StartPosition(_screens);

        _avatar = avatar;
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
            [StateKind.MoveTo] = _moveTo,
            [StateKind.WalkOnTop] = new WalkOnTopState(),
            [StateKind.ReactClick] = new ReactClickState(),
            [StateKind.ReactDrag] = _drag,
        };

        _controller = new CharacterController(_body, states, StateKind.Idle, MakeContext);
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
        Windows = _windows?.Windows ?? [],
        UnclimbableWindows = UnclimbableWindows(),
        RequestTransition = _ => { },   // CharacterController가 자기 것으로 갈아 끼운다
    };

    /// 전역 핫키. 지금은 "펫 부르기"만 물린다 — 음성(PTT)과 입력 버블은
    /// 각각 Phase 6과 이 Phase의 뒤쪽 태스크가 채운다.
    private void RegisterHotkeys()
    {
        _hotkeys = new GlobalHotkeyManager();
        _hotkeys.RegisterAll(HotkeyBindings.Defaults, new Dictionary<string, Action>
        {
            [nameof(HotkeyBindings.SummonPet)] = SummonToCursor,
            [nameof(HotkeyBindings.TextInput)] = ShowInputBubble,
        });

        if (_hotkeys.Unavailable.Count > 0)
            AppLogger.Warning("hotkey", "다른 프로그램이 이미 쓰고 있어 등록하지 못한 핫키가 있습니다",
                new Dictionary<string, object?> { ["names"] = string.Join(", ", _hotkeys.Unavailable) });
    }

    /// 펫 옆에 입력 버블을 띄운다. 받은 문장은 Phase 3의 에이전트가 가져간다 —
    /// 지금은 로그에만 남긴다.
    private void ShowInputBubble()
    {
        if (_body is null || _avatar is null || _screens is null) return;

        if (_bubble is null)
        {
            _bubble = new TextInputBubbleWindow();
            _bubble.Submitted += text =>
                AppLogger.Log(LogLevel.Info, "input", "입력 버블에서 문장을 받았습니다",
                    new Dictionary<string, object?> { ["text"] = text });
        }

        _bubble.ShowAt(BubbleOrigin());
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

        _moveTo.Target = perch ?? new Point(cursor.X, LandingY(cursor));
        _controller.Request(StateKind.MoveTo);
    }

    /// 배회 타이머가 찼다. 창 목록을 아는 건 여기뿐이라 "저 창을 타고 오르자"는
    /// 결정이 여기서 나온다.
    public void WanderRequested(WanderOutcome outcome)
    {
        if (_body is null || _controller is null || _walk is null) return;

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
        _screens = ScreenSpace.Current() ?? _screens;

        PollMouse();
        _controller.Advance(dt);
        _body.UpdateBounce(_avatar.CurrentClipKey, _stopwatch.Elapsed);

        // 버블이 떠 있으면 펫을 따라온다. 한 번 놓고 두면 펫이 걸어 나간
        // 자리에 빈 상자만 남는다.
        if (_bubble is { IsVisible: true }) _bubble.MoveTo(BubbleOrigin());

        _window.MoveTo(_body.Position, _body.VisualBounds);
        _window.UpdateClickThrough(_avatar);
    }

    /// 오버레이는 대부분의 시간 WS_EX_TRANSPARENT 상태라 마우스 이벤트를
    /// 받지 못한다. 그래서 커서와 버튼 상태를 프레임마다 직접 묻는다.
    private void PollMouse()
    {
        var cursor = PetOverlayWindow.CursorPosition;
        var pressed = PetOverlayWindow.LeftButtonDown;
        var now = _stopwatch.Elapsed.TotalSeconds;

        if (pressed && !_wasPressed)
        {
            // 그림 위를 눌렀을 때만 제스처가 시작된다.
            var relative = new Point(cursor.X - _avatar!.Position.X, cursor.Y - _avatar.Position.Y);
            if (_avatar.HitTest(relative, PetOverlayWindow.HitTolerance))
                _gestures.OnMouseDown(cursor, now);
        }
        else if (pressed)
        {
            _gestures.OnMouseMove(cursor, now);
        }
        else if (_wasPressed)
        {
            _gestures.OnMouseUp(cursor, now);
        }

        _wasPressed = pressed;
    }

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
        _hotkeys?.Dispose();
        _bubble?.Close();
        _foreground?.Dispose();
        _windows?.Dispose();
        _tray?.Dispose();
        _window?.Close();
    }
}
