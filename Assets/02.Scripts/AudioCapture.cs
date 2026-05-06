using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[RequireComponent(typeof(AudioListener))]
public class AudioCapture : MonoBehaviour
{
    [Header("Save")]
    [Tooltip("저장 폴더(Application.persistentDataPath 기준 상대 경로). StopRecording에 절대경로 넘기면 무시됨.")]
    [SerializeField] private string _saveFolder = "Captures";

    [Header("Buffer")]
    [Tooltip("초기 버퍼 사이즈(초). 초과 시 자동 확장됨.")]
    [SerializeField] private int _initialBufferSeconds = 30;

    private readonly object _bufferLock = new();
    private List<float> _buffer;
    private int _recordedChannels = 2;
    private int _sampleRate;
    private bool _isRecording;

    public bool IsRecording => _isRecording;
    public int SampleRate => _sampleRate;
    public int Channels => _recordedChannels;

    /// <summary>
    /// 마지막 StopRecording이 저장한 WAV 파일 경로.
    /// </summary>
    public string LastSavedPath { get; private set; }

    void Awake()
    {
        _sampleRate = AudioSettings.outputSampleRate;
        _buffer = new List<float>(Mathf.Max(1, _sampleRate * 2 * _initialBufferSeconds));
    }

    /// <summary>
    /// 마스터 출력(Spatializer 적용 후 binaural) 캡처 시작. 기존 버퍼는 비워짐.
    /// </summary>
    public void StartRecording()
    {
        lock (_bufferLock)
        {
            _buffer.Clear();
            _isRecording = true;
        }
    }

    /// <summary>
    /// 캡처 종료 후 16-bit PCM WAV로 저장. filePath가 null이면 saveFolder/타임스탬프.wav로 저장.
    /// </summary>
    public string StopRecording(string filePath = null)
    {
        float[] samples;
        int channels;
        int rate;
        lock (_bufferLock)
        {
            if (!_isRecording && _buffer.Count == 0)
            {
                Debug.LogWarning($"{nameof(AudioCapture)}: 녹음된 샘플이 없습니다.");
                return null;
            }
            _isRecording = false;
            samples = _buffer.ToArray();
            _buffer.Clear();
            channels = _recordedChannels;
            rate = _sampleRate;
        }

        string resolvedPath = ResolvePath(filePath);
        WriteWav16(resolvedPath, samples, channels, rate);
        LastSavedPath = resolvedPath;
        return resolvedPath;
    }

    /// <summary>
    /// 녹음을 멈추고 버퍼만 비움(파일 저장 X).
    /// </summary>
    public void Discard()
    {
        lock (_bufferLock)
        {
            _isRecording = false;
            _buffer.Clear();
        }
    }

    private string ResolvePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            string dir = Path.Combine(Application.persistentDataPath, _saveFolder);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"capture_{DateTime.Now:yyyyMMdd_HHmmss_fff}.wav");
        }

        if (!Path.IsPathRooted(filePath))
            filePath = Path.Combine(Application.persistentDataPath, filePath);

        string parent = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        return filePath;
    }

    // 오디오 스레드에서 호출됨. AudioListener와 같은 GameObject에 붙어 있어야 마스터 출력이 들어옴.
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_isRecording) return;
        lock (_bufferLock)
        {
            _recordedChannels = channels;
            int len = data.Length;
            if (_buffer.Capacity < _buffer.Count + len)
                _buffer.Capacity = Mathf.Max(_buffer.Capacity * 2, _buffer.Count + len);
            for (int i = 0; i < len; i++) _buffer.Add(data[i]);
        }
    }

    private static void WriteWav16(string path, float[] samples, int channels, int sampleRate)
    {
        const short bitsPerSample = 16;
        const short formatPcm = 1;
        int dataBytes = samples.Length * sizeof(short);

        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((byte)'R'); bw.Write((byte)'I'); bw.Write((byte)'F'); bw.Write((byte)'F');
            bw.Write(36 + dataBytes);
            bw.Write((byte)'W'); bw.Write((byte)'A'); bw.Write((byte)'V'); bw.Write((byte)'E');

            bw.Write((byte)'f'); bw.Write((byte)'m'); bw.Write((byte)'t'); bw.Write((byte)' ');
            bw.Write(16);
            bw.Write(formatPcm);
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(sampleRate * channels * bitsPerSample / 8);
            bw.Write((short)(channels * bitsPerSample / 8));
            bw.Write(bitsPerSample);

            bw.Write((byte)'d'); bw.Write((byte)'a'); bw.Write((byte)'t'); bw.Write((byte)'a');
            bw.Write(dataBytes);

            for (int i = 0; i < samples.Length; i++)
            {
                float s = samples[i];
                if (s > 1f) s = 1f; else if (s < -1f) s = -1f;
                bw.Write((short)(s * 32767f));
            }
        }
    }
}
