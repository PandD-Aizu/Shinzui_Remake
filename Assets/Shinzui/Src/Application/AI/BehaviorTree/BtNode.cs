using System;
using Shinzui.Domain.ValueObjects.Enemy;

namespace Shinzui.Application.AI.BehaviorTree
{
    /// <summary>ノードの実行結果</summary>
    public enum BtStatus { Failure, Success, Running }

    /// <summary>割り込み時に状態を破棄できる行動ノード</summary>
    public abstract class BtNode
    {
        /// <summary>ツリー全体で共有する評価時刻</summary>
        protected sealed class BtClock
        {
            public float Time;
            public int Depth;
        }
        private BtClock _clock = new();
        protected float Time => _clock.Time;
        public string Name { get; }
        public string Path { get; private set; }
        public virtual string ActivePath => Path;

        /// <summary>デバッグ用の名前を設定</summary>
        /// <param name="name">ノード名</param>
        protected BtNode(string name) { Name = name; Path = name; }

        /// <summary>構築時だけパス文字列を生成</summary>
        /// <param name="parentPath">親のパス</param>
        public virtual void BindPath(string parentPath) => Path = parentPath + "/" + Name;

        /// <summary>行動を一段進める</summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>実行結果</returns>
        public BtStatus Tick(float deltaTime)
        {
            // 呼び出しの最上位だけ時計を進め、未選択のクールダウンにも時間を反映する
            if (_clock.Depth == 0) _clock.Time += deltaTime;
            _clock.Depth++;
            try { return Evaluate(deltaTime); }
            finally { _clock.Depth--; }
        }

        /// <summary>ノード固有の処理を評価</summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>実行結果</returns>
        protected abstract BtStatus Evaluate(float deltaTime);

        /// <summary>子孫を同じ時計へ接続</summary>
        /// <param name="clock">共有する時計</param>
        protected virtual void SetClock(BtClock clock) => _clock = clock;

        /// <summary>子ノードを親の時計とパスへ接続</summary>
        /// <param name="child">接続する子</param>
        protected void Attach(BtNode child) { child.SetClock(_clock); child.BindPath(Path); }

        /// <summary>中断された実行状態を初期化</summary>
        public virtual void Reset() { }
    }

    /// <summary>子ノードのパスと中断処理をまとめる基底</summary>
    public abstract class BtComposite : BtNode
    {
        protected readonly BtNode[] Children;
        protected int ActiveIndex = -1;
        public override string ActivePath => ActiveIndex >= 0 ? Children[ActiveIndex].ActivePath : Path;

        /// <summary>構築時に子ノードを保持</summary>
        /// <param name="name">ノード名</param>
        /// <param name="children">優先順の子</param>
        protected BtComposite(string name, params BtNode[] children) : base(name)
        {
            Children = children;
            for (int i = 0; i < children.Length; i++) Attach(children[i]);
        }

        /// <summary>全ての子へ評価時刻を共有</summary>
        /// <param name="clock">共有する時計</param>
        protected override void SetClock(BtClock clock)
        {
            base.SetClock(clock);
            for (int i = 0; i < Children.Length; i++) Attach(Children[i]);
        }

        /// <summary>子孫のパスを構築</summary>
        /// <param name="parentPath">親のパス</param>
        public override void BindPath(string parentPath)
        {
            base.BindPath(parentPath);
            for (int i = 0; i < Children.Length; i++) Children[i].BindPath(Path);
        }

        /// <summary>実行中の子を含めて中断</summary>
        public override void Reset()
        {
            for (int i = 0; i < Children.Length; i++) Children[i].Reset();
            ActiveIndex = -1;
        }
    }

    /// <summary>毎フレーム先頭から評価して高優先の行動を割り込ませる</summary>
    public sealed class BtSelector : BtComposite
    {
        /// <summary>優先順位を設定</summary>
        /// <param name="name">ノード名</param>
        /// <param name="children">高優先から並べた子</param>
        public BtSelector(string name, params BtNode[] children) : base(name, children) { }

