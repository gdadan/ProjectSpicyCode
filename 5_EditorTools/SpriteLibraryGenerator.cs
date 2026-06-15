using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// 스프라이트 라이브러리 자동 생성 에디터 도구
//
// Melee/Range/Rush 모드: Sprites/Creature/{Type}/ 폴더의 각 PNG(스프라이트시트)에 대해
//   override .spriteLib 1개씩 생성. PNG의 9프레임을 카테고리 전체에 0..8 순서로 매핑.
//
// Boss 모드: Sprites/Creature/Boss/{보스폴더}/ 안의 PNG 여러 개(액션별 시트)를
//   하나의 override .spriteLib로 합성. PNG suffix를 메인 카테고리명과 정규화 매칭(_ 무시)해서
//   해당 카테고리 라벨 _0.._8 에 9프레임 매핑. 매칭 안 되는 카테고리는 override 없이 m_FromMain만.
//   _backup 폴더는 제외.
public class SpriteLibraryGenerator : EditorWindow
{
    // 타입별 메인 라이브러리 경로
    static readonly Dictionary<string, string> MainLibraryPaths = new Dictionary<string, string>
    {
        { "Melee", "Assets/100_AH/5_Animations/Library/Melee/MeleeAttack_Library.spriteLib" },
        { "Range", "Assets/100_AH/5_Animations/Library/Range/RangeAttack_Library.spriteLib" },
        { "Rush",  "Assets/100_AH/5_Animations/Library/Rush/RushAttack_Library.spriteLib" },
        { "Boss",  "Assets/100_AH/5_Animations/Library/Boss/BossAttack_Library.spriteLib" },
    };

    // 스프라이트 폴더 경로
    static readonly Dictionary<string, string> SpriteFolders = new Dictionary<string, string>
    {
        { "Melee", "Assets/100_AH/1_Arts/Sprites/Creature/Melee" },
        { "Range", "Assets/100_AH/1_Arts/Sprites/Creature/Range" },
        { "Rush",  "Assets/100_AH/1_Arts/Sprites/Creature/Rush" },
        { "Boss",  "Assets/100_AH/1_Arts/Sprites/Creature/Boss" },
    };

    // 라이브러리 출력 폴더
    static readonly Dictionary<string, string> LibraryFolders = new Dictionary<string, string>
    {
        { "Melee", "Assets/100_AH/5_Animations/Library/Melee" },
        { "Range", "Assets/100_AH/5_Animations/Library/Range" },
        { "Rush",  "Assets/100_AH/5_Animations/Library/Rush" },
        { "Boss",  "Assets/100_AH/5_Animations/Library/Boss" },
    };

    // Boss 모드에서 제외할 하위 폴더명
    static readonly HashSet<string> BossExcludedFolders = new HashSet<string> { "_backup" };

    string _selectedType = "Melee";
    Vector2 _scrollPos;
    // Melee/Range/Rush: PNG 경로 리스트, Boss: 보스 하위 폴더 경로 리스트
    List<string> _missingItems = new List<string>();
    List<string> _resultLog = new List<string>();

    [MenuItem("Tools/AH/Sprite Library Generator")]
    static void Open()
    {
        GetWindow<SpriteLibraryGenerator>("SpriteLib Generator");
    }

    void OnGUI()
    {
        GUILayout.Label("스프라이트 라이브러리 자동 생성", EditorStyles.boldLabel);
        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(_selectedType == "Melee", "Melee", "Button")) _selectedType = "Melee";
        if (GUILayout.Toggle(_selectedType == "Range", "Range", "Button")) _selectedType = "Range";
        if (GUILayout.Toggle(_selectedType == "Rush",  "Rush",  "Button")) _selectedType = "Rush";
        if (GUILayout.Toggle(_selectedType == "Boss",  "Boss",  "Button")) _selectedType = "Boss";
        GUILayout.EndHorizontal();

