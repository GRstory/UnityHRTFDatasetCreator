using System;
using UnityEngine;

[Serializable]
public class TrajectorySettings
{
    [Header("Circular Orbit")]
    [Tooltip("궤도 속도 범위 (도/초)")]
    public float circularSpeedMinDeg = 20f;
    public float circularSpeedMaxDeg = 90f;

    [Header("Linear Sweep")]
    [Tooltip("이동 속도 범위 (m/초)")]
    public float linearSpeedMin = 0.5f;
    public float linearSpeedMax = 4f;

    [Header("Random Waypoints")]
    [Tooltip("이동 속도 범위 (m/초)")]
    public float waypointSpeedMin = 0.5f;
    public float waypointSpeedMax = 3f;
    [Tooltip("경유점 수 범위")]
    public int waypointCountMin = 2;
    public int waypointCountMax = 6;
    [Tooltip("경유점 리스너 기준 거리 범위 (m)")]
    public float waypointDistMin = 1f;
    public float waypointDistMax = 10f;
}

public abstract class SoundTrajectory
{
    public abstract Vector3 GetPosition(float elapsed);

    public static SoundTrajectory Create(ETrajectoryType type, Vector3 spawnWorldPos, Vector3 listenerWorldPos, TrajectorySettings s, float minDist, float maxDist)
    {
        return type switch
        {
            ETrajectoryType.CircularOrbit   => new CircularOrbitTrajectory(spawnWorldPos, listenerWorldPos, s),
            ETrajectoryType.LinearSweep     => new LinearSweepTrajectory(spawnWorldPos, listenerWorldPos, s, minDist, maxDist),
            ETrajectoryType.RandomWaypoints => new RandomWaypointTrajectory(spawnWorldPos, listenerWorldPos, s),
            _                               => new FixedTrajectory(spawnWorldPos),
        };
    }
}

public sealed class FixedTrajectory : SoundTrajectory
{
    readonly Vector3 _pos;
    public FixedTrajectory(Vector3 pos) => _pos = pos;
    public override Vector3 GetPosition(float elapsed) => _pos;
}

public sealed class CircularOrbitTrajectory : SoundTrajectory
{
    readonly Vector3 _center;
    readonly float _radius;
    readonly float _speedRad;
    readonly float _startAngle;
    readonly float _elevationRad;

    public CircularOrbitTrajectory(Vector3 spawnPos, Vector3 listenerPos, TrajectorySettings s)
    {
        _center = listenerPos;
        Vector3 offset = spawnPos - listenerPos;
        _radius = offset.magnitude;
        if (_radius < 0.001f) _radius = 1f;
        Vector3 horizontal = new Vector3(offset.x, 0f, offset.z);
        _startAngle = Mathf.Atan2(offset.x, offset.z);
        _elevationRad = Mathf.Atan2(offset.y, Mathf.Max(0.001f, horizontal.magnitude));
        float speedDeg = UnityEngine.Random.Range(s.circularSpeedMinDeg, s.circularSpeedMaxDeg);
        _speedRad = speedDeg * Mathf.Deg2Rad * (UnityEngine.Random.value < 0.5f ? 1f : -1f);
    }

    public override Vector3 GetPosition(float elapsed)
    {
        float angle = _startAngle + _speedRad * elapsed;
        float cosEl = Mathf.Cos(_elevationRad);
        return _center + new Vector3(
            _radius * Mathf.Sin(angle) * cosEl,
            _radius * Mathf.Sin(_elevationRad),
            _radius * Mathf.Cos(angle) * cosEl
        );
    }
}

public sealed class LinearSweepTrajectory : SoundTrajectory
{
    readonly Vector3 _start;
    readonly Vector3 _end;
    readonly float _duration;

    public LinearSweepTrajectory(Vector3 spawnPos, Vector3 listenerPos, TrajectorySettings s, float minDist, float maxDist)
    {
        _start = spawnPos;
        float targetDist = UnityEngine.Random.Range(minDist, maxDist);
        _end = listenerPos + UnityEngine.Random.onUnitSphere * targetDist;
        float speed = UnityEngine.Random.Range(s.linearSpeedMin, s.linearSpeedMax);
        _duration = Vector3.Distance(_start, _end) / Mathf.Max(0.001f, speed);
    }

    public override Vector3 GetPosition(float elapsed)
    {
        if (_duration <= 0f) return _end;
        return Vector3.Lerp(_start, _end, Mathf.Clamp01(elapsed / _duration));
    }
}

public sealed class RandomWaypointTrajectory : SoundTrajectory
{
    readonly Vector3[] _waypoints;
    readonly float[] _cumTime;

    public RandomWaypointTrajectory(Vector3 spawnPos, Vector3 listenerPos, TrajectorySettings s)
    {
        int count = UnityEngine.Random.Range(s.waypointCountMin, s.waypointCountMax + 1);
        _waypoints = new Vector3[count + 1];
        _cumTime = new float[count + 1];
        _waypoints[0] = spawnPos;
        _cumTime[0] = 0f;

        for (int i = 1; i <= count; i++)
        {
            float dist = UnityEngine.Random.Range(s.waypointDistMin, s.waypointDistMax);
            _waypoints[i] = listenerPos + UnityEngine.Random.onUnitSphere * dist;
            float segDist = Vector3.Distance(_waypoints[i - 1], _waypoints[i]);
            float speed = UnityEngine.Random.Range(s.waypointSpeedMin, s.waypointSpeedMax);
            _cumTime[i] = _cumTime[i - 1] + segDist / Mathf.Max(0.001f, speed);
        }
    }

    public override Vector3 GetPosition(float elapsed)
    {
        int last = _waypoints.Length - 1;
        if (elapsed >= _cumTime[last]) return _waypoints[last];

        for (int i = 1; i <= last; i++)
        {
            if (elapsed <= _cumTime[i])
            {
                float segDur = _cumTime[i] - _cumTime[i - 1];
                float t = segDur > 0f ? (elapsed - _cumTime[i - 1]) / segDur : 1f;
                return Vector3.Lerp(_waypoints[i - 1], _waypoints[i], t);
            }
        }
        return _waypoints[last];
    }
}
