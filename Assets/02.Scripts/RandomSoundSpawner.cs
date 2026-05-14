using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomSoundSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private int _maxConcurrent = 3;
    [SerializeField] private float _minSpawnInterval = 0.1f;
    [SerializeField] private float _maxSpawnInterval = 1.5f;
    [SerializeField] private bool _playOnStart = true;
    [Tooltip("한 시퀀스의 길이(초). 이 시간이 지나면 새 사운드 스폰을 멈추고 OnSequenceEnded 발화.")]
    [SerializeField] private float _sequenceDuration = 30f;

    [Header("Position (relative to listener)")]
    [SerializeField] private Transform _listener;
    [SerializeField] private float _minDistance = 1f;
    [SerializeField] private float _maxDistance = 10f;
    [Tooltip("True면 y >= 0 영역(상반구)에서만 위치 샘플링.")]
    [SerializeField] private bool _restrictToUpperHemisphere = false;

    [Header("AudioSource Prefab")]
    [Tooltip("Steam Audio Source 등 spatializer 컴포넌트가 설정된 AudioSource 프리팹. _maxConcurrent 만큼 풀로 인스턴스화됨.")]
    [SerializeField] private AudioSource _audioSourcePrefab;

    [Header("Library")]
    [SerializeField] private SoundEntry[] _library;

    [Header("Trajectory")]
    [SerializeField] private TrajectorySettings _trajectorySettings = new();

    private readonly List<AudioSource> _sources = new();
    private readonly List<SoundEntry> _playableEntries = new();
    private Coroutine _spawnLoop;

    private SoundTrajectory[] _slotTrajectories;
    private float[] _slotElapsed;
    private SpawnInfo?[] _slotInfos;

    private static readonly ETrajectoryType[] TrajectoryPool =
    {
        ETrajectoryType.Fixed,
        ETrajectoryType.CircularOrbit,
        ETrajectoryType.LinearSweep,
        ETrajectoryType.RandomWaypoints,
    };

    public struct SpawnInfo
    {
        public AudioSource Source;
        public ESoundEvent SoundEvent;
        public AudioClip Clip;
        public Vector3 WorldPosition;
        public Vector3 DirectionFromListener;
        public float Distance;
        public double DspStartTime;
    }

    public event System.Action<SpawnInfo> OnSoundSpawned;
    public event System.Action OnSequenceStarted;
    public event System.Action OnSequenceEnded;

    private float _sequenceStartTime;
    private float _sequenceFrozenElapsed;

    public bool IsSequenceRunning { get; private set; }
    public float SequenceDuration => _sequenceDuration;
    public float SequenceElapsed => IsSequenceRunning ? Time.time - _sequenceStartTime : _sequenceFrozenElapsed;

    void Awake()
    {
        if (_listener == null && Camera.main != null)
            _listener = Camera.main.transform;

        if (_audioSourcePrefab == null)
        {
            Debug.LogError($"{nameof(RandomSoundSpawner)}: AudioSource 프리팹이 지정되지 않았습니다.");
        }
        else
        {
            for (int i = 0; i < _maxConcurrent; i++)
            {
                var src = Instantiate(_audioSourcePrefab, transform);
                src.gameObject.name = $"SoundSlot_{i}";
                _sources.Add(src);
            }
        }

        int slotCount = _sources.Count;
        _slotTrajectories = new SoundTrajectory[slotCount];
        _slotElapsed = new float[slotCount];
        _slotInfos = new SpawnInfo?[slotCount];

        if (_library != null)
        {
            foreach (var entry in _library)
            {
                if (entry == null || entry.Clips == null) continue;
                bool hasClip = false;
                foreach (var c in entry.Clips) { if (c != null) { hasClip = true; break; } }
                if (hasClip) _playableEntries.Add(entry);
            }
        }
    }

    void Start()
    {
        if (_playOnStart) StartSpawning();
    }

    void Update()
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (!_slotInfos[i].HasValue) continue;

            if (!_sources[i].isPlaying)
            {
                _slotInfos[i] = null;
                _slotTrajectories[i] = null;
                _slotElapsed[i] = 0f;
                continue;
            }

            _slotElapsed[i] += Time.deltaTime;
            if (_slotTrajectories[i] != null)
                _sources[i].transform.position = _slotTrajectories[i].GetPosition(_slotElapsed[i]);
        }
    }

    public void StartSpawning()
    {
        if (_spawnLoop != null) return;
        if (_playableEntries.Count == 0)
        {
            Debug.LogWarning($"{nameof(RandomSoundSpawner)}: library에 재생 가능한 클립이 없습니다.");
            return;
        }
        _spawnLoop = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (_spawnLoop != null) StopCoroutine(_spawnLoop);
        _spawnLoop = null;
        if (IsSequenceRunning)
        {
            _sequenceFrozenElapsed = Time.time - _sequenceStartTime;
            IsSequenceRunning = false;
        }
    }

    /// <summary>
    /// 진행 중인 시퀀스를 멈추고(재생 중인 사운드도 정지) 새 시퀀스를 시작.
    /// </summary>
    public void RestartSequence()
    {
        StopSpawning();
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i] != null) _sources[i].Stop();
            _slotInfos[i] = null;
            _slotTrajectories[i] = null;
            _slotElapsed[i] = 0f;
        }
        StartSpawning();
    }

    /// <summary>
    /// 현재 재생 중인 모든 슬롯에 대해 콜백을 실행. LabelRecorder 등 외부 시스템용.
    /// </summary>
    public void ForEachActiveSlot(System.Action<SpawnInfo, Vector3> action)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_slotInfos[i].HasValue && _sources[i].isPlaying)
                action(_slotInfos[i].Value, _sources[i].transform.position);
        }
    }

    /// <summary>
    /// 자동 스폰 시퀀스의 1회분을 외부에서 트리거.
    /// </summary>
    public SpawnInfo? PlayRandom()
    {
        if (_playableEntries.Count == 0) return null;
        int slotIdx = GetFreeSlotIndex();
        if (slotIdx < 0) return null;
        var entry = _playableEntries[Random.Range(0, _playableEntries.Count)];
        return SpawnOn(_sources[slotIdx], slotIdx, entry);
    }

    private IEnumerator SpawnLoop()
    {
        IsSequenceRunning = true;
        _sequenceStartTime = Time.time;
        _sequenceFrozenElapsed = 0f;
        OnSequenceStarted?.Invoke();

        PlayRandom();

        while (Time.time - _sequenceStartTime < _sequenceDuration)
        {
            yield return new WaitForSeconds(Random.Range(_minSpawnInterval, _maxSpawnInterval));
            if (Time.time - _sequenceStartTime >= _sequenceDuration) break;
            PlayRandom();
        }

        _sequenceFrozenElapsed = Time.time - _sequenceStartTime;
        _spawnLoop = null;
        IsSequenceRunning = false;
        OnSequenceEnded?.Invoke();
    }

    private int GetFreeSlotIndex()
    {
        for (int i = 0; i < _sources.Count; i++)
            if (!_sources[i].isPlaying) return i;
        return -1;
    }

    private SpawnInfo? SpawnOn(AudioSource src, int slotIdx, SoundEntry entry)
    {
        AudioClip clip = PickClip(entry);
        if (clip == null) return null;

        Vector3 dir = _restrictToUpperHemisphere ? RandomUpperHemisphereDir() : Random.onUnitSphere;
        float dist = Random.Range(_minDistance, _maxDistance);
        Vector3 listenerPos = _listener != null ? _listener.position : Vector3.zero;
        Vector3 worldPos = listenerPos + dir * dist;

        src.transform.position = worldPos;
        src.clip = clip;
        src.Play();

        var info = new SpawnInfo
        {
            Source = src,
            SoundEvent = entry.SoundEvent,
            Clip = clip,
            WorldPosition = worldPos,
            DirectionFromListener = dir,
            Distance = dist,
            DspStartTime = AudioSettings.dspTime
        };

        ETrajectoryType type = TrajectoryPool[Random.Range(0, TrajectoryPool.Length)];
        _slotTrajectories[slotIdx] = SoundTrajectory.Create(type, worldPos, listenerPos, _trajectorySettings, _minDistance, _maxDistance);
        _slotElapsed[slotIdx] = 0f;
        _slotInfos[slotIdx] = info;

        OnSoundSpawned?.Invoke(info);

        return info;
    }

    private static AudioClip PickClip(SoundEntry entry)
    {
        int n = entry.Clips.Length;
        int start = Random.Range(0, n);
        for (int i = 0; i < n; i++)
        {
            var c = entry.Clips[(start + i) % n];
            if (c != null) return c;
        }
        return null;
    }

    private static Vector3 RandomUpperHemisphereDir()
    {
        Vector3 d = Random.onUnitSphere;
        d.y = Mathf.Abs(d.y);
        return d;
    }
}
