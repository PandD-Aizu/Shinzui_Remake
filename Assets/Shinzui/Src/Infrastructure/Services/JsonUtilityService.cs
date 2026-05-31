using System;
using Cysharp.Threading.Tasks;
using Shinzui.Application.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Shinzui.Infrastructure.Services
{
    public class JsonUtilityService : IJsonUtilityService
    {
        /// <inheritdoc />
        public async UniTask<T> ConvertJsonToAnyObjectAsync<T>(string addressableJsonKey)
        {
            if (string.IsNullOrEmpty(addressableJsonKey))
            {
                Debug.LogError($"[JsonUtilityProvider] Invalid addressable JSON key: {addressableJsonKey}");
                return default;
            }

            var handle = Addressables.LoadAssetAsync<TextAsset>(addressableJsonKey);
            var jsonAsset = await handle.Task;

            try
            {
                if (jsonAsset is null)
                {
                    Debug.LogError($"[JsonUtilityProvider] Failed to load JSON asset with key: {addressableJsonKey}");
                    return default;
                }

                var result = JsonUtility.FromJson<T>(jsonAsset.text);
                if (result is null)
                {
                    Debug.LogError($"[JsonUtilityProvider] Failed to parse JSON from asset with key: {addressableJsonKey}");
                    return default;
                }

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonUtilityProvider] Exception occurred while converting JSON to object with key: {addressableJsonKey}");
                Debug.LogError(e);
                return default;
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        /// <inheritdoc />
        public string ConvertAnyObjectToJsonAsync<T>(T obj, bool prettyPrint = false)
        {
            if (obj is null)
            {
                Debug.LogError($"[JsonUtilityProvider] Cannot convert null object to JSON.");
                return string.Empty;
            }
            
            string result = JsonUtility.ToJson(obj, prettyPrint);
            return result;
        }

        /// <inheritdoc />
        public T ConvertRawJsonToAnyObject<T>(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("[JsonUtilityProvider] Cannot parse empty JSON text.");
                return default;
            }

            try
            {
                var result = JsonUtility.FromJson<T>(jsonText);
                if (result == null)
                {
                    Debug.LogError("[JsonUtilityProvider] Failed to parse raw JSON text.");
                    return default;
                }

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError("[JsonUtilityProvider] Exception occurred while converting raw JSON text to object.");
                Debug.LogError(e);
                return default;
            }
        }
    }
}