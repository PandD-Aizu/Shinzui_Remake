using Shinzui.View.Interaction;
using UnityEngine;

namespace Shinzui.View
{
    /// <summary>
    /// 蜘蛛の巣が公開するインタラクト情報と、成功時のView効果を担当する。
    /// 装備判定とマッチ棒消費はApplication層のInteractionUseCaseが行う。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpiderWebInteractable : InteractableComponent
    {
        public const string InteractionId = "spider_web";

        public override string DisplayName => "[E] 火をつける";
        public override string InteractableId => InteractionId;
        public override string InteractMessage => "蜘蛛の巣が燃え上がった。";

        public override void ExecuteInteractEffect()
        {
            CanInteract = false;

            SpiderWeb spiderWeb = GetComponent<SpiderWeb>();
            if (spiderWeb != null)
            {
                spiderWeb.Burn();
            }
        }
    }
}