        /// <summary>最初に成功または実行中となった子を選択</summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>選択された子の結果</returns>
        protected override BtStatus Evaluate(float deltaTime)
        {
            for (int i = 0; i < Children.Length; i++)
            {
                BtStatus status = Children[i].Tick(deltaTime);
                if (status == BtStatus.Failure) { Children[i].Reset(); continue; }
                if (ActiveIndex >= 0 && ActiveIndex != i) Children[ActiveIndex].Reset();
                ActiveIndex = i;
                return status;
            }
            ActiveIndex = -1;
            return BtStatus.Failure;
        }
    }

    /// <summary>実行中の子を記憶して順に進める</summary>
    public sealed class BtSequence : BtComposite
    {
        private int _next;

        /// <summary>実行順序を設定</summary>
        /// <param name="name">ノード名</param>
        /// <param name="children">実行順の子</param>
        public BtSequence(string name, params BtNode[] children) : base(name, children) { }

        /// <summary>実行中の子から再開</summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>列全体の実行結果</returns>
        protected override BtStatus Evaluate(float deltaTime)
        {
            while (_next < Children.Length)
            {
                ActiveIndex = _next;
                BtStatus status = Children[_next].Tick(deltaTime);
                if (status == BtStatus.Running) return status;
                if (status == BtStatus.Failure) { Reset(); return status; }
                Children[_next++].Reset();
                // 同じフレームの時間を次の行動で二重計上しない
                deltaTime = 0f;
            }
            _next = 0;
            return BtStatus.Success;
        }

        /// <summary>列を先頭へ戻す</summary>
        public override void Reset() { base.Reset(); _next = 0; }
    }

    /// <summary>状態の条件を評価</summary>
    public sealed class BtCondition : BtNode
    {
        private readonly Func<bool> _condition;
        /// <summary>条件を保持</summary>
        /// <param name="name">ノード名</param>
        /// <param name="condition">評価する条件</param>
        public BtCondition(string name, Func<bool> condition) : base(name) => _condition = condition;
        /// <summary>条件の成立を返す</summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>成立時Success</returns>
        protected override BtStatus Evaluate(float deltaTime) => _condition() ? BtStatus.Success : BtStatus.Failure;
    }

    /// <summary>構築時に登録した行動を実行</summary>
    public sealed class BtAction : BtNode
    {
        private readonly Func<float, BtStatus> _action;
        private readonly Action _reset;
        /// <summary>行動と中断処理を保持</summary>
        /// <param name="name">ノード名</param>
        /// <param name="action">実行する行動</param>
        /// <param name="reset">中断時の初期化</param>
        public BtAction(string name, Func<float, BtStatus> action, Action reset = null) : base(name)
        { _action = action; _reset = reset; }
        /// <summary>行動を実行</summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>行動の結果</returns>
        protected override BtStatus Evaluate(float deltaTime) => _action(deltaTime);
        /// <summary>行動固有の進捗を破棄</summary>
        public override void Reset() => _reset?.Invoke();
    }

    /// <summary>単一の子へ条件や時間制約を付ける基底</summary>
    public abstract class BtDecorator : BtNode
    {
        protected readonly BtNode Child;
        public override string ActivePath => Child.ActivePath;
        /// <summary>装飾する子を保持</summary>
        /// <param name="name">ノード名</param>
        /// <param name="child">子ノード</param>
        protected BtDecorator(string name, BtNode child) : base(name) { Child = child; Attach(Child); }
        /// <summary>装飾する子へ評価時刻を共有</summary>
        /// <param name="clock">共有する時計</param>
        protected override void SetClock(BtClock clock) { base.SetClock(clock); Attach(Child); }
        /// <summary>子のパスを構築</summary>
        /// <param name="parentPath">親のパス</param>
        public override void BindPath(string parentPath) { base.BindPath(parentPath); Child.BindPath(Path); }
        /// <summary>子の実行を中断</summary>
        public override void Reset() => Child.Reset();
    }

