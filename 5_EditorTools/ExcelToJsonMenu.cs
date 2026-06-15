using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Data.xlsx → JSON 변환 (excel_to_json.py 실행)
// Editor 폴더의 Python 스크립트를 호출하여 8_Data의 JSON들을 재생성한다.
public static class ExcelToJsonMenu
{
    private const string SCRIPT_RELATIVE = "100_AH/3_Scripts/Editor/excel_to_json.py";
    private const string EXCEL_RELATIVE = "100_AH/8_Data/Data.xlsx";

    [MenuItem("Tools/AH/Excel To Json")]
    public static void Run()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string scriptPath = Path.GetFullPath(Path.Combine(Application.dataPath, SCRIPT_RELATIVE));
        string excelPath = Path.GetFullPath(Path.Combine(Application.dataPath, EXCEL_RELATIVE));

        if (!File.Exists(scriptPath))
        {
            Debug.LogError($"[ExcelToJson] 스크립트 없음: {scriptPath}");
            return;
        }
        if (!File.Exists(excelPath))
        {
            Debug.LogError($"[ExcelToJson] 엑셀 파일 없음: {excelPath}");
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
                    Debug.Log($"[ExcelToJson]\n{stdout}");
                if (!string.IsNullOrEmpty(stderr))
                    Debug.LogWarning($"[ExcelToJson stderr]\n{stderr}");

                if (p.ExitCode != 0)
                {
                    Debug.LogError($"[ExcelToJson] 종료 코드 {p.ExitCode}");
                    return;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ExcelToJson] 실행 실패 (python이 PATH에 있는지 확인): {e.Message}");
            return;
        }

        AssetDatabase.Refresh();
        Debug.Log("[ExcelToJson] 완료");
    }
}
