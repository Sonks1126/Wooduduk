using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wooduduk.Data.DataSO;

namespace Wooduduk.Data.Core.TSVConvert.Editor
{
    public class DataImporter
    {
        private const string DataManagerPath = "Assets/_WOODUDUK/Data/DataManager.asset";

        #region TSV 경로 (구글 시트 연동 시 이 파일을 덮어씀)
        private const string SymbolTSVPath       = "Assets/_WOODUDUK/Scripts/Data/Core/TSVConvert/DataSheets/Symbol_Stats.tsv";
        private const string CardTSVPath         = "Assets/_WOODUDUK/Scripts/Data/Core/TSVConvert/DataSheets/Card_Stats.tsv";
        private const string MakgeolliTSVPath    = "Assets/_WOODUDUK/Scripts/Data/Core/TSVConvert/DataSheets/Makgeolli_Stats.tsv";
        private const string WeaponTSVPath       = "Assets/_WOODUDUK/Scripts/Data/Core/TSVConvert/DataSheets/Weapon_Stats.tsv";
        private const string RouletteSkinTSVPath = "Assets/_WOODUDUK/Scripts/Data/Core/TSVConvert/DataSheets/RouletteSkin_Stats.tsv";
        #endregion

        #region SO 저장 폴더
        private const string SymbolSavePath       = "Assets/_WOODUDUK/Data/Symbol";
        private const string CardSavePath         = "Assets/_WOODUDUK/Data/Card";
        private const string MakgeolliSavePath    = "Assets/_WOODUDUK/Data/Makgeolli";
        private const string WeaponSavePath       = "Assets/_WOODUDUK/Data/Weapon";
        private const string RouletteSkinSavePath = "Assets/_WOODUDUK/Data/RouletteSkin";
        #endregion

        // 구글 시트 탭 gid (URL의 #gid= 숫자)
        private const int SymbolGid       = 0;
        private const int MakgeolliGid    = 953400646;
        private const int CardGid         = 1082198758;
        private const int WeaponGid       = 662138804;
        private const int RouletteSkinGid = 475532175;

        [MenuItem("Tools/Data Import/All")]
        public static void ImportAll()
        {
            ImportSymbol();
            ImportCard();
            ImportMakgeolli();
            ImportWeapon();
            ImportRouletteSkin();
            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[DataImport] 전체 데이터 갱신 완료!</color>");
        }

        [MenuItem("Tools/Data Import/Symbol")]
        public static void ImportSymbol()
        {
            Log("Symbol");
            GoogleSheetsImporter.ImportFromGoogleSheets(SymbolTSVPath, SymbolGid);
            TSVImporter.ImportTSV<SymbolDataSO>(SymbolTSVPath, SymbolSavePath);
            RefreshDM<SymbolDataSO>(SymbolSavePath, (dm, list) => dm._symbolDataList = list);
        }

        [MenuItem("Tools/Data Import/Card")]
        public static void ImportCard()
        {
            Log("Card");
            GoogleSheetsImporter.ImportFromGoogleSheets(CardTSVPath, CardGid);
            TSVImporter.ImportTSV<CardDataSO>(CardTSVPath, CardSavePath);
            RefreshDM<CardDataSO>(CardSavePath, (dm, list) => dm._cardDataList = list);
        }

        [MenuItem("Tools/Data Import/Makgeolli")]
        public static void ImportMakgeolli()
        {
            Log("Makgeolli");
            GoogleSheetsImporter.ImportFromGoogleSheets(MakgeolliTSVPath, MakgeolliGid);
            TSVImporter.ImportTSV<MakgeolliEffectDataSO>(MakgeolliTSVPath, MakgeolliSavePath);
            RefreshDM<MakgeolliEffectDataSO>(MakgeolliSavePath, (dm, list) => dm._makgeolliDataList = list);
        }

        [MenuItem("Tools/Data Import/Weapon")]
        public static void ImportWeapon()
        {
            Log("Weapon");
            GoogleSheetsImporter.ImportFromGoogleSheets(WeaponTSVPath, WeaponGid);
            TSVImporter.ImportTSV<WeaponDataSO>(WeaponTSVPath, WeaponSavePath);
            RefreshDM<WeaponDataSO>(WeaponSavePath, (dm, list) => dm._weaponDataList = list);
        }

        [MenuItem("Tools/Data Import/RouletteSkin")]
        public static void ImportRouletteSkin()
        {
            Log("RouletteSkin");
            GoogleSheetsImporter.ImportFromGoogleSheets(RouletteSkinTSVPath, RouletteSkinGid);
            TSVImporter.ImportTSV<RouletteSkinDataSO>(RouletteSkinTSVPath, RouletteSkinSavePath);
            RefreshDM<RouletteSkinDataSO>(RouletteSkinSavePath, (dm, list) => dm._rouletteSkinDataList = list);
        }

        private static void Log(string sheet) =>
            Debug.Log($"<color=yellow>[DataImport] {sheet} 다운로드 및 변환 중...</color>");

        private static void RefreshDM<T>(string folderPath, System.Action<DataManagerSO, List<T>> setter)
            where T : ScriptableObject
        {
            var dm = AssetDatabase.LoadAssetAtPath<DataManagerSO>(DataManagerPath);
            if (dm == null)
            {
                string dir = System.IO.Path.GetDirectoryName(DataManagerPath);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                dm = ScriptableObject.CreateInstance<DataManagerSO>();
                AssetDatabase.CreateAsset(dm, DataManagerPath);
                AssetDatabase.Refresh();
                Debug.Log("<color=lime>[DataImport] DataManager.asset 자동 생성. gameBalance·roulette는 인스펙터에서 직접 연결하세요.</color>");
            }
            TSVImporter.RefreshList<T>(dm, folderPath, list => setter(dm, list));
            Debug.Log("<color=cyan>[DataImport] DataManager 등록 완료!</color>");
        }
    }
}
