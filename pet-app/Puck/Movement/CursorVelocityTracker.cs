using System.Windows;

namespace Puck.Movement;

/// 던져진 속도를 재는 것. 마지막 한 쌍의 표본만 보면 손을 멈춘 채로
/// 놓는 흔한 동작이 속도 0으로 읽혀, 세게 휘두른 던지기가 제자리
/// 낙하가 된다. 최근 창(window) 전체에 걸쳐 재는 이유가 그것이다.
public sealed class CursorVelocityTracker
{
    /// 이보다 오래된 표본은 버린다. 짧게 잡으면 던지기가 마지막 순간의
    /// 잡음을 따라가고, 길게 잡으면 방향을 바꾼 손짓이 평균으로 상쇄된다.
    public const double WindowSeconds = 0.12;

    private readonly List<(Point Position, double Timestamp)> _samples = [];

    public void Record(Point position, double timestamp)
    {
        _samples.Add((position, timestamp));

        // 창보다 오래된 표본은 버리되 마지막 두 개는 남긴다. 천천히 끄는
        // 손짓은 표본 간격이 창보다 넓어서, 조건 없이 버리면 표본이 하나만
        // 남아 속도가 0으로 읽힌다 — 느린 던지기가 제자리 낙하가 된다.
        while (_samples.Count > 2 && timestamp - _samples[0].Timestamp > WindowSeconds)
            _samples.RemoveAt(0);
    }

    public Vector Velocity
    {
        get
        {
            if (_samples.Count < 2) return new Vector(0, 0);

            var first = _samples[0];
            var last = _samples[^1];
            var elapsed = last.Timestamp - first.Timestamp;
            if (elapsed <= 0) return new Vector(0, 0);

            return new Vector((last.Position.X - first.Position.X) / elapsed,
                              (last.Position.Y - first.Position.Y) / elapsed);
        }
    }

    public void Reset() => _samples.Clear();
}
