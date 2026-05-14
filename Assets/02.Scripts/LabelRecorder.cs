using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class LabelRecorder : MonoBehaviour
{
    [SerializeField] private RandomSoundSpawner _spawner;
    [SerializeField] private Transform _listener;
    [SerializeField] private float _recordInterval = 0.1f;

    private List<string> _rows;
    private int _frameIndex;
    private Coroutine _loop;
    private string _hrtfLabel;
    private readonly StringBuilder _sb = new();

    public bool IsRecording => _loop != null;

    public void StartRecording(string hrtfLabel)
    {
        if (_loop != null) return;
        _hrtfLabel = hrtfLabel;
        _rows = new List<string> { "frame_index,sound_class_id,azimuth_deg,elevation_deg,distance_m,hrtf_logic" };
        _frameIndex = 0;
        _loop = StartCoroutine(RecordLoop());
    }

    public string StopRecording(string filePath = null)
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
        string path = ResolvePath(filePath);
        File.WriteAllLines(path, _rows);
        return path;
    }

    public void Discard()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
        _rows = null;
    }

    private IEnumerator RecordLoop()
    {
        var wait = new WaitForSeconds(_recordInterval);
        while (true)
        {
            CaptureFrame();
            _frameIndex++;
            yield return wait;
        }
    }

    private void CaptureFrame()
    {
        if (_spawner == null || _listener == null) return;

        _spawner.ForEachActiveSlot((info, worldPos) =>
        {
            Vector3 local = _listener.InverseTransformPoint(worldPos);
            float az = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float horiz = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            float el = Mathf.Atan2(local.y, Mathf.Max(0.0001f, horiz)) * Mathf.Rad2Deg;
            float dist = local.magnitude;

            _sb.Clear();
            _sb.Append(_frameIndex).Append(',');
            _sb.Append((int)info.SoundEvent).Append(',');
            _sb.Append(az.ToString("F2")).Append(',');
            _sb.Append(el.ToString("F2")).Append(',');
            _sb.Append(dist.ToString("F3")).Append(',');
            _sb.Append(_hrtfLabel);
            _rows.Add(_sb.ToString());
        });
    }

    private string ResolvePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            string dir = Path.Combine(Application.persistentDataPath, "Labels");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"label_{System.DateTime.Now:yyyyMMdd_HHmmss_fff}.csv");
        }
        if (!Path.IsPathRooted(filePath))
            filePath = Path.Combine(Application.persistentDataPath, filePath);
        string parent = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        return filePath;
    }
}
