using UnityEngine;
using static Define;

// AI 동료 컨트롤러 - 플레이어를 따라다니며 자동으로 스킬 사용, 체력 소진 시 사망
// 보정 스탯(CriRate/CriDamage 등)은 CharacterController에서 상속
public class CompanionController : AllyController
{
    SkeletonAnimationHandler _spineHandler;

    public float FollowDistance = 5f;
    PlayerController _player;

    public override bool Init()
    {
        _spineHandler = GetComponent<SkeletonAnimationHandler>();
        if (_spineHandler == null)
            _spineHandler = Util.FindChild<SkeletonAnimationHandler>(gameObject);

        base.Init();
        ObjectType = ObjectType.Player;

        return true;
    }

    private void Start()
    {
        _player = Managers.Game.Player;
        InitSkill();
        ActivateAllSkills();
        CreatureState = CreatureState.Idle;
    }

    void FixedUpdate()
    {
        if (_player == null || !_player.IsValid())
            return;

        //FollowPlayer();
        Rigid.linearVelocity = Vector2.zero;
        Rigid.MovePosition(transform.position);
    }

    void FollowPlayer()
    {
        float dist = Vector2.Distance(transform.position, _player.transform.position);

        if (dist > FollowDistance)
        {
            Vector2 dir = (_player.transform.position - transform.position).normalized;
            Vector2 targetPos = (Vector2)transform.position + dir * MoveSpeed * Time.fixedDeltaTime;
            Rigid.MovePosition(targetPos);

            FaceDirection(dir.x);

            if (CreatureState != CreatureState.Moving)
                CreatureState = CreatureState.Moving;
        }
        else
        {
            if (CreatureState != CreatureState.Idle)
                CreatureState = CreatureState.Idle;
        }
    }

    void ActivateAllSkills()
    {
        foreach (var skill in Skills.SkillList)
        {
            if (skill.SkillType == Define.SkillType.None) continue;
            skill.Level = 1;
            skill.ActivateSkill();
        }
    }

    public override void FaceDirection(float dirX)
    {
        if (_spineHandler != null && dirX != 0)
            _spineHandler.SetFlip(dirX);
    }

    protected override void UpdateAnimation()
    {
        if (_spineHandler == null)
            return;

        switch (CreatureState)
        {
            case CreatureState.Idle:
                _spineHandler.PlayAnimationForState("idle", 0);
                break;
            case CreatureState.Moving:
                _spineHandler.PlayAnimationForState("walk", 0);
                break;
            case CreatureState.Attack:
                float attackAnimDuration = 0f;
                var attackAnim = _spineHandler.GetAnimationForState("attack");
                if (attackAnim != null)
                {
                    _spineHandler.PlayOneShot(attackAnim, 0);
                    attackAnimDuration = attackAnim.Duration;
                }
                break;
        }
    }


    // 사망 시 풀에서 제거하지 않고 비활성화 — 스테이지 재시작 시 부활용
    public override void OnDead()
    {
        base.OnDead();
        gameObject.SetActive(false);
    }
}