    /// <summary>記憶型Sequenceの実行中も毎フレーム前提条件を確認</summary>
    public sealed class BtGuard : BtDecorator
    {
        private readonly Func<bool> _condition;
        /// <summary>継続条件を設定</summary>
        /// <param name="name">ノード名</param>
        /// <param name="condition">継続条件</param>
        /// <param name="child">対象の行動</param>
        public BtGuard(string name, Func<bool> condition, BtNode child) : base(name, child) => _condition = condition;
        /// <summary>前提条件が失われたら中断</summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>子の結果またはFailure</returns>
        protected override BtStatus Evaluate(float deltaTime)
        {
            if (_condition()) return Child.Tick(deltaTime);
            Child.Reset();
            return BtStatus.Failure;
        }
    }

    /// <summary>解放済みの行動だけを実行</summary>
    public sealed class BtGate : BtDecorator
    {
        private readonly EnemyBehaviorFlags _flag;
        private readonly Func<EnemyBehaviorFlags> _unlocked;
        /// <summary>解放条件を設定</summary>
        /// <param name="flag">必要なフラグ</param>
        /// <param name="child">解放する行動</param>
        /// <param name="unlocked">現在の解放済みフラグ</param>
        public BtGate(EnemyBehaviorFlags flag, BtNode child, Func<EnemyBehaviorFlags> unlocked)
            : base("Gate(" + flag + ")", child) { _flag = flag; _unlocked = unlocked; }
        /// <summary>未解放なら次の候補へ進む</summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>子の結果またはFailure</returns>
        protected override BtStatus Evaluate(float deltaTime)
        {
            if ((_unlocked() & _flag) == _flag) return Child.Tick(deltaTime);
            Child.Reset();
            return BtStatus.Failure;
        }
    }

    /// <summary>行動の完了後に再実行の待ち時間を設ける</summary>
    public sealed class BtCooldown : BtDecorator
    {
        private readonly float _seconds;
        private float _nextTime;
        /// <summary>待ち時間を設定</summary>
        /// <param name="seconds">再実行までの秒数</param>
        /// <param name="child">対象の行動</param>
        public BtCooldown(float seconds, BtNode child) : base("Cooldown", child) => _seconds = seconds;
        /// <summary>待ち時間経過後だけ実行</summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>子の結果またはFailure</returns>
        protected override BtStatus Evaluate(float deltaTime)
        {
            if (Time < _nextTime) return BtStatus.Failure;
            BtStatus status = Child.Tick(deltaTime);
            if (status != BtStatus.Running) { _nextTime = Time + _seconds; Child.Reset(); }
            return status;
        }
    }

    /// <summary>行動全体の実行時間を制限</summary>
    public sealed class BtTimeLimit : BtDecorator
    {
        private readonly float _seconds;
        private readonly Action _expired;
        private float _elapsed;
        /// <summary>時間制限を設定</summary>
        /// <param name="seconds">実行可能な秒数</param>
        /// <param name="child">制限する行動</param>
        /// <param name="expired">時間切れ時の処理</param>
        public BtTimeLimit(float seconds, BtNode child, Action expired = null) : base("TimeLimit", child)
        { _seconds = seconds; _expired = expired; }
        /// <summary>制限時間内で子を実行</summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>時間切れならFailure</returns>
        protected override BtStatus Evaluate(float deltaTime)
        {
            _elapsed += deltaTime;
            if (_elapsed >= _seconds) { _expired?.Invoke(); Reset(); return BtStatus.Failure; }
            BtStatus status = Child.Tick(deltaTime);
            if (status != BtStatus.Running) Reset();
            return status;
        }
        /// <summary>時間と子の状態を初期化</summary>
        public override void Reset() { base.Reset(); _elapsed = 0f; }
    }
}
