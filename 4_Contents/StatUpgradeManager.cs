using System;
using System.Collections.Generic;
using Data;
using static Define;

// 골드 기반 스탯 업그레이드 관리 - 레벨/비용 계산/플레이어 스탯 적용
public class StatUpgradeManager
{
    Dictionary<StatType, int> _statLevels = new Dictionary<StatType, int>();

    public Action OnStatUpgraded;

    // UserDB에서 저장된 업그레이드 레벨 복원
    public void LoadFromUserDB()
    {
        if (Managers.UserDB == null) return;
        foreach (StatType st in Enum.GetValues(typeof(StatType)))
        {
            int level = Managers.UserDB.GetStatLevel(st);
            if (level > 0)
                _statLevels[st] = level;
        }
    }

    public int GetStatLevel(StatType statType)
    {
        _statLevels.TryGetValue(statType, out int level);
        return level;
    }

    // 업그레이드 비용 (등비): BasePrice × PriceRate^level
    public long GetUpgradePrice(StatType statType)
    {
        int level = GetStatLevel(statType);
        if (!Managers.Data.StatUpgradeDic.TryGetValue(statType.ToString(), out StatUpgradeData data))
            return long.MaxValue;

        double price = data.BasePrice * Math.Pow(data.PriceRate, level);
        return price >= long.MaxValue ? long.MaxValue : (long)price;
    }

    public bool IsMaxLevel(StatType statType)
    {
        if (!Managers.Data.StatUpgradeDic.TryGetValue(statType.ToString(), out StatUpgradeData data))
            return true;
        return GetStatLevel(statType) >= data.MaxLevel;
    }

    // N회 업그레이드 총 비용 계산 (등비수열 합)
    public long GetTotalPrice(StatType statType, int count)
    {
        if (!Managers.Data.StatUpgradeDic.TryGetValue(statType.ToString(), out StatUpgradeData data))
            return long.MaxValue;

        int level = GetStatLevel(statType);
        int maxLevel = data.MaxLevel;
        double total = 0;

        for (int i = 0; i < count && level + i < maxLevel; i++)
        {
            total += data.BasePrice * Math.Pow(data.PriceRate, level + i);
            if (total >= long.MaxValue)
                return long.MaxValue;
        }

        return (long)total;
    }

    // 실제 업그레이드 가능 횟수 (맥스레벨 고려)
    public int GetUpgradeableCount(StatType statType, int count)
    {
        if (!Managers.Data.StatUpgradeDic.TryGetValue(statType.ToString(), out StatUpgradeData data))
            return 0;

        int level = GetStatLevel(statType);
        return Math.Min(count, data.MaxLevel - level);
    }

    // 업그레이드 시도 - 골드 차감, 레벨 증가, 스탯 적용
    public bool TryUpgrade(StatType statType, int count = 1)
    {
        int actualCount = GetUpgradeableCount(statType, count);
        if (actualCount <= 0)
            return false;

        long totalPrice = GetTotalPrice(statType, actualCount);
        if (Managers.Game.Gold < totalPrice)
            return false;

        Managers.Game.Gold -= totalPrice;
        _statLevels[statType] = GetStatLevel(statType) + actualCount;

        // UserDB에 레벨 동기화 및 저장
        if (Managers.UserDB != null)
        {
            Managers.UserDB.SetStatLevel(statType, _statLevels[statType]);
            Managers.UserDB.Save();
        }

        ApplyStatToPlayer(statType);
        OnStatUpgraded?.Invoke();
        return true;
    }

    // 해당 스탯의 성장 총량 (등비수열 합)
    // GrowthRate=1.0이면 산술: GrowthValue × level
    // 그 외엔 등비: GrowthValue × (GrowthRate^level - 1) / (GrowthRate - 1)
    public float GetGrowthTotal(StatType statType)
    {
        int level = GetStatLevel(statType);
        if (!Managers.Data.StatUpgradeDic.TryGetValue(statType.ToString(), out StatUpgradeData data))
            return 0;

        if (Math.Abs(data.GrowthRate - 1.0f) < 0.0001f)
            return data.GrowthValue * level;

        double total = data.GrowthValue * (Math.Pow(data.GrowthRate, level) - 1) / (data.GrowthRate - 1);
        return (float)total;
    }

    // 플레이어에 성장 스탯 적용 (베이스 + 강화 보너스 + StatUpgrade 누적 + 장비 효과)
    void ApplyStatToPlayer(StatType statType)
    {
        PlayerController player = Managers.Game.Player;
        if (player == null) return;

        float total = GetGrowthTotal(statType);
        float equip = Managers.Game.GetEquipBonusStat(statType.ToString());
        CharacterData charData = player.CreatureData as CharacterData;

        switch (statType)
        {
            case StatType.Atk:
                player.Atk = player.CreatureData.Atk + (charData?.AtkBonus ?? 0f) + total + equip;
                break;
            case StatType.MaxHp:
                float prevMaxHp = player.MaxHp;
                player.MaxHp = player.CreatureData.MaxHp + (charData?.MaxHpBonus ?? 0f) + total + equip;
                player.Hp += (player.MaxHp - prevMaxHp);
                break;
            case StatType.Def:
                player.Def = player.CreatureData.Def + total + equip;
                break;
            case StatType.CriRate:
                player.CriRate = total + equip;
                break;
            case StatType.CriDamage:
                player.CriDamage = AllyController.DEFAULT_CRI_DAMAGE + total + equip;
                break;
        }

        player.OnPlayerDataUpdated?.Invoke();
    }

    // 전체 성장 스탯 재적용
    public void ApplyAllStats()
    {
        foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            ApplyStatToPlayer(statType);
    }
}
