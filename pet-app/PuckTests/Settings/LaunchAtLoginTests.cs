using Puck.Settings;

namespace PuckTests.Settings;

/// 작업 관리자의 "시작 앱" 탭이 남기는 표시를 읽는 규칙. 등록부 없이
/// 시험할 수 있게 순수 함수로 떼어 둔 자리다.
public class LaunchAtLoginTests
{
    [Fact]
    public void NoMarkMeansNobodyHasTouchedIt()
    {
        // 한 번도 만진 적 없는 항목에는 이 값이 생기지 않는다.
        Assert.True(LaunchAtLogin.ApprovedByWindows(null));
        Assert.True(LaunchAtLogin.ApprovedByWindows([]));
    }

    [Fact]
    public void TheMarkWindowsLeavesWhenItIsOnReadsAsOn()
    {
        Assert.True(LaunchAtLogin.ApprovedByWindows([0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void TheMarkWindowsLeavesWhenSomebodyTurnedItOffReadsAsOff()
    {
        // Run 값은 그대로 남아 있다. 그것만 보면 꺼 둔 것을 켜졌다고 읽는다.
        Assert.False(LaunchAtLogin.ApprovedByWindows([0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));
    }

    [Theory]
    [InlineData(0x06)]
    [InlineData(0x00)]
    public void AnyEvenFirstByteIsOn(byte first)
    {
        // 표시는 여러 값을 쓴다. 정하는 것은 최하위 비트 하나다.
        Assert.True(LaunchAtLogin.ApprovedByWindows([first, 0, 0, 0]));
    }

    [Theory]
    [InlineData(0x07)]
    [InlineData(0x01)]
    public void AnyOddFirstByteIsOff(byte first)
    {
        Assert.False(LaunchAtLogin.ApprovedByWindows([first, 0, 0, 0]));
    }
}
