using System;
using System.Collections.Generic;
using Data;
using static Define;

// 캐릭터별 성급 강화 관리 - 성급/재화 조회, 승급, 해금된 패시브 조회
public class CharacterEnhanceManager
{
    public const int MAX_STAR = 5;
    static readonly int[] PASSIVE_UNLOCK_STARS = { 1, 3, 5 };

    // 캐릭터ID → 성급
    Dictionary<int, int> _starLevels = new Dictionary<int, int>();
    // 캐릭터ID → 강화 재화 보유량
    Dictionary<int, int> _currencies = new Dictionary<int, int>();

    public Action<int> OnStarChanged;      // characterId
    public Action<int> OnCurrencyChanged;  // characterId

    // UserDB에서 저장된 성급/재화 복원
    public void LoadFromUserDB()
    {
        if (Managers.UserDB == null) return;

        _starLevels.Clear();
        _currencies.Clear();

        var entries = Managers.UserDB.Data.CharacterEnhance.Entries;
        foreach (var entry in entries)
        {
            _starLevels[entry.CharacterId] = entry.StarLevel;
            _currencies[entry.CharacterId] = entry.EnhanceCurrency;
        }
    }

    // ============================================================
    //  성급 / 재화 조회
    // ============================================================

    public int GetStarLevel(int characterId)
    {
        _starLevels.TryGetValue(characterId, out int level);
        return level;
    }

    public bool IsMaxStar(int characterId) => GetStarLevel(characterId) >= MAX_STAR;

    public int GetCurrency(int characterId)
    {
        _currencies.TryGetValue(characterId, out int amount);
        return amount;
    }

    public void AddCurrency(int characterId, int amount)
    {
        if (amount == 0) return;
        int next = Math.Max(0, GetCurrency(characterId) + amount);
        _currencies[characterId] = next;

        if (Managers.UserDB != null)
        {
            Managers.UserDB.SetCharacterEnhanceCurrency(characterId, next);
            Managers.UserDB.Save();
        }

        OnCurrencyChanged?.Invoke(characterId);
    }

    // 다음 성급 승급 비용 (이미 5성이면 -1)
    public int GetNextUpgradeCost(int characterId)
    {
        int level = GetStarLevel(characterId);
        if (level >= MAX_STAR) return -1;

        if (!Managers.Data.CharacterEnhanceDic.TryGetValue(characterId, out CharacterEnhanceData data))
            return -1;
        if (data.StarCosts == null || level >= data.StarCosts.Count) return -1;

        return data.StarCosts[level];
    }

    public bool CanUpgrade(int characterId)
    {
        int cost = GetNextUpgradeCost(characterId);
        if (cost < 0) return false;
        return GetCurrency(characterId) >= cost;
    }

    // ============================================================
    //  승급
    // ============================================================

    public bool TryUpgrade(int characterId)
    {
        if (!CanUpgrade(characterId)) return false;

        int cost = GetNextUpgradeCost(characterId);
        int nextLevel = GetStarLevel(characterId) + 1;

        _currencies[characterId] = GetCurrency(characterId) - cost;
        _starLevels[characterId] = nextLevel;

        if (Managers.UserDB != null)
        {
            Managers.UserDB.SetCharacterEnhanceCurrency(characterId, _currencies[characterId]);
            Managers.UserDB.SetCharacterStarLevel(characterId, nextLevel);
            Managers.UserDB.Save();
        }

        ReapplyPassivesToSpawned(characterId);

        OnCurrencyChanged?.Invoke(characterId);
        OnStarChanged?.Invoke(characterId);
        return true;
    }

    // ============================================================
    //  패시브 조회 / 적용
    // ============================================================

    // 해금된 패시브 ID 리스트 (성급 기준 1/3/5★ 통과분만 포함)
    public List<int> GetUnlockedPassiveIds(int characterId)
    {
        var result = new List<int>();
        if (!Managers.Data.CharacterEnhanceDic.TryGetValue(characterId, out CharacterEnhanceData data))
            return result;

        int star = GetStarLevel(characterId);
        if (star >= 1 && data.PassiveAt1 > 0) result.Add(data.PassiveAt1);
        if (star >= 3 && data.PassiveAt3 > 0) result.Add(data.PassiveAt3);
        if (star >= 5 && data.PassiveAt5 > 0) result.Add(data.PassiveAt5);
        return result;
    }

    // 슬롯별 패시브 ID + 해금 여부 (UI용) - 항상 3개 반환 (1성/3성/5성 슬롯)
    public List<(int passiveId, int requiredStar, bool unlocked)> GetPassiveSlots(int characterId)
    {
        var result = new List<(int, int, bool)>();
        if (!Managers.Data.CharacterEnhanceDic.TryGetValue(characterId, out CharacterEnhanceData data))
            return result;

        int star = GetStarLevel(characterId);
        result.Add((data.PassiveAt1, 1, star >= 1));
        result.Add((data.PassiveAt3, 3, star >= 3));
        result.Add((data.PassiveAt5, 5, star >= 5));
        return result;
    }

    // 이미 스폰된 캐릭터(플레이어/동료)에 패시브 재적용
    void ReapplyPassivesToSpawned(int characterId)
    {
        if (Managers.Object == null) return;

        if (Managers.Object.Player != null && Managers.Object.Player.DataId == characterId)
            Managers.Object.Player.ApplyEnhancePassives();

        foreach (var companion in Managers.Object.Companions)
        {
            if (companion != null && companion.DataId == characterId)
                companion.ApplyEnhancePassives();
        }
    }
}
