using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Define;

// 오브젝트 생성/제거 및 탐색 담당 - Spawn/Despawn, 몬스터 조회, 데미지 폰트 표시
public class ObjectManager
{
    public PlayerController Player { get; private set; }                              // 플레이어 참조
    public HashSet<MonsterController> Monsters { get; } = new HashSet<MonsterController>();       // 활성 몬스터 집합
    public HashSet<ProjectileController> Projectiles { get; } = new HashSet<ProjectileController>(); // 활성 투사체 집합
    public HashSet<GoldController> Golds { get; } = new HashSet<GoldController>();                // 활성 골드 집합
    public List<CompanionController> Companions { get; } = new List<CompanionController>();       // 활성 동료 목록

    public void Clear()
    {
        Monsters.Clear();
        Projectiles.Clear();
        Golds.Clear();
        Companions.Clear();
    }

    // 맵 프리팹을 Instantiate하고 Map.Init() 호출
    public void LoadMap(string mapName)
    {
        // 기존 맵 제거
        DestroyCurrentMap();

        GameObject objMap = Managers.Resource.Instantiate(mapName);
        objMap.transform.position = Vector3.zero;
        objMap.name = "@Map";

        objMap.GetComponent<Map>().Init();
    }

    // 현재 맵 오브젝트 제거
    public void DestroyCurrentMap()
    {
        if (Managers.Game.CurrentMap != null)
        {
            Managers.Resource.Destroy(Managers.Game.CurrentMap.gameObject);
            Managers.Game.CurrentMap = null;
        }
    }

    // 데미지 폰트 표시 (치명타 여부, 아군 피격 여부에 따라 다른 프리팹 사용)
    public void ShowDamageFont(Vector2 pos, float damage, Transform parent, bool isCritical = false, bool isAlly = false)
    {
        string prefabName;
        if (isAlly)
            prefabName = "PlayerDamagedFont";
        else if (isCritical)
            prefabName = "CriticalDamageFont";
        else
            prefabName = "DamageFont";

        GameObject go = Managers.Resource.Instantiate(prefabName, pooling: true);
        DamageFont damageText = go.GetOrAddComponent<DamageFont>();
        damageText.SetInfo(pos, damage, parent, isCritical, isAlly);
    }

    // 타입에 따라 오브젝트 생성 및 초기화 (Player/Monster/Boss/Projectile/Gold)
    public T Spawn<T>(Vector3 position, int templateID = 0, string prefabName = "") where T : BaseController
    {
        System.Type type = typeof(T);

        if (type == typeof(PlayerController))
        {
            Data.CharacterData cd = Managers.Data.CharacterDic[templateID];
            GameObject go = Managers.Resource.Instantiate(cd.PrefabName);
            go.transform.position = position;
            PlayerController pc = go.GetOrAddComponent<PlayerController>();
            pc.SetInfo(templateID, CreatureController.CreatureDataType.Character);
            Player = pc;
            Managers.Game.Player = pc;

            pc.ApplyEnhancePassives();
            return pc as T;
        }
        else if (type == typeof(MonsterController))
        {
            Data.MonsterData md = Managers.Data.MonsterDic[templateID];
            GameObject go = Managers.Resource.Instantiate(md.PrefabName, pooling: true);
            MonsterController mc = go.GetOrAddComponent<MonsterController>();
            go.transform.position = position;
            mc.SetInfo(templateID, CreatureController.CreatureDataType.Monster);
            go.name = md.PrefabName;
            Monsters.Add(mc);

            return mc as T;
        }
        else if (type == typeof(BossController))
        {
            Data.MonsterData md = Managers.Data.MonsterDic[templateID];
            GameObject go = Managers.Resource.Instantiate(md.PrefabName, pooling: true);
            BossController bc = go.GetOrAddComponent<BossController>();
            go.transform.position = position;
            bc.SetInfo(templateID, CreatureController.CreatureDataType.Monster);
            go.name = md.PrefabName;
            Monsters.Add(bc);

            return bc as T;
        }
        else if (type == typeof(ProjectileController))
        {
            GameObject go = Managers.Resource.Instantiate(prefabName, pooling: true);
            ProjectileController pc = go.GetOrAddComponent<ProjectileController>();
            go.transform.position = position;
            Projectiles.Add(pc);

            return pc as T;
        }
        else if (type == typeof(GoldController))
        {
            GameObject go = Managers.Resource.Instantiate("Gold", pooling: true);
            GoldController gc = go.GetOrAddComponent<GoldController>();
            go.transform.position = position;
            gc.SetInfo();
            Golds.Add(gc);

            return gc as T;
        }
        else if (type == typeof(CompanionController))
        {
            Data.CharacterData cd = Managers.Data.CharacterDic[templateID];
            GameObject go = Managers.Resource.Instantiate(cd.PrefabName);
            CompanionController cc = go.GetOrAddComponent<CompanionController>();
            go.transform.position = position;
            cc.SetInfo(templateID, CreatureController.CreatureDataType.Character);
            go.name = cd.PrefabName;
            Companions.Add(cc);

            cc.ApplyEnhancePassives();
            return cc as T;
        }

        return null;
    }

    // 타입에 따라 오브젝트 제거 및 목록에서 삭제
    public void Despawn<T>(T obj) where T : BaseController
    {
        System.Type type = typeof(T);

        if (type == typeof(PlayerController))
        {
            // ?
        }
        else if (type == typeof(MonsterController))
        {
            Monsters.Remove(obj as MonsterController);
            Managers.Resource.Destroy(obj.gameObject);
        }
        else if (type == typeof(BossController))
        {
            Monsters.Remove(obj as MonsterController);
            Managers.Resource.Destroy(obj.gameObject);
        }
        else if (type == typeof(ProjectileController))
        {
            Projectiles.Remove(obj as ProjectileController);
            Managers.Resource.Destroy(obj.gameObject);
        }
        else if (type == typeof(GoldController))
        {
            Golds.Remove(obj as GoldController);
            Managers.Resource.Destroy(obj.gameObject);
        }

    }

