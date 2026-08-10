using System.Collections.Generic;

namespace Wooduduk.Data.Result
{
    [System.Serializable]
    public class LeaderboardViewData
    {
        // 내 기록 탭
        public RankingData _latestRecord;     // 최신 기록
        public int[] _myTop3Scores;           // 최고 점수 1,2,3위

        // TOP100 탭
        public RankingData _myRankRecord;     // 내 전체 순위 기록 (100밖도 가능)
        public List<RankingData> _top100;     // TOP100 리스트
    }
}