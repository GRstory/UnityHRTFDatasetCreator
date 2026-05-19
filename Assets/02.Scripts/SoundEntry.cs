using UnityEngine;

[CreateAssetMenu(fileName = "SoundEntry", menuName = "HRTF Dataset/Sound Entry")]
public class SoundEntry : ScriptableObject
{
    public ESoundEvent SoundEvent;
    public AudioClip[] Clips;

    [Header("Movement Speed")]
    [Tooltip("Trajectory 속도 배수. 1.0 = 기본, 3.0 = 차량급 빠른 이동")]
    public float SpeedMultiplier = 1f;

    [Header("Repeat Mode")]
    [Tooltip("짧은 단일 클립을 반복 재생하는 모드 (발소리, 총소리 등)")]
    public bool UseRepeatMode;
    [Tooltip("반복 재생 지속 시간 (초)")]
    public float RepeatDuration = 5f;
    [Tooltip("반복 간 최소 인터벌 (초)")]
    public float MinRepeatInterval = 0.3f;
    [Tooltip("반복 간 최대 인터벌 (초)")]
    public float MaxRepeatInterval = 0.8f;
}
