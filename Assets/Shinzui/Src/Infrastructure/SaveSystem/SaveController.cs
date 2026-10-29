using System;
using System.Threading.Tasks;
using Shinzui.Application.Interfaces;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

public enum SaveMode { None, Save, Load, Delete }

namespace Shinzui.Infrastructure.SaveSystem
{
    /// <summary>セーブポイントの確認画面と保存操作を制御する</summary>
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
        [SerializeField] private Button[] slotButtons;
        private SaveMode _saveMode;
        private SaveManager _saveManager;
        private IInputService _inputService;
        private bool _busy;
        private int _selectedSlot;

        /// <summary>依存とボタン操作を登録する</summary>
        /// <param name="saveManager">保存処理</param>
        /// <param name="inputService">ゲーム入力</param>
        [Inject]
        public void Construct(SaveManager saveManager, IInputService inputService)
        {
            _saveManager = saveManager;
            _inputService = inputService;
            saveSlotSelectPanel.SetActive(false);
            saveCheckPanel.SetActive(false);
            saveButton.onClick.AddListener(() => SetSaveMode(SaveMode.Save));
            loadButton.onClick.AddListener(() => SetSaveMode(SaveMode.Load));
            deleteButton.onClick.AddListener(() => SetSaveMode(SaveMode.Delete));
            yesButton.onClick.AddListener(YesButton);
            noButton.onClick.AddListener(() => ChangeCheckPanelActive(false));
            backButton.onClick.AddListener(() => ChangeSavePanelActive(false));
            if (slotButtons != null)
            {
                for (var i = 0; i < slotButtons.Length; i++)
                {
                    var slot = i;
                    slotButtons[i].onClick.AddListener(() => SelectSlot(slot));
                }
            }
        }

        /// <summary>保存画面の表示とゲーム入力を切り替える</summary>
        /// <param name="active">表示するか</param>
        public void ChangeSavePanelActive(bool active)
        {
            saveSlotSelectPanel.SetActive(active);
            saveCheckPanel.SetActive(false);
            SelectSlot(_saveManager.CurrentSlot);
            _inputService.SetBlocked(active);
        }

        /// <summary>保存操作の対象スロットを選択する</summary>
        /// <param name="slot">保存スロット番号</param>
        private void SelectSlot(int slot)
        {
            _selectedSlot = slot;
            saveText.text = $"古びたノートだ\nスロット {slot + 1}";
        }

        /// <summary>確認画面と操作画面を切り替える</summary>
        /// <param name="active">確認画面を表示するか</param>
        private void ChangeCheckPanelActive(bool active)
        {
            if (_busy) return;
            saveCheckPanel.SetActive(active);
            saveSlotSelectPanel.SetActive(!active);
            if (!active) _saveMode = SaveMode.None;
        }

        /// <summary>確認する操作を設定する</summary>
        /// <param name="mode">保存、ロード、削除</param>
        private void SetSaveMode(SaveMode mode)
        {
            _saveMode = mode;
            checkText.color = new Color(0.82f, 0.13f, 0.12f);
            checkText.text = $"スロット {_selectedSlot + 1}\n本当に？";
            ChangeCheckPanelActive(true);
        }

        /// <summary>処理完了を待って結果を表示する</summary>
        private async void YesButton()
        {
            if (_busy) return;
            _busy = true;
            yesButton.interactable = noButton.interactable = false;
            try
            {
                var slot = _selectedSlot;
                switch (_saveMode)
                {
                    case SaveMode.Save: await _saveManager.Save(slot); break;
                    case SaveMode.Load: await _saveManager.Load(slot); break;
                    case SaveMode.Delete: await _saveManager.DeleteSaveData(slot); break;
                }
                checkText.color = new Color(0.88f, 0.86f, 0.82f);
                checkText.text = _saveMode == SaveMode.Save ? "持ち物を記録した" :
                    _saveMode == SaveMode.Load ? "持ち物を思い出した" : "記録を消した";
                await Task.Delay(1500);
                if (this == null) return;
                _busy = false;
                ChangeCheckPanelActive(false);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"保存操作に失敗: {exception.Message}");
                if (this != null) checkText.text = "処理に失敗した\n保存データを確認してください";
            }
            finally
            {
                _busy = false;
                if (this != null) yesButton.interactable = noButton.interactable = true;
            }
        }
    }
}
