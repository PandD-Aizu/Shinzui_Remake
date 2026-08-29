using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shinzui.Src.Title
{
    /// <summary>
    /// クレジット画面のデータを管理するScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "CreditData", menuName = "CreditScreen/CreditData")]
    public class CreditData : UnityEngine.ScriptableObject
    {
        /// <summary>
        /// クレジットの各セクション情報を保持するクラス
        /// </summary>
        [Serializable]
        public class CreditSection
        {
            public string sectionTitle;
            public List<string> credits;
        }

        [Header("Credit Information")]
        public string gameTitle = "";
        
        [Header("Credit Sections")]
        public List<CreditSection> creditSections = new List<CreditSection>();
        
        [Header("Scroll Settings")]
        public float scrollSpeed = 50f;
        public float delayBeforeStart = 2f;
        public float delayAfterEnd = 3f;
        
        // 定数定義
        public const float DEFAULT_SCROLL_SPEED = 50f;
        public const float DEFAULT_DELAY_BEFORE_START = 2f;
        public const float DEFAULT_DELAY_AFTER_END = 3f;
    }
}