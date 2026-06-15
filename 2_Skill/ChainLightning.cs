using Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 몬스터들 사이를 연쇄적으로 튀어다니는 번개 스킬
public class ChainLightning : RepeatSkill
{
    private void Awake()
    {
        SkillType = Define.SkillType.ChainLightning;
    }

    // 체인 번개 코루틴 시작
    protected override void DoSkillJob()
    {
        StartCoroutine(CoChainLightning());
    }

    // 투사체 수만큼 체인 번개 발사 - 각 번개마다 체인 대상 목록 생성 후 순차 투사체 발사
    IEnumerator CoChainLightning()
    {
        string prefabName = SkillData.PrefabName;

        if (Managers.Game.Player != null)
        {
            for (int i = 0; i < SkillData.ProjectileCount; i++)
            {
                Vector3 startPos = Managers.Game.Player.CenterPosition;
                int minDist = (int)SkillData.BounceDist - 1;
                int maxDist = (int)SkillData.BounceDist + 1;
                List<MonsterController> targets = GetChainMonsters(SkillData.BounceCount, minDist, maxDist, index : i);
                if (targets == null)
                    continue;
                for (int j = 0; j < targets.Count; j++)
                {
                    if (j > 0)
                        startPos = targets[j - 1].CenterPosition;
                    Vector3 dir = (targets[j].CenterPosition - startPos).normalized;
                    GenerateProjectile(Managers.Game.Player, prefabName, startPos, dir, targets[j].CenterPosition, this);
                }
                yield return null;
            }
        }
    }

    // 체인 번개 대상 목록 생성 - 시작 몬스터부터 근접 몬스터 순서로 체인 구성
    public List<MonsterController> GetChainMonsters(int numTargets, float minDistance, float maxDistance, float angleRange = 180, int index = 0)
    {
        List<MonsterController> chainMonsters = new List<MonsterController>();
        // ProjectileRange 이상의 몬스터만 검색
        List<MonsterController> nearestMonster = Managers.Object.GetNearestMonsters(SkillData.ProjectileCount, (int)SkillData.ProjectileRange);
        if (nearestMonster != null)
        {
            int idx = Mathf.Min(index, nearestMonster.Count-1);
            chainMonsters.Add(nearestMonster[idx]);

            for (int i = 1; i < numTargets; i++)
            {
                MonsterController chainMonster = GetChainMonster(chainMonsters[i - 1].transform.position, minDistance, maxDistance, angleRange, chainMonsters);
                if (chainMonster != null)
                {
                    chainMonsters.Add(chainMonster);
                }
                else
                {
                    break;
                }
            }
        }

        return chainMonsters;
    }

    // 이전 몬스터 위치에서 거리 범위 내 다음 체인 대상 탐색 (이미 체인된 몬스터 제외)
    public MonsterController GetChainMonster(Vector3 origin, float minDistance, float maxDistance, float angleRange, List<MonsterController> ignoreMonsters)
    {
        LayerMask targetLayer = LayerMask.GetMask("Monster", "Boss");
        Collider2D[] targets = Physics2D.OverlapCircleAll(origin, maxDistance, targetLayer);

        float closestDistance = Mathf.Infinity;
        MonsterController closestMonster = null;
        foreach (Collider2D target in targets)
        {
            if (ignoreMonsters.Contains(target.GetComponent<MonsterController>()))
            {
                continue;
            }

            Vector3 targetPosition = target.transform.position;
            float distance = Vector3.Distance(origin, targetPosition);
            if (distance >= minDistance && distance <= maxDistance && distance < closestDistance)
            {
                closestDistance = distance;
                closestMonster = target.GetComponent<MonsterController>();
            }
        }

        return closestMonster;
    }
}
