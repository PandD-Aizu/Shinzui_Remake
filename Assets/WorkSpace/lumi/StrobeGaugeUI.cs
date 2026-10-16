using UnityEngine;
using UnityEngine.UI;

public class StrobeGaugeUI : MonoBehaviour
{

    [SerializeField] private Image testImage; // 塗りつぶすImage
    [SerializeField] private bool clockwise = false;
    [SerializeField] private int origin = (int)Image.Origin360.Top; // Top/Right/Bottom/Left

    private float duration = 3.0f;
    private float t = 0f;

    void Start()
    {
        // 同じGameObjectにアタッチされているImageを取得
        testImage = GetComponent<Image>();

        testImage.type = Image.Type.Filled;
        testImage.fillMethod = Image.FillMethod.Radial360;
        testImage.fillOrigin = origin;
        testImage.fillClockwise = clockwise;
        testImage.fillAmount = 1f;
    }

}