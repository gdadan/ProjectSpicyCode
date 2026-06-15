
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

// Library 폴더(Boss/Melee/Range/Rush 포함, 재귀)의 .spriteLib 자산을
// Addressables "Library" 그룹 등록 상태 + "PreLoad" 라벨 부착 여부로 검사하고,
// 누락된 항목을 한 번에 등록/라벨링하는 에디터 도구.
public class LibraryAddressableChecker : EditorWindow
{
    private Vector2 _scrollPos;
    private List<string> _missingPaths = new List<string>();
    private List<string> _registeredPaths = new List<string>();
    private List<string> _missingPreloadPaths = new List<string>(); // 등록됐지만 PreLoad 라벨 없는 항목
    private bool _autoPreload = true;
    private const string LIBRARY_FOLDER = "Assets/100_AH/5_Animations/Library";
    private const string GROUP_NAME = "Library";
    private const string PRELOAD_LABEL = "PreLoad";

    [MenuItem("Tools/AH/Library Addressable Checker")]
    public static void ShowWindow()
    {
        GetWindow<LibraryAddressableChecker>("Library Addressable Checker");
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Refresh()
    {
        _missingPaths.Clear();
        _registeredPaths.Clear();
        _missingPreloadPaths.Clear();

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;

        // Find all .spriteLib files in Library folder
        string[] guids = AssetDatabase.FindAssets("t:UnityEngine.U2D.Animation.SpriteLibraryAsset", new[] { LIBRARY_FOLDER });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var entry = settings.FindAssetEntry(guid);

            if (entry == null)
            {
                _missingPaths.Add(path);
            }
            else
            {
                _registeredPaths.Add(path);
                if (!entry.labels.Contains(PRELOAD_LABEL))
                    _missingPreloadPaths.Add(path);
            }
        }

        _missingPaths.Sort();
        _registeredPaths.Sort();
        _missingPreloadPaths.Sort();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "검사 대상\n" +
            $"  • {LIBRARY_FOLDER} 의 모든 .spriteLib (하위 폴더 Boss/Melee/Range/Rush 포함)\n\n" +
            "검사 항목\n" +
            $"  • Missing      : Addressables에 미등록\n" +
            $"  • No PreLoad   : 등록되었지만 \"{PRELOAD_LABEL}\" 라벨 없음\n" +
            $"  • Registered   : 등록 + \"{PRELOAD_LABEL}\" 라벨 보유 (정상)\n\n" +
            "동작\n" +
            $"  • Register   : \"{GROUP_NAME}\" 그룹에 등록, address = 파일명\n" +
            $"  • Auto PreLoad Label 켜짐 시 등록과 동시에 \"{PRELOAD_LABEL}\" 라벨 부착",
            MessageType.Info);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh", GUILayout.Width(100)))
            Refresh();
        _autoPreload = EditorGUILayout.ToggleLeft("Auto PreLoad Label", _autoPreload, GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (_missingPaths.Count > 0)
        {
            if (GUILayout.Button($"Register All Missing ({_missingPaths.Count})", GUILayout.Width(250)))
                RegisterAllMissing();
        }
        if (_missingPreloadPaths.Count > 0)
        {
            if (GUILayout.Button($"Add PreLoad Label ({_missingPreloadPaths.Count})", GUILayout.Width(250)))
                AddPreloadLabelToAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"Registered: {_registeredPaths.Count}  |  Missing: {_missingPaths.Count}  |  No PreLoad: {_missingPreloadPaths.Count}", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        if (_missingPaths.Count > 0)
        {
            EditorGUILayout.LabelField("Missing (not in Addressables):", EditorStyles.boldLabel);
            foreach (string path in _missingPaths)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(path));
                GUI.color = Color.white;
                if (GUILayout.Button("Register", GUILayout.Width(80)))
                {
                    RegisterSingle(path);
                    Refresh();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space(10);
        }

        EditorGUILayout.LabelField("Registered:", EditorStyles.boldLabel);
        foreach (string path in _registeredPaths)
        {
            GUI.color = Color.green;
            EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(path));
        }
        GUI.color = Color.white;

        EditorGUILayout.EndScrollView();
    }

    private void RegisterAllMissing()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        AddressableAssetGroup group = settings.groups.FirstOrDefault(g => g.Name == GROUP_NAME);

        if (group == null)
        {
            Debug.LogError($"Addressable group '{GROUP_NAME}' not found!");
            return;
        }

        int count = 0;
        foreach (string path in _missingPaths)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = Path.GetFileNameWithoutExtension(path);
            if (_autoPreload)
                entry.SetLabel(PRELOAD_LABEL, true, true);
            count++;
            Debug.Log($"Registered: {entry.address}{(_autoPreload ? " +PreLoad" : "")}");
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"Done! Registered {count} Library assets to Addressables.");
        Refresh();
    }

    private void RegisterSingle(string path)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        AddressableAssetGroup group = settings.groups.FirstOrDefault(g => g.Name == GROUP_NAME);

        if (group == null) return;

        string guid = AssetDatabase.AssetPathToGUID(path);
        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = Path.GetFileNameWithoutExtension(path);
        if (_autoPreload)
            entry.SetLabel(PRELOAD_LABEL, true, true);

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"Registered: {entry.address}{(_autoPreload ? " +PreLoad" : "")}");
    }

    private void AddPreloadLabelToAll()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        int count = 0;

        foreach (string path in _missingPreloadPaths)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.FindAssetEntry(guid);
            if (entry != null)
            {
                entry.SetLabel(PRELOAD_LABEL, true, true);
                count++;
            }
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"Done! Added PreLoad label to {count} entries.");
        Refresh();
    }
}
