using Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;
using static Define;

public class CreatureController : BaseController
{
    const float HIT_FLASH_DURATION = 0.1f;
    static readonly Vector3 HP_BAR_OFFSET = new Vector3(0, -0.3f, 0);
    static readonly Vector3 HP_BAR_SCALE = new Vector3(0.012f, 0.02f, 0.02f);

    [SerializeField]
    protected SpriteRenderer CreatureSprite;
    public Material DefaultMat;
    public Material HitEffectMat;
    [SerializeField]
    protected bool isPlayDamagedAnim = false;

    public Renderer CreatureRenderer;
    public MaterialPropertyBlock NormalBlock, HitBlock;

    protected UI_HPBar _hpBar; // 월드 스페이스 HP바

    public Rigidbody2D Rigid { get; set; }
    public Animator Anim { get; set; }
    public CreatureData CreatureData = new CreatureData();

    public virtual int DataId { get; set; }
    public virtual float Hp { get; set; }
    public virtual float MaxHp { get; set; }
    public virtual float Atk { get; set; }
    public virtual float Def { get; set; }
    public virtual float MoveSpeed { get; set; }
    public virtual SkillBook Skills { get; set; }

    private Collider2D _offset;
    CreatureState _creatureState = CreatureState.Moving;

    public Vector3 CenterPosition
    {
        get
        {
            if (_offset == null)
                return transform.position;
            // bounds.center는 Physics2D 동기화 전까지 stale 값을 반환할 수 있으므로
            // transform 기반으로 즉시 계산 (텔레포트/풀 재활용 직후에도 정확)
            Vector2 worldOffset = Vector2.Scale(_offset.offset, transform.lossyScale);
            return transform.position + (Vector3)worldOffset;
        }
    }

    public virtual CreatureState CreatureState
    {
        get { return _creatureState; }
        set
        {
            _creatureState = value;
            UpdateAnimation();
        }
    }

    public bool IsMonster()
    {
        switch (ObjectType)
        {
            case ObjectType.Boss:
            case ObjectType.Monster:
                //case ObjectType.EliteMonster:
                return true;
            case ObjectType.Player:
            case ObjectType.Projectile:
                return false; ;
            default:
                return false;
        }
    }

    public override bool Init()
    {
        base.Init();

        Skills = gameObject.GetOrAddComponent<SkillBook>();
        _offset = GetComponent<Collider2D>();
        Rigid = GetComponent<Rigidbody2D>();
        DefaultMat = Managers.Resource.Load<Material>("CreatureDefaultMat");
        HitEffectMat = Managers.Resource.Load<Material>("PaintWhite");

        CreatureRenderer = GetComponent<Renderer>();
        if (CreatureRenderer == null)
            CreatureRenderer = Util.FindChild<Renderer>(gameObject);

        NormalBlock = new MaterialPropertyBlock();
        HitBlock = new MaterialPropertyBlock();

        if (CreatureRenderer != null && !(CreatureRenderer is SpriteRenderer))
        {
            HitBlock.SetFloat("_FillPhase", 1f);
            HitBlock.SetColor("_FillColor", Color.white);
            NormalBlock.SetFloat("_FillPhase", 0f);
        }

        CreatureSprite = GetComponent<SpriteRenderer>();
        if (CreatureSprite == null)
            CreatureSprite = Util.FindChild<SpriteRenderer>(gameObject);

        Anim = GetComponent<Animator>();
        if (Anim == null)
            Anim = Util.FindChild<Animator>(gameObject);

        // Sprite Library 교체 (몬스터 전용)
        if (CreatureData is Data.MonsterData monsterData && !string.IsNullOrEmpty(monsterData.SpriteLibraryName))
        {
            SpriteLibrary spriteLib = GetComponent<SpriteLibrary>();
            if (spriteLib == null)
                spriteLib = Util.FindChild<SpriteLibrary>(gameObject);

            if (spriteLib != null)
            {
                SpriteLibraryAsset asset = Managers.Resource.Load<SpriteLibraryAsset>(monsterData.SpriteLibraryName);
                if (asset != null)
                {
                    spriteLib.spriteLibraryAsset = asset;
                    if (Anim != null)
                        Anim.Rebind();
                }
                else
                    Debug.LogWarning($"[Creature] SpriteLibraryAsset not found: {monsterData.SpriteLibraryName}");
            }
        }

        // 플레이어/동료(아군 캐릭터)에게 월드 스페이스 HP바 부착
        if (_hpBar == null && this is AllyController)
        {
            GameObject hpBarGo = Managers.Resource.Instantiate("UI_HPBar");
            if (hpBarGo != null)
            {
                hpBarGo.transform.SetParent(transform);
                hpBarGo.transform.localPosition = HP_BAR_OFFSET;
                hpBarGo.transform.localRotation = Quaternion.identity;
                hpBarGo.transform.localScale = HP_BAR_SCALE;
                _hpBar = hpBarGo.GetOrAddComponent<UI_HPBar>();
                _hpBar.SetOwner(this);
            }
        }

        return true;
    }

    public virtual void InitSkill()
    {
        if (CreatureData.SkillList == null) return;
        foreach (int skillId in CreatureData.SkillList)
        {
            SkillType type = Util.GetSkillTypeFromInt(skillId);
            if (type != SkillType.None)
            {
                Skills.AddSkill(type, this, skillId);
            }
        }
    }

    protected virtual void UpdateAnimation()
    {
        if (Anim == null)
            return;
    }

    /// <summary>
    /// 크리티컬 데미지 계산 (CriRate/CriDamage는 % 단위)
    /// </summary>
    public (float damage, bool isCritical) CalcCriticalDamage(float damage, float criRatePercent, float criDamagePercent)
    {
        float rate = criRatePercent / 100f;
        float multiplier = 1f + (criDamagePercent / 100f);
        bool isCritical = Random.value <= rate;
        if (isCritical)
            damage *= multiplier;
        return (damage, isCritical);
    }

    public virtual void OnDamaged(BaseController attacker, SkillBase skill = null, float damage = 0)
    {
        bool isCritical = false;
        // 아군 캐릭터(플레이어/동료) 모두 크리티컬 적용
        AllyController allyAttacker = attacker as AllyController;
        if (allyAttacker != null)
        {
            var crit = CalcCriticalDamage(damage, allyAttacker.CriRate, allyAttacker.CriDamage);
            damage = crit.damage;
            isCritical = crit.isCritical;
        }

        if (skill)
            skill.TotalDamage += damage;
        Hp -= damage;
        bool isAlly = this is AllyController;
        Managers.Object.ShowDamageFont(CenterPosition, damage, transform, isCritical, isAlly);

        if (gameObject.IsValid() && this.IsValid())
        {
            if (Hp <= 0)
            {
                transform.localScale = Vector3.one;
                OnDead();
            }
            else
            {
                StartCoroutine(PlayDamageAnimation());
            }
        }
    }

    // 스킬 사용 시 타겟 방향으로 스파인 플립 (자식 클래스에서 오버라이드)
    public virtual void FaceDirection(float dirX) { }

    public virtual void OnDead()
    {
        Rigid.simulated = false;
        transform.localScale = Vector3.one;
        CreatureState = CreatureState.Dead;
    }

    public virtual void InitCreatureStat(bool isFullHp = true)
    {
        bool isMonster = CreatureData is Data.MonsterData;

        if (isMonster)
        {
            int stageLevel = Managers.Game?.GameScene?.CurrentStage?.StageLevel ?? 1;
            float scale = StageStatProvider.GetMonsterScale(stageLevel);
            MaxHp = CreatureData.MaxHp * scale;
            Atk = CreatureData.Atk * scale;
            Def = CreatureData.Def * scale;
        }
        else
        {
            MaxHp = CreatureData.MaxHp;
            Atk = CreatureData.Atk;
            Def = CreatureData.Def;
        }

        Hp = MaxHp;
        MoveSpeed = CreatureData.MoveSpeed * CreatureData.MoveSpeedRate;
    }

    // 캐릭터 강화 패시브 적용 - CharacterController(아군)에서 오버라이드. 몬스터는 무시
    public virtual void ApplyEnhancePassives() { }

    public enum CreatureDataType { Character, Monster }

    public void SetInfo(int creatureId, CreatureDataType type = CreatureDataType.Character)
    {
        Data.CreatureData creatureData = null;

        if (type == CreatureDataType.Character)
        {
            if (Managers.Data.CharacterDic.TryGetValue(creatureId, out Data.CharacterData cd))
                creatureData = cd;
        }
        else
        {
            if (Managers.Data.MonsterDic.TryGetValue(creatureId, out Data.MonsterData md))
                creatureData = md;
        }

        if (creatureData == null)
        {
            Debug.LogError($"[CreatureController] {type} data not found: {creatureId}");
            return;
        }
        DataId = creatureId;
        CreatureData = creatureData;
        InitCreatureStat();
        Init();
    }

    protected IEnumerator PlayDamageAnimation()
    {
        if (isPlayDamagedAnim)
            yield break;

        isPlayDamagedAnim = true;

        if (CreatureRenderer is SpriteRenderer)
        {
            CreatureRenderer.material = HitEffectMat;
            yield return new WaitForSeconds(HIT_FLASH_DURATION);
            CreatureRenderer.material = DefaultMat;
        }
        else
        {
            CreatureRenderer.SetPropertyBlock(HitBlock);
            yield return new WaitForSeconds(HIT_FLASH_DURATION);
            CreatureRenderer.SetPropertyBlock(NormalBlock);
        }

        isPlayDamagedAnim = false;
    }
}

