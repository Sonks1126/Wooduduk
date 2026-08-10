using System;
using Wooduduk.Data.Static.StaticEnum;

namespace Wooduduk.Data.Static.Roulette
{
    /// <summary>
    /// 룰렛 결과 단위 (순수 결과 데이터)
    /// </summary>
    [Serializable]
    public class SymbolData
    {
        public string _id;
        public string _name;

        /// <summary>
        /// UI 효과 설명
        /// </summary>
        public string _effectDescription;

        public SymbolType _symbolType;     // 판정 분기 (_effectType string 대체)

        public float _baseWeight;    // 기본 출현율(가중치)
    }
}