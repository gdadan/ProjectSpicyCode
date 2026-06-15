using Data;
using System;
using UnityEngine;
using static Define;

// 모든 스킬의 기반 클래스 - 스킬 타입, 레벨, 데이터를 관리
public class SkillBase : BaseController
{
    public CreatureController Owner { get; set; } // 스킬 시전자

    SkillType _skillType;
    public SkillType SkillType
    {
        get { return _skillType; }
        set { _skillType = value; }
    }

    int _level = 0;
    public int Level
    {
        get { return _level; }
        set { _level = value; }
    }

    SkillData _skillData;
    public SkillData SkillData
    {
        get { return _skillData; }
        set { _skillData = value; }
    }

    public float TotalDamage { get; set; } = 0; // 결과창/일시정지용 누적 데미지
    public bool IsLearnedSkill { get { return Level > 0; } } // 습득 여부 (Level > 0)

    // 현재 레벨에 맞는 SkillData를 딕셔너리에서 로드
    // Level 1 → SkillType 기본 ID, Level 2 이상 → ID + (Level - 1)
    public SkillData UpdateSkillData(int dataId = 0)
    {
        int id = 0;

        if (dataId == 0)
            id = Level < 2 ? (int)SkillType : (int)SkillType + Level - 1;
        else
            id = dataId;

        if (Managers.Data.SkillDic.TryGetValue(id, out SkillData skillData) == false)
        {
            Debug.LogError($"Invalid skillData: id={id}, SkillType={SkillType}, Level={Level}");
            return SkillData;
        }

        SkillData = skillData;
        OnChangedSkillData();
        return SkillData;
    }

    // 스킬 데이터 변경 시 호출 (각 스킬에서 오버라이드해서 비주얼 갱신 등 처리)
    public virtual void OnChangedSkillData() { }

    // 레벨 0 → 1이 될 때만 호출 (스킬 활성화)
    public virtual void ActivateSkill()
    {
        UpdateSkillData();
    }

    // 레벨업 처리 - Level 0이면 먼저 ActivateSkill 호출 후 레벨 증가
    public virtual void OnLevelUp()
    {
        if (Level == 0)
            ActivateSkill();
        Level++;
        UpdateSkillData();
    }

    // 투사체 생성 - startPos에서 dir 방향으로 발사 또는 targetPos 위치에 낙하
    protected virtual void GenerateProjectile(CreatureController Owner, string prefabName, Vector3 startPos, Vector3 dir, Vector3 targetPos, SkillBase skill)
    {
        // 타겟 방향으로 스파인 플립
        if (Owner != null)
            Owner.FaceDirection(dir.x);

        ProjectileController pc = Managers.Object.Spawn<ProjectileController>(startPos, prefabName: prefabName);
        pc.SetInfo(Owner, startPos, dir, targetPos, skill);
    }
}
