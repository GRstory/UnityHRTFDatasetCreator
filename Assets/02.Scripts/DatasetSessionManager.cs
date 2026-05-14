using System.IO;
using UnityEngine;

public class DatasetSessionManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RandomSoundSpawner _spawner;
    [SerializeField] private AudioCapture _audioCapture;
    [SerializeField] private LabelRecorder _labelRecorder;

    [Header("HRTF")]
    [SerializeField] private EHRTFLogic _hrtfLogic = EHRTFLogic.SteamAudio;

    [Header("Output")]
    [Tooltip("Application.persistentDataPath 기준 상대 경로")]
    [SerializeField] private string _outputFolder = "Dataset";

    private int _sessionCount;
    private string _currentWavPath;
    private string _currentCsvPath;

    public bool IsSessionActive { get; private set; }
    public int SessionCount => _sessionCount;

    void OnEnable()
    {
        if (_spawner != null) _spawner.OnSequenceEnded += OnSequenceEnded;
    }

    void OnDisable()
    {
        if (_spawner != null) _spawner.OnSequenceEnded -= OnSequenceEnded;
    }

    public void StartSession()
    {
        if (IsSessionActive)
        {
            Debug.LogWarning($"[DatasetSession] 이미 세션이 진행 중입니다.");
            return;
        }

        _sessionCount++;
        string hrtfName = _hrtfLogic.ToString();
        string fileName = $"{hrtfName}_{_sessionCount:D4}";
        string root = Path.Combine(Application.persistentDataPath, _outputFolder);
        string audioDir = Path.Combine(root, "Audio");
        string labelDir = Path.Combine(root, "Label");
        Directory.CreateDirectory(audioDir);
        Directory.CreateDirectory(labelDir);

        _currentWavPath = Path.Combine(audioDir, fileName + ".wav");
        _currentCsvPath = Path.Combine(labelDir,  fileName + ".csv");

        _audioCapture.StartRecording();
        _labelRecorder.StartRecording(hrtfName);
        _spawner.RestartSequence();
        IsSessionActive = true;

        Debug.Log($"[DatasetSession #{_sessionCount}] 시작: {fileName}");
    }

    public void StopSession()
    {
        if (!IsSessionActive) return;

        _spawner.StopSpawning();
        string wavPath = _audioCapture.StopRecording(_currentWavPath);
        string csvPath = _labelRecorder.StopRecording(_currentCsvPath);
        IsSessionActive = false;

        Debug.Log($"[DatasetSession #{_sessionCount}] 저장 완료:\n  WAV: {wavPath}\n  CSV: {csvPath}");
    }

    private void OnSequenceEnded()
    {
        if (IsSessionActive) StopSession();
    }
}
