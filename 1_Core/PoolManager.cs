using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// 단일 프리팹에 대한 오브젝트 풀 - Unity ObjectPool<GameObject> 래핑
class Pool
{
    GameObject _prefab;
    IObjectPool<GameObject> _pool;

    Transform _root; // 풀링된 오브젝트들의 부모 Transform
    Transform Root
    {
        get
        {
            if (_root == null)
            {
                GameObject go = new GameObject() { name = $"{_prefab.name}Pool" };
                _root = go.transform;
            }

            return _root;
        }
    }

    public Pool(GameObject prefab)
    {
        _prefab = prefab;
        _pool = new ObjectPool<GameObject>(OnCreate, OnGet, OnRelease, OnDestroy);
    }
    
    // 오브젝트를 풀에 반환 (비활성화)
    public void Push(GameObject go)
    {
        if (go.activeSelf)
            _pool.Release(go);
    }

    // 풀에서 오브젝트를 꺼냄 (없으면 새로 생성)
    public GameObject Pop()
    {
        return _pool.Get();
    }

    #region Funcs
    GameObject OnCreate()
    {
        GameObject go = GameObject.Instantiate(_prefab);
        go.transform.SetParent(Root);
        go.name = _prefab.name;
        return go;
    } 

    void OnGet(GameObject go)
    {
        go.SetActive(true);
    }

    void OnRelease(GameObject go)
    {
        go.SetActive(false);
    }

    void OnDestroy(GameObject go)
    {
        GameObject.Destroy(go);
    }
    #endregion
}

// 프리팹 이름으로 Pool을 관리하는 매니저 - Pop/Push로 오브젝트 재사용
public class PoolManager
{
    Dictionary<string, Pool> _pools = new Dictionary<string, Pool>(); // 프리팹명 → Pool 매핑

    // 해당 프리팹의 풀에서 오브젝트를 꺼냄 (풀이 없으면 새로 생성)
    public GameObject Pop(GameObject prefab)
    {
        if (_pools.ContainsKey(prefab.name) == false)
            CreatePool(prefab);

        return _pools[prefab.name].Pop();
    }

    // 오브젝트를 해당 프리팹 풀에 반환, 등록되지 않은 오브젝트면 false 반환
    public bool Push(GameObject go)
    {
        if (_pools.ContainsKey(go.name) == false)
            return false;

        _pools[go.name].Push(go);
        return true;
    }

    public void Clear()
    {
        _pools.Clear();
    }

    void CreatePool(GameObject original)
    {
        Pool pool = new Pool(original);
        _pools.Add(original.name, pool);
    }
}
