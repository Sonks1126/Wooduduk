using Wooduduk.Data.Static.StaticEnum;
using System;

namespace Wooduduk.Data.Static
{
    /// <summary>
    /// 모든 시스템(카드 / 막걸리 / 아이템 / 심볼)이 사용하는 공통 효과 언어
    /// </summary>
    [Serializable]
    public class EffectData
    {
        /// <summary>
        /// 효과 종류 (확률 / 배율 / 실패 / 보호 등)
        /// </summary>
        public string _effectType;

        /// <summary>
        /// 적용 대상 (Self / All / Enemy / Global)
        /// </summary>
        public EffectTarget _target;

        /// <summary>
        /// 효과 대상 심볼 ID (출현율/가치 변경 대상. 없으면 빈 문자열)
        /// 예) 막걸리 "불타는 도끼" → 스윙 심볼 출현율 ↑ 일 때 스윙 ID
        /// </summary>
        public string _targetSymbolId;

        /// <summary>
        /// 주요 값 (배율 / 증가량 등)
        /// </summary>
        public float _value1;

        /// <summary>
        /// 보조 값 (시간 / 추가 수치 등)
        /// </summary>
        public float _value2;

        /// <summary>
        /// 지속 시간 (0 = 즉시, 스핀 기준이면 spin count)
        /// </summary>
        public int _durationSpin;
    }
}