using System.Threading.Tasks;
using Shinzui.Application.Interfaces;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

public enum SaveMode
{
    None,
    Save,
    Load,
    Delete
}

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
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;
        [SerializeField] private TextMeshProUGUI saveText;
        [SerializeField] private TextMeshProUGUI checkText;
        //[SerializeField] private List<Button> saveSlots;
        
        private SaveMode _saveMode;
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
            
            _saveMode = SaveMode.None;
            
            // セーブ、ロード、削除の登録(現在は仮でスロットを0に固定)
            saveButton.onClick
                .AddListener( () => SetSaveMode(SaveMode.Save));
            loadButton.onClick
                .AddListener( () => SetSaveMode(SaveMode.Load));
            deleteButton.onClick
                .AddListener( () => SetSaveMode(SaveMode.Delete));
            
            // 決定、キャンセルの登録
            yesButton.onClick
                .AddListener( () => YesButton());
            noButton.onClick
                .AddListener(() => NoButton());
            
            // バックボタンを押すとセーブ画面を閉じる
            backButton.onClick
                .AddListener(() => ChangeSavePanelActive(false));
            
            // テキストの初期化
            saveText.text = "古びたノートだ";
            checkText.color = Color.red;
            checkText.text = "本当に？";
        }

        public void ChangeSavePanelActive(bool active)
        {
            saveSlotSelectPanel.SetActive(active);
            _inputService.SetBlocked(active);
        }

        private void ChangeCheckPanelActive(bool active)
        {
            saveCheckPanel.SetActive(active);
            saveSlotSelectPanel.SetActive(!active);
            _saveMode = !active ? SaveMode.None : _saveMode;
        }

        private void SetSaveMode(SaveMode mode)
        {
            _saveMode = mode;
            ChangeCheckPanelActive(true);
        }
        
        private void Save(int index)
        {
            if(_saveManager == null) Debug.LogWarning("SaveManager is null!");
            _saveManager.Save(index);
            checkText.text = "持ち物を記録した";
        }

        private void Load(int index)
        {
            if(_saveManager == null) Debug.LogWarning("SaveManager is null!");
            _saveManager.Load(index);
            checkText.text = "持ち物を思い出した";
        }

        private void Delete(int index)
        {
            if(_saveManager == null) Debug.LogWarning("SaveManager is null!");
            _saveManager.DeleteSaveData(index);
            checkText.text = "記録を消した";
        }

        private async void YesButton()
        {
            checkText.color = Color.white;
            switch (_saveMode)
            {
                case SaveMode.Save:
                    Save(0);
                    break;
                case SaveMode.Load:
                    Load(0);
                    break;
                case SaveMode.Delete:
                    Delete(0);
                    break;
                default:
                    break;
            }

            await Task.Delay(2000);
            ChangeCheckPanelActive(false);
            checkText.color = Color.red;
            checkText.text = "本当に？";
        }

        private void NoButton()
        {
            ChangeCheckPanelActive(false);
        }
    }
}
