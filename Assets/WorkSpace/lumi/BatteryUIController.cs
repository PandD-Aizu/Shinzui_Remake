using UnityEngine;
using Shinzui.Domain.Entities.Flashlight;

public class BatteryUIController : MonoBehaviour
{

    [Header("マスク設定")]
    [Tooltip("マスクするオブジェクト")]
    [SerializeField] private GameObject _maskObject;
    [Tooltip("マスクされるオブジェクト")]
    [SerializeField] private GameObject _maskedObject;

    [Header("チャージ設定")]
    [SerializeField] private float _minPoint = 0f;
    [SerializeField] private float _maxPoint = 1f;

    [Header("MIN時の位置")]
    [SerializeField] private Vector3 _minPosition;

    [Header("MAX時の位置")]
    [SerializeField] private Vector3 _maxPosition;

    private FlashlightEntity _flashlightEntity;

    /// <summary>
    /// FlashlightEntityを設定する
    /// </summary>
    public void Initialize(FlashlightEntity flashlightEntity)
    {
        _flashlightEntity = flashlightEntity;
    }

    private void Update()
    {
        if (_flashlightEntity == null)
            return;

        // ストロボのチャージ進行度を取得（0.0～1.0）
        float charge = _flashlightEntity.StrobeCharge.CurrentValue;

        UpdateMaskPosition(charge);
    }

    /// <summary>
    /// チャージ進行度に応じてマスクの位置と角度を変更
    /// </summary>
    private void UpdateMaskPosition(float point)
    {
        if (_maskObject == null || _maskedObject == null)
            return;

        float t = Mathf.InverseLerp(_minPoint, _maxPoint, point);

        _maskObject.transform.localPosition =
            Vector3.Lerp(_minPosition, _maxPosition, t);
    }
}