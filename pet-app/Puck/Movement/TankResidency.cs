using System.Windows;

namespace Puck.Movement;

/// 펫의 수조가 어디 있고, 그것이 그렇게 말했을 때 얼마나 컸으며, 그 안의
/// 펫이 얼마나 커야 하는가.
///
/// 흩어 두면 서로 어긋나는 다섯 가지가 하나의 생각이다: 펫은 바탕화면에
/// 나와 있거나 어딘가의 유리 상자 안에 있고, 그 상자의 크기가 안에 있는
/// 동안의 펫 크기를 정한다.
public sealed class TankResidency
{
    /// 섬 위에 선 펫의 키. 섬은 누가 보든 같은 높이라, 비율이 아니라 고정
    /// 값이다 — 비율로 두면 설정에 따라 펫이 섬을 가득 채우거나 그 안에서
    /// 달그락거린다.
    public const double DefaultPetHeight = 56;

    /// 갈 수 있는 수조. 없으면 null.
    public Rect? Area { get; private set; }

    /// 수조가 마지막으로 보고한 크기. 펫이 그 안에서 얼마나 커야 하는지를
    /// 여기서 계산하므로, 영역보다 먼저 기억된다.
    public Size? LastReportedSize { get; private set; }

    public double PetHeight { get; set; } = DefaultPetHeight;

    /// 펫이 지금 수조 안에 사는가.
    public bool IsHome { get; set; }

    /// 창이 보고한 것을 받아 둔다. 담을 수 없는 수조는 **거절한다** —
    /// 좁은 틈에 끼워 넣은 펫은 펫이 아니라 버그로 읽힌다.
    public void Report(Rect? reported, Rect screen, Size petSize)
    {
        if (reported is not { } rect)
        {
            Area = null;
            LastReportedSize = null;
            return;
        }

        LastReportedSize = rect.Size;
        Area = PetTankArea.RoamableArea(rect, screen, petSize);
    }

    /// 펫을 `PetHeight`로 만드는 배율 — 수조가 그만큼 담지 못하면 담기는 만큼.
    ///
    /// 아바타가 아직 없으면 1이다.
    public double ScaleFor(Size desktopSize)
    {
        if (desktopSize.Height <= 0) return 1;

        if (LastReportedSize is not { } tank || desktopSize.Width <= 0)
            return PetHeight / desktopSize.Height;

        var fitted = PetTankArea.FittedPetHeight(
            PetHeight, tank, desktopSize.Width / desktopSize.Height);

        return fitted / desktopSize.Height;
    }

    /// 수조 안에서 펫이 설 자리. 바닥 한가운데다.
    public Point? StandingPoint()
        => Area is { } area ? new Point(area.Left + area.Width / 2, area.Bottom) : null;
}
