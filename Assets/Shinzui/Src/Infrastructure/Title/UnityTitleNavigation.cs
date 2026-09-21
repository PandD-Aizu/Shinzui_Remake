using System;
using System.Threading.Tasks;
using Shinzui.Application.Title;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Shinzui.Infrastructure.Title
{
    public sealed class UnityTitleNavigation : ITitleNavigation
    {
        public Task LoadGameAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("The title game scene address is empty.", nameof(address));

            var completion = new TaskCompletionSource<bool>();
            var handle = Addressables.LoadSceneAsync(address, LoadSceneMode.Single);
            handle.Completed += operation =>
            {
                if (operation.Status == AsyncOperationStatus.Succeeded)
                {
                    // Addressables owns successful scene handles until the scene is unloaded.
                    completion.TrySetResult(true);
                }
                else
                {
                    var error = operation.OperationException ?? new InvalidOperationException($"Failed to load '{address}'.");
                    if (operation.IsValid()) Addressables.Release(operation);
                    completion.TrySetException(error);
                }
            };
            return completion.Task;
        }

        public Task LoadSceneAsync(string sceneName)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null) throw new InvalidOperationException($"Failed to load '{sceneName}'.");
            var completion = new TaskCompletionSource<bool>();
            operation.completed += _ => completion.TrySetResult(true);
            return completion.Task;
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
    }
}
