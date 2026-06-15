using UnityEngine;

// 게임 전체 매니저 진입점 - 싱글턴으로 모든 매니저 인스턴스를 보유하며 씬 간 유지
public class Managers : MonoBehaviour
{
    private static Managers _instance;

    public static Managers Instance
    {
        get { Init(); return _instance; }
    }

    #region Core Managers
    GameManager _game = new GameManager();
    ObjectManager _object = new ObjectManager();
    TimeManager _time = new TimeManager();
    AdsManager _ads = new AdsManager();
    AchievementManager _achievement = new AchievementManager();
    BackendManager _backend = new BackendManager();

    public static GameManager Game { get { return Instance?._game; } }
    public static ObjectManager Object { get { return Instance?._object; } }
    public static TimeManager Time { get { return Instance?._time; } }
    public static AdsManager Ads { get { return Instance?._ads; } }
    public static AchievementManager Achievement { get { return Instance?._achievement; } }
    public static BackendManager Backend { get { return Instance?._backend; } }
    #endregion

    #region Contents Managers
    DataManager _data = new DataManager();
    StatUpgradeManager _statUpgrade = new StatUpgradeManager();
    CharacterEnhanceManager _characterEnhance = new CharacterEnhanceManager();
    PoolManager _pool = new PoolManager();
    ResourceManager _resource = new ResourceManager();
    SceneManagerEx _scene = new SceneManagerEx();
    SoundManager _sound = new SoundManager();
    UIManager _ui = new UIManager();
    UserDatabaseManager _userDB = new UserDatabaseManager();


    public static DataManager Data { get { return Instance?._data; } }
    public static PoolManager Pool { get { return Instance?._pool; } }
    public static ResourceManager Resource { get { return Instance?._resource; } }
    public static SceneManagerEx Scene { get { return Instance?._scene; } }
    public static SoundManager Sound { get { return Instance?._sound; } }
    public static UIManager UI { get { return Instance?._ui; } }
    public static StatUpgradeManager StatUpgrade { get { return Instance?._statUpgrade; } }
    public static CharacterEnhanceManager CharacterEnhance { get { return Instance?._characterEnhance; } }
    public static UserDatabaseManager UserDB { get { return Instance?._userDB; } }
    #endregion


    // @Managers GameObject를 찾거나 생성하여 DontDestroyOnLoad로 유지
    public static void Init()
    {
        if (_instance == null)
        {
            GameObject go = GameObject.Find("@Managers");
            if (go == null)
            {
                go = new GameObject { name = "@Managers" };
                go.AddComponent<Managers>();
            }

            DontDestroyOnLoad(go);
            _instance = go.GetComponent<Managers>();

            // 뒤끝 서버 초기화 (게스트 로그인은 타이틀 씬에서 분기 처리)
            _instance._backend.Init();

            // 유저 세이브 데이터 로드 및 런타임 데이터 복원
            _instance._userDB.Init();
            _instance._game.Gold = _instance._userDB.Gold;
            _instance._game.TotalKillCount = _instance._userDB.TotalKillCount;
            _instance._game.CurrentStageId = _instance._userDB.Data.BattleProgress.HighestClearedStageId + 1;
            _instance._statUpgrade.LoadFromUserDB();
            _instance._characterEnhance.LoadFromUserDB();
        }
    }

    // 씬 전환 시 각 매니저 상태 초기화
    public static void Clear()
    {
        UI.Clear();
        Object.Clear();
        Pool.Clear();
    }

    // 앱 백그라운드 전환 시 유저 데이터 저장
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && _userDB != null)
        {
            _userDB.Gold = _game.Gold;
            _userDB.TotalKillCount = _game.TotalKillCount;
            _userDB.Save();
        }
    }

    // 앱 종료 시 유저 데이터 저장
    void OnApplicationQuit()
    {
        if (_userDB != null)
        {
            _userDB.Gold = _game.Gold;
            _userDB.TotalKillCount = _game.TotalKillCount;
            _userDB.Save();
        }
    }
}
