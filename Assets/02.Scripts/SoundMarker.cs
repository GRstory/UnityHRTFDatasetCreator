using TMPro;
using UnityEngine;

public class SoundMarker : MonoBehaviour
{
    [Header("References")]
    [Tooltip("사운드 위치를 표시할 시각 오브젝트의 Transform. 매 프레임 source 위치로 이동됨.")]
    [SerializeField] private Transform _sphere;
    [Tooltip("listener → source를 잇는 LineRenderer.")]
    [SerializeField] private LineRenderer _line;
    [Tooltip("3D 라벨용 TMP_Text. 비워두면 라벨 미표시. 색상/폰트/크기 등은 프리팹에서 설정.")]
    [SerializeField] private TMP_Text _label;
    [SerializeField] private bool _billboardLabel = true;

    public void Apply(Vector3 listenerPos, Vector3 sourcePos, string labelText)
    {
        if (_sphere != null) _sphere.position = sourcePos;

        if (_line != null)
        {
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.SetPosition(0, listenerPos);
            _line.SetPosition(1, sourcePos);
        }

        if (_label != null) _label.text = labelText;
    }

    void LateUpdate()
    {
        if (!_billboardLabel || _label == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 toCam = cam.transform.position - _label.transform.position;
        if (toCam.sqrMagnitude > 0.0001f)
            _label.transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
    }
}
