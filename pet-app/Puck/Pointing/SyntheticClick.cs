using System.Runtime.InteropServices;
using System.Windows;
using Puck.Diagnostics;
using Puck.Interop;

namespace Puck.Pointing;

/// 사람 대신 눌러 준다. mac의 `CGEventPost` 자리를 `SendInput`이 대신한다.
///
/// **UIPI:** 관리자 권한으로 실행된 창에는 이 입력이 가지 않는다. 실패도
/// 조용하다 — 눌렀는데 아무 일도 안 일어난 것과 구분되지 않으므로, 도구
/// 응답이 그 가능성을 말해 줘야 한다.
public static class SyntheticClick
{
    /// 절대 좌표는 0..65535로 정규화해야 하고, 그 기준은 **가상 화면 전체**다.
    /// 주 모니터 기준으로 계산하면 보조 모니터에서 엉뚱한 곳을 누른다.
    public static (int X, int Y) Normalise(Point point, Rect virtualScreen)
    {
        if (virtualScreen.Width <= 0 || virtualScreen.Height <= 0) return (0, 0);

        var x = (point.X - virtualScreen.Left) / virtualScreen.Width * 65535.0;
        var y = (point.Y - virtualScreen.Top) / virtualScreen.Height * 65535.0;

        return ((int)Math.Round(Math.Clamp(x, 0, 65535)),
                (int)Math.Round(Math.Clamp(y, 0, 65535)));
    }

    /// `point`(가상 화면 물리 픽셀)에 왼쪽 클릭 한 번.
    /// 승인은 에이전트가 하는 일이고, 여기는 시키는 대로 누르기만 한다.
    public static bool Click(Point point, Rect virtualScreen)
    {
        var (nx, ny) = Normalise(point, virtualScreen);

        const uint moveAbsolute = Win32.MOUSEEVENTF_MOVE | Win32.MOUSEEVENTF_ABSOLUTE |
                                  Win32.MOUSEEVENTF_VIRTUALDESK;

        var inputs = new[]
        {
            MouseInput(nx, ny, moveAbsolute),
            MouseInput(nx, ny, moveAbsolute | Win32.MOUSEEVENTF_LEFTDOWN),
            MouseInput(nx, ny, moveAbsolute | Win32.MOUSEEVENTF_LEFTUP),
        };

        var sent = Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
        if (sent == inputs.Length) return true;

        AppLogger.Warning("pointing", "합성 클릭이 전달되지 않았습니다",
            new Dictionary<string, object?>
            {
                ["x"] = (int)point.X,
                ["y"] = (int)point.Y,
                ["sent"] = sent,
                ["hint"] = "권한이 더 높은 창이면 입력이 차단됩니다(UIPI)",
            });
        return false;
    }

    private static Win32.INPUT MouseInput(int x, int y, uint flags) => new()
    {
        type = Win32.INPUT_MOUSE,
        u = new Win32.InputUnion
        {
            mi = new Win32.MOUSEINPUT { dx = x, dy = y, mouseData = 0, dwFlags = flags, time = 0, dwExtraInfo = IntPtr.Zero },
        },
    };
}
