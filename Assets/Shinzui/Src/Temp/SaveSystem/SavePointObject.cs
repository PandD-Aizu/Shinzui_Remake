using Shinzui.View.Interaction;
using Shinzui.Infrastructure.SaveData;
using UnityEngine;

/// <summary>
/// インタラクトするとセーブを行えるオブジェクト
/// </summary>
public class SavePointObject : InteractableComponent
{
    public override string InteractableId => "SavePoint";
    public override string InteractMessage => "これまでのことを記録しておこうか。";
    public override void ExecuteInteractEffect()
    {
        base.ExecuteInteractEffect();

        
    }
}
