using UnityEngine;
using static Define;

// 모든 컨트롤러의 최상위 기반 클래스 - 오브젝트 타입과 중복 초기화 방지를 담당
public class BaseController : MonoBehaviour
{
    public ObjectType ObjectType { get; protected set; } // 오브젝트 종류 (Player, Monster, Projectile 등)

    bool _init = false; // 중복 초기화 방지 플래그

    void Awake()
    {
        Init();
    }

    // 한 번만 초기화 - 이미 초기화됐으면 false 반환
    public virtual bool Init()
    {
        if (_init)
            return false;

        _init = true;
        return true;
    }
}
