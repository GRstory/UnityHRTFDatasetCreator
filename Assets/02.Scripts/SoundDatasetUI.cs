using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SoundDatasetUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RandomSoundSpawner _spawner;
    [SerializeField] private TMP_Text _sequenceText;
    [Tooltip("동시 재생 슬롯별 TMP_Text. 길이만큼만 표시됨(권장 3개).")]
    [SerializeField] private TMP_Text[] _slotTexts;
    [SerializeField] private Button _restartButton;

    [Header("Messages")]
    [SerializeField] private string _idleMessage = "Press Restart to begin";
    [SerializeField] private string _endedMessage = "SEQUENCE ENDED";
    [SerializeField, Tooltip("종료 메시지 색상 (rich text hex, # 없이).")]
    private string _endedColorHex = "ffcc55";
    [SerializeField] private string _emptySlotText = "—";

    [Header("Slot Colors (rich text)")]
    [Tooltip("슬롯 인덱스별 색상. 슬롯 텍스트 전체가 이 색의 <color> 태그로 감싸짐. 길이는 _slotTexts와 동일하게 권장.")]
    [SerializeField]
    private Color[] _slotColors = new Color[]
    {
        new(1f, 0.45f, 0.45f),
        new(0.5f, 1f, 0.5f),
        new(0.45f, 0.7f, 1f),
    };

    private struct ActiveEntry
    {
        public bool Used;
        public RandomSoundSpawner.SpawnInfo Info;
        public float StartSequenceTime;
        public float EndSequenceTime;
    }

    private ActiveEntry[] _slots;
    private readonly StringBuilder _sb = new();
    private bool _ended;
    private bool _hasStartedOnce;

    void Awake()
    {
        _slots = new ActiveEntry[_slotTexts != null ? _slotTexts.Length : 0];

        if (_restartButton != null)
            _restartButton.onClick.AddListener(OnRestartClicked);
    }

    void OnEnable()
    {
        if (_spawner == null) return;
        _spawner.OnSoundSpawned += HandleSpawned;
        _spawner.OnSequenceStarted += HandleStarted;
        _spawner.OnSequenceEnded += HandleEnded;
    }

    void OnDisable()
    {
        if (_spawner == null) return;
        _spawner.OnSoundSpawned -= HandleSpawned;
        _spawner.OnSequenceStarted -= HandleStarted;
        _spawner.OnSequenceEnded -= HandleEnded;
    }

    private void HandleStarted()
    {
        for (int i = 0; i < _slots.Length; i++) _slots[i] = default;
        _ended = false;
        _hasStartedOnce = true;
    }

    private void HandleSpawned(RandomSoundSpawner.SpawnInfo info)
    {
        float startSeq = _spawner.SequenceElapsed;
        float clipLen = info.Clip != null ? info.Clip.length : 0f;
        var entry = new ActiveEntry
        {
            Used = true,
            Info = info,
            StartSequenceTime = startSeq,
            EndSequenceTime = startSeq + clipLen
        };

        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].Used) { _slots[i] = entry; return; }
        }
    }

    private void HandleEnded()
    {
        _ended = true;
    }

    void Update()
    {
        if (_spawner == null) return;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].Used) continue;
            var s = _slots[i].Info.Source;
            if (s == null || !s.isPlaying) _slots[i] = default;
        }

        if (_restartButton != null)
            _restartButton.interactable = !_spawner.IsSequenceRunning;

        if (_sequenceText != null) _sequenceText.text = BuildSequenceText();

        if (_slotTexts != null)
        {
            for (int i = 0; i < _slotTexts.Length; i++)
            {
                if (_slotTexts[i] == null) continue;
                _slotTexts[i].text = _slots[i].Used ? BuildSlotText(_slots[i], i) : _emptySlotText;
            }
        }
    }

    private string BuildSequenceText()
    {
        _sb.Clear();
        if (_spawner.IsSequenceRunning)
        {
            _sb.AppendFormat("<b>Sequence</b>  t = {0:F2} / {1:F2} s",
                _spawner.SequenceElapsed, _spawner.SequenceDuration);
        }
        else if (_ended)
        {
            _sb.AppendFormat("<b>Sequence</b>  t = {0:F2} / {1:F2} s   <color=#{2}>[{3}]</color>",
                _spawner.SequenceElapsed, _spawner.SequenceDuration, _endedColorHex, _endedMessage);
        }
        else
        {
            _sb.AppendFormat("<b>Sequence</b>  <i>{0}</i>",
                _hasStartedOnce ? _endedMessage : _idleMessage);
        }
        return _sb.ToString();
    }

    private string BuildSlotText(ActiveEntry e, int slotIndex)
    {
        Vector3 dir = e.Info.DirectionFromListener;
        float az = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float horiz = new Vector2(dir.x, dir.z).magnitude;
        float el = Mathf.Atan2(dir.y, Mathf.Max(0.0001f, horiz)) * Mathf.Rad2Deg;

        Color slotColor = (_slotColors != null && slotIndex < _slotColors.Length)
            ? _slotColors[slotIndex]
            : Color.white;
        string colorHex = ColorUtility.ToHtmlStringRGB(slotColor);

        _sb.Clear();
        _sb.AppendFormat("<color=#{0}>", colorHex);
        _sb.AppendFormat("<b>{0}</b>\n", e.Info.SoundEvent);
        _sb.AppendFormat("{0:F2}s → {1:F2}s\n", e.StartSequenceTime, e.EndSequenceTime);
        _sb.AppendFormat("az={0,6:+0.0;-0.0;0.0}°  el={1,6:+0.0;-0.0;0.0}°  d={2:F1}m",
            az, el, e.Info.Distance);
        _sb.Append("</color>");
        return _sb.ToString();
    }

    private void OnRestartClicked()
    {
        if (_spawner == null) return;
        _spawner.RestartSequence();
    }
}
