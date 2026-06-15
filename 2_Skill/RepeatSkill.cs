using System.Collections;
using UnityEngine;

// 쿨타임마다 반복 실행되는 스킬의 추상 기반 클래스
public abstract class RepeatSkill : SkillBase
{
    public float CoolTime { get; set; } = 1.0f; // 스킬 쿨타임

    #region CoSkill
    Coroutine _coSkill; // 스킬 반복 코루틴 참조

    // 기존 코루틴 중단 후 새 스킬 반복 코루틴 시작
    public override void ActivateSkill()
    {
        base.ActivateSkill();

        if (_coSkill != null)
            StopCoroutine(_coSkill);

        gameObject.SetActive(true);
        _coSkill = StartCoroutine(CoStartSkill());
    }

    // 코루틴 중단 및 비활성화
    public virtual void DeactivateSkill()
    {
        if (_coSkill != null)
        {
            StopCoroutine(_coSkill);
            _coSkill = null;
        }
    }

    // 각 반복마다 실행할 스킬 동작 (자식 클래스에서 구현)
    protected abstract void DoSkillJob();

    // 쿨타임마다 DoSkillJob을 반복 실행하는 코루틴
    protected virtual IEnumerator CoStartSkill()
    {
        WaitForSeconds wait = new WaitForSeconds(SkillData.CoolTime);

        yield return wait;

        while (true)
        {
            if (SkillData.CoolTime != 0)
                Managers.Sound.Play(Define.Sound.Effect, SkillData.CastingSound);
            if (Owner != null)
                Owner.CreatureState = Define.CreatureState.Attack;
            DoSkillJob();

            yield return wait;
        }
    }
    #endregion
}
