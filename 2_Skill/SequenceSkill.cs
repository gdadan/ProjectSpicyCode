using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 순서대로 실행되는 스킬의 추상 기반 클래스 (주로 몬스터 AI 패턴용)
public abstract class SequenceSkill : SkillBase
{
    public int DataId; // 스킬 타입 ID가 아닌 데이터 ID (여러 데이터를 같은 스킬 클래스로 처리할 때 사용)
    public abstract void DoSkill(Action callback = null); // 스킬 실행 (콜백으로 다음 동작 연결)

    // Animator Controller의 "State" 파라미터에 매핑되는 애니메이션 상태
    protected enum AniState
    {
        Idle = 0,
        Walk = 1,
        Attack1 = 2,
        Attack2 = 3,
        Attack3 = 4,
        Attack4 = 5,
    }

    // Owner의 Animator에 State 파라미터 설정 (null safe)
    protected void SetAniState(AniState state)
    {
        Owner.Anim?.SetInteger("State", (int)state);
    }

    // DataId 기반으로 스킬 데이터 로드
    public override void ActivateSkill()
    {
        UpdateSkillData(DataId);
    }
}
