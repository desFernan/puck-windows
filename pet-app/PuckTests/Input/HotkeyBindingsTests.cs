using Puck.Input;

namespace PuckTests.Input;

public class HotkeyBindingsTests
{
    [Fact]
    public void TheDefaultsAreTheMacOnesTranslatedToWindowsKeys()
    {
        // mac의 Option은 Windows의 Alt, Cmd 자리는 Ctrl이 받는다.
        var d = HotkeyBindings.Defaults;
        Assert.Equal(new HotkeyBinding(0x20, HotkeyModifiers.Alt), d.PushToTalk);
        Assert.Equal(new HotkeyBinding(0x20, HotkeyModifiers.Alt | HotkeyModifiers.Shift), d.TextInput);
        Assert.Equal(new HotkeyBinding(0x20, HotkeyModifiers.Alt | HotkeyModifiers.Control), d.SummonPet);
    }

    [Fact]
    public void TheDefaultsDoNotFightEachOther()
    {
        Assert.Empty(HotkeyBindings.Defaults.Conflicts());
    }

    [Fact]
    public void NoDefaultUsesTheWindowsKey()
    {
        // Windows가 예약한 조합이 많다. 사람이 고른 것도 아닌데 충돌을 만들 이유가 없다.
        Assert.All(HotkeyBindings.Defaults.All,
            pair => Assert.False(pair.Binding.Modifiers.HasFlag(HotkeyModifiers.Windows)));
    }

    [Fact]
    public void TwoBindingsOnTheSameComboAreReportedAsAConflict()
    {
        var clashing = HotkeyBindings.Defaults with
        {
            TextInput = HotkeyBindings.Defaults.PushToTalk,
        };

        var conflict = Assert.Single(clashing.Conflicts());
        Assert.Contains("PushToTalk", new[] { conflict.Item1, conflict.Item2 });
        Assert.Contains("TextInput", new[] { conflict.Item1, conflict.Item2 });
    }

    [Fact]
    public void TheSameKeyWithDifferentModifiersIsNotAConflict()
    {
        // 기본값 자체가 Space 하나를 셋이 나눠 쓴다 — 보조키가 다르면 다른 키다.
        var spaces = HotkeyBindings.Defaults.All.Where(p => p.Binding.VirtualKey == 0x20).ToList();
        Assert.Equal(3, spaces.Count);
        Assert.Empty(HotkeyBindings.Defaults.Conflicts());
    }

    [Fact]
    public void EveryBindingIsListedForRegistration()
    {
        // 등록과 충돌 검사가 같은 목록을 봐야 한 쪽만 아는 핫키가 안 생긴다.
        Assert.Equal(5, HotkeyBindings.Defaults.All.Count);
    }
}
