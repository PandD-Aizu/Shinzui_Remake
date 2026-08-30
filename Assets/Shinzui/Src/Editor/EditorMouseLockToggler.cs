using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Shinzui.Editor
{
    [InitializeOnLoad]
    public static class EditorMouseLockToggler
    {
        static EditorMouseLockToggler()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // プレイモード開始時に監視用GameObjectを作成
                var go = new GameObject("EditorMouseLockTogglerHelper");
                go.hideFlags = HideFlags.HideAndDontSave; // ヒエラルキーに表示せず、シーン保存もさせない
                go.AddComponent<EditorMouseLockTogglerHelper>();
            }
        }
    }

    internal class EditorMouseLockTogglerHelper : MonoBehaviour
    {
        private bool? lastAltState = null;

        private void Update()
        {
            if (SceneManager.GetActiveScene().name == "Title") return;
            
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
        private void ApplyCursorLock(bool lockCursor)
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

        private void OnDestroy()
        {
            // 破棄されるときはカーソルロックを安全に解除しておく
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
