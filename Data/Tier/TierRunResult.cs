namespace Wooduduk.Data.Tier
{
    [System.Serializable]
    public class TierRunResult
    {
        public string _userId;
        public string _userNick;

        // 티어 계산용
        public int _rawScore;

        // 고스트 저장용
        public int _settlementCount;
        public int _maxCombo;

        public long _timestamp;
    }
}