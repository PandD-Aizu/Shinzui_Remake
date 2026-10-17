using Shinzui.Infrastructure.SaveSystem;
using UnityEngine;

namespace Shinzui.View.Interaction
{
    /// <summary>
    /// インタラクトするとセーブを行えるオブジェクト
    /// </summary>
    public class SavePointObject : InteractableComponent
    {
        [SerializeField] private GameObject savePanel;
        public override string InteractableId => "SavePoint";
        public override string InteractMessage => "これまでのことを記録しておこうか。";

        public override void ExecuteInteractEffect()
        {
            base.ExecuteInteractEffect();
            savePanel.GetComponent<SaveController>().ChangeSavePanelActive(true);
        }
    }
}