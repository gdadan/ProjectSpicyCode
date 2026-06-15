using System;
using System.IO;
using Data;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// userdata.json 전체 초기화 (Settings 포함).
// 게임 실행 중: UserDatabaseManager.ResetAll()
// 게임 정지 중: 파일을 새 UserData로 직접 덮어쓰기
public static class ResetUserDataMenu
{
    const string SAVE_FILE_NAME = "userdata.json";

    [MenuItem("Tools/AH/Reset User Data (All)")]
    public static void Run()
    {
        if (!EditorUtility.DisplayDialog(
            "Reset User Data",
            "유저 데이터를 모두 초기화합니다.\n(Settings 포함 — BGM/효과음/언어 모두 초기값으로)\n\n계속하시겠습니까?",
            "초기화",
            "취소"))
            return;

        if (Application.isPlaying && Managers.UserDB != null)
        {
            Managers.UserDB.ResetAll(preserveSettings: false);
            Debug.Log("[ResetUserData] 게임 실행 중 - UserDB 통해 초기화 완료");
            return;
        }

        ResetFileDirectly();
    }

    [MenuItem("Tools/AH/Open Save Folder")]
    public static void OpenSaveFolder()
    {
        EditorUtility.RevealInFinder(Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME));
    }

    static void ResetFileDirectly()
    {
        string path = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

        try
        {
            var newData = new UserData();
            string newJson = JsonConvert.SerializeObject(newData, Formatting.Indented);
            File.WriteAllText(path, newJson);
            Debug.Log($"[ResetUserData] 초기화 완료: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ResetUserData] 저장 실패: {e.Message}");
        }
    }
}
