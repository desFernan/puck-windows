using Puck.Localization;

namespace PuckTests.Localization;

public class StringsTests
{
    [Fact]
    public void KnownKeysResolveToKorean()
    {
        Assert.Equal("펫 보이기/숨기기", Strings.TrayToggleVisible);
        Assert.Equal("커스터마이징 폴더 열기", Strings.TrayOpenCustomisationFolder);
        Assert.Equal("아바타 다시 불러오기", Strings.TrayReloadAvatar);
        Assert.Equal("종료", Strings.TrayQuit);
    }

    [Fact]
    public void AnUnknownKeyReturnsTheKeyItselfRatherThanThrowing()
    {
        // 문자열 하나가 빠졌다고 UI가 죽으면 안 된다 — 키가 그대로 보이면
        // 무엇이 빠졌는지도 알 수 있다.
        Assert.Equal("no.such.key", Strings.Get("no.such.key"));
    }

    [Fact]
    public void EveryNamedPropertyHasAnEntry()
    {
        // 명명 속성이 늘어나는데 테이블에 넣는 걸 잊는 게 이 클래스의
        // 유일한 실패 방식이다.
        foreach (var property in typeof(Strings).GetProperties())
        {
            if (property.PropertyType != typeof(string)) continue;
            var value = (string)property.GetValue(null)!;
            Assert.False(LooksLikeAKey(value), $"{property.Name}이 테이블에 없습니다");
        }
    }

    /// 해결되지 못한 키는 키 자체로 돌아온다 — `settings.notchCaption`처럼
    /// 점이 든 ASCII 식별자다.
    ///
    /// "점이 있고 소문자와 같다"로는 모자랐다. 마침표로 끝나는 한국어
    /// 문장은 소문자 변환이 아무것도 바꾸지 않아 전부 키로 오인된다 —
    /// 설명문을 한 줄 넣을 때마다 이 테스트가 터졌다. 키에는 없고 문장에는
    /// 있는 것(공백, 한글)으로 가른다.
    private static bool LooksLikeAKey(string value)
        => value.Contains('.')
        && !value.Any(char.IsWhiteSpace)
        && value.All(char.IsAscii);
}
