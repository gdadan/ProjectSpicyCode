using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// UI 매니저 - 팝업 스택, 씬 UI, 토스트 관리 및 팝업 개수에 따른 TimeScale 제어
public class UIManager
{
    int _order = 10;         // 팝업 소팅 오더 (누적 증가)
    int _toastOrder = 500;   // 토스트 소팅 오더 (토스트는 항상 최상위)

    Stack<UI_Popup> _popupStack = new Stack<UI_Popup>(); // 열려있는 팝업 스택
    Stack<UI_Toast> _toastStack = new Stack<UI_Toast>(); // 열려있는 토스트 스택
    UI_Scene _sceneUI;
    public UI_Scene SceneUI { get { return _sceneUI; } }
    public event Action<int> OnTimeScaleChanged; // TimeScale 변경 시 호출 (팝업 유무)

    public GameObject Root
    {
        get
        {
            GameObject root = GameObject.Find("@UI_Root");
            if (root == null)
                root = new GameObject { name = "@UI_Root" };
            return root;
        }
    }

    // 캔버스 설정
    public void SetCanvas(GameObject go, bool sort = true, int sortOrder = 0, bool isToast = false)
    {
        Canvas canvas = go.GetOrAddComponent<Canvas>();

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
        }

        CanvasScaler cs = go.GetOrAddComponent<CanvasScaler>();
        if (cs != null)
        {
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1080, 1920);
        }

        go.GetOrAddComponent<GraphicRaycaster>();

        if (sort)
        {
            canvas.sortingOrder = _order;
            _order++;
        }
        else
        {
            canvas.sortingOrder = sortOrder;
        }

        if (isToast)
        {
            _toastOrder++;
            canvas.sortingOrder = _toastOrder;
        }
    }

    // 씬 전용 UI 생성 및 등록
    public T ShowSceneUI<T>(string name = null) where T : UI_Scene
    {
        if (string.IsNullOrEmpty(name))
            name = typeof(T).Name;

        GameObject go = Managers.Resource.Instantiate($"{name}");
        T sceneUI = go.GetOrAddComponent<T>();
        _sceneUI = sceneUI;
        go.transform.SetParent(Root.transform);

        return sceneUI;
    }

    // 팝업 UI 생성 및 스택에 추가 (팝업이 열리면 게임 일시정지)
    public T ShowPopupUI<T>(string name = null) where T : UI_Popup
    {
        if (string.IsNullOrEmpty(name))
            name = typeof(T).Name;

        GameObject go = Managers.Resource.Instantiate($"{name}");
        T popup = go.GetOrAddComponent<T>();
        _popupStack.Push(popup);

        go.transform.SetParent(Root.transform);
        RefreshTimeScale();

        return popup;
    }

    public void ClosePopupUI(UI_Popup popup)
    {
        if (popup == null)
            return;

        if (_popupStack.Peek() != popup)
        {
            Debug.Log("Close Popup Failed!");
            return;
        }

        Managers.Sound.PlayPopupClose();
        ClosePopupUI();
    }

    public void ClosePopupUI()
    {
        if (_popupStack.Count == 0)
            return;

        UI_Popup popup = _popupStack.Pop();
        Managers.Resource.Destroy(popup.gameObject);
        popup = null;
        _order--;
        RefreshTimeScale();
    }

    public void CloseAllPopupUI()
    {
        while (_popupStack.Count > 0)
        {
            UI_Popup popup = _popupStack.Peek();
            popup.OnBeforeClose();
            ClosePopupUI();
        }
    }

    public void Clear()
    {
        CloseAllPopupUI();
        UnityEngine.Time.timeScale = 1;
        _sceneUI = null;
    }

    // 토스트 메시지 표시 - duration초 후 자동 제거. 동시 표시 시 _toastOrder가 자동 누적되어 위로 쌓임
    public UI_Toast ShowToast(string msg, float duration = 1f)
    {
        GameObject go = Managers.Resource.Instantiate(nameof(UI_Toast), pooling: true);
        if (go == null)
            return null;

        UI_Toast toast = go.GetOrAddComponent<UI_Toast>();
        SetCanvas(go, sort: false, isToast: true);
        go.transform.SetParent(Root.transform);
        toast.SetInfo(msg);

        CoroutineManager.StartCoroutine(CloseToastAfter(toast, duration));
        return toast;
    }

    IEnumerator CloseToastAfter(UI_Toast toast, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (toast != null)
            Managers.Resource.Destroy(toast.gameObject);
    }

    // 팝업 유무에 따라 TimeScale 조정 (팝업 있으면 0, 없으면 1)
    public void RefreshTimeScale()
    {
        if (SceneManager.GetActiveScene().name != Define.Scene.GameScene.ToString())
        {
            UnityEngine.Time.timeScale = 1;
            return;
        }

        UnityEngine.Time.timeScale = 1;

        DOTween.timeScale = 1;
        OnTimeScaleChanged?.Invoke((int)UnityEngine.Time.timeScale);
    }

}