        if (_selectedType == "Boss")
        {
            EditorGUILayout.HelpBox(
                "Boss 모드: Sprites/Creature/Boss/{보스폴더} 단위로 라이브러리 1개 생성.\n" +
                "PNG suffix와 메인 카테고리명을 '_' 제거 후 매칭 (예: Attack_1 ↔ Attack1).\n" +
                "_backup 폴더는 제외.",
                MessageType.Info);
        }

        GUILayout.Space(5);

        string findButtonLabel = _selectedType == "Boss"
            ? "라이브러리 없는 보스 폴더 검색"
            : "라이브러리 없는 PNG 검색";

        if (GUILayout.Button(findButtonLabel))
            FindMissing();

        if (_missingItems.Count > 0)
        {
            string itemLabel = _selectedType == "Boss" ? "보스 폴더" : "PNG";
            GUILayout.Label($"라이브러리 없는 {itemLabel}: {_missingItems.Count}개", EditorStyles.miniLabel);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(150));
            foreach (var item in _missingItems)
                GUILayout.Label("  " + Path.GetFileName(item), EditorStyles.miniLabel);
            GUILayout.EndScrollView();

            if (GUILayout.Button("선택된 타입의 라이브러리 자동 생성"))
                GenerateAll();
        }

        GUILayout.Space(5);
        if (GUILayout.Button("전체 타입 일괄 생성"))
            GenerateAllTypes();

        if (_resultLog.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("결과:", EditorStyles.boldLabel);
            foreach (var log in _resultLog)
                GUILayout.Label(log, EditorStyles.miniLabel);
        }
    }

    void FindMissing()
    {
        _missingItems.Clear();
        if (_selectedType == "Boss")
            FindMissingBossFolders();
        else
            FindMissingLibraries();
    }

    void FindMissingLibraries()
    {
        if (!SpriteFolders.ContainsKey(_selectedType)) return;

        string spriteFolder = SpriteFolders[_selectedType];
        string libFolder = LibraryFolders[_selectedType];

        string[] pngGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { spriteFolder });
        foreach (string guid in pngGuids)
        {
            string pngPath = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(pngPath);
            string libPath = Path.Combine(libFolder, fileName + "_Library.spriteLib");

            if (!File.Exists(libPath))
                _missingItems.Add(pngPath);
        }
    }

    void FindMissingBossFolders()
    {
        string spriteRoot = SpriteFolders["Boss"];
        string libFolder = LibraryFolders["Boss"];

        if (!Directory.Exists(spriteRoot)) return;

        foreach (var dir in Directory.GetDirectories(spriteRoot))
        {
            string folderName = Path.GetFileName(dir);
            if (BossExcludedFolders.Contains(folderName)) continue;

            string libPath = Path.Combine(libFolder, folderName + "_Library.spriteLib");
            if (!File.Exists(libPath))
                _missingItems.Add(dir.Replace("\\", "/"));
        }
    }

    void GenerateAllTypes()
    {
        _resultLog.Clear();
        foreach (var type in MainLibraryPaths.Keys)
        {
            _selectedType = type;
            FindMissing();
            if (_missingItems.Count > 0)
                GenerateAll();
        }
        AssetDatabase.Refresh();
    }

    void GenerateAll()
    {
        _resultLog.Clear();
        if (_selectedType == "Boss")
            GenerateAllBoss();
        else
            GenerateAllStandard();
    }

    void GenerateAllStandard()
    {
        string mainLibPath = MainLibraryPaths[_selectedType];

        if (!File.Exists(mainLibPath))
        {
            _resultLog.Add($"[ERROR] 메인 라이브러리를 찾을 수 없음: {mainLibPath}");
            return;
        }

        string mainLibGuid = AssetDatabase.AssetPathToGUID(mainLibPath);
        string mainLibContent = File.ReadAllText(mainLibPath, Encoding.UTF8);
        var mainCategories = ParseLibraryCategories(mainLibContent);

        if (mainCategories.Count == 0)
        {
            _resultLog.Add("[ERROR] 메인 라이브러리에서 카테고리를 파싱할 수 없음");
            return;
        }

        int totalEntries = 0;
        foreach (var cat in mainCategories)
            totalEntries += cat.Entries.Count;

        foreach (string pngPath in _missingItems)
        {
            string fileName = Path.GetFileNameWithoutExtension(pngPath);
            string libFolder = LibraryFolders[_selectedType];
            string outputPath = Path.Combine(libFolder, fileName + "_Library.spriteLib");

            string pngGuid = AssetDatabase.AssetPathToGUID(pngPath);
            var spriteFileIds = GetSpriteFileIds(pngPath + ".meta");

            if (spriteFileIds.Count == 0)
            {
                _resultLog.Add($"[SKIP] {fileName}: 스프라이트를 찾을 수 없음");
                continue;
            }

            if (spriteFileIds.Count < totalEntries)
            {
                _resultLog.Add($"[WARN] {fileName}: 스프라이트 {spriteFileIds.Count}개 < 필요 {totalEntries}개, 부족분은 첫번째 스프라이트로 채움");
            }

            string yaml = GenerateOverrideLibrary(mainCategories, mainLibGuid, pngGuid, spriteFileIds);
            File.WriteAllText(outputPath, yaml, Encoding.UTF8);
            _resultLog.Add($"[OK] {fileName}_Library.spriteLib 생성 완료");
        }

        AssetDatabase.Refresh();
        FindMissing();
    }

    void GenerateAllBoss()
    {
        string mainLibPath = MainLibraryPaths["Boss"];

        if (!File.Exists(mainLibPath))
        {
            _resultLog.Add($"[ERROR] 보스 메인 라이브러리를 찾을 수 없음: {mainLibPath}");
            return;
        }

        string mainLibGuid = AssetDatabase.AssetPathToGUID(mainLibPath);
        string mainLibContent = File.ReadAllText(mainLibPath, Encoding.UTF8);
        var mainCategories = ParseLibraryCategoriesLite(mainLibContent);

        if (mainCategories.Count == 0)
        {
            _resultLog.Add("[ERROR] 보스 메인에서 카테고리 파싱 실패");
            return;
        }

        string libFolder = LibraryFolders["Boss"];

        foreach (string bossFolderPath in _missingItems)
        {
            string bossFolderName = Path.GetFileName(bossFolderPath);
            string outputPath = Path.Combine(libFolder, bossFolderName + "_Library.spriteLib");

            var categoryToPng = MatchCategoryToPng(mainCategories, bossFolderPath, bossFolderName);

            int unmatchedCats = mainCategories.Count - categoryToPng.Count;
            int unmatchedPngs = CountUnmatchedPngs(bossFolderPath, bossFolderName, mainCategories);

            string yaml = GenerateBossOverrideLibrary(mainCategories, mainLibGuid, categoryToPng);
            File.WriteAllText(outputPath, yaml, Encoding.UTF8);

            string warn = "";
            if (unmatchedCats > 0) warn += $" (메인 카테고리 {unmatchedCats}개 PNG 없음)";
            if (unmatchedPngs > 0) warn += $" (메인에 없는 PNG {unmatchedPngs}개 무시)";
            _resultLog.Add($"[OK] {bossFolderName}_Library.spriteLib  매칭 {categoryToPng.Count}/{mainCategories.Count}{warn}");
        }

        AssetDatabase.Refresh();
        FindMissing();
    }

    // 보스 폴더 안 PNG들을 메인 카테고리에 매핑 (suffix vs 카테고리명, '_' 무시 정규화)
    Dictionary<string, PngMatch> MatchCategoryToPng(List<CategoryData> mainCategories, string bossFolderPath, string bossFolderName)
    {
        var result = new Dictionary<string, PngMatch>();
        string[] pngPaths = Directory.GetFiles(bossFolderPath, "*.png");

        foreach (var cat in mainCategories)
        {
            string normCat = Normalize(cat.Name);
            foreach (var pngPath in pngPaths)
            {
                string pngName = Path.GetFileNameWithoutExtension(pngPath);
                string suffix = ExtractActionSuffix(pngName, bossFolderName);
                if (Normalize(suffix) != normCat) continue;

                string assetPath = pngPath.Replace("\\", "/");
                string pngGuid = AssetDatabase.AssetPathToGUID(assetPath);
                var fileIds = GetSpriteFileIds(assetPath + ".meta");
                if (fileIds.Count == 0) continue;

                result[cat.Name] = new PngMatch { PngGuid = pngGuid, FileIds = fileIds };
                break;
            }
        }
        return result;
    }

    int CountUnmatchedPngs(string bossFolderPath, string bossFolderName, List<CategoryData> mainCategories)
    {
        var catNorms = new HashSet<string>();
        foreach (var c in mainCategories) catNorms.Add(Normalize(c.Name));

        int count = 0;
        foreach (var pngPath in Directory.GetFiles(bossFolderPath, "*.png"))
        {
            string pngName = Path.GetFileNameWithoutExtension(pngPath);
            string suffix = ExtractActionSuffix(pngName, bossFolderName);
            if (!catNorms.Contains(Normalize(suffix))) count++;
        }
        return count;
    }

    string ExtractActionSuffix(string pngFileName, string folderName)
    {
        string prefix = folderName + "_";
        return pngFileName.StartsWith(prefix) ? pngFileName.Substring(prefix.Length) : pngFileName;
    }

    string Normalize(string s) => s.Replace("_", "").ToLowerInvariant();

    // 표준 메인(sprite reference 포함) 파서. fileID/guid 매칭 필수.
    List<CategoryData> ParseLibraryCategories(string yaml)
    {
        var categories = new List<CategoryData>();

        var catMatches = Regex.Matches(yaml,
            @"- m_Name: (.+)\n\s+m_Hash: (\d+)\n\s+m_CategoryList: \[\]\n\s+m_OverrideEntries:\n((?:\s+- m_Name:[\s\S]*?(?=\n  - m_Name:|\n\s+m_FromMain:))?)\n\s+m_FromMain: \d+\n\s+m_EntryOverrideCount: (\d+)",
            RegexOptions.Multiline);

        foreach (Match catMatch in catMatches)
        {
            var cat = new CategoryData
            {
                Name = catMatch.Groups[1].Value.Trim(),
                Hash = catMatch.Groups[2].Value.Trim(),
            };

            string entriesBlock = catMatch.Groups[3].Value;
            var entryMatches = Regex.Matches(entriesBlock,
                @"- m_Name: (.+)\n\s+m_Hash: (\d+)\n\s+m_Sprite: \{fileID: (-?\d+), guid: ([a-f0-9]+), type: 3\}",
                RegexOptions.Multiline);

            foreach (Match entryMatch in entryMatches)
            {
                cat.Entries.Add(new EntryData
                {
                    Name = entryMatch.Groups[1].Value.Trim(),
                    Hash = entryMatch.Groups[2].Value.Trim(),
                    MainFileId = entryMatch.Groups[3].Value.Trim(),
                    MainGuid = entryMatch.Groups[4].Value.Trim()
                });
            }

            categories.Add(cat);
        }

        return categories;
    }

    // 라이트 파서: sprite reference 유무와 무관하게 카테고리/라벨 이름·hash만 추출 (보스 메인용).
    // 들여쓰기 기준: 카테고리 "  - m_Name:" (2 spaces), 라벨 "    - m_Name:" (4 spaces).
    List<CategoryData> ParseLibraryCategoriesLite(string yaml)
    {
        var result = new List<CategoryData>();
        var lines = yaml.Replace("\r\n", "\n").Split('\n');
        CategoryData currentCat = null;

        for (int i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i];

            // 카테고리: "  - m_Name: <name>" + next line "    m_Hash: <hash>"
            var catStart = Regex.Match(line, @"^  - m_Name:\s*(.+)$");
            if (catStart.Success)
            {
                var hashMatch = Regex.Match(lines[i + 1], @"^\s{4}m_Hash:\s*(\d+)\s*$");
                if (hashMatch.Success)
                {
                    currentCat = new CategoryData
                    {
                        Name = catStart.Groups[1].Value.Trim(),
                        Hash = hashMatch.Groups[1].Value
                    };
                    result.Add(currentCat);
                }
                continue;
            }

            // 라벨: "    - m_Name: <name>" + next line "      m_Hash: <hash>"
            var entryStart = Regex.Match(line, @"^    - m_Name:\s*(.+)$");
            if (entryStart.Success && currentCat != null)
            {
                var hashMatch = Regex.Match(lines[i + 1], @"^\s{6}m_Hash:\s*(\d+)\s*$");
                if (hashMatch.Success)
                {
                    currentCat.Entries.Add(new EntryData
                    {
                        Name = entryStart.Groups[1].Value.Trim(),
                        Hash = hashMatch.Groups[1].Value
                    });
                }
            }
        }

        return result;
    }

    // PNG meta 파일에서 스프라이트 fileID 목록 추출 (순서대로).
    // spriteMode를 먼저 분기:
    //   - Single (1): 표준 fileID 21300000 단일 반환 (옛 Multiple 잔재 spriteID/internalID 무시)
    //   - Multiple (2): sprites 섹션의 internalID 패턴 매칭
    // Single PNG meta에 옛 Multiple 잔재가 남아있어도 안전하게 21300000을 쓰도록 분기 순서 중요.
    List<string> GetSpriteFileIds(string metaPath)
    {
        var fileIds = new List<string>();
        if (!File.Exists(metaPath)) return fileIds;

        string content = File.ReadAllText(metaPath, Encoding.UTF8);

        var modeMatch = Regex.Match(content, @"spriteMode:\s*(\d+)");
        bool isSingle = modeMatch.Success && modeMatch.Groups[1].Value == "1";

        if (isSingle)
        {
            fileIds.Add("21300000");
        }
        else
        {
            var matches = Regex.Matches(content, @"spriteID: [a-f0-9]+\s*\r?\n\s+internalID: (-?\d+)", RegexOptions.Multiline);
            foreach (Match m in matches)
                fileIds.Add(m.Groups[1].Value);
        }

        return fileIds;
    }

    // override spriteLib YAML 생성 (Melee/Range/Rush: PNG 1개 → 전체 카테고리에 0..N 순으로 분배)
    string GenerateOverrideLibrary(List<CategoryData> categories, string mainLibGuid,
        string pngGuid, List<string> spriteFileIds)
    {
        var sb = new StringBuilder();
        AppendYamlHeader(sb);

        foreach (var cat in categories)
        {
            sb.AppendLine($"  - m_Name: {cat.Name}");
            sb.AppendLine($"    m_Hash: {cat.Hash}");
            sb.AppendLine("    m_CategoryList: []");
            sb.AppendLine("    m_OverrideEntries:");

            int spriteIndex = 0;
            foreach (var entry in cat.Entries)
            {
                string overrideFileId = spriteIndex < spriteFileIds.Count
                    ? spriteFileIds[spriteIndex]
                    : spriteFileIds[0];

                sb.AppendLine($"    - m_Name: {entry.Name}");
                sb.AppendLine($"      m_Hash: {entry.Hash}");
                sb.AppendLine($"      m_Sprite: {{fileID: {entry.MainFileId}, guid: {entry.MainGuid}, type: 3}}");
                sb.AppendLine("      m_FromMain: 1");
                sb.AppendLine($"      m_SpriteOverride: {{fileID: {overrideFileId}, guid: {pngGuid}, type: 3}}");

                spriteIndex++;
            }

            sb.AppendLine("    m_FromMain: 1");
            sb.AppendLine($"    m_EntryOverrideCount: {cat.Entries.Count}");
        }

        AppendYamlFooter(sb, mainLibGuid);
        return sb.ToString();
    }

    // override spriteLib YAML 생성 (Boss: 카테고리별 PNG 매핑)
    string GenerateBossOverrideLibrary(List<CategoryData> categories, string mainLibGuid,
        Dictionary<string, PngMatch> categoryToPng)
    {
        var sb = new StringBuilder();
        AppendYamlHeader(sb);

        foreach (var cat in categories)
        {
            sb.AppendLine($"  - m_Name: {cat.Name}");
            sb.AppendLine($"    m_Hash: {cat.Hash}");
            sb.AppendLine("    m_CategoryList: []");
            sb.AppendLine("    m_OverrideEntries:");

            bool hasMatch = categoryToPng.TryGetValue(cat.Name, out var match);

            int spriteIndex = 0;
            foreach (var entry in cat.Entries)
            {
                sb.AppendLine($"    - m_Name: {entry.Name}");
                sb.AppendLine($"      m_Hash: {entry.Hash}");
                sb.AppendLine("      m_Sprite: {fileID: 0}");
                sb.AppendLine("      m_FromMain: 1");

                if (hasMatch)
                {
                    string overrideFileId = spriteIndex < match.FileIds.Count
                        ? match.FileIds[spriteIndex]
                        : match.FileIds[0];
                    sb.AppendLine($"      m_SpriteOverride: {{fileID: {overrideFileId}, guid: {match.PngGuid}, type: 3}}");
                }
                else
                {
                    sb.AppendLine("      m_SpriteOverride: {fileID: 0}");
                }
                spriteIndex++;
            }

            sb.AppendLine("    m_FromMain: 1");
            sb.AppendLine($"    m_EntryOverrideCount: {cat.Entries.Count}");
        }

        AppendYamlFooter(sb, mainLibGuid);
        return sb.ToString();
    }

    void AppendYamlHeader(StringBuilder sb)
    {
        sb.AppendLine("%YAML 1.1");
        sb.AppendLine("%TAG !u! tag:unity3d.com,2011:");
        sb.AppendLine("--- !u!114 &1");
        sb.AppendLine("MonoBehaviour:");
        sb.AppendLine("  m_ObjectHideFlags: 0");
        sb.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
        sb.AppendLine("  m_PrefabInstance: {fileID: 0}");
        sb.AppendLine("  m_PrefabAsset: {fileID: 0}");
        sb.AppendLine("  m_GameObject: {fileID: 0}");
        sb.AppendLine("  m_Enabled: 1");
        sb.AppendLine("  m_EditorHideFlags: 0");
        sb.AppendLine("  m_Script: {fileID: 11500000, guid: a5e6fedc2472449cead18ef23b5cb30d, type: 3}");
        sb.AppendLine("  m_Name: ");
        sb.AppendLine("  m_EditorClassIdentifier: Unity.2D.Animation.Runtime::UnityEngine.U2D.Animation.SpriteLibrarySourceAsset");
        sb.AppendLine("  m_Library:");
    }

    void AppendYamlFooter(StringBuilder sb, string mainLibGuid)
    {
        sb.AppendLine($"  m_PrimaryLibraryGUID: {mainLibGuid}");
        sb.AppendLine($"  m_ModificationHash: {GenerateModificationHash()}");
        sb.Append("  m_Version: 1");
    }

    long GenerateModificationHash()
    {
        return System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000L + Random.Range(0, 999999);
    }

    class CategoryData
    {
        public string Name;
        public string Hash;
        public List<EntryData> Entries = new List<EntryData>();
    }

    class EntryData
    {
        public string Name;
        public string Hash;
        public string MainFileId;
        public string MainGuid;
    }

    class PngMatch
    {
        public string PngGuid;
        public List<string> FileIds;
    }
}
