using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 몬스터 기본 공격 - 플레이어를 향해 원형 범위 공격을 실행하는 시퀀스 스킬
public class BasicAttack : SequenceSkill
{
    private void Awake()
    {
        SkillType = Define.SkillType.BasicAttack;
    }

    public override void OnChangedSkillData()
    {
    }

    // Skill 상태일 때만 실행, 범위 내 타겟이 있을 때만 공격
    public override void DoSkill(Action callback = null)
    {
        CreatureController owner = GetComponent<CreatureController>();
        if (owner.CreatureState != Define.CreatureState.Skill)
            return;

        UpdateSkillData(DataId);

        // 가장 가까운 타겟이 범위 밖이면 스킵
        MonsterController mc = GetComponent<MonsterController>();
        CreatureController target = mc != null ? mc.GetNearestAllyTarget() : null;
        if (target == null)
        {
            callback?.Invoke();
            return;
        }

        float dist = Vector3.Distance(transform.position, target.CenterPosition);
        if (dist > SkillData.ProjectileRange + 1f)
        {
            _coroutine = null;
            _coroutine = StartCoroutine(CoMoveToTarget(target, callback));
            return;
        }

        _coroutine = null;
        _coroutine = StartCoroutine(CoSkill(callback));
    }

    Coroutine _coroutine;

    // 타겟 방향으로 이동 후 다음 스킬로
    IEnumerator CoMoveToTarget(CreatureController target, Action callback = null)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        float moveTime = 4f;
        float elapsed = 0f;
        SetAniState(AniState.Idle);

        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            if (target.IsValid() == false)
                break;

            Vector2 dir = ((Vector2)target.CenterPosition - rb.position).normalized;
            rb.MovePosition(rb.position + dir * Owner.MoveSpeed * Time.fixedDeltaTime);

            float dist = Vector3.Distance(transform.position, target.CenterPosition);
            if (dist <= SkillData.ProjectileRange)
                break;

            yield return new WaitForFixedUpdate();
        }

        rb.linearVelocity = Vector2.zero;
        callback?.Invoke();
    }

    // 범위 가이드라인 표시 → 애니메이션 재생 → 범위 내 플레이어 데미지 → 히트 이펙트
    IEnumerator CoSkill(Action callback = null)
    {
        // 스킬 범위 가이드라인 표시
        GameObject obj = Managers.Resource.Instantiate("SkillRange", pooling: true);
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;
        SkillRange sr = obj.GetComponent<SkillRange>();
        float radius = SkillData.ProjectileRange;
        float wait = sr.SetCircle(radius * 2);
        SetAniState(AniState.Attack1);
        yield return new WaitForSeconds(wait);
        
        Managers.Resource.Destroy(obj);
        
        // 플레이어랑 나랑 거리가 radius 이하면 대미지 주기
        // 1. 타겟 콜라이더 반지름



        float targetRadus = 1;

        // 플레이어+동료 중 범위 안에 있는 대상 전부 데미지
        float damage = Owner.Atk * SkillData.DamageMultiplier;
        PlayerController pc = Managers.Object.Player;
        if (pc.IsValid() && Vector3.Distance(transform.position, pc.CenterPosition) < radius + targetRadus)
            pc.OnDamaged(Owner, this, damage);
        foreach (var companion in Managers.Object.Companions)
        {
            if (companion.IsValid() && Vector3.Distance(transform.position, companion.CenterPosition) < radius + targetRadus)
                companion.OnDamaged(Owner, this, damage);
        }

        // Hit Effect
        GameObject HitEffectObj = Managers.Resource.Instantiate("BossSmashHitEffect", pooling: true);
        HitEffectObj.transform.SetParent(transform);
        HitEffectObj.transform.localPosition = Vector3.zero;
        HitEffectObj.transform.localScale = Vector3.one * radius * 0.3f;
        yield return new WaitForSeconds(0.7f);
        Managers.Resource.Destroy(HitEffectObj);

        // 애니메이션 끝날 때까지 대기 후 Idle
        yield return null;
        if (Owner.Anim != null)
            yield return new WaitForSeconds(Owner.Anim.GetCurrentAnimatorStateInfo(0).length);

        SetAniState(AniState.Idle);

        yield return new WaitForSeconds(SkillData.AttackInterval);

        callback?.Invoke();
    }
}
