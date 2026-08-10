using System;
using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Slot; 

namespace Wooduduk.Data.DataSO
{
    // 슬롯 옆 심볼 족보
    [CreateAssetMenu(fileName = "SymbolTable", menuName = "GameData/SymbolTable")]
    public class SymbolTableSO : ScriptableObject
    {
        [Serializable]
        public struct Row
        {
            public SymbolId _symbol;     // 조건 숫자 인용 + 정렬 기준
            public string _displayName;  // "스윙"
            public int _spriteIndex;     // TMP Sprite Asset 인덱스(-1이면 인라인 아이콘 없음)
            public string _effectSign;   // "×" "＋" "＝" "★" "✗" "–"
            [TextArea] public string _effectText; // "배율 ×1.5"
            public Color _color;         // 효과 색(명중=청록, 보너스=호박, 위험=빨강, 꽝=회색)
        }

        public List<Row> _rows = new List<Row>();
    }
}