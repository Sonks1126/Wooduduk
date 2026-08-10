namespace Wooduduk.Data.Result
{

    [System.Serializable]
    public class RunResultData
    {
        public string _userId;
        public string _userNick;

        // 최종 점수
        public int _finalScore;
        // 이번 런에서 획득한 총 골드
        public int _earnedGold;
        // 최고 콤보
        public int _maxCombo;
        // 생존 시간(초)
        public float _survivalTime;
        // 정산 횟수
        public int _settlementCount;

        // 멀티용
        public long _rawScore;       // ★ 티어용 (배율 적용 전 순수 점수)
        public int _rank;            // ★ 멀티 등수 (1등=1, 동점 시 공동)


        // 최고 점수 갱신 여부
        public bool _isNewRecord;
        // 종료 시각 (UTC Unix Timestamp)
        public long _timestamp;
    }
}