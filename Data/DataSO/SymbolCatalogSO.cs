using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Slot;

namespace Wooduduk.Data.DataSO
{
    // 심볼 데이터 SO + 기본 가중치
    // 추첨 엔진
    public class SymbolCatalogSO : ScriptableObject, ISymbolCatalog
    {
        [SerializeField] private List<SymbolDef> _symbols = new List<SymbolDef>();

        private SymbolDef[] _byId;

        public IReadOnlyList<SymbolDef> All => _symbols;

        public SymbolDef Get(SymbolId id)
        {
            if (_byId == null) BuildIndex();
            return _byId[(int)id];
        }

        private void BuildIndex()
        {
            int count = System.Enum.GetValues(typeof(SymbolId)).Length;
            _byId = new SymbolDef[count];
            for (int i = 0; i < _symbols.Count; i++)
            {
                var d = _symbols[i];
                if (d != null) _byId[(int)d._id] = d;
            }
        }

        // 에디터에서 값 변경 시 인덱스 무효화 → 다음 Get에서 재구성
        private void OnValidate() => _byId = null;
    }
}
