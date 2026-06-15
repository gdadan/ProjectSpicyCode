using System;
using UnityEngine;
using static Define;

// 플레이어 컨트롤러 - 액션 이벤트, 스킬 초기화 담당
// 보정 스탯(CriRate/CriDamage 등)은 CharacterController에서 상속
public class PlayerController : AllyController
{
    SkeletonAnimationHandler _spineHandler;
    //public int KillCount { get; set; }

    #region Action
    public Action OnPlayerDataUpdated; // 플레이어 스탯 변경 시 호출
    public Action OnPlayerHpUpdated;   // HP 변경 시 호출
    public Action OnPlayerLevelUp;     // 레벨업 시 호출
    public Action OnPlayerDead;        // 사망 시 호출
    public Action OnPlayerDamaged;     // 피격 시 호출
    public Action OnPlayerMove;        // 이동 시 호출
    #endregion

    // 스킬 초기화 (게임 시작 시 한 번 실행)
    private void Start()
    {
        InitSkill();
        ActivateBaseAttack();

        // 시작 시 모든 스킬(마지막 제외) 1레벨 상승
        var skillList = Skills.SkillList;
        for (int i = 0; i < skillList.Count - 1; i++)
            skillList[i].Level++;

        // 장비 장착/해제 시 스탯 즉시 갱신
        Managers.Game.OnEquipChanged += OnEquipChanged;
    }

    void OnDestroy()
    {
        if (Managers.Game != null)
            Managers.Game.OnEquipChanged -= OnEquipChanged;
    }

    void OnEquipChanged()
    {
        Managers.StatUpgrade?.ApplyAllStats();
    }

    // 기본공격(ForkShot) 항상 활성화
    void ActivateBaseAttack()
    {
        SkillBase forkShot = Skills.GetSkill(Define.SkillType.ForkShot);
        if (forkShot != null && !forkShot.IsLearnedSkill)
        {
            forkShot.Level = 1;
            forkShot.ActivateSkill();
        }
    }
    // 물리 속도 초기화 (외부 힘이 누적되지 않도록)
    void FixedUpdate()
    {
        Rigid.linearVelocity = Vector2.zero;
    }

    public override bool Init()
    {
        _spineHandler = GetComponent<SkeletonAnimationHandler>();
        if (_spineHandler == null)
            _spineHandler = Util.FindChild<SkeletonAnimationHandler>(gameObject);

        base.Init();

        ObjectType = ObjectType.Player;

        return true;
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
                var attackAnim = _spineHandler.GetAnimationForState("attack");
                if (attackAnim != null)
                    _spineHandler.PlayOneShot(attackAnim, 0);
                break;
            //case CreatureState.OnDamaged:
            //    // hit 애니메이션 없음 - idle로 대체
            //    _spineHandler.PlayAnimationForState("idle", 0);
            //    break;
            //case CreatureState.Dead:
            //    // dead 애니메이션 없음 - idle 정지 (DOTween 축소로 처리)
            //    _spineHandler.PlayAnimationForState("idle", 0);
            //    _spineHandler.ControlTimeScale(0f);
            //    break;
        }
    }

    public override void InitSkill()
    {
        base.InitSkill();
    }

    // 사망 처리 - 베이스 OnDead 이후 외부 알림 (GameScene이 스테이지 실패로 변환)
    public override void OnDead()
    {
        base.OnDead();
        OnPlayerDead?.Invoke();
    }

    // 패시브 적용 전: 골드 스탯 업그레이드 성장값을 베이스에 반영
    protected override void BeforePassiveModifier()
    {
        Managers.StatUpgrade?.ApplyAllStats();
    }

    // 패시브 적용 후: UI 갱신 이벤트 발사
    protected override void OnPassiveApplied()
    {
        OnPlayerDataUpdated?.Invoke();
    }
}
