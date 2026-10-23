using Shinzui.Application.Interfaces;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

namespace Shinzui.Infrastructure.SaveSystem
{
    /// <summary>
    /// Save & LoadのUI制御と実行
    /// </summary>
    public class SaveController : MonoBehaviour
    {
        [SerializeField] private GameObject saveSlotSelectPanel;
        [SerializeField] private GameObject saveCheckPanel;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TextMeshProUGUI saveText;
        //[SerializeField] private List<Button> saveSlots;
        
        private SaveManager _saveManager;
        private IInputService _inputService;
        
        [Inject]
        public void Construct(
            SaveManager saveManager,
            IInputService inputService)
        {
            _saveManager = saveManager;
            _inputService = inputService;
            
            saveSlotSelectPanel.SetActive(false);
            saveCheckPanel.SetActive(false);
            
            // セーブとロードの登録(現在は仮でスロットを0に固定)
            saveButton.onClick
                .AddListener( () => SaveButton(0));
            loadButton.onClick
                .AddListener( () => LoadButton(0));
            
            // バックボタンを押すとセーブ画面を閉じる
            backButton.onClick
                .AddListener(() => ChangeSavePanelActive(false));

            saveText.text = "持ち物を確認しておかないと";
        }

        public void ChangeSavePanelActive(bool active)
        {
            saveSlotSelectPanel.SetActive(active);
            _inputService.SetBlocked(active);
        }
        
        public void SaveButton(int index)
        {
            if(_saveManager == null) Debug.LogWarning("SaveManager is null!");
            _saveManager.Save(index);
            saveText.text = "持ち物を記憶した";
        }

        public void LoadButton(int index)
        {
            if(_saveManager == null) Debug.LogWarning("SaveManager is null!");
            _saveManager.Load(index);
            saveText.text = "持ち物を思い出した";
        }
    }
}
