using UnityEngine;

[CreateAssetMenu(fileName = "SoundEntry", menuName = "HRTF Dataset/Sound Entry")]
public class SoundEntry : ScriptableObject
{
    public ESoundEvent SoundEvent;
    public AudioClip[] Clips;
}
