using UnityEngine;
using UnityEngine.UI;
using VContainer;

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
        //[SerializeField] private List<Button> saveSlots;
        private SaveManager _saveManager;
        
        [Inject]
        public void Construct(SaveManager saveManager)
        {
            Debug.Log("SaveController: Construct");
            _saveManager = saveManager;
            
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
        }

        public void ChangeSavePanelActive(bool active)
        {
            saveSlotSelectPanel.SetActive(active);
            ApplyCursorLock(!active);
        }
        
        /// <summary>
        /// lockCursor = trueのときカーソルを非表示にし、画面中央に固定する
        /// </summary>
        private void ApplyCursorLock(bool lockCursor)
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        
        public void SaveButton(int index)
        {
            if(_saveManager == null) Debug.LogWarning("SaveManager is null!");
            _saveManager.Save(index);
        }

        public void LoadButton(int index)
        {
            if(_saveManager == null) Debug.LogWarning("SaveManager is null!");
            _saveManager.Load(index);
        }
    }
}
