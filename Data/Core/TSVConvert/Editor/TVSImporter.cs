using Wooduduk.Data.DataSO;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wooduduk.Data.Core.TSVConvert.Editor
{
    public static class TSVImporter
    {
        public static void ImportTSV<T>(string tsvPath, string savePath) where T : ScriptableObject, ISheetData
        {
            if (!File.Exists(tsvPath))
            {
                Debug.LogError($"<color=red>[Error] 파일을 찾을 수 없습니다!</color> 경로를 다시 확인하세요: {tsvPath}");
                return;
            }

            if (!Directory.Exists(savePath)) // 폴더 없으면 폴더 생성
            {
                Directory.CreateDirectory(savePath);
                AssetDatabase.Refresh();
                Debug.Log($"[Info] 새 폴더를 생성했습니다: {savePath}");
            }
            // 한글 깨짐 방지 UTF-8 읽기
            string[] allLines = File.ReadAllLines(tsvPath, System.Text.Encoding.UTF8);
            if (allLines.Length < 3) return; // 0, 1번은 헤더

            // 헤더 읽기 (2번째 줄)
            string[] headers = allLines[1].Split('\t').Select(h => h.Trim()).ToArray();
            if (headers.Length == 0 || string.IsNullOrWhiteSpace(headers[0]))
            {
                Debug.LogError("[Error] 헤더를 읽지 못했습니다. 파일 형식/헤더 줄을 확인하세요.");
                return;
            }
            Debug.Log($"[Check] 첫 번째 헤더: {headers[0]}");

            for (int i = 2; i < allLines.Length; i++) // 1:설명, 2:헤더, 3:데이터
            {
                if (string.IsNullOrWhiteSpace(allLines[i])) continue;

                string[] values = allLines[i].Split('\t');
                var rowData = new Dictionary<string, string>();

                for (int j = 0; j < headers.Length; j++)
                {
                    if (j < values.Length)
                        rowData[headers[j]] = values[j].Trim();
                }

                string idKey = rowData.Keys.FirstOrDefault(k => k.Trim().ToUpper() == "ID");

                if (string.IsNullOrEmpty(idKey) || string.IsNullOrEmpty(rowData[idKey]))
                {
                    Debug.LogWarning($"<color=orange>[Warning] {i}번째 줄에 'ID' 컬럼을 찾을 수 없거나 값이 비어있습니다.</color>");
                    continue;
                }

                string id = rowData[idKey]; // ID 추출 및 
                string name = rowData.ContainsKey("Name") ? rowData["Name"] : "NoName";

                T asset = LoadOrCreateAsset<T>(id, name, savePath);
                asset.SetData(rowData); // 인터페이스를 통해 데이터 설정
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets(); // 실제 asset으로 기록 및 새로고침
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[Importer] {typeof(T).Name} 임포트 완료!</color>");
        }

        private static T LoadOrCreateAsset<T>(string id, string name, string savePath) where T : ScriptableObject, ISheetData
        {
            string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            string fullPath = $"{savePath}/{id}_{safeName}.asset";
            T asset = AssetDatabase.LoadAssetAtPath<T>(fullPath);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                asset.ID = id; // 인터페이스를 통해 ID 설정
                AssetDatabase.CreateAsset(asset, fullPath);
            }
            return asset;
        }

        public static void RefreshList<T>(DataManagerSO dm, string folderPath, System.Action<List<T>> updateAction) where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folderPath });
            List<T> list = new List<T>();
            foreach (var g in guids) list.Add(AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)));

            updateAction(list);
            EditorUtility.SetDirty(dm);
        }
    }
}