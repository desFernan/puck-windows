namespace Puck.Input;

/// RegisterHotKey의 MOD_* 플래그. mac의 CGEventFlags 자리다.
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
}

/// 키 하나 + 보조키 조합. `VirtualKey`는 Win32 가상 키 코드다(0x20 = Space).
public readonly record struct HotkeyBinding(uint VirtualKey, HotkeyModifiers Modifiers);

/// 설정 가능한 핫키들. 충돌 검사가 있는 이유는 사람이 다시 지정할 수 있기
/// 때문이다 — 원본 기획서도 키 입력 UI에 "충돌 검사"를 요구한다.
public sealed record HotkeyBindings
{
    /// Space.
    private const uint VkSpace = 0x20;
    private const uint Vk1 = 0x31;
    private const uint Vk2 = 0x32;

    public required HotkeyBinding PushToTalk { get; init; }
    public required HotkeyBinding TextInput { get; init; }
    public required HotkeyBinding SummonPet { get; init; }
    public required HotkeyBinding SummonToy1 { get; init; }
    public required HotkeyBinding SummonToy2 { get; init; }

    /// mac의 기본값을 Windows 키로 옮긴 것. mac의 Option은 Windows의 Alt이고,
    /// Cmd 자리는 Ctrl이 받는다.
    ///
    /// Win 키는 쓰지 않는다 — Windows가 예약한 조합이 많아서, 사람이 고른
    /// 것이 아니라 우리가 고른 것으로 충돌을 만들 이유가 없다.
    public static HotkeyBindings Defaults { get; } = new()
    {
        PushToTalk = new(VkSpace, HotkeyModifiers.Alt),
        TextInput = new(VkSpace, HotkeyModifiers.Alt | HotkeyModifiers.Shift),
        SummonPet = new(VkSpace, HotkeyModifiers.Alt | HotkeyModifiers.Control),
        SummonToy1 = new(Vk1, HotkeyModifiers.Alt | HotkeyModifiers.Shift),
        SummonToy2 = new(Vk2, HotkeyModifiers.Alt | HotkeyModifiers.Shift),
    };

    /// 이름과 바인딩 쌍. 등록과 충돌 검사가 같은 목록을 본다.
    public IReadOnlyList<(string Name, HotkeyBinding Binding)> All => new[]
    {
        (nameof(PushToTalk), PushToTalk),
        (nameof(TextInput), TextInput),
        (nameof(SummonPet), SummonPet),
        (nameof(SummonToy1), SummonToy1),
        (nameof(SummonToy2), SummonToy2),
    };

    /// 같은 키 + 같은 보조키를 쓰는 쌍들.
    public IReadOnlyList<(string, string)> Conflicts()
    {
        var all = All;
        var found = new List<(string, string)>();

        for (var i = 0; i < all.Count; i++)
            for (var j = i + 1; j < all.Count; j++)
                if (all[i].Binding == all[j].Binding)
                    found.Add((all[i].Name, all[j].Name));

        return found;
    }
}
