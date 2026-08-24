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
            Assert.False(value.Contains('.') && value == value.ToLowerInvariant(),
                $"{property.Name}이 테이블에 없습니다");
        }
    }
}
