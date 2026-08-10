using System;
using Wooduduk.Data.Result;
using Wooduduk.Data.Static;

namespace Wooduduk.Data.Service
{
    public static class RankingBuilder
    {
        public static RankingData Build(UserData user, RunResultData result)
        {
            if (user == null || result == null) return null;

            return new RankingData
            {
                _rank = 0, // 로드 시 계산되므로 0으로 전송

                _userId = user._userId,
                _nickname = user._userNick ?? "Unknown",

                // 리더보드에는 사용자의 역대 최고 점수(TOP 1)를 등록
                _score = user._bestScores[0],

                // 이번 런의 기록을 함께 저장 (선택 사항)
                _maxCombo = result._maxCombo,
                _settlementCount = result._settlementCount,

                _timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
    }
}