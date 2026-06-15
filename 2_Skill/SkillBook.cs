using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using static Define;

// 스킬 목록 관리 - 스킬 추가, 레벨업, 장착/해제, 조회 기능 제공
public class SkillBook : MonoBehaviour
{
    public const int MAX_EQUIP_COUNT = 4; // 최대 장착 가능 스킬 수

    int _sequenceIndex = 0;

    [SerializeField]
    List<SkillBase> _skillList = new List<SkillBase>(); // 등록된 모든 스킬 목록
    public List<SkillBase> SkillList { get { return _skillList; } }
    public List<SequenceSkill> SequenceSkills { get; } = new List<SequenceSkill>(); // 등록된 시퀀스 스킬 목록

    // 덱 슬롯 배열 (index 0~3, SkillType.None = 비어있음)
    SkillType[] _deckSlots = new SkillType[MAX_EQUIP_COUNT];
    public SkillType[] DeckSlots => _deckSlots;

    // 습득된(Level > 0) 스킬만 필터링해서 반환
    public List<SkillBase> ActivatedSkills
    {
        get { return SkillList.Where(skill => skill.IsLearnedSkill).ToList(); }
    }

    [SerializeField]
    public Dictionary<Define.SkillType, int> SavedBattleSkill = new Dictionary<SkillType, int>(); // 전투 중 스킬 타입별 레벨 저장 딕셔너리

    // 스킬 장착 여부 확인
    public bool IsEquipped(SkillType skillType)
    {
        if (skillType == SkillType.None) return false;
        for (int i = 0; i < _deckSlots.Length; i++)
            if (_deckSlots[i] == skillType) return true;
        return false;
    }