    // 동료 제거
    public void DespawnCompanion(CompanionController companion)
    {
        Companions.Remove(companion);
        Managers.Resource.Destroy(companion.gameObject);
    }

    #region 몬스터 찾기
    // 플레이어와 가장 가까운 몬스터
    public List<MonsterController> GetNearestMonsters(int count = 1, int distanceThreshold = 0, bool isInCamera = true)
    {
        List<MonsterController> monsterList;

        if (isInCamera)
        {
            monsterList = Monsters.Where(monster => IsWithInCamera(Camera.main.WorldToViewportPoint(monster.CenterPosition)))
                .OrderBy(monster => (Player.CenterPosition - monster.CenterPosition).sqrMagnitude).ToList();
        }
        else
        {
            monsterList = Monsters.OrderBy(monster => (Player.CenterPosition - monster.CenterPosition).sqrMagnitude).ToList();
        }

        if (distanceThreshold > 0)
            monsterList = monsterList.Where(monster => (Player.CenterPosition - monster.CenterPosition).magnitude > distanceThreshold).ToList();

        if (monsterList.Count == 0) return null;

        int min = Mathf.Min(count, monsterList.Count);

        List<MonsterController> nearestMonsters = monsterList.Take(min).ToList();

        // 요소 개수가 count와 다른 경우 마지막 요소 반복해서 추가
        while (nearestMonsters.Count < count)
        {
            nearestMonsters.Add(nearestMonsters.Last());
        }

        return nearestMonsters;
    }

    public List<MonsterController> GetMonsters(int count = 1, bool isInCamera = true)
    {
        List<MonsterController> monsterList;
        if (isInCamera)
        {
            monsterList = Monsters.Where(monster => IsWithInCamera(Camera.main.WorldToViewportPoint(monster.CenterPosition))).ToList();
        }
        else
        {
            monsterList = Monsters.ToList();
        }


        monsterList.Shuffle();

        if (monsterList.Count == 0) return null;

        int min = Mathf.Min(count, monsterList.Count);

        List<MonsterController> randomMonsters = monsterList.Take(min).ToList();

        // 요소 개수가 count와 다른 경우 마지막 요소 반복해서 추가
        while (randomMonsters.Count < count)
        {
            randomMonsters.Add(randomMonsters.Last());
        }

        return randomMonsters;
    }

    //public List<MonsterController> GetMonsterWithinCamera(int count = 1)
    //{
    //    List<MonsterController> monsterList = Monsters.ToList().Where(monster => IsWithInCamera(Camera.main.WorldToViewportPoint(monster.CenterPosition)) == true).ToList();
    //    monsterList.Shuffle();

    //    if (monsterList.Count == 0) return null;

    //    int min = Mathf.Min(count, monsterList.Count);

    //    List<MonsterController> monsters = monsterList.Take(min).ToList();

    //    while (monsters.Count < count)
    //    {
    //        monsters.Add(monsters.Last());
    //    }

    //    return monsters;
    //}

    public List<Transform> GetFindMonstersInFanShape(Vector3 origin, Vector3 forward, float radius = 2, float angleRange = 80)
    {
        List<Transform> listMonster = new List<Transform>();
        LayerMask targetLayer = LayerMask.GetMask("Monster", "Boss");
        RaycastHit2D[] _targets = Physics2D.CircleCastAll(origin, radius, Vector2.zero, 0, targetLayer);

        // 타겟중에 부채꼴 안에 있는애만 리스트에 넣는다.
        foreach (RaycastHit2D target in _targets)
        {
            // '타겟-origin 벡터'와 '내 정면 벡터'를 내적
            float dot = Vector3.Dot((target.transform.position - origin).normalized, forward);
            // 두 벡터 모두 단위 벡터이므로 내적 결과에 cos의 역을 취해서 theta를 구함
            float theta = Mathf.Acos(dot);
            // angleRange와 비교하기 위해 degree로 변환
            float degree = Mathf.Rad2Deg * theta;
            // 시야각 판별
            if (degree <= angleRange / 2f)
                listMonster.Add(target.transform);
        }

        return listMonster;
    }
    #endregion

    // 모든 일반 몬스터 즉시 제거 및 화면 화이트 플래시 연출
    public void KillAllMonsters()
    {
        UI_GameScene scene = Managers.UI.SceneUI as UI_GameScene;

        if (scene != null)
            scene.DoWhiteFlash();

        foreach (MonsterController monster in Monsters.ToList())
        {
            if (monster.ObjectType == ObjectType.Monster || monster.ObjectType == ObjectType.Boss)
                monster.KillMonster();
        }
        DespawnAllMonsterProjectiles();
    }

    public void DespawnAllMonsterProjectiles()
    {
        foreach (ProjectileController proj in Projectiles.ToList())
        {
            if (proj.Owner is MonsterController)
                Despawn(proj);
        }
    }

    // 모든 골드를 일괄 수집 (타이머 주기 or 스테이지 종료 시 호출)
    public void CollectAllGolds()
    {
        foreach (GoldController gold in Golds.ToList())
        {
            gold.GetItem();
        }
    }

    // 뷰포트 좌표가 카메라 화면 안에 있는지 판별
    bool IsWithInCamera(Vector3 pos)
    {
        if (pos.x >= 0 && pos.x <= 1 && pos.y >= 0 && pos.y <= 1)
            return true;
        return false;
    }

}
