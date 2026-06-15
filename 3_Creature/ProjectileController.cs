using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using static Define;

// 투사체 컨트롤러 - 방향 이동, 충돌 데미지, 일정 시간 후 자동 제거 처리
public class ProjectileController : SkillBase
{
    const float AUTO_DESTROY_TIME = 5f;
    const float CHAIN_LIGHTNING_DURATION = 0.25f;
    const float METEOR_EXPLOSION_RANGE = 1.5f;
    const float METEOR_SHADOW_MAX_SCALE = 2.5f;
    const float METEOR_SHADOW_DISTANCE_FACTOR = 10f;
    const float METEOR_HIT_DISTANCE = 0.3f;
    const float PHOTON_STRIKE_TIMEOUT = 3f;
    const float PHOTON_ROTATE_AMOUNT = 1000f;
    const float WIND_CUTTER_TRAVEL_TIME = 1f;
    const float WIND_CUTTER_SECOND_SEQ_START = 0.7f;
    const float WIND_CUTTER_SECOND_SEQ_DURATION = 1.8f;
    const float WIND_CUTTER_RETURN_SPEED_MULT = 4f;

    CreatureController _owner;
    public SkillBase Skill;
    Vector2 _spawnPos;
    Vector3 _dir = Vector3.zero;
    Vector3 _target = Vector3.zero;
    Define.SkillType _skillType;
    Rigidbody2D _rigid;
    int _pierceCount;
    public int _bounceCount = 1;
    GameObject _meteorShadow;

    List<CreatureController> _enteredColliderList = new List<CreatureController>();
    Coroutine _coDotDamage;
    List<Transform> _chainLightningList = new List<Transform>();
    
