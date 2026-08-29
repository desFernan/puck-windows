using System.Windows;

namespace Puck.Movement;

/// 펫을 화면 안에 두고, 가장자리에서 튕긴다.
///
/// 규칙이 여기 사는 이유는 "화면 안"의 정의가 걷기·떨어지기·던져지기에
/// 대해 하나여야 하기 때문이다. 그리고 그 정의는 펫의 위치(발밑 한 점,
/// 그림의 가로 중심)가 아니라 펫 자신의 외곽선으로 쓰여 있다.
public static class PetBounds
{
    /// 튕긴 뒤 남는 속도의 비율. 완전 탄성이면 펫이 영원히 벽 사이를
    /// 오가는 것으로 읽힌다.
    public const double Restitution = 0.55;

    /// 이 아래면 튕길 값어치가 없다 — 점점 작아지는 도약으로 가장자리에서
    /// 떨리기만 한다.
    public const double MinimumBounceSpeed = 60;

    /// 착지는 벽 충돌보다 에너지를 더 잃는다. 고무공이 벽에서 튀는 게
    /// 아니라 바닥에 털썩 떨어지는 것 — 짧게 두어 번 쿵, 긴 랠리가 아니라.
    public const double LandingRestitution = 0.35;

    /// 이 아래면 그냥 바닥에 눕는다.
    public const double MinimumLandingBounceSpeed = 100;

    public readonly record struct Bounce(Point Position, double Velocity);

    /// 펫이 영역보다 넓으면 맞는 위치가 존재하지 않아 Contain/BounceHorizontally가
    /// 입력과 무관하게 언제나 왼쪽 한계로 고정한다. 자기 위치와 그 결과를
    /// 비교해 튕김을 감지하는 호출자는 매 프레임 불일치를 보고 영원히
    /// 진동하게 되므로, 그런 호출자는 이걸 먼저 물어야 한다.
    public static bool IsOversizedHorizontally(Rect visualBounds, Rect area)
        => (area.Left - visualBounds.Left) > (area.Right - visualBounds.Right);

    /// 속도를 개입시키지 않고 `visualBounds`가 `area` 안에 있도록 가둔다.
    /// 스스로 움직이는 상태(걷기, 오르기)가 화면을 벗어나지만 않게 할 때.
    public static Point Contain(Point position, Rect visualBounds, Rect area)
    {
        // 외곽선이 접지점에서 얼마나 벗어나 있는가. 좌우 대칭 그림이면
        // -너비/2와 +너비/2지만, 대칭을 요구하지는 않는다.
        var leftLimit = area.Left - visualBounds.Left;
        var rightLimit = area.Right - visualBounds.Right;
        if (leftLimit > rightLimit) return new Point(leftLimit, position.Y);

        return new Point(Math.Clamp(position.X, leftLimit, rightLimit), position.Y);
    }

    public static Bounce BounceHorizontally(Point position, double velocity, Rect visualBounds, Rect area)
    {
        var leftLimit = area.Left - visualBounds.Left;
        var rightLimit = area.Right - visualBounds.Right;
        if (leftLimit > rightLimit) return new Bounce(new Point(leftLimit, position.Y), 0);

        double limit;
        if (position.X < leftLimit && velocity < 0) limit = leftLimit;
        else if (position.X > rightLimit && velocity > 0) limit = rightLimit;
        else return new Bounce(position, velocity);

        var (coordinate, reflected) = Reflect(position.X, limit, velocity);
        return new Bounce(new Point(coordinate, position.Y), reflected);
    }

    /// 위로 가는 움직임만 다룬다: 내려오는 건 착지이고, 어느 면에
    /// 내려앉는지는 착지면 판정(창 윗면, 화면 바닥)의 일이다.
    public static Bounce BounceOffCeiling(Point position, double velocity, Rect visualBounds, Rect area)
    {
        // Y는 아래로 증가하므로 위쪽 속도는 음수이고, 펫의 머리는
        // position.Y + visualBounds.Top(음수 오프셋)에 있다.
        var topLimit = area.Top - visualBounds.Top;
        if (velocity >= 0 || position.Y >= topLimit) return new Bounce(position, velocity);

        var (coordinate, reflected) = Reflect(position.Y, topLimit, velocity);
        return new Bounce(new Point(position.X, coordinate), reflected);
    }

    /// 착지면에 멈춰 서는 대신 튕긴다. `floorY`는 고정된 영역 가장자리가
    /// 아니라 그때그때의 착지면이다.
    public static Bounce BounceOffFloor(Point position, double velocity, double floorY)
    {
        if (velocity <= 0 || position.Y < floorY) return new Bounce(position, velocity);

        var (coordinate, reflected) = Reflect(
            position.Y, floorY, velocity, LandingRestitution, MinimumLandingBounceSpeed);
        return new Bounce(new Point(position.X, coordinate), reflected);
    }

    /// 지금 가장자리에 닿고 있는 한 축의 튕김. 산술이 1차원이라 좌/우/상/하
    /// 특수 케이스가 여기 없고, 호출자가 Point를 소유한다.
    ///
    /// 속도 0을 돌려주면 튕길 에너지가 다해 가장자리에 정지했다는 뜻이다.
    public static (double Coordinate, double Velocity) Reflect(
        double coordinate, double limit, double velocity,
        double restitution = Restitution, double minimumBounceSpeed = MinimumBounceSpeed)
    {
        var speed = Math.Abs(velocity) * restitution;
        if (speed < minimumBounceSpeed)
            // 에너지 소진: 가장자리에서 떨지 말고 붙어서 쉰다.
            return (limit, 0);

        // 가장자리에 대해 반사 — 깊이 지나친 프레임은 들어간 만큼 되나온다.
        // 한계에 딱 세우면 움직임의 일부를 삼켜 빠른 튕김이 눈에 띄게
        // 거리를 잃는다.
        return (2 * limit - coordinate, velocity < 0 ? speed : -speed);
    }
}
