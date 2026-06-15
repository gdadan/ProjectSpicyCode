using Data;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Define;

// 랜덤 몬스터들을 향해 화면 밖에서 낙하하는 운석 스킬
public class Meteor : RepeatSkill
{
    private void Awake()
    {
        SkillType = Define.SkillType.Meteor;
    }

    public override void ActivateSkill()
    {
        base.ActivateSkill();
    }

    public override void OnChangedSkillData()
    {
    }

    // 랜덤 몬스터 위치를 향해 화면 밖에서 운석 투사체 생성
    IEnumerator GenerateMeteor()
    {
        List<MonsterController> targets = Managers.Object.GetMonsters(SkillData.ProjectileCount, true);
        if (targets == null)
            yield break;
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].IsValid() == true)
            { 
                Vector2 startPos = GetMeteorPosition(targets[i].CenterPosition);
                GenerateProjectile(Managers.Game.Player, SkillData.PrefabName, startPos, Vector3.zero, targets[i].CenterPosition, this);
                yield return new WaitForSeconds(SkillData.AttackInterval);
            }
        }
    }

    // 카메라 기준 화면 우상단 외부 생성 위치 계산
    public Vector2 GetMeteorPosition(Vector3 target)
    {
        float angleInRadians = 60f * Mathf.Deg2Rad;
        float spawnMargin = 1f;
        // 화면의 높이 절반
        float halfHeight = Camera.main.orthographicSize;
        // 화면의 너비 절반
        float halfWidth = Camera.main.aspect * halfHeight;

        float spawnX = target.x + (halfWidth + spawnMargin) * Mathf.Cos(angleInRadians);
        float spawnY = target.y + (halfHeight + spawnMargin) * Mathf.Sin(angleInRadians);
        Vector2 spawnPosition = new Vector2(spawnX, spawnY);

        return spawnPosition;
    }

    // 운석 생성 코루틴 시작
    Coroutine _coSkillJob;
    protected override void DoSkillJob()
    {
        if (_coSkillJob != null)
            StopCoroutine(_coSkillJob);
        _coSkillJob = StartCoroutine(GenerateMeteor());
    }
}
