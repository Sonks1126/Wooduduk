namespace Wooduduk.Data.Result
{
    [System.Serializable]
    public class SettlementResultData
    {
        /// <summary>
        /// 몇 번째 정산인지
        /// </summary>
        public int _settlementIndex;

        /// <summary>
        /// 기본 장작 획득량
        /// </summary>
        public int _baseWood;

        /// <summary>
        /// 콤보 보너스 장작
        /// </summary>
        public int _comboBonusWood;

        /// <summary>
        /// 정밀도 보너스 배율
        /// </summary>
        public float _precisionBonusRate;

        /// <summary>
        /// 최종 획득 장작
        /// </summary>
        public int _totalWood;

        /// <summary>
        /// 퍼펙트 여부
        /// </summary>
        public bool _isPerfect;
    }
}