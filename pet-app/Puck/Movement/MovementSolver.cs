using System.Windows;
using Puck.Avatar;

namespace Puck.Movement;

/// MoveTo/Walk/Fall의 순수 산술. 엔티티도 상태도 모른다 — 이 위의 상태들은
/// 언제 이걸 부를지만 정한다.
///
/// 좌표는 가상 화면 물리 픽셀, Y는 아래로. 그래서 중력은 Y에 더해진다.
public static class MovementSolver
{
    /// 기본 걷기 속도, px/sec.
    public const double WalkSpeed = 90;

    /// 기본 중력, px/sec². 창 윗면에서 떨어지는 짧은 낙하도 창 한 채
    /// 높이 안에서 제대로 속도가 붙을 만큼 높다.
    public const double Gravity = 2400;

    /// 낙하가 안착하는 속도, px/sec. 상한이 없으면 긴 낙하의 마지막
    /// 4분의 1을 한 프레임에 35px씩 움직여, 착지가 아니라 바닥으로
    /// 낚아채이는 것처럼 보인다.
    public const double TerminalVelocity = 1200;

    /// "도착"으로 치는 거리.
    public const double ArrivalRadius = 2;

    /// 던져질 수 있는 최고 속도, px/sec. 손목 스냅 한 번이면 커서는
    /// 초당 수천 px을 가고, 그대로 두면 눈이 따라가기 전에 펫이 화면
    /// 반대편으로 사라진다. 이 값은 디스플레이 하나를 약 0.5초에 가로지른다.
    public const double MaxThrowSpeed = 2500;

    /// 착지 후 수평 속도가 줄어드는 비율. 즉시 멈추는 대신 미끄러져
    /// 멈춰야 착지가 통통 튀는 것으로 읽힌다.
    public const double GroundFrictionRate = 3.0;

    public readonly record struct Step(Point Position, bool HasArrived);

    /// 낙하 한 프레임의 결과. 플랜의 이름은 FallStep이지만 같은 이름의
    /// 메서드가 아래에 있고, C#에서는 중첩 타입과 멤버가 이름을 나눠 쓸 수 없다.
    public readonly record struct FallResult(
        Point Position, double Velocity, bool HasLanded, bool TouchedFloor);

    /// `target`을 향한 등속 한 프레임.
    ///
    /// 이동 거리는 남은 거리로 잘린다: 자르지 않으면 빠른 속도나 긴 프레임에서
    /// 목표를 지나쳐 그 주위를 영원히 진동한다.
    public static Step StepToward(Point from, Point target, double dt,
                                  double speed = WalkSpeed, double arrivalRadius = ArrivalRadius)
    {
        var dx = target.X - from.X;
        var dy = target.Y - from.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance <= arrivalRadius) return new Step(from, true);

        var travel = speed * dt;
        if (travel >= distance) return new Step(target, true);

        // 정규화 — 대각선이 축 방향보다 1.41배 빨라지지 않게.
        return new Step(new Point(from.X + dx / distance * travel,
                                  from.Y + dy / distance * travel), false);
    }

    /// 방향은 유지한 채 크기만 `maxSpeed`로 자른다. 축별이 아니라 던진
    /// 방향을 따라 자르므로, 강한 대각선은 느려지되 휘지는 않는다.
    public static Point CappedThrow(Vector velocity, double maxSpeed = MaxThrowSpeed)
    {
        var speed = Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
        if (speed <= maxSpeed) return new Point(velocity.X, velocity.Y);

        var scale = maxSpeed / speed;
        return new Point(velocity.X * scale, velocity.Y * scale);
    }

    /// `target`으로 가려면 어느 쪽을 봐야 하는가. 순수 수직 이동이면 null —
    /// 벽을 타는 동안 뒤집히면 안 된다.
    public static AvatarFacing? FacingToward(Point from, Point target)
    {
        var dx = target.X - from.X;
        if (dx == 0) return null;
        return dx > 0 ? AvatarFacing.Right : AvatarFacing.Left;
    }

    /// 가속하는 자유낙하 한 프레임. `landingY`는 프레임이 지나쳤을 때
    /// 표면을 뚫고 내려가는 대신 그 위에 세운다.
    public static FallResult FallStep(Point from, double velocity, double dt, double landingY)
    {
        var next = Math.Min(velocity + Gravity * dt, TerminalVelocity);
        var y = from.Y + next * dt;

        if (y >= landingY)
            return new FallResult(new Point(from.X, landingY), next, HasLanded: true, TouchedFloor: true);

        return new FallResult(new Point(from.X, y), next, HasLanded: false, TouchedFloor: false);
    }

    /// 프레임률에 독립적인 지수 감쇠 — 60Hz에서 맞춘 감각이 144Hz에서도 같다.
    public static double ApplyGroundFriction(double horizontalSpeed, double dt)
        => horizontalSpeed * Math.Exp(-GroundFrictionRate * dt);
}
