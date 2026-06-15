using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// JSON → Data.xlsx 머지 (json_to_excel.py 실행)
// 8_Data 폴더의 JSON들을 엑셀에 역동기화한다 (DataId/ID/StatType 머지).
public static class JsonToExcelMenu
{
    private const string SCRIPT_RELATIVE = "100_AH/3_Scripts/Editor/json_to_excel.py";
    private const string EXCEL_RELATIVE = "100_AH/8_Data/Data.xlsx";

    [MenuItem("Tools/AH/Json To Excel")]
    public static void Run()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string scriptPath = Path.GetFullPath(Path.Combine(Application.dataPath, SCRIPT_RELATIVE));
        string excelPath = Path.GetFullPath(Path.Combine(Application.dataPath, EXCEL_RELATIVE));

        if (!File.Exists(scriptPath))
        {
            Debug.LogError($"[JsonToExcel] 스크립트 없음: {scriptPath}");
            return;
        }
        if (!File.Exists(excelPath))
        {
            Debug.LogError($"[JsonToExcel] 엑셀 파일 없음: {excelPath}");
            return;
        }

        var psi = new ProcessStartInfo("python", $"\"{scriptPath}\" \"{excelPath}\"")
        {
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        try
        {
            using (var p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log($"[JsonToExcel]\n{stdout}");
                if (!string.IsNullOrEmpty(stderr))
                    Debug.LogWarning($"[JsonToExcel stderr]\n{stderr}");

                if (p.ExitCode != 0)
                {
                    Debug.LogError($"[JsonToExcel] 종료 코드 {p.ExitCode} (엑셀이 열려있으면 실패할 수 있음)");
                    return;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[JsonToExcel] 실행 실패 (python이 PATH에 있는지 확인): {e.Message}");
            return;
        }

        AssetDatabase.Refresh();
        Debug.Log("[JsonToExcel] 완료");
    }
}
