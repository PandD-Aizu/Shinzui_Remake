using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shinzui.Editor
{
    [InitializeOnLoad]
    public static class EditorMouseLockToggler
    {
        private static bool? lastAltState = null;

        static EditorMouseLockToggler()
        {
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            // プレイモード中のみ動作
            if (!EditorApplication.isPlaying)
            {
                lastAltState = null; // プレイモード終了時に状態をリセット
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // Altキーが押されているかどうか
                bool isAltPressed = keyboard.altKey.isPressed;

                // 状態が変化した瞬間、または起動直後の最初のフレームのみ実行
                if (lastAltState == null || isAltPressed != lastAltState.Value)
                {
                    lastAltState = isAltPressed;
                    ApplyCursorLock(!isAltPressed);
                }
            }
        }

        /// <summary>
        /// カーソルのロック状態を適用
        /// lockCursorがtrueの場合、カーソルをロックして非表示
        /// </summary>
        /// <param name="lockCursor"></param>
        private static void ApplyCursorLock(bool lockCursor)
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
