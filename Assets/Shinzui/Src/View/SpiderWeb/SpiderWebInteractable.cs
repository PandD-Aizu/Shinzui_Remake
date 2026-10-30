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
            ExecuteInteractEffect(null);
        }

        /// <summary>
        /// プレイヤーがマッチを擦って巣へ近づける演出を再生し、触れた糸から燃やす
        /// 演出できない場合は巣の中心から直接燃やす
        /// </summary>
        /// <param name="actor">操作したプレイヤー</param>
        public override void ExecuteInteractEffect(PlayerView actor)
        {
            CanInteract = false;

            SpiderWeb spiderWeb = GetComponent<SpiderWeb>();
            if (spiderWeb == null)
            {
                return;
            }

            // マッチ演出はプレイヤー側に必要になった時点で追加する
            MatchIgnitionView match = null;
            if (actor != null && !actor.TryGetComponent(out match))
            {
                match = actor.gameObject.AddComponent<MatchIgnitionView>();
            }

            if (match == null || !match.Play(spiderWeb))
            {
                spiderWeb.Burn();
            }
        }
    }
}
