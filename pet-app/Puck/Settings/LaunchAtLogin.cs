using System.IO;
using Microsoft.Win32;
using Puck.Diagnostics;

namespace Puck.Settings;

/// 로그인할 때 자동으로 시작하기.
///
/// mac은 `SMAppService`가 앱 번들 하나를 등록하지만, Windows에는 그런
/// 등록부가 없다. 실질적인 표준은 HKCU의 Run 키 하나이고, 그 값은 실행할
/// 명령줄 자체다 — 그래서 앱을 다른 폴더로 옮기면 값이 낡는다. 그 낡음을
/// 이쪽에서 고친다: 켜져 있는데 값이 지금 실행 파일과 다르면 다시 쓴다.
///
/// HKCU를 쓰는 이유는 이것이 사용자 한 사람의 선택이기 때문이다. HKLM은
/// 관리자 권한을 요구하고, 그 기계를 쓰는 모두에게 펫을 켠다.
public static class LaunchAtLogin
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Puck";

    /// 지금 실행 중인 exe. 단일 파일로 게시하면 `Assembly.Location`이 비므로
    /// 프로세스 쪽에 묻는다.
    private static string? ExecutablePath
    {
        get
        {
            var path = Environment.ProcessPath;
            return string.IsNullOrEmpty(path) ? null : path;
        }
    }

    /// 등록부에 적힌 대로. 설정 파일이 아니라 여기가 정본이다 — 사람이
    /// 작업 관리자의 시작 프로그램 탭에서 꺼 두었을 수 있고, 그러면 설정
    /// 파일 쪽이 틀린 것이다.
    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string;
            }
            catch (Exception ex)
            {
                AppLogger.Warning("settings", "시작 프로그램 등록을 읽지 못했습니다",
                    new Dictionary<string, object?> { ["error"] = ex.Message });
                return false;
            }
        }
    }

    /// 등록부를 원하는 상태로 맞춘다. 실패는 기록만 하고 삼킨다 — 자동
    /// 시작이 안 되는 것 때문에 앱이 뜨지 않으면 안 된다.
    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return;

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return;
            }

            if (ExecutablePath is not { } exe || !File.Exists(exe)) return;

            // 따옴표로 감싼다. Program Files처럼 공백이 든 경로를 그냥 두면
            // 셸이 첫 공백에서 잘라 엉뚱한 것을 실행하려 든다.
            var command = $"\"{exe}\"";
            if (key.GetValue(ValueName) as string == command) return;
            key.SetValue(ValueName, command, RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            AppLogger.Warning("settings", "시작 프로그램 등록을 쓰지 못했습니다",
                new Dictionary<string, object?> { ["error"] = ex.Message, ["enabled"] = enabled });
        }
    }

    /// 시작할 때 한 번. 설정이 켜져 있는데 실행 파일이 옮겨졌으면 값을
    /// 다시 쓰고, 꺼져 있으면 남아 있을지 모르는 값을 치운다.
    public static void Reconcile(bool enabled) => Apply(enabled);
}
