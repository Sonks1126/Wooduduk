using System.Collections.Generic;
using Wooduduk.Data.Result;
using Wooduduk.Data.Static;

namespace Wooduduk.Network.Firebase.Leaderboard
{
    public static class LeaderboardService
    {
        // ★ forceRefresh 기본값을 true로 설정! (열 때마다 무조건 서버에서 갱신)
        public static void Load(UserData user, bool forceRefresh = true)
        {
            if (user == null)
            {
                LeaderboardEvents.RaiseLoaded(new LeaderboardViewData
                {
                    _latestRecord = null,
                    _myTop3Scores = new int[3],
                    _myRankRecord = null,
                    _top100 = new List<RankingData>()
                });
                return;
            }

            // ★ 1단계: forceRefresh가 false일 때만 캐시를 사용함
            if (!forceRefresh && LeaderboardCache.TryGet(user._userId, out var cached))
            {
                LeaderboardEvents.RaiseLoaded(cached);
                return;
            }

            // ★ 2단계: 서버 캐시(user_ranks) + TOP100 로드 (서버 통신)
            var repository = new LeaderboardRepository();

            repository.LoadMyRank(user._userId, (myRank, myScore) =>
            {
                repository.LoadTop100(top100 =>
                {
                    RankingData latestRecord = new()
                    {
                        _rank = 0,
                        _userId = user._userId,
                        _nickname = user._userNick,
                        _score = user._latestScore
                    };

                    RankingData myRankRecord = myRank > 0
                        ? new RankingData
                        {
                            _rank = myRank,
                            _userId = user._userId,
                            _nickname = user._userNick,
                            _score = myScore
                        }
                        : null;

                    int[] top3 = user._bestScores != null
                        ? (int[])user._bestScores.Clone()
                        : new int[3];

                    LeaderboardViewData data = new()
                    {
                        _latestRecord = latestRecord,
                        _myTop3Scores = top3,
                        _myRankRecord = myRankRecord,
                        _top100 = top100
                    };

                    // 다음을 위해 캐시에 저장은 해둠
                    LeaderboardCache.Save(user._userId, data);

                    LeaderboardEvents.RaiseLoaded(data);
                });
            });
        }
    }
}