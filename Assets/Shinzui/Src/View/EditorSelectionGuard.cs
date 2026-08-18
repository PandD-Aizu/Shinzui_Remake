using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shinzui.View
{
    internal static class EditorSelectionGuard
    {
        public static void ClearIfSelected(GameObject target)
        {
#if UNITY_EDITOR
            if (target == null)
            {
                return;
            }

            foreach (var selected in Selection.gameObjects)
            {
                if (selected == null)
                {
                    continue;
                }

                if (selected == target || selected.transform.IsChildOf(target.transform))
                {
                    Selection.activeObject = null;
                    return;
                }
            }
#endif
        }
    }
}
