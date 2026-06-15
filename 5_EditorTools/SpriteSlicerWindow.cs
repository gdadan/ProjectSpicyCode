using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace AH.EditorTools
{
    public class SpriteSlicerWindow : EditorWindow
    {
        private const string RushFolderToken = "/Rush/";
        private const string DefaultFolderPath = "Assets/100_AH/1_Arts/Sprites/Creature";

        private DefaultAsset _targetFolder;
        private int _columns = 3;
        private int _rows = 3;
        private float _slicePixelsPerUnit = 100f;
        private float _rushPixelsPerUnit = 70f;
        private bool _overwriteExistingSlices = true;

        [MenuItem("Tools/AH/Sprite Slicer")]
        public static void Open()
        {
            var window = GetWindow<SpriteSlicerWindow>("Sprite Slicer");
            window.minSize = new Vector2(360f, 220f);
        }

        private void OnEnable()
        {
            if (_targetFolder == null)
            {
                _targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultFolderPath);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Sprite Slicer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Target Folder", _targetFolder, typeof(DefaultAsset), false);

            _columns = Mathf.Max(1, EditorGUILayout.IntField("Columns", _columns));
            _rows = Mathf.Max(1, EditorGUILayout.IntField("Rows", _rows));
            _slicePixelsPerUnit = Mathf.Max(1f, EditorGUILayout.FloatField("Slice PPU (non-Rush)", _slicePixelsPerUnit));
            _rushPixelsPerUnit = Mathf.Max(1f, EditorGUILayout.FloatField("Rush PPU", _rushPixelsPerUnit));
            _overwriteExistingSlices = EditorGUILayout.Toggle("Overwrite Existing Slices", _overwriteExistingSlices);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"폴더 경로에 \"{RushFolderToken.Trim('/')}\"가 포함된 PNG는 Single 임포트 + Pivot=Bottom + PPU={_rushPixelsPerUnit}로 처리합니다.\n" +
                $"나머지는 {_columns}x{_rows} GridByCellCount + Pivot=Bottom + PPU={_slicePixelsPerUnit}로 슬라이스합니다.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(_targetFolder == null))
            {
                if (GUILayout.Button("Slice", GUILayout.Height(32f)))
                {
                    Slice();
                }
            }
        }

        private void Slice()
        {
            string folderPath = AssetDatabase.GetAssetPath(_targetFolder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                EditorUtility.DisplayDialog("Sprite Slicer", "유효한 폴더가 아닙니다.", "OK");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            int sliced = 0;
            int singled = 0;
            int skipped = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    string normalized = path.Replace("\\", "/");

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Sprite Slicer",
                            $"{i + 1}/{guids.Length}  {Path.GetFileName(path)}",
                            (float)i / Mathf.Max(1, guids.Length)))
                    {
                        break;
                    }

                    bool isRush = normalized.Contains(RushFolderToken);
                    if (isRush)
                    {
                        if (ApplySingle(path)) singled++;
                        else skipped++;
                    }
                    else
                    {
                        if (ApplyGridSlice(path, _columns, _rows)) sliced++;
                        else skipped++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[SpriteSlicer] sliced={sliced}, single={singled}, skipped={skipped}, total={guids.Length}");
            EditorUtility.DisplayDialog(
                "Sprite Slicer",
                $"완료\n슬라이스: {sliced}\n단일 임포트(Rush): {singled}\n스킵: {skipped}",
                "OK");
        }

        private bool ApplySingle(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return false;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
            settings.spritePixelsPerUnit = _rushPixelsPerUnit;
            ti.SetTextureSettings(settings);

            ti.SaveAndReimport();
            return true;
        }

        private bool ApplyGridSlice(string path, int cols, int rowsCount)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return false;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return false;

            int width = tex.width;
            int height = tex.height;
            int cellW = width / cols;
            int cellH = height / rowsCount;
            if (cellW <= 0 || cellH <= 0)
            {
                Debug.LogWarning($"[SpriteSlicer] cell size 0 → skip: {path} (w={width}, h={height})");
                return false;
            }

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Multiple;
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
            settings.spritePixelsPerUnit = _slicePixelsPerUnit;
            ti.SetTextureSettings(settings);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(ti);
            if (dataProvider == null)
            {
                Debug.LogWarning($"[SpriteSlicer] dataProvider null → skip: {path}");
                return false;
            }
            dataProvider.InitSpriteEditorDataProvider();

            if (!_overwriteExistingSlices)
            {
                var existing = dataProvider.GetSpriteRects();
                if (existing != null && existing.Length > 0)
                {
                    ti.SaveAndReimport();
                    return true;
                }
            }

            string baseName = Path.GetFileNameWithoutExtension(path);
            var newRects = new List<SpriteRect>(cols * rowsCount);
            int index = 0;
            for (int r = 0; r < rowsCount; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    newRects.Add(new SpriteRect
                    {
                        name = $"{baseName}_{index}",
                        rect = new Rect(c * cellW, height - (r + 1) * cellH, cellW, cellH),
                        alignment = SpriteAlignment.BottomCenter,
                        pivot = new Vector2(0.5f, 0f),
                        spriteID = GUID.Generate(),
                    });
                    index++;
                }
            }

            dataProvider.SetSpriteRects(newRects.ToArray());

            // internalIDToNameTable 갱신 — 옛 이름이 stable mapping으로 남는 것을 막기 위해 필수
            var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameFileIdProvider != null)
            {
                var pairs = new List<SpriteNameFileIdPair>(newRects.Count);
                foreach (var r in newRects)
                {
                    pairs.Add(new SpriteNameFileIdPair(r.name, r.spriteID));
                }
                nameFileIdProvider.SetNameFileIdPairs(pairs);
            }

            dataProvider.Apply();
            ti.SaveAndReimport();
            return true;
        }
    }
}
