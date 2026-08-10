namespace Wooduduk.Data.Result
{
    [System.Serializable]
    public class RankingData
    {
        public int _rank;

        public string _userId;
        public string _nickname;

        public int _score; // 최고 점수

        // 확장용
        public int _maxCombo; // 최대 콤보
        public int _settlementCount; // 정산 횟수

        public long _timestamp;
    }
}