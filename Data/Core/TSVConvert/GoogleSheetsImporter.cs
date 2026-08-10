using System.Net;
using UnityEngine;

namespace Wooduduk.Data.Core.TSVConvert
{
    public static class GoogleSheetsImporter
    {
        // 시트 링크: https://docs.google.com/spreadsheets/d/1VLo78B7wvL2IIRliUL2MxfyhY1G3pxdFmSKGBsL7G_c/edit?hl=ko&gid=1082198758#gid=1082198758
        private const string GOOGLE_SHEET_LINK = "https://docs.google.com/spreadsheets/d/1VLo78B7wvL2IIRliUL2MxfyhY1G3pxdFmSKGBsL7G_c/edit?hl=ko&gid=1082198758#gid=1082198758";

        public static void ImportFromGoogleSheets(string localTSVPath, int sheetIndex = 0)
        {
            string sheetId = ExtractSheetId(GOOGLE_SHEET_LINK);
            string tsvUrl = $"https://docs.google.com/spreadsheets/d/{sheetId}/export?format=tsv&gid={sheetIndex}";

            try
            {
                using (WebClient client = new WebClient())
                {
                    string csvContent = client.DownloadString(tsvUrl);
                    System.IO.File.WriteAllText(localTSVPath, csvContent, System.Text.Encoding.UTF8);
                    Debug.Log($"<color=green>[Success] TSV 다운로드 완료 (탭: {sheetIndex}): {localTSVPath}</color>");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"<color=red>[Error] 다운로드 실패: {e.Message}</color>");
            }
        }

        private static string ExtractSheetId(string sheetLink)
        {
            var parts = sheetLink.Split('/');
            return parts[5];
        }
    }
}