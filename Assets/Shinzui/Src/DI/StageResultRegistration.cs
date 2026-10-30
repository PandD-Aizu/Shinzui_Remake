using Shinzui.Application.Interfaces;
using Shinzui.Application.Title;
using Shinzui.Application.UseCases;
using Shinzui.Domain.ValueObjects.StageResults;
using Shinzui.Infrastructure;
using Shinzui.Infrastructure.Services;
using Shinzui.Infrastructure.Title;
using Shinzui.Presentation;
using Shinzui.View;
using Shinzui.View.GenerateTunnel;
using Shinzui.View.Settings;
using Shinzui.View.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Shinzui.DI
{
    /// <summary>シーンやPrefabを変更せず結果画面をプレイヤースコープへ組み立てる</summary>
    public static class StageResultRegistration
    {
        /// <summary>結果集計と画面と遷移を一つのステージへ登録する</summary>
        /// <param name="builder">登録先</param>
        /// <param name="player">同じステージのプレイヤー</param>
        /// <param name="definition">ステージ固有の定義または未指定</param>
        public static void Register(IContainerBuilder builder, PlayerView player, StageResultDefinition definition)
        {
            // Scene上に未配置でもプレイヤーから結果画面を開けるようにする
            if (player == null) return;
            var trigger = player.GetComponent<StageResultTrigger>();
            if (trigger == null) trigger = player.gameObject.AddComponent<StageResultTrigger>();
            var resultObject = new GameObject("StageResult", typeof(RectTransform), typeof(StageResultView));
            SceneManager.MoveGameObjectToScene(resultObject, player.gameObject.scene);
            var view = resultObject.GetComponent<StageResultView>();
            var pause = Resources.Load<PauseMenuView>("PauseMenu");
            view.ConfigureFont(pause != null ? pause.Font : null);

            // 未指定時は同梱のResourcesアセット、それもなければコードの既定値を使う
            if (definition == null) definition = Resources.Load<StageResultDefinition>("StageResultDefinition");
            builder.RegisterInstance(definition != null ? definition.ToRules() : StageResultRules.CreateDefault());
            builder.Register<StageResultUseCase>(Lifetime.Scoped);
            builder.Register<UnityTitleNavigation>(Lifetime.Scoped).As<ITitleNavigation>();
            builder.Register<FMODStageResultAudio>(Lifetime.Scoped).As<IStageResultAudio>();
            builder.RegisterComponent(trigger);
            builder.RegisterComponent(view);
            builder.RegisterEntryPoint<StageResultPresenter>();
        }
    }
}
