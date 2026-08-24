using System.Diagnostics;
using System.IO;
using System.Windows;
using Puck.Avatar;
using Puck.Diagnostics;
using Puck.Localization;
using Puck.Movement;
using Puck.Movement.States;
using Puck.Overlay;
using Puck.Settings;
using Puck.WindowSensing;

namespace Puck.App;

/// 전부를 엮는 한 곳. 여기서 아바타를 고르고, 창을 띄우고, 프레임
/// 루프를 돌리고, 제스처를 상태 전이로 옮긴다.
public sealed class PetBootstrap : IDisposable
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
        var states = new Dictionary<StateKind, IStateHandler>
        {
            [StateKind.Idle] = new IdleState(new WanderScheduler()),
            [StateKind.Walk] = new WalkState { Ledge = ledge },
            [StateKind.Fall] = new FallState(),
            [StateKind.Land] = new LandState(),
            [StateKind.ClimbLedge] = ledge,
            [StateKind.Climb] = new ClimbState(),
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
        _foreground?.Dispose();
        _windows?.Dispose();
        _tray?.Dispose();
        _window?.Close();
    }
}
