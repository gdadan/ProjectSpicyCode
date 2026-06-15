using static Define;

// 캐릭터형 크리처 (플레이어/동료) 공통 베이스 - 강화/패시브/보정 스탯 보유
// CreatureController에서 분리한 캐릭터 전용 스탯과 강화 패시브 적용 로직을 담당
public abstract class AllyController : CreatureController
{
    public const float DEFAULT_CRI_DAMAGE = 1.5f;

    // ============================================================
    //  캐릭터 전용 보정 스탯 (Player/Companion 공유)
    // ============================================================
    public float MaxHpBonusRate { get; set; } = 1f; // 최대 HP 배율 보정
    public float HpRegen { get; set; }              // 초당 HP 재생량
    public float AttackRate { get; set; } = 1f;     // 공격력 배율 보정
    public float DefRate { get; set; }              // 방어력 배율 보정
    public float CriRate { get; set; }              // 치명타 확률
    public float CriDamage { get; set; } = DEFAULT_CRI_DAMAGE; // 치명타 배율
    public float DamageReduction { get; set; }      // 받는 피해 감소율
    public float MoveSpeedRate { get; set; } = 1f;  // 이동속도 배율 보정

    // ============================================================
    //  캐릭터 강화 패시브 적용
    // ============================================================
    // 호출 시점: 스폰 직후, CharacterEnhanceManager에서 성급 변경 시
    public override void ApplyEnhancePassives()
    {
        // 1. 베이스 스탯 복구 (반복 호출 시 누적 방지)
        InitCreatureStat();

        // 2. 플레이어인 경우 골드 스탯 업그레이드 재적용 (베이스 → 업그레이드 → 패시브 순서)
        BeforePassiveModifier();

        if (Managers.CharacterEnhance == null) return;
        var passiveIds = Managers.CharacterEnhance.GetUnlockedPassiveIds(DataId);
        if (passiveIds.Count == 0) return;

        // 3. Add / Rate 합산
        float atkAdd = 0f, hpAdd = 0f, defAdd = 0f, moveSpeedAdd = 0f;
        float criRateAdd = 0f, criDmgAdd = 0f;
        float atkRate = 0f, hpRate = 0f, defRate = 0f;

        foreach (int id in passiveIds)
        {
            if (!Managers.Data.PassiveDic.TryGetValue(id, out Data.PassiveData p)) continue;
            switch (p.EffectType)
            {
                case PassiveEffectType.AtkAdd: atkAdd += p.EffectValue; break;
                case PassiveEffectType.MaxHpAdd: hpAdd += p.EffectValue; break;
                case PassiveEffectType.DefAdd: defAdd += p.EffectValue; break;
                case PassiveEffectType.MoveSpeedAdd: moveSpeedAdd += p.EffectValue; break;
                case PassiveEffectType.CriRateAdd: criRateAdd += p.EffectValue; break;
                case PassiveEffectType.CriDamageAdd: criDmgAdd += p.EffectValue; break;
                case PassiveEffectType.AtkRate: atkRate += p.EffectValue; break;
                case PassiveEffectType.MaxHpRate: hpRate += p.EffectValue; break;
                case PassiveEffectType.DefRate: defRate += p.EffectValue; break;
            }
        }

        // 4. (베이스 + Add) × (1 + Rate)
        Atk = (Atk + atkAdd) * (1f + atkRate);
        MaxHp = (MaxHp + hpAdd) * (1f + hpRate);
        Hp = MaxHp;
        Def = (Def + defAdd) * (1f + defRate);
        MoveSpeed += moveSpeedAdd;
        CriRate += criRateAdd;
        CriDamage += criDmgAdd;

        OnPassiveApplied();
    }

    // 플레이어 전용 훅: 스탯 업그레이드 성장값을 베이스에 반영 (Companion은 미사용)
    protected virtual void BeforePassiveModifier() { }

    // 패시브 적용 후 후처리 (UI 갱신 이벤트 등)
    protected virtual void OnPassiveApplied() { }
}
