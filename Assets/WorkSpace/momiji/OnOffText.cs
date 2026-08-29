using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OnOffText : MonoBehaviour
{
    [SerializeField] private Toggle targetToggle;
    [SerializeField] private TextMeshProUGUI onOffText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        OnOffChange();
    }

    //オンオフの切り替えに合わせてテキストを変更する
    public void OnOffChange()
    {
        if (targetToggle.isOn)
        {
            onOffText.text = "OFF";
        }
        else
        {
            onOffText.text = "ON";
        }
    }
}
