using System;
using System.Collections.Generic;
using Wooduduk.Data.Static.Roulette;
using Wooduduk.Data.DataSO;

namespace Wooduduk.Data.Static
{
    /// <summary>
    /// 하나의 룰렛 정의
    /// (심볼 + 릴 + 규칙 묶음)
    /// </summary>
    [Serializable]
    public class RouletteData
    {
        /// <summary>
        /// 룰렛 ID
        /// </summary>
        public string _id;

        /// <summary>
        /// 릴 구조
        /// </summary>
        public ReelData _reelData;

        /// <summary>
        /// 이 룰렛이 사용하는 심볼 풀 (SO 참조 — 값 복제 X).
        /// 각 심볼의 _baseWeight 가 기본 출현율이고, 런타임이 활성 효과로 가중치를 보정한다.
        /// </summary>
        public List<SymbolDataSO> _symbols;
    }
}