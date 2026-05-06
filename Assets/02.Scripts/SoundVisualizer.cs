using UnityEngine;

public class SoundVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RandomSoundSpawner _spawner;
    [SerializeField] private Transform _listener;

    [Header("Slots")]
    [Tooltip("동시 활성 사운드 상한(spawner의 _maxConcurrent와 같게).")]
    [SerializeField] private int _maxSlots = 3;

    [Header("Scene Gizmos (editor)")]
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private float _gizmoSphereRadius = 0.3f;

    [Header("Scene Markers (runtime)")]
    [SerializeField] private bool _drawSceneMarkers = true;
    [Tooltip("슬롯별 SoundMarker 프리팹. 인덱스 i가 슬롯 i에 매핑됨(머티리얼/메쉬/라벨 색을 슬롯마다 다르게 가능).")]
    [SerializeField] private SoundMarker[] _markerPrefabs;

    [Header("Radar UI")]
    [SerializeField] private bool _showRadar = true;
    [SerializeField] private Vector2 _radarScreenPos = new(20, 20);
    [SerializeField] private float _radarSize = 220f;
    [Tooltip("레이더 반경에 해당하는 실세계 거리(m).")]
    [SerializeField] private float _radarRangeMeters = 15f;
    [SerializeField] private bool _showActiveList = true;

    private struct Slot
    {
        public bool Used;
        public RandomSoundSpawner.SpawnInfo Info;
    }

    private Slot[] _slots;
    private SoundMarker[] _markers;
    private Texture2D _whiteTex;
    private GUIStyle _labelStyle;
    private static readonly int _enumCount = System.Enum.GetValues(typeof(ESoundEvent)).Length;

    void Awake()
    {
        _slots = new Slot[Mathf.Max(1, _maxSlots)];
        _markers = new SoundMarker[_slots.Length];

        _whiteTex = new Texture2D(1, 1);
        _whiteTex.SetPixel(0, 0, Color.white);
        _whiteTex.Apply();
        _whiteTex.hideFlags = HideFlags.HideAndDontSave;

        if (_listener == null && Camera.main != null) _listener = Camera.main.transform;

        if (_drawSceneMarkers)
        {
            for (int i = 0; i < _markers.Length; i++)
            {
                var prefab = (_markerPrefabs != null && i < _markerPrefabs.Length) ? _markerPrefabs[i] : null;
                if (prefab == null)
                {
                    Debug.LogWarning($"{nameof(SoundVisualizer)}: 슬롯 {i}의 Marker 프리팹이 비어있어 해당 슬롯 마커가 비활성화됩니다.");
                    continue;
                }
                var m = Instantiate(prefab, transform);
                m.gameObject.name = $"SoundMarker_{i}";
                m.gameObject.SetActive(false);
                _markers[i] = m;
            }
        }
    }

    void OnEnable()
    {
        if (_spawner != null) _spawner.OnSoundSpawned += HandleSpawned;
    }

    void OnDisable()
    {
        if (_spawner != null) _spawner.OnSoundSpawned -= HandleSpawned;
    }

    private void HandleSpawned(RandomSoundSpawner.SpawnInfo info)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].Used)
            {
                _slots[i].Used = true;
                _slots[i].Info = info;
                return;
            }
        }
    }

    void Update()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].Used) continue;
            var s = _slots[i].Info.Source;
            if (s == null || !s.isPlaying) _slots[i] = default;
        }

        if (_drawSceneMarkers && _listener != null) UpdateMarkers();
        else HideAllMarkers();
    }

    private void UpdateMarkers()
    {
        for (int i = 0; i < _markers.Length; i++)
        {
            var m = _markers[i];
            if (m == null) continue;

            if (i < _slots.Length && _slots[i].Used && _slots[i].Info.Source != null)
            {
                var info = _slots[i].Info;
                Vector3 srcPos = info.Source.transform.position;

                if (!m.gameObject.activeSelf) m.gameObject.SetActive(true);
                m.Apply(_listener.position, srcPos, $"{info.SoundEvent}\nd={info.Distance:F1}m");
            }
            else if (m.gameObject.activeSelf)
            {
                m.gameObject.SetActive(false);
            }
        }
    }

    private void HideAllMarkers()
    {
        if (_markers == null) return;
        for (int i = 0; i < _markers.Length; i++)
        {
            if (_markers[i] != null && _markers[i].gameObject.activeSelf)
                _markers[i].gameObject.SetActive(false);
        }
    }

    void OnDrawGizmos()
    {
        if (!_drawGizmos || _listener == null || _slots == null) return;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].Used) continue;
            var info = _slots[i].Info;
            if (info.Source == null) continue;
            Gizmos.color = ColorForEvent(info.SoundEvent);
            Vector3 srcPos = info.Source.transform.position;
            Gizmos.DrawLine(_listener.position, srcPos);
            Gizmos.DrawSphere(srcPos, _gizmoSphereRadius);
        }
    }

    void OnGUI()
    {
        if (!_showRadar || _listener == null || _slots == null) return;

        if (_labelStyle == null)
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };

        Rect radarRect = new(_radarScreenPos.x, _radarScreenPos.y, _radarSize, _radarSize);
        DrawRect(radarRect, new Color(0, 0, 0, 0.55f));

        Vector2 center = new(radarRect.x + _radarSize * 0.5f, radarRect.y + _radarSize * 0.5f);
        float halfSize = _radarSize * 0.5f - 10f;

        DrawCircleApprox(center, halfSize, new Color(1, 1, 1, 0.25f));
        DrawCircleApprox(center, halfSize * 0.5f, new Color(1, 1, 1, 0.18f));

        DrawRect(new Rect(center.x - 0.5f, radarRect.y + 4, 1f, halfSize), new Color(0.5f, 0.9f, 1f, 0.6f));
        DrawDot(center, 5f, Color.white);

        int activeCount = 0;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].Used) continue;
            activeCount++;
            var info = _slots[i].Info;
            if (info.Source == null) continue;
            Vector3 local = _listener.InverseTransformPoint(info.Source.transform.position);

            float horiz = new Vector2(local.x, local.z).magnitude;
            float r = Mathf.Clamp01(horiz / Mathf.Max(0.001f, _radarRangeMeters)) * halfSize;
            float az = Mathf.Atan2(local.x, local.z);
            Vector2 p = center + new Vector2(Mathf.Sin(az) * r, -Mathf.Cos(az) * r);

            float elevNorm = Mathf.Clamp(local.y / _radarRangeMeters, -1f, 1f);
            float dotR = 5f + elevNorm * 2f;
            DrawDot(p, dotR, ColorForEvent(info.SoundEvent));
        }

        GUI.Label(new Rect(radarRect.x + 6, radarRect.y + 2, _radarSize, 16),
            $"Radar  range={_radarRangeMeters:F0}m  active={activeCount}", _labelStyle);

        if (!_showActiveList) return;

        const float lineH = 16f;
        float listW = _radarSize + 80f;
        float listH = lineH * (_slots.Length + 1) + 10f;
        Rect listRect = new(radarRect.x, radarRect.yMax + 4, listW, listH);
        DrawRect(listRect, new Color(0, 0, 0, 0.55f));

        GUI.Label(new Rect(listRect.x + 6, listRect.y + 4, listW, lineH),
            $"Active sounds ({activeCount})", _labelStyle);

        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].Used) continue;
            var info = _slots[i].Info;
            if (info.Source == null) continue;
            Vector3 local = _listener.InverseTransformPoint(info.Source.transform.position);
            float dist = local.magnitude;
            float az = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float horiz = new Vector2(local.x, local.z).magnitude;
            float el = Mathf.Atan2(local.y, Mathf.Max(0.0001f, horiz)) * Mathf.Rad2Deg;

            var prev = GUI.color;
            GUI.color = ColorForEvent(info.SoundEvent);
            GUI.Label(new Rect(listRect.x + 6, listRect.y + 4 + lineH * (i + 1), listW, lineH),
                $"● [{i}] {info.SoundEvent}    d={dist:F1}m   az={az,6:+0.0;-0.0;0.0}°   el={el,6:+0.0;-0.0;0.0}°",
                _labelStyle);
            GUI.color = prev;
        }
    }

    private static Color ColorForEvent(ESoundEvent e)
    {
        float h = ((int)e) / Mathf.Max(1f, _enumCount);
        return Color.HSVToRGB(h, 0.75f, 1f);
    }

    private void DrawRect(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, _whiteTex);
        GUI.color = prev;
    }

    private void DrawDot(Vector2 c, float radius, Color color)
    {
        DrawRect(new Rect(c.x - radius, c.y - radius, radius * 2f, radius * 2f), color);
    }

    private void DrawCircleApprox(Vector2 center, float radius, Color color)
    {
        const int seg = 32;
        for (int i = 0; i < seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f;
            Vector2 p = center + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            DrawRect(new Rect(p.x - 1f, p.y - 1f, 2f, 2f), color);
        }
    }
}
