namespace Shinzui.Src.Title
{
    /// <summary>
    /// クレジット画面のデータとスクロール状態を管理するモデルクラス
    /// </summary>
    public class CreditScreenModel
    {
        private CreditData creditData;
        private float currentScrollPosition;
        private bool isScrolling;
        private float scrollTimer;
        
        public CreditData CreditData => creditData;
        public float CurrentScrollPosition => currentScrollPosition;
        public bool IsScrolling => isScrolling;
        public float ScrollTimer => scrollTimer;
        
        /// <summary>
        /// モデルを初期化する
        /// </summary>
        /// <param name="data">クレジット情報を含むScriptableObject</param>
        public void Initialize(CreditData data)
        {
            creditData = data;
            currentScrollPosition = 0f;
            isScrolling = false;
            scrollTimer = 0f;
        }
        
        /// <summary>
        /// スクロールを開始する
        /// </summary>
        public void StartScrolling()
        {
            isScrolling = true;
            scrollTimer = 0f;
        }
        
        /// <summary>
        /// スクロールを停止する
        /// </summary>
        public void StopScrolling()
        {
            isScrolling = false;
        }
        
        /// <summary>
        /// スクロール位置を更新する
        /// </summary>
        /// <param name="deltaTime">前フレームからの経過時間</param>
        public void UpdateScrollPosition(float deltaTime)
        {
            if (!isScrolling) return;
            
            scrollTimer += deltaTime;
            
            if (scrollTimer > creditData.delayBeforeStart)
            {
                currentScrollPosition += creditData.scrollSpeed * deltaTime;
            }
        }
        
        /// <summary>
        /// スクロール位置をリセットする
        /// </summary>
        public void ResetScroll()
        {
            currentScrollPosition = 0f;
            scrollTimer = 0f;
        }
        
        /// <summary>
        /// スクロールが終端に到達したか確認する
        /// </summary>
        /// <param name="maxScrollPosition">スクロール可能な最大位置</param>
        /// <returns>終端に到達した場合はtrue</returns>
        public bool HasReachedEnd(float maxScrollPosition)
        {
            return currentScrollPosition >= maxScrollPosition;
        }
    }
}