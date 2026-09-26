using System.Collections.Generic;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Infrastructure.SaveData;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Shinzui.Temp.SaveSystem
{
    public class SaveController : MonoBehaviour
    {
        [SerializeField] private GameObject saveSlotSelectPanel;
        [SerializeField] private GameObject saveCheckPanel;
        [SerializeField] private List<Button> saveSlots;
        private SaveManager _saveManager;
        private IInventoryRepository _inventoryRepository;
        private ISpecialItemRepository _specialItemRepository;
        
        [Inject]
        public void Construct(SaveManager saveManager)
        {
            _saveManager = saveManager;
            saveSlotSelectPanel.SetActive(false);
            saveCheckPanel.SetActive(false);
        }

        public void ChangeSavePanelActive(bool active)
        {
            saveSlotSelectPanel.SetActive(active);
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
