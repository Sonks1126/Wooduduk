using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.Core.TSVConvert;
using Wooduduk.Data.Static.StaticEnum;
using Wooduduk.Slot;

namespace Wooduduk.Data.DataSO
{
    [CreateAssetMenu(fileName = "DataManager", menuName = "GameData/DataManager")]
    public class DataManagerSO : ScriptableObject
    {
        #region 인스펙터 — 에디터에서 드래그
        public List<SymbolDataSO> _symbolDataList;
        public List<CardDataSO> _cardDataList;
        public List<MakgeolliEffectDataSO> _makgeolliDataList;
        public List<WeaponDataSO> _weaponDataList;
        public List<RouletteSkinDataSO> _rouletteSkinDataList;

        // TSV 미사용, 인스펙터에서 직접 연결
        public GameBalanceDataSO _gameBalance;
        public RouletteDataSO _roulette;
        #endregion

        #region 런타임 딕셔너리
        private Dictionary<string, SymbolDataSO> _symbolDict;
        private Dictionary<string, CardDataSO> _cardDict;
        private Dictionary<string, MakgeolliEffectDataSO> _makgeolliDict;
        private Dictionary<string, WeaponDataSO> _weaponDict;
        private Dictionary<string, RouletteSkinDataSO> _rouletteSkinDict;
        #endregion

        public void Initialize()
        {
            _symbolDict = BuildDict(_symbolDataList);
            _cardDict = BuildDict(_cardDataList);
            _makgeolliDict = BuildDict(_makgeolliDataList);
            _weaponDict = BuildDict(_weaponDataList);
            _rouletteSkinDict = BuildDict(_rouletteSkinDataList);

            Debug.Log("[DataManager] 초기화 완료");
        }

        private Dictionary<string, T> BuildDict<T>(List<T> list) where T : ScriptableObject, ISheetData
        {
            var dict = new Dictionary<string, T>();
            if (list == null) return dict;
            foreach (var item in list)
            {
                if (item == null) continue;
                if (dict.ContainsKey(item.ID))
                    Debug.LogWarning($"[DataManager] 중복 ID: {item.ID}");
                else
                    dict.Add(item.ID, item);
            }
            return dict;
        }

        #region 게터
        public SymbolDataSO GetSymbol(string id)
        {
            if (_symbolDict == null) Initialize();
            return _symbolDict.TryGetValue(id, out var so) ? so : null;
        }

        public CardDataSO GetCard(string id)
        {
            if (_cardDict == null) Initialize();
            return _cardDict.TryGetValue(id, out var so) ? so : null;
        }

        public MakgeolliEffectDataSO GetMakgeolli(string id)
        {
            if (_makgeolliDict == null) Initialize();
            return _makgeolliDict.TryGetValue(id, out var so) ? so : null;
        }

        public WeaponDataSO GetWeapon(string id)
        {
            if (_weaponDict == null) Initialize();
            return _weaponDict.TryGetValue(id, out var so) ? so : null;
        }

        public RouletteSkinDataSO GetRouletteSkin(string id)
        {
            if (_rouletteSkinDict == null) Initialize();
            return _rouletteSkinDict.TryGetValue(id, out var so) ? so : null;
        }
        #endregion

        #region 런타임 빌더

        // symbolDataList(TSV 임포트)에서 슬롯 추첨 엔진용 ISymbolCatalog를 빌드.
        // SymbolType(룰렛/UI 열거형) → SymbolId(슬롯 엔진 열거형) 변환 + 가중치 ×10(TSV 32 → 엔진 320).
        // SlotMachineFactory에서 SlotSymbolDefaults.CreateCatalog() 대신 이걸 호출.
        public ISymbolCatalog BuildSymbolCatalog()
        {
            var defs = new List<SymbolDef>();
            foreach (var s in _symbolDataList)
            {
                if (s?._data == null) continue;
                if (TryMapSymbolId(s._data._symbolType, out SymbolId id))
                    defs.Add(new SymbolDef(id, s._data._name, (int)(s._data._baseWeight * 10)));
            }
            return new InMemorySymbolCatalog(defs);
        }

        // SymbolType과 SymbolId는 이름이 다른 같은 심볼 (INSTANT=도토리, WILD=행운, BLANK=옹이...).
        // 매핑 실패한 타입은 조용히 스킵 → symbolDataList에 모르는 타입 추가돼도 엔진 안 깨짐
        private static bool TryMapSymbolId(SymbolType t, out SymbolId id)
        {
            switch (t)
            {
                case SymbolType.SWING: id = SymbolId.SWING; return true;
                case SymbolType.PERFECT: id = SymbolId.PERFECT; return true;
                case SymbolType.INSTANT: id = SymbolId.ACORN; return true;
                case SymbolType.WILD: id = SymbolId.LUCK; return true;
                case SymbolType.BOOST: id = SymbolId.FRIEBAT; return true;
                case SymbolType.BLANK: id = SymbolId.KNOT; return true;
                case SymbolType.FAIL: id = SymbolId.WORM; return true;
                case SymbolType.TRIGGER: id = SymbolId.MAKGEOLLI; return true;
                default: id = default; return false;
            }
        }

        #endregion

    }
}
