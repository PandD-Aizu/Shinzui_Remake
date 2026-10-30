using UnityEngine;
using UnityEngine.UI;
using Shinzui.Domain.Entities.Flashlight;

public class BatteryUIController : MonoBehaviour
{

    [Header("マスク設定")]
    [Tooltip("マスクするUI Image")]
    [SerializeField] private Image _maskImage;

    [Tooltip("マスクされるUI Image")]
    [SerializeField] private Image _maskedImage;

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
        if (_flashlightEntity == null || _maskImage == null)
            return;

        // ストロボのチャージ進行度（0～1）
        float charge = _flashlightEntity.StrobeCharge.CurrentValue;

        UpdateMaskPosition(charge);
    }

    /// <summary>
    /// チャージ進行度に応じてUI Imageの位置と角度を変更
    /// </summary>
    private void UpdateMaskPosition(float point)
    {
        float t = Mathf.InverseLerp(_minPoint, _maxPoint, point);

        RectTransform maskRect = _maskImage.rectTransform;

        // 位置を補間
        maskRect.localPosition =
            Vector3.Lerp(_minPosition, _maxPosition, t);
    }
}