    private void OnDisable()
    {
        StopAllCoroutines();
    }
    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        ObjectType = ObjectType.Projectile;
        return true;
    }

    // 투사체 초기화 - 발사자, 방향, 목표 설정 후 자동 제거 코루틴 시작
    public void SetInfo(CreatureController owner, Vector2 position, Vector2 dir, Vector2 target, SkillBase skill)
    {
        _owner = owner;
        Owner = owner;
        _spawnPos = position;
        _dir = dir;
        Skill = skill;
        _rigid = GetComponent<Rigidbody2D>();
        _target = target;
        _timer = 0f;
        transform.localScale = Vector3.one * Skill.SkillData.ScaleMultiplier;
        _pierceCount = skill.SkillData.PierceCount;
        _bounceCount = skill.SkillData.BounceCount;
        switch (skill.SkillType)
        {
            case Define.SkillType.ChainLightning:
                StartCoroutine(CoChainLightning(_spawnPos, _target, true));
                break;
            case Define.SkillType.PhotonStrike:
                StartCoroutine(CoPhotonStrike());
                break;
            case Define.SkillType.Shuriken:
                _bounceCount = Skill.SkillData.BounceCount;
                _rigid.linearVelocity = _dir * Skill.SkillData.ProjectileSpeed;
                break;
            case Define.SkillType.ComboShot:
                LaunchComboShot();
                break;
            case Define.SkillType.WindCutter:
                if (gameObject.activeInHierarchy)
                    StartCoroutine(CoWindCutter());
                break;
            case Define.SkillType.Meteor:
                _dir = (_target - transform.position).normalized;
                transform.rotation = Quaternion.FromToRotation(Vector3.up, _dir);
                _rigid.linearVelocity = _dir * Skill.SkillData.ProjectileSpeed;
                _meteorShadow = Managers.Resource.Instantiate("MeteorShadow", pooling: true);
                _meteorShadow.transform.position = target; //+ new Vector3(-0.5f, -0.45f, 1);
                if (gameObject.activeInHierarchy)
                    StartCoroutine(CoMeteor());
                break;
            case Define.SkillType.PoisonField:
                if (gameObject.activeInHierarchy)
                    StartCoroutine(CoPosionField(skill));
                break;
            case Define.SkillType.EgoSword:
            case Define.SkillType.StormBlade:
                StartCoroutine(CoDestroy());
                transform.rotation = Quaternion.FromToRotation(Vector3.up, _dir);
                _rigid.linearVelocity = _dir * Skill.SkillData.ProjectileSpeed;
                break;
            default:
                transform.rotation = Quaternion.FromToRotation(Vector3.up, _dir);
                _pierceCount = Skill.SkillData.PierceCount;
                _rigid.linearVelocity = _dir * Skill.SkillData.ProjectileSpeed;
                break;
        }

        if (gameObject.activeInHierarchy)
            StartCoroutine(CoCheckDestory());
    }
    float _timer = 0;

    IEnumerator CoChainLightning(Vector3 startPos, Vector3 endPos, bool isFollow = false)
    {
        SetParticleSize(startPos, endPos);
        yield return new WaitForSeconds(CHAIN_LIGHTNING_DURATION);
        DestroyProjectile();
    }

    void SetParticleSize(Vector3 startPos, Vector3 endPos)
    {
        ParticleSystem particle = GetComponent<ParticleSystem>();
        ParticleSystem childParticle = Util.FindChild<ParticleSystem>(gameObject);
        var main = particle.main;
        var main2 = childParticle.main;

        // Scale
        transform.position = startPos;
        float dist = Vector3.Distance(startPos, endPos);
        main.startSizeX = main2.startSizeX = dist;
        main.startSizeY = main2.startSizeY = 8;
        // rotatate
        Vector3 dir = (endPos - startPos).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x);
        main.startRotation = main2.startRotation = angle * -1f;

        // Cast box
        List<Transform> listMonster = new List<Transform>();
        LayerMask targetLayer = LayerMask.GetMask("Monster", "Boss");
        float boxWidth = 1f;
        Vector3 midPos = (startPos + endPos) / 2f; // 시작점과 끝점 사이의 중간 지점
        Vector2 boxSize = new Vector2(boxWidth, boxWidth);
        //Vector2 boxSize = new Vector2(300, 399);
        float angleRad = angle * Mathf.Deg2Rad;

        RaycastHit2D[] colliders = Physics2D.BoxCastAll(midPos, boxSize, 0, dir, dist * 1.3f, targetLayer);

        foreach (RaycastHit2D hit in colliders)
        {
            MonsterController monster = hit.transform.GetComponent<MonsterController>();
            if (monster != null)
            {
                monster.OnDamaged(_owner, Skill);
            }
        }
    }

    IEnumerator CoWindCutter()
    {
        Vector3 targePoint = Managers.Game.Player.CenterPosition + _dir * Skill.SkillData.ProjectileSpeed;
        transform.localScale = Vector3.zero;
        transform.localScale = Vector3.one * Skill.SkillData.ScaleMultiplier;

        Sequence seq = DOTween.Sequence();
        // 1. 목표지점까지 빠르게 도착
        // 2. 도착수 약간 더 전진
        // 3. 되돌아옴

        seq.Append(transform.DOMove(targePoint, WIND_CUTTER_TRAVEL_TIME).SetEase(Ease.OutExpo))
            .Insert(WIND_CUTTER_SECOND_SEQ_START, transform.DOMove(targePoint + _dir, WIND_CUTTER_SECOND_SEQ_DURATION).SetEase(Ease.Linear));

        yield return new WaitForSeconds(Skill.SkillData.Duration);

        while (true)
        {
            transform.position = Vector2.MoveTowards(transform.position, Managers.Game.Player.CenterPosition, Time.deltaTime * Skill.SkillData.ProjectileSpeed * WIND_CUTTER_RETURN_SPEED_MULT);
            if (Managers.Game.Player.CenterPosition == transform.position)
            {
                DestroyProjectile();
                break;
            }
            yield return new WaitForFixedUpdate();
        }
    }

    IEnumerator CoPhotonStrike()
    {
        List<MonsterController> target = Managers.Object.GetMonsters(1);
        while (true)
        {
            _timer += Time.deltaTime;
            if (_timer > PHOTON_STRIKE_TIMEOUT || target == null)
            {
                DestroyProjectile();
                _timer = 0;
                break;
            }

            if (target[0].IsValid() == false)
                break;

            Vector2 direction = (Vector2)target[0].CenterPosition - _rigid.position;
            float rotateSpeed = Vector3.Cross(direction.normalized, transform.up).z;
            _rigid.angularVelocity = -PHOTON_ROTATE_AMOUNT * rotateSpeed;
            _rigid.linearVelocity = transform.up * Skill.SkillData.ProjectileSpeed;

            //if (Vector2.Distance(_rigid.position, targetPos) < 0.3f)
            //    ExplosionMeteor();
            yield return new WaitForFixedUpdate();
        }
    }

    IEnumerator CoMeteor()
    {
        while (true)
        {

            if (_meteorShadow != null)
            {
                Vector2 shadowPosition = _meteorShadow.transform.position;

                float distance = Vector2.Distance(shadowPosition, transform.position);
                float scale = Mathf.Lerp(0f, METEOR_SHADOW_MAX_SCALE, 1 - distance / METEOR_SHADOW_DISTANCE_FACTOR);
                _meteorShadow.transform.position = shadowPosition;
                _meteorShadow.transform.localScale = new Vector3(scale, scale, 1f);
            }
            if (Vector2.Distance(_rigid.position, _target) < METEOR_HIT_DISTANCE)
                ExplosionMeteor();
            yield return new WaitForFixedUpdate();
        }
    }

    IEnumerator CoPosionField(SkillBase skill)
    {
        while (true)
        {
            transform.position = Vector2.MoveTowards(this.transform.position, _target, Time.deltaTime * Skill.SkillData.ProjectileSpeed);

            if (transform.position == _target)
            {
                string effectName = skill.Level == 6 ? "PoisonFieldEffect_Final" : "PoisonFieldEffect";

                GameObject fireEffect = Managers.Resource.Instantiate(effectName, pooling: true);
                fireEffect.GetComponent<PoisonFieldEffect>().SetInfo(Managers.Game.Player, skill);
                fireEffect.transform.position = _target;
                DestroyProjectile();
            }
            yield return new WaitForFixedUpdate();
        }
    }
    void ExplosionMeteor()
    {
        Managers.Resource.Destroy(_meteorShadow);
        float scanRange = METEOR_EXPLOSION_RANGE;
        string prefabName = Level == 6 ? "MeteorHitEffect_Final" : "MeteorHitEffect";
        GameObject obj = Managers.Resource.Instantiate(prefabName, pooling: true);
        obj.transform.position = transform.position;

        RaycastHit2D[] _targets = Physics2D.CircleCastAll(transform.position, scanRange, Vector2.zero, 0);

        foreach (RaycastHit2D _target in _targets)
        {
            CreatureController creature = _target.transform.GetComponent<CreatureController>();
            if (creature?.IsMonster() == true)
                creature.OnDamaged(_owner, Skill);
        }
        DestroyProjectile();
    }

    void LaunchComboShot()
    {
        Vector3 targePoint = _owner.CenterPosition + _dir * Skill.SkillData.ProjectileRange;
        float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        Sequence seq = DOTween.Sequence();
        float duration = Skill.SkillData.Duration;

        seq.Append(transform.DOMove(targePoint, 0.5f).SetEase(Ease.Linear)).AppendInterval(duration - 0.5f).OnComplete(() =>
        {
            Vector3 targetDir = Managers.Game.Player.CenterPosition - transform.position;
            angle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
            _rigid.linearVelocity = targetDir.normalized * Skill.SkillData.ProjectileSpeed;
        });

    }

    void BounceProjectile(CreatureController creature)
    {
        List<Transform> list = new List<Transform>();
        list = Managers.Object.GetFindMonstersInFanShape(creature.CenterPosition, _dir, 5.5f, 240);

        List<Transform> sortedList = (from t in list
                                      orderby Vector3.Distance(t.position, transform.position) descending
                                      select t).ToList();

        if (sortedList.Count == 0)
        {
            DestroyProjectile();
        }
        else
        {
            int index = Random.Range(sortedList.Count / 2, sortedList.Count);
            _dir = (sortedList[index].position - transform.position).normalized;
            _rigid.linearVelocity = _dir * Skill.SkillData.BounceSpeed;
        }
    }

    // 투사체 제거 (오브젝트 풀 반환)
    public void DestroyProjectile()
    {
        Managers.Object.Despawn(this);
    }

    // 5초 후 자동 제거
    IEnumerator CoCheckDestory()
    {
        while (true)
        {
            yield return new WaitForSeconds(AUTO_DESTROY_TIME);
            DestroyProjectile();
        }
    }
    IEnumerator CoStartDotDamage()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            foreach (CreatureController target in _enteredColliderList)
            {
                target.OnDamaged(_owner, Skill);
            }
        }
    }
    IEnumerator CoDestroy()
    {
        yield return new WaitForSeconds(Skill.SkillData.Duration);
        DestroyProjectile();
    }
    // 충돌 시 스킬 타입에 따라 처리 후 데미지 적용
    void OnTriggerEnter2D(Collider2D collision)
    {
        CreatureController creature = collision.transform.GetComponent<CreatureController>();
        if (creature.IsValid() == false)
            return;
        if (this.IsValid() == false)
            return;

        switch (Skill.SkillType)
        {
            case Define.SkillType.PlayerBasicAttack:
                _rigid.linearVelocity = Vector3.zero;
                DestroyProjectile();
                break;
            default:
                break;
        }
        float dmg = _owner != null ? _owner.Atk * Skill.SkillData.DamageMultiplier : 0;
        creature.OnDamaged(_owner, Skill, dmg);
    }

}