    // 특정 슬롯의 스킬 반환
    public SkillType GetDeckSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MAX_EQUIP_COUNT)
            return SkillType.None;
        return _deckSlots[slotIndex];
    }

    // 특정 덱 슬롯에 스킬 장착 (교체 지원)
    // 반환: 교체된 기존 스킬 타입 (없으면 None)
    public SkillType EquipSkillToSlot(SkillType skillType, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MAX_EQUIP_COUNT)
            return SkillType.None;

        SkillBase skill = GetSkill(skillType);
        if (skill == null || !skill.IsLearnedSkill)
            return SkillType.None;

        // 이미 다른 슬롯에 같은 스킬이 있으면 기존 슬롯 비우기
        for (int i = 0; i < _deckSlots.Length; i++)
        {
            if (_deckSlots[i] == skillType)
            {
                _deckSlots[i] = SkillType.None;
                break;
            }
        }

        // 기존 슬롯의 스킬 비활성화
        SkillType prevType = _deckSlots[slotIndex];
        if (prevType != SkillType.None)
        {
            SkillBase prevSkill = GetSkill(prevType);
            if (prevSkill is RepeatSkill prevRepeat)
                prevRepeat.DeactivateSkill();
        }

        // 새 스킬 장착 및 활성화
        _deckSlots[slotIndex] = skillType;
        if (skill is RepeatSkill repeatSkill)
            repeatSkill.ActivateSkill();

        return prevType;
    }

    // 스킬 장착 해제 - 해당 슬롯 비우기
    public bool UnequipSkill(SkillType skillType)
    {
        for (int i = 0; i < _deckSlots.Length; i++)
        {
            if (_deckSlots[i] == skillType)
            {
                _deckSlots[i] = SkillType.None;
                SkillBase skill = GetSkill(skillType);
                if (skill is RepeatSkill repeatSkill)
                    repeatSkill.DeactivateSkill();
                return true;
            }
        }
        return false;
    }

    // 스킬 타입에 따라 스킬을 추가 (프리팹 기반 또는 컴포넌트 기반)
    public void AddSkill(Define.SkillType skillType, CreatureController owner, int skillId = 0)
    {
        // 풀 재활용 대응 - 이미 등록된 스킬이면 Owner만 갱신하고 종료
        // (이 가드 없으면 AddComponent가 매 호출마다 누적되어 코루틴이 다중 실행됨)
        SkillBase existing = GetSkill(skillType);
        if (existing != null)
        {
            existing.Owner = owner;
            return;
        }

        string className = skillType.ToString();

        // 프리팹을 자식 오브젝트로 생성하는 스킬들
        if (skillType == SkillType.FrozenHeart || skillType == SkillType.SavageSmash || skillType == SkillType.EletronicField
            || skillType == SkillType.MeleeSwing || skillType == SkillType.SmashWave)
        {
            GameObject go = Managers.Resource.Instantiate(skillType.ToString(), gameObject.transform);
            if (go != null)
            {
                SkillBase skill = go.GetComponent<SkillBase>();
                if (skill == null)
                    skill = go.AddComponent(Type.GetType(className)) as SkillBase;
                skill.Owner = owner;
                SkillList.Add(skill);
                if (SavedBattleSkill.ContainsKey(skillType))
                    SavedBattleSkill[skillType] = skill.Level;
                else
                    SavedBattleSkill.Add(skillType, skill.Level);
            }
        }
        else
        {
            // SequenceSkill은 AddComponent로 추가, RepeatSkill은 이미 프리팹에 붙어있으므로 GetComponent
            SequenceSkill skill = gameObject.AddComponent(Type.GetType(className)) as SequenceSkill;
            if (skill != null)
            {
                skill.Owner = GetComponent<CreatureController>();
                skill.DataId = skillId;
                skill.ActivateSkill();
                SkillList.Add(skill);
                SequenceSkills.Add(skill);
            }
            else
            {
                RepeatSkill skillbase = gameObject.GetComponent(Type.GetType(className)) as RepeatSkill;
                if (skillbase == null)
                {
                    Debug.LogError($"[SkillBook] RepeatSkill not found: {className}");
                    return;
                }
                skillbase.Owner = owner;
                SkillList.Add(skillbase);
                if (SavedBattleSkill.ContainsKey(skillType))
                    SavedBattleSkill[skillType] = skillbase.Level;
                else
                    SavedBattleSkill.Add(skillType, skillbase.Level);
            }
        }
    }

    // 해당 타입의 스킬 레벨업 및 저장 딕셔너리 갱신
    public void LevelUpSkill(Define.SkillType skillType)
    {
        for (int i = 0; i < SkillList.Count; i++)
        {
            if (SkillList[i].SkillType == skillType)
            {
                SkillList[i].OnLevelUp();
                if (SavedBattleSkill.ContainsKey(skillType))
                {
                    SavedBattleSkill[skillType] = SkillList[i].Level;
                }
            }
        }
    }

    // 스킬 타입으로 스킬 반환
    public SkillBase GetSkill(Define.SkillType skillType)
    {
        return SkillList.Find(s => s.SkillType == skillType);
    }

    public void StartNextSequenceSkill()
    {
        if (_stopped)
            return;
        if (SequenceSkills.Count == 0)
            return;

        Debug.Log($"[SkillBook] 스킬 시작 [{_sequenceIndex + 1}/{SequenceSkills.Count}] {SequenceSkills[_sequenceIndex].GetType().Name} (DataId: {SequenceSkills[_sequenceIndex].DataId})");
        SequenceSkills[_sequenceIndex].DoSkill(OnFinishedSequenceSkill);
    }

    void OnFinishedSequenceSkill()
    {
        Debug.Log($"[SkillBook] 스킬 완료 [{_sequenceIndex + 1}/{SequenceSkills.Count}] {SequenceSkills[_sequenceIndex].GetType().Name}");
        _sequenceIndex = (_sequenceIndex + 1) % SequenceSkills.Count;
        StartNextSequenceSkill();
    }

    bool _stopped = false;

    public void StopSkills()
    {
        _stopped = true;

        foreach (var skill in ActivatedSkills)
        {
            skill.StopAllCoroutines();
        }
    }

    // 풀링된 객체 재사용 시 상태 초기화 (보스 등) - _stopped/인덱스/등록 스킬과
    // 이전 사이클에서 AddComponent로 붙은 SequenceSkill 컴포넌트들도 정리
    public void ResetForReuse()
    {
        _stopped = false;
        _sequenceIndex = 0;

        foreach (var skill in SequenceSkills)
        {
            if (skill != null)
            {
                skill.StopAllCoroutines();
                UnityEngine.Object.Destroy(skill);
            }
        }
        SequenceSkills.Clear();
        SkillList.RemoveAll(s => s == null || s is SequenceSkill);
    }
}
