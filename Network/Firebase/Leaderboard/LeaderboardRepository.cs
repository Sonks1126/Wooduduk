using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wooduduk.Data.Result;

namespace Wooduduk.Network.Firebase.Leaderboard
{
    public class LeaderboardRepository
    {
        private readonly DatabaseReference _database;

        public LeaderboardRepository()
        {
            _database = FirebaseDatabase.DefaultInstance.RootReference;
        }

        // ★ 점수 저장 + 내 순위 캐시 갱신
        public void Save(RankingData data)
        {
            if (data == null || string.IsNullOrEmpty(data._userId)) return;

            string json = JsonUtility.ToJson(data);
            _database.Child("leaderboard").Child(data._userId).SetRawJsonValueAsync(json);
        }

        // ★ 내 순위 캐시 저장
        public void SaveMyRank(string userId, int rank, int score)
        {
            var data = new Dictionary<string, object>
            {
                { "_rank", rank },
                { "_score", score },
                { "_updatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
            };
            _database.Child("user_ranks").Child(userId).UpdateChildrenAsync(data);
        }

        // ★ TOP100 로드
        public void LoadTop100(Action<List<RankingData>> callback)
        {
            _database.Child("leaderboard").GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("[LeaderboardRepository] LoadTop100 실패: " + task.Exception?.GetBaseException().Message);
                    callback?.Invoke(new List<RankingData>());
                    return;
                }

                DataSnapshot snapshot = task.Result;
                Debug.Log($"[LeaderboardRepository] snapshot 수신: {snapshot.ChildrenCount}건");

                List<RankingData> rankings = new();
                foreach (var child in snapshot.Children)
                {
                    RankingData data = JsonUtility.FromJson<RankingData>(child.GetRawJsonValue());
                    if (data != null && data._score > -1) rankings.Add(data);
                }

                rankings = rankings.OrderByDescending(x => x._score).Take(100).ToList();

                for (int i = 0; i < rankings.Count; i++)
                    rankings[i]._rank = i + 1;

                callback?.Invoke(rankings);
            });
        }

        // ★ 내 순위 캐시 로드 (1건만)
        public void LoadMyRank(string userId, Action<int, int> callback)
        {
            _database.Child("user_ranks").Child(userId)
                .GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                    {
                        callback?.Invoke(-1, 0);
                        return;
                    }

                    var snap = task.Result;
                    var rankNode = snap.Child("_rank");
                    var scoreNode = snap.Child("_score");

                    if (!rankNode.Exists || !scoreNode.Exists)
                    {
                        callback?.Invoke(-1, 0);
                        return;
                    }

                    if (!int.TryParse(rankNode.Value?.ToString(), out int rank)) rank = -1;
                    if (!int.TryParse(scoreNode.Value?.ToString(), out int score)) score = 0;

                    callback?.Invoke(rank, score);
                });
        }
    }
}