# Project Spicy 코드

> 🚧 현재 진행 중인 프로젝트로, 코드와 구조는 계속 다듬어지고 있습니다.

<br/>

## 📝 프로젝트 소개

| 항목 | 내용 |
|------|------|
| **개발 기간** | 2025.12 ~ |
| **개발 환경** | C#, Unity 6 (6000.3 LTS) |
| **개발 장르** | 2D 모바일 하이브리드 퍼즐 RPG (퍼즐 + 방치형) |
| **담당 영역** | **방치형 RPG 파트 전담** — 코어 아키텍처 / 스킬 / 크리처 / 콘텐츠 / 에디터 툴링 |

**한 줄 설명** : 퍼즐과 방치형 RPG를 결합한 하이브리드 모바일 게임입니다.

> 본 저장소는 담당 파트에서 **핵심 코드만 선별**해 공개한 것으로, 단독 컴파일용이 아닌 **설계·구현을 보여주는 열람용**입니다.

<br/>

## 🎯 핵심 작업 요약

| 영역 | 핵심 키워드 |
|------|------------|
| **코어 아키텍처** | 매니저 허브 · 오브젝트 풀링 · Addressables 리소스 로딩 · UI 스택 |
| **스킬 시스템** | 데이터 주도 · 반복형(Repeat) / 시퀀스형(Sequence) 추상화 · Template Method |
| **크리처 컨트롤러** | `BaseController` 기반 상속 계층 · 피격 연출 · 크리티컬 계산 |
| **콘텐츠 시스템** | 골드 기반 스탯 강화(등비수열 비용) · 캐릭터 강화 패시브 |
| **에디터 툴링 🤖** | 스프라이트 파이프라인 · Excel↔JSON 데이터 파이프라인 *(AI 활용 제작)* |

<br/>

---

## 🛠 1. 코어 아키텍처 — 매니저 허브

### 무엇을
게임 전역 시스템(리소스 · 풀 · UI · 사운드 · 데이터 · 백엔드)을 **단일 진입점 `Managers`** 로 묶어 씬 간에 유지되도록 설계했습니다.

### 어떻게
`Managers.Instance`가 `DontDestroyOnLoad`로 살아남으며, 모든 매니저를 정적 프로퍼티로 노출합니다. `Managers.Resource`, `Managers.Pool`, `Managers.UI`처럼 어디서든 일관되게 접근합니다.

```csharp
public static GameManager   Game     { get { return Instance?._game; } }
public static ObjectManager Object   { get { return Instance?._object; } }
public static PoolManager   Pool     { get { return Instance?._pool; } }
public static ResourceManager Resource { get { return Instance?._resource; } }
// ...

public static void Init()
{
    if (_instance == null)
    {
        GameObject go = GameObject.Find("@Managers") ?? new GameObject { name = "@Managers" };
        DontDestroyOnLoad(go);
        _instance = go.GetComponent<Managers>() ?? go.AddComponent<Managers>();

        _instance._backend.Init();     // 뒤끝 서버 초기화
        _instance._userDB.Init();      // 유저 세이브 로드 → 런타임 데이터 복원
        _instance._statUpgrade.LoadFromUserDB();
        _instance._characterEnhance.LoadFromUserDB();
    }
}
```

- **세이브/로드 연동** : 앱 일시정지(`OnApplicationPause`)·종료(`OnApplicationQuit`) 시 유저 데이터를 자동 저장.
- **씬 전환 정리** : `Clear()`로 UI·오브젝트·풀 상태를 일괄 초기화.

> 📌 매니저 허브 구조 자체는 널리 쓰이는 패턴을 채택한 것이고, **백엔드 서버(뒤끝) 연동·유저 세이브 복원·콘텐츠 매니저 구성**이 직접 설계·구현한 부분입니다.

🔗 [Managers.cs](./1_Core/Managers.cs) · [PoolManager.cs](./1_Core/PoolManager.cs) · [ResourceManager.cs](./1_Core/ResourceManager.cs) · [ObjectManager.cs](./1_Core/ObjectManager.cs)

<br/>

---

## 🛠 2. 데이터 주도 스킬 시스템 — 반복형 / 시퀀스형 추상화

### 무엇을
스킬을 **쿨타임마다 반복 발동하는 `RepeatSkill`** 과 **순서대로 실행되는 `SequenceSkill`**(몬스터 AI 패턴 등) 두 갈래로 추상화하고, 모든 수치를 데이터(`SkillData`)로 분리했습니다.

### 어떻게
공통 베이스 `SkillBase`가 레벨→데이터 매핑과 투사체 생성을 담당하고, 자식은 **자기 동작만** 구현합니다 (Template Method).

```csharp
public abstract class RepeatSkill : SkillBase
{
    protected abstract void DoSkillJob();            // 자식이 구현할 '내용'

    protected virtual IEnumerator CoStartSkill()      // 부모가 정한 '흐름'
    {
        WaitForSeconds wait = new WaitForSeconds(SkillData.CoolTime);
        yield return wait;
        while (true)
        {
            Managers.Sound.Play(Define.Sound.Effect, SkillData.CastingSound);
            Owner.CreatureState = Define.CreatureState.Attack;
            DoSkillJob();                             // 쿨타임마다 자식 동작 실행
            yield return wait;
        }
    }
}
```

레벨에 따라 다른 `SkillData`를 자동 로드해, **레벨업 = 데이터 교체**로 동작합니다.
```csharp
// Level 1 → 기본 ID, Level 2 이상 → ID + (Level - 1)
id = Level < 2 ? (int)SkillType : (int)SkillType + Level - 1;
```

**예시 — 체인 라이트닝** : 가장 가까운 몬스터부터 거리 범위 내 다음 대상을 연쇄 탐색해 번개를 튕깁니다. 분기 수·사거리·튕김 횟수 모두 데이터로 제어.
```csharp
MonsterController GetChainMonster(Vector3 origin, float min, float max, ...)
{
    Collider2D[] targets = Physics2D.OverlapCircleAll(origin, max, LayerMask.GetMask("Monster", "Boss"));
    // 이미 체인된 대상 제외 + 거리 범위 내 가장 가까운 적 선택
    ...
}
```

> 💡 **회고** : [섀도우 헌터](https://github.com/gdadan/ShadowHunterCode)의 추상 클래스 스킬 시스템이, 여기서는 **데이터 주도 + Repeat/Sequence 이원화**로 한 단계 더 발전했습니다.

🔗 [SkillBase.cs](./2_Skill/SkillBase.cs) · [RepeatSkill.cs](./2_Skill/RepeatSkill.cs) · [SequenceSkill.cs](./2_Skill/SequenceSkill.cs) · [ChainLightning.cs](./2_Skill/ChainLightning.cs)

<br/>

---

## 🛠 3. 크리처 컨트롤러 — 상속 계층으로 공통 로직 통일

### 무엇을
`BaseController` → `CreatureController` → `AllyController` → `PlayerController` / `CompanionController`, 그리고 `MonsterController`로 이어지는 **상속 계층**을 설계해, 체력·피격·사망·애니메이션 같은 공통 로직을 부모에서 통일했습니다.

### 어떻게
**(1) 피격 연출 — `MaterialPropertyBlock`으로 배칭을 깨지 않는 히트 플래시**
```csharp
protected IEnumerator PlayDamageAnimation()
{
    CreatureRenderer.SetPropertyBlock(HitBlock);          // 흰색 플래시
    yield return new WaitForSeconds(HIT_FLASH_DURATION);
    CreatureRenderer.SetPropertyBlock(NormalBlock);       // 원복
}
```

**(2) 크리티컬 계산을 공통 메서드로**
```csharp
public (float damage, bool isCritical) CalcCriticalDamage(float damage, float criRatePercent, float criDamagePercent)
{
    bool isCritical = Random.value <= criRatePercent / 100f;
    if (isCritical) damage *= 1f + (criDamagePercent / 100f);
    return (damage, isCritical);
}
```

- 데미지 처리(`OnDamaged`)는 부모가 담당하고, 아군만 크리티컬을 적용하는 등 **타입별 차이는 다형성으로** 분기.
- 몬스터는 스테이지 레벨에 따라 스탯을 스케일링(`InitCreatureStat`)해 난이도 곡선을 데이터로 조절.

🔗 [CreatureController.cs](./3_Creature/CreatureController.cs) · [BaseController.cs](./3_Creature/BaseController.cs) · [PlayerController.cs](./3_Creature/PlayerController.cs)

<br/>

---

## 🛠 4. 콘텐츠 시스템 — 골드 기반 스탯 강화

### 무엇을
골드를 소모해 스탯을 올리는 성장 시스템을 구현했습니다. **비용은 등비수열, 성장량도 등비/산술 선택형**으로 데이터가 결정합니다.

### 어떻게
```csharp
// 업그레이드 비용 (등비): BasePrice × PriceRate^level
public long GetUpgradePrice(StatType statType)
{
    int level = GetStatLevel(statType);
    var data = Managers.Data.StatUpgradeDic[statType.ToString()];
    double price = data.BasePrice * Math.Pow(data.PriceRate, level);
    return price >= long.MaxValue ? long.MaxValue : (long)price;
}

// N회 강화 총 비용 = 등비수열 합 (오버플로 가드 포함)
public long GetTotalPrice(StatType statType, int count) { ... }
```

- **N연속 강화** 시 총 비용을 등비수열 합으로 계산하고, 맥스 레벨·골드·`long` 오버플로를 모두 가드.
- 최종 스탯은 **베이스 + 강화 보너스 + 누적 성장 + 장비 효과**를 합산해 플레이어에 반영, 변경 시 `OnStatUpgraded` 이벤트로 UI 갱신.

🔗 [StatUpgradeManager.cs](./4_Contents/StatUpgradeManager.cs) · [CharacterEnhanceManager.cs](./4_Contents/CharacterEnhanceManager.cs)

<br/>

---

## 🤖 5. 에디터 툴링 — AI를 활용한 워크플로우 자동화

> ⚠️ **이 폴더(`5_EditorTools`)의 코드는 Claude(AI)를 활용해 작성했습니다.** 직접 한 땀씩 짠 코드가 아니라, **반복 작업의 병목을 발견하고 AI로 자동화 도구를 만들어 생산성을 끌어올린 사례**로 봐주시면 됩니다.

작업 중 반복되던 수작업을 도구로 자동화했습니다.

| 도구 | 해결한 문제 |
|------|------------|
| **SpriteLibraryGenerator** | 스프라이트시트 → Unity Sprite Library 에셋 자동 생성 (캐릭터/보스 모드) |
| **SpriteSlicerWindow** | 폴더 단위 스프라이트 일괄 슬라이싱 |
| **LibraryAddressableChecker** | `.spriteLib`의 Addressables 그룹/라벨 등록 누락 검사·일괄 등록 |
| **ExcelToJsonMenu / JsonToExcelMenu** | 기획 데이터(Excel) ↔ JSON 양방향 변환 파이프라인 |
| **ResetUserDataMenu** | 개발용 유저 세이브 초기화 |

→ 아티스트·기획 협업의 수작업을 줄이는 **파이프라인 관점**과, **AI를 실무에 활용하는 능력**을 함께 보여주는 영역입니다.

🔗 [5_EditorTools](./5_EditorTools)

<br/>

---

## 💡 회고 — 이 프로젝트에서 배운 것

- **출시를 향한 전체 구조 설계** : 백엔드 세이브, 광고, 업적, Addressables까지 엮인 **실제 출시를 목표로 한 게임의 전체 구조**를 2인 팀에서 직접 다루며, 시스템 간 의존성을 정리하는 법을 배우고 있습니다.
- **데이터 주도 설계의 심화** : 스킬·스탯·강화 수치를 전부 데이터로 빼, 기획 변경에 코드 수정 없이 대응하는 구조를 체득했습니다.
- **성장의 연속선** : [촐랑이](https://github.com/gdadan/CholangsAdventure)의 `enum` 분기 → [섀도우 헌터](https://github.com/gdadan/ShadowHunterCode)의 추상 클래스 → 여기서의 **데이터 주도 + 상속 계층 + Template Method**로, 같은 문제를 점점 더 나은 구조로 풀어왔습니다.
- **AI 활용** : 반복 작업을 AI로 자동화해 본질적인 게임 로직에 더 집중할 수 있었습니다.

<br/>

## 📁 코드 구조 (공개 범위)

```
ProjectSpicyCode/
├── 1_Core/          # 매니저 허브 · 풀링 · 리소스 · UI · 사운드
├── 2_Skill/         # 데이터 주도 스킬 (Repeat / Sequence + 대표 구현)
├── 3_Creature/      # BaseController 기반 크리처 상속 계층
├── 4_Contents/      # 스탯 강화 · 캐릭터 강화 콘텐츠 매니저
└── 5_EditorTools/   # 🤖 AI 활용 에디터 자동화 도구
```
