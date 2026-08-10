using System.Collections.Generic;
using Wooduduk.Data.Result;
using Wooduduk.Data.Service;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase.Leaderboard;

namespace Wooduduk.Network.Firebase
{
    public static class RunResultSaveService
    {
        public static void Save(RunResultData result, UserData user)
        {
            if (result == null || user == null) return;

            user._gamesPlayed++;
            int previousBest = user._bestScores[0];
            UpdateBestScores(user._bestScores, result._finalScore);
            result._isNewRecord = user._bestScores[0] > previousBest;
            user._gold += result._earnedGold;
            user._latestScore = result._finalScore;

            LeaderboardCache.Clear();

            SaveManager.Instance.UpdateUserFields(user._userId, new Dictionary<string, object>
            {
                { "_bestScores", new List<int> { user._bestScores[0], user._bestScores[1], user._bestScores[2] } },
                { "_gold", user._gold },
                { "_latestScore", user._latestScore },
                { "_gamesPlayed", user._gamesPlayed }
            });

            if (result._isNewRecord)
            {
                var ranking = RankingBuilder.Build(user, result);
                var repo = new LeaderboardRepository();
                if (ranking != null)
                {
                    repo.Save(ranking);
                    repo.LoadTop100(top100 =>
                    {
                        int index = top100.FindIndex(x => x._userId == user._userId);
                        int myRank = index >= 0 ? index + 1 : -1;
                        repo.SaveMyRank(user._userId, myRank, result._finalScore);
                    });
                }
            }
        }

        private static void UpdateBestScores(int[] scores, int newScore)
        {
            if (scores == null || scores.Length < 3) return;
            if (newScore > scores[0]) { scores[2] = scores[1]; scores[1] = scores[0]; scores[0] = newScore; }
            else if (newScore > scores[1]) { scores[2] = scores[1]; scores[1] = newScore; }
            else if (newScore > scores[2]) { scores[2] = newScore; }
        }
    }
}