using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

// 리소스 로드/생성/제거 담당 - 캐시 기반 동기 로드와 Addressables 비동기 로드 지원
public class ResourceManager
{
    Dictionary<string, UnityEngine.Object> _resources = new Dictionary<string, UnityEngine.Object>(); // 캐시된 리소스

    #region Resource Load
    // 캐시에서 리소스 반환 (없으면 null, Addressables로 미리 로드돼 있어야 함)
    public T Load<T>(string key) where T : Object
    {
        if (_resources.TryGetValue(key, out Object resource))
        {
            return resource as T;
        }

        //스프라이트 로드할때 항상 .sprite가 붙어 있어야하는데 데이터시트에 .sprite가 붙어있지 않은 데이터가 많음
        if (typeof(T) ==  typeof(Sprite))
        {
            key = key + ".sprite";
            if (_resources.TryGetValue(key, out Object temp))
            {
                return temp as T;
            }
        }
        return null;
    }

    // 프리팹 생성 - pooling=true면 오브젝트 풀에서 꺼냄
    public GameObject Instantiate(string key, Transform parent = null, bool pooling = false)
    {
        GameObject prefab = Load<GameObject>($"{key}");
        if (prefab == null)
        {
            Debug.LogError($"Failed to load prefab : {key}");
            return null;
        }

        if (pooling)
            return Managers.Pool.Pop(prefab);

        GameObject go = Object.Instantiate(prefab, parent);
        go.name = prefab.name;

        return go;
    }

    // 오브젝트 제거 - 풀링 오브젝트면 반환, 아니면 Destroy
    public void Destroy(GameObject go)
    {
        if (go == null)
            return;

        if (Managers.Pool.Push(go))
            return;

        Object.Destroy(go);
    }
    #endregion

    #region Addressable
    // Addressables 비동기 로드 - 완료 시 캐시에 저장하고 callback 호출
    public void LoadAsync<T>(string key, Action<T> callback = null) where T : UnityEngine.Object
    {
        // 스프라이트인 경우 하위객체의 진짜 이름으로 로드하면 스프라이트로 로딩이 됨
        string loadKey = key;
        if (key.Contains(".sprite"))
            loadKey = $"{key}[{key.Replace(".sprite", "")}]";

        var asyncOperation = Addressables.LoadAssetAsync<T>(loadKey);
        asyncOperation.Completed += (op) =>
        {
            // 캐시 확인
            if (_resources.TryGetValue(key, out Object value))
            {
                callback?.Invoke(op.Result);
                return;
            }

            _resources.Add(key, op.Result);
            callback?.Invoke(op.Result);
        };
    }

    // Addressables 라벨 일괄 로드 - 진행상황(현재/전체)을 callback으로 전달
    public void LoadAllAsync<T>(string label, Action<string, int, int> callback) where T : Object
    {
        var opHandle = Addressables.LoadResourceLocationsAsync(label, typeof(T));
        opHandle.Completed += (op) =>
        {
            int loadCount = 0;
            int totalCount = op.Result.Count;

            if (totalCount == 0)
            {
                callback?.Invoke(string.Empty, 0, 0);
                return;
            }

            foreach (var result in op.Result)
            {
                if (result.PrimaryKey.Contains(".sprite"))
                {
                    LoadAsync<Sprite>(result.PrimaryKey, (obj) =>
                    {
                        loadCount++;
                        callback?.Invoke(result.PrimaryKey, loadCount, totalCount);
                    });
                }
                else
                {
                    LoadAsync<T>(result.PrimaryKey, (obj) =>
                    {
                        loadCount++;
                        callback?.Invoke(result.PrimaryKey, loadCount, totalCount);
                    });
                }
            }
        };
    }
    #endregion
}
