using System;
using Shinzui.Application.Title;
using Shinzui.Infrastructure.Title;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shinzui.Infrastructure.SaveSystem
{
    /// <summary>タイトルから保存スロットを選択して再開または削除する</summary>
    public sealed class SaveSelectController : MonoBehaviour
    {
        [SerializeField] private Button[] slotButtons;
        [SerializeField] private TMP_Text[] slotLabels;
        [SerializeField] private GameObject confirmationPanel;
        [SerializeField] private TMP_Text message;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button backButton;
        [SerializeField] private string gameSceneAddress;
        private SaveFileStore _store;
        private readonly UnityTitleNavigation _navigation = new UnityTitleNavigation();
        private int _selectedSlot;
        private bool _busy;
        private bool _confirmDelete;

        /// <summary>ボタンを登録してスロットの状態を表示する</summary>
        private void Start()
        {
            // 保存スロットをマウスで操作できるようにカーソルを表示
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _store = new SaveFileStore();
            for (var i = 0; i < slotButtons.Length; i++)
            {
                var slot = i;
                slotButtons[i].onClick.AddListener(() => Select(slot));
            }
            loadButton.onClick.AddListener(Load);
            deleteButton.onClick.AddListener(Delete);
            cancelButton.onClick.AddListener(() => { if (!_busy) confirmationPanel.SetActive(false); });
            backButton.onClick.AddListener(Back);
            confirmationPanel.SetActive(false);
            RefreshSlots();
        }

        /// <summary>セーブ選択画面の操作中はカーソルの表示を維持する</summary>
        private void LateUpdate()
        {
            // 他の入力処理によるカーソルのロックや非表示を解除する
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>保存済みと空きの状態を更新する</summary>
        private void RefreshSlots()
        {
            for (var i = 0; i < slotButtons.Length; i++)
            {
                var exists = _store.Exists(i);
                slotLabels[i].text = $"スロット {i + 1}　{(exists ? "保存済み" : "空き")}";
                slotButtons[i].interactable = exists && !_busy;
            }
        }

        /// <summary>選択したスロットの操作を表示する</summary>
        /// <param name="slot">保存スロット番号</param>
        private void Select(int slot)
        {
            if (_busy) return;
            _selectedSlot = slot;
            _confirmDelete = false;
            message.text = $"スロット {slot + 1}から再開しますか？";
            loadButton.interactable = true;
            confirmationPanel.SetActive(true);
        }

        /// <summary>ファイルを検証してゲームへ遷移する</summary>
        private async void Load()
        {
            if (_busy) return;
            SetBusy(true);
            try
            {
                _store.Load(_selectedSlot);
                GameStartRequest.Continue(_selectedSlot);
                await _navigation.LoadGameAsync(gameSceneAddress);
            }
            catch (Exception exception)
            {
                GameStartRequest.NewGame();
                if (this == null) return;
                message.text = "読み込みに失敗しました\n保存データを確認してください";
                Debug.LogWarning($"ロード失敗: {exception.Message}");
                SetBusy(false);
            }
        }

        /// <summary>二段階の確認後に選択スロットを削除する</summary>
        private void Delete()
        {
            if (_busy) return;
            if (!_confirmDelete)
            {
                _confirmDelete = true;
                loadButton.interactable = false;
                message.text = "記録を削除しますか？\nもう一度「削除」を押すと確定します";
                return;
            }
            try
            {
                _store.Delete(_selectedSlot);
                confirmationPanel.SetActive(false);
                RefreshSlots();
            }
            catch (Exception exception)
            {
                message.text = "削除に失敗しました";
                Debug.LogWarning($"削除失敗: {exception.Message}");
            }
        }

        /// <summary>タイトル画面へ戻る</summary>
        private async void Back()
        {
            if (_busy) return;
            SetBusy(true);
            try { await _navigation.LoadGameAsync("Title"); }
            catch (Exception exception)
            {
                if (this == null) return;
                confirmationPanel.SetActive(true);
                message.text = "タイトルへの移動に失敗しました";
                Debug.LogWarning(exception.Message);
                SetBusy(false);
            }
        }

        /// <summary>処理中の重複操作を防ぐ</summary>
        /// <param name="busy">処理中か</param>
        private void SetBusy(bool busy)
        {
            _busy = busy;
            loadButton.interactable = deleteButton.interactable = cancelButton.interactable = backButton.interactable = !busy;
            RefreshSlots();
        }
    }
}
