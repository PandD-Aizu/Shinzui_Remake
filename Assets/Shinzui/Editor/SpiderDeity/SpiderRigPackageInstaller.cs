using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Shinzui.Editor.SpiderDeity
{
    /// <summary>Explicit, headless-safe installation; does not run on normal Editor startup.</summary>
    public static class SpiderRigPackageInstaller
    {
        private static AddRequest request;
        private static double deadline;
        public static void Install()
        {
            request = Client.Add("com.unity.animation.rigging@1.4.1");
            deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update += Poll;
        }
        private static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            if (request.IsCompleted && request.Status == StatusCode.Success)
            {
                Debug.Log("SPIDER_RIG_PACKAGE_INSTALLED " + request.Result.packageId);
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError(request.Error?.message ?? "Animation Rigging installation timed out.");
                EditorApplication.Exit(1);
            }
        }
    }
}
