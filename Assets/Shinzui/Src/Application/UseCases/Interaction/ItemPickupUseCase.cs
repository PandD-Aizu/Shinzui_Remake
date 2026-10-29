using System;

namespace Shinzui.Application.UseCases.Interaction
{
    /// <summary>取得操作の進行段階</summary>
    public enum ItemPickupState { Idle, Reaching, Waiting, Committing, Returning }

    /// <summary>長押しと演出待ちを管理し、取得確定を一度だけ許可</summary>
    public sealed class ItemPickupUseCase
    {
        public ItemPickupState State { get; private set; }
        public float Progress { get; private set; }
        public bool Succeeded { get; private set; }
        public bool IsActive => State != ItemPickupState.Idle;
        private float _duration;

        /// <summary>新しい取得操作を開始</summary>
        /// <param name="duration">長押しに必要な秒数</param>
        public void Begin(float duration)
        {
            if (IsActive) return;
            _duration = Math.Max(.05f, duration);
            Progress = 0f;
            Succeeded = false;
            State = ItemPickupState.Reaching;
        }

        /// <summary>長押しまたは手を戻す進捗を更新</summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <param name="held">操作ボタンを保持しているか</param>
        /// <param name="valid">対象への操作を継続できるか</param>
        public void Tick(float deltaTime, bool held, bool valid)
        {
            if ((State == ItemPickupState.Reaching || State == ItemPickupState.Waiting) && (!held || !valid)) Cancel();

            // 演出待ちと取得確定中は進捗を止める
            if (State == ItemPickupState.Reaching)
                Progress = Math.Min(1f, Progress + Math.Max(0f, deltaTime) / _duration);
            else if (State == ItemPickupState.Returning)
            {
                Progress = Math.Max(0f, Progress - Math.Max(0f, deltaTime) / .3f);
                if (Progress <= 0f) State = ItemPickupState.Idle;
            }
        }

        /// <summary>演出の完了まで手を止める</summary>
        public void Pause()
        {
            if (State == ItemPickupState.Reaching) State = ItemPickupState.Waiting;
        }

        /// <summary>演出終了後に長押しを再開</summary>
        public void Resume()
        {
            if (State == ItemPickupState.Waiting) State = ItemPickupState.Reaching;
        }

        /// <summary>取得前の操作を中止し手を戻す</summary>
        public void Cancel()
        {
            if (State == ItemPickupState.Reaching || State == ItemPickupState.Waiting)
                State = ItemPickupState.Returning;
        }

        /// <summary>完了した長押しの取得確定を一度だけ開始</summary>
        /// <returns>取得処理を実行できるか</returns>
        public bool TryCommit()
        {
            if (State != ItemPickupState.Reaching || Progress < 1f) return false;
            State = ItemPickupState.Committing;
            return true;
        }

        /// <summary>取得結果を保存して手を戻す</summary>
        /// <param name="success">インベントリへの追加が成功したか</param>
        public void Finish(bool success)
        {
            if (State != ItemPickupState.Committing) return;
            Succeeded = success;
            State = ItemPickupState.Returning;
        }
    }
}
