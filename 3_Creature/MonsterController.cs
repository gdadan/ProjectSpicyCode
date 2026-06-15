using DG.Tweening;
using System;
using System.Collections;
using UnityEngine;
using static Define;

public class MonsterController : CreatureController
{
    float _shrinkTime = 0.2f;
    float _dotDamageCoolTime = 1f;
    float _meleeDamageCoolTime = 1f; // 애니메이션 시간 생각

    AttackType AttackType; // 공격 방식 (Rush/Melee/Ranged)

    Vector3 _moveDir; // 플레이어 방향 이동 벡터

    Coroutine _coDotDamage; // Rush 타입 지속 데미지 코루틴
    Coroutine _coKnockback; // 넉백 코루틴
    Coroutine _coMeleeAttack; // 근접 공격 코루틴

    public event Action<MonsterController> BossInfoUpdate; // 보스일 때 UI 갱신 이벤트

    private void OnEnable()
    {
        if (DataId != 0)
            SetInfo(DataId, CreatureDataType.Monster);
    }

    public override bool Init()
    {
        base.Init();

        ObjectType = ObjectType.Monster;
        CreatureState = CreatureState.Moving;

        Rigid.simulated = true;
        transform.localScale = Vector3.one;
        _moveDir = Vector3.zero; // 풀 재활용 시 이전 사이클의 방향이 첫 프레임에 잘못 반영되지 않도록 초기화

        AttackType = CreatureData.PrefabName switch
        {
            "RangeMonster" => AttackType.Ranged,
            "RushMonster"  => AttackType.Rush,
            _              => AttackType.Melee,
        };

        SetMonsterPosition();
        InitByAttackType();

        return true;
    }

    // 발사·스킬 사용 시 타겟 방향으로 스프라이트 좌우 반전 (정지 중 발사할 때 잘못된 방향을 바라보는 문제 해결)
    public override void FaceDirection(float dirX)
    {
        if (CreatureSprite != null)
            CreatureSprite.flipX = dirX < 0;
    }

    // 공격 타입에 따라 초기화 (원거리: 스킬 초기화, 근접: 공격 코루틴 시작)
    void InitByAttackType()
    {
        switch (AttackType)
        {
            case AttackType.Ranged:
                InitSkill();
                // 몬스터는 캐릭터처럼 자체 Level 관리 로직이 없으므로 여기서 활성화
                foreach (var skill in Skills.SkillList)
                {
                    if (skill.Level == 0)
                    {
                        skill.Level = 1;
                        skill.ActivateSkill();
                    }
                }
                break;
            case AttackType.Melee:
                _coMeleeAttack = StartCoroutine(CoMeleeAttack());
                break;
                // Rush: OnCollisionEnter2D에서 처리
        }
    }

    // 플레이어+동료 중 가장 가까운 대상 방향으로 이동, 스프라이트 좌우 반전
    void FixedUpdate()
    {
        Vector3? nearestPos = GetNearestAllyPosition();
        if (nearestPos == null)
            return;

        _moveDir = nearestPos.Value - transform.position;
        CreatureSprite.flipX = _moveDir.x < 0;

        if (CreatureState != Define.CreatureState.Moving)
            return;

        Vector3 newPos = transform.position + _moveDir.normalized * Time.fixedDeltaTime * MoveSpeed;
        Rigid.MovePosition(newPos);
    }

    Vector3? GetNearestAllyPosition()
    {
        var target = GetNearestAllyTarget();
        return target != null ? target.CenterPosition : null;
    }

    // 플레이어+동료 중 가장 가까운 대상 반환 (보스 스킬에서도 사용)
    public CreatureController GetNearestAllyTarget()
    {
        float minDist = float.MaxValue;
        CreatureController nearest = null;

        PlayerController pc = Managers.Object.Player;
        if (pc.IsValid())
        {
            float dist = (pc.transform.position - transform.position).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                nearest = pc;
            }
        }

        foreach (var companion in Managers.Object.Companions)
        {
            if (companion.IsValid() == false) continue;
            float dist = (companion.transform.position - transform.position).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                nearest = companion;
            }
        }

        return nearest;
    }

    // 플레이어 주변 밖의 랜덤 위치에 배치
    public void SetMonsterPosition()
    {
        Vector2 randCirclePos = Util.GenerateMonsterSpawnPosition(Managers.Game.Player.CenterPosition);
        transform.position = randCirclePos;
    }


    public override void OnDamaged(BaseController attacker, SkillBase skill = null, float damage = 0)
    {
        float multiplier = skill != null ? skill.SkillData.DamageMultiplier : 1.0f;
        if (skill != null)
        {
            Managers.Sound.Play(Sound.Effect, skill.SkillData.HitSoundLabel);
        }
        float totalDmg = Managers.Game.Player.Atk * multiplier;

        base.OnDamaged(attacker, skill, totalDmg);

        InvokeBossData();
        if (ObjectType == ObjectType.Monster)
        {
            if (_coKnockback == null)
            {
                _coKnockback = StartCoroutine(CoKnockBack());
            }
        }
    }

    // 사망 처리 - 킬카운트 증가, 골드 드롭, DOTween 수축 연출 후 디스폰
    public override void OnDead()
    {
        // 중복 방지 위해 (ex - 독 + 투사체 동시에 맞아서 2번 실행)
        if (CreatureState == CreatureState.Dead)
            return;
        base.OnDead();

        InvokeBossData();
        Managers.Game.GameScene.OnMonsterKilled();

        //gold
        GoldController gold = Managers.Object.Spawn<GoldController>(transform.position);
        if (gold != null)
        {
            int stageLevel = Managers.Game?.GameScene?.CurrentStage?.StageLevel ?? 1;
            gold.ApplyScale(StageStatProvider.GetGoldScale(stageLevel));
        }

        // 죽을 때 연출 -> 0.2초동안 0으로 점점 작아짐
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOScale(0f, _shrinkTime).SetEase(Ease.InOutBounce)).OnComplete(() =>
        {
            StopAllCoroutines();
            _coKnockback = null;
            _coMeleeAttack = null;
            Rigid.linearVelocity = Vector2.zero;
            Managers.Object.Despawn(this);
        });
    }

    // 즉시 제거 (연출 없이 강제 디스폰, KillAllMonsters에서 호출)
    public void KillMonster()
    {
        Rigid.simulated = false;
        transform.localScale = Vector3.one;
        CreatureState = CreatureState.Dead;
        StopAllCoroutines();

        if (isPlayDamagedAnim)
        {
            CreatureSprite.material = DefaultMat;
            isPlayDamagedAnim = false;
        }
        _coKnockback = null;
        _coMeleeAttack = null;
        Rigid.linearVelocity = Vector2.zero;
        Managers.Object.Despawn(this);
    }

    // KNOCKBACK_TIME 동안 반대 방향으로 밀려나고 KNOCKBACK_COOLTIME 동안 재넉백 방지
    IEnumerator CoKnockBack()
    {
        float elapsed = 0;
        CreatureState = CreatureState.OnDamaged;
        while (true)
        {
            elapsed += Time.deltaTime;
            if (elapsed > KNOCKBACK_TIME)
                break;

            Vector3 dir = _moveDir * -1f;
            Vector2 nextVec = dir.normalized * KNOCKBACK_SPEED * Time.fixedDeltaTime;
            Rigid.MovePosition(Rigid.position + nextVec);

            yield return null;
        }
        CreatureState = CreatureState.Moving;

        yield return new WaitForSeconds(KNOCKBACK_COOLTIME);
        _coKnockback = null;
        yield break;
    }


    // 크리처 상태에 따라 애니메이션 파라미터 설정 (Moving/Idle → Moving, Attack/Skill → Attack)
    protected override void UpdateAnimation()
    {
        if (Anim == null)
            return;

        switch (CreatureState)
        {
            case CreatureState.Moving:
            case CreatureState.Idle:
                Anim.SetInteger("State", (int)CreatureState.Moving);
                break;
            case CreatureState.Attack:
            case CreatureState.Skill:
                Anim.SetInteger("State", (int)CreatureState.Attack);
                break;
                //case CreatureState.Dead:
                //    Anim.SetInteger("State", 0);
                //    break;
        }
    }

    CreatureController GetAllyTarget(GameObject go)
    {
        PlayerController player = go.GetComponent<PlayerController>();
        if (player.IsValid()) return player;

        CompanionController companion = go.GetComponent<CompanionController>();
        if (companion.IsValid()) return companion;

        return null;
    }

    // 충돌 시작 - Rush 타입만 플레이어/동료와 접촉 데미지 시작
    public virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (this.IsValid() == false) return;
        if (AttackType != AttackType.Rush) return;

        CreatureController target = GetAllyTarget(collision.gameObject);
        if (target == null) return;

        if (_coDotDamage != null) StopCoroutine(_coDotDamage);
        _coDotDamage = StartCoroutine(CoStartDotDamage(target));
    }

    // 트리거 충돌 - 콜라이더가 Trigger인 경우 대응
    public virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (this.IsValid() == false) return;
        if (AttackType != AttackType.Rush) return;

        CreatureController target = GetAllyTarget(collision.gameObject);
        if (target == null) return;

        if (_coDotDamage != null) StopCoroutine(_coDotDamage);
        _coDotDamage = StartCoroutine(CoStartDotDamage(target));
    }

    // 충돌 종료 - Rush 타입 플레이어/동료 이탈 시 코루틴 중단
    public virtual void OnCollisionExit2D(Collision2D collision)
    {
        if (this.IsValid() == false) return;
        if (AttackType != AttackType.Rush) return;
        if (GetAllyTarget(collision.gameObject) == null) return;

        if (_coDotDamage != null) StopCoroutine(_coDotDamage);
        _coDotDamage = null;
    }

    public virtual void OnTriggerExit2D(Collider2D collision)
    {
        if (this.IsValid() == false) return;
        if (AttackType != AttackType.Rush) return;
        if (GetAllyTarget(collision.gameObject) == null) return;

        if (_coDotDamage != null) StopCoroutine(_coDotDamage);
        _coDotDamage = null;
    }

    // 1초마다 사거리 내 플레이어에게 근접 공격 반복
    IEnumerator CoMeleeAttack()
    {
        WaitForSeconds attackCooldown = new WaitForSeconds(_meleeDamageCoolTime);

        while (true)
        {
            PlayerController pc = Managers.Object.Player;
            float dist = (pc.transform.position - transform.position).magnitude;
            if (pc.IsValid() && dist <= CreatureData.AttackRange)
            {
                CreatureState = CreatureState.Attack;
                pc.OnDamaged(this, null, Atk);

                yield return attackCooldown;

                if (CreatureState != CreatureState.Dead)
                    CreatureState = CreatureState.Moving;
            }
            yield return null;
        }
    }

    // 0.1초마다 지속 데미지 적용 (Rush 타입 접촉 시, 플레이어/동료 대상)
    public IEnumerator CoStartDotDamage(CreatureController target)
    {
        while (true)
        {
            target.OnDamaged(this, null, Atk);
            yield return new WaitForSeconds(_dotDamageCoolTime);
        }
    }

    // 보스일 때 UI 갱신 이벤트 호출
    public void InvokeBossData()
    {
        if (this.IsValid() && gameObject.IsValid() && ObjectType != ObjectType.Monster)
        {
            BossInfoUpdate?.Invoke(this);
        }
    }

}
