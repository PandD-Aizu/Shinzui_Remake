using UnityEngine;
using Shinzui.Infrastructure.SaveData;
using VContainer;

/// <summary>
/// セーブ&ロード動作のテスト用
/// ボタンなどにアタッチする
/// </summary>
public class SaveLoadTest : MonoBehaviour
{
    private SaveManager _saveManager;

    [Inject]
    public void SaveManager(SaveManager saveManager)
    {
        _saveManager = saveManager;
    }

    public void SaveButton()
    {
        if(_saveManager == null) Debug.LogWarning("SaveManager is null!");
        _saveManager.Save(0);
    }

    public void LoadButton()
    {
        if(_saveManager == null) Debug.LogWarning("SaveManager is null!");
        _saveManager.Load(0);
    }
}
