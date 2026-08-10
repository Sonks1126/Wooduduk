using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.Tier;
using Wooduduk.Network.Firebase.Matchmaking;

namespace Wooduduk.Network.Firebase.Leaderboard
{
    // Firebase ghosts/{userId} 경로에 런 기록을 저장하고 불러오는 저장소.
    // 저장: 런 종료 시 내 기록을 고스트로 남겨 다른 유저의 매칭 후보가 됨.
    // 로드: 게임 시작 시 내 티어와 비슷한 고스트들을 불러와 게임 내 비교 대상으로 배치.
    public class GhostRepository
    {
        private readonly DatabaseReference _db;

        // 고스트 후보 허용 티어 차이 (내 티어 ±1 → 1단계 위아래까지 매칭)
        private const int DEFAULT_TIER_BAND = 1;

        public GhostRepository()
        {
            _db = FirebaseDatabase.DefaultInstance.RootReference;
        }

        // 런 종료 시 내 기록을 ghosts/{userId}에 저장.
        // 유저당 최신 1개만 유지 (UpdateChildrenAsync로 덮어쓰기).
        // currentTier: TierRunResult에 티어 필드가 없으므로 호출 측에서 전달.
        public void Save(TierRunResult result, int currentTier)
        {
            if (result == null || string.IsNullOrEmpty(result._userId))
            {
                Debug.LogWarning("[GhostRepository] result가 null이거나 userId가 없습니다.");
                return;
            }

            int gamesPlayed = 0;

            if (UserManager.Instance != null && UserManager.Instance.TryGetUser(out var user))
                gamesPlayed = user._gamesPlayed;

            Debug.Log(
                $"[GhostRepository] Save 시도: " +
                $"uid={result._userId}, score={result._rawScore}, tier={currentTier}, games={gamesPlayed}"
            );

            var data = new Dictionary<string, object>
            {
                { "_userId",          result._userId },
                { "_nick",            result._userNick ?? "" },
                { "_score",           result._rawScore },
                { "_maxCombo",        result._maxCombo },
                { "_settlementCount", result._settlementCount },
                { "_tier",            currentTier },
                { "_gamesPlayed",     gamesPlayed },
                { "_timestamp",       result._timestamp },
            };

            _db.Child("ghosts").Child(result._userId)
                .UpdateChildrenAsync(data)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogError(
                            $"[GhostRepository] Save 실패 uid={result._userId}: " +
                            $"{task.Exception?.GetBaseException()?.Message}"
                        );
                    }
                    else
                    {
                        Debug.Log($"[GhostRepository] Save 성공 uid={result._userId}");
                    }
                });
        }

        // 게임 시작 시 내 티어 ±bandSize 범위의 고스트 목록을 가져옴.
        // 결과는 MatchmakingService가 받아서 게임 내 배치 대상으로 사용.
        // 현재는 ghosts/ 전체 읽기 후 클라 필터링. 유저 수 증가 시 Firebase 인덱스 쿼리로 교체 예정.
        public void LoadCandidates(int targetTier, Action<List<GhostEntry>> callback, int bandSize = DEFAULT_TIER_BAND)
        {
            _db.Child("ghosts")
                .OrderByChild("_tier")
                .StartAt(targetTier - bandSize)
                .EndAt(targetTier + bandSize)
                .GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogError("[GhostRepository] LoadCandidates 실패");
                        callback?.Invoke(new List<GhostEntry>());
                        return;
                    }

                    var list = new List<GhostEntry>();
                    if (!task.Result.Exists)
                    {
                        callback?.Invoke(list);
                        return;
                    }

                    foreach (var child in task.Result.Children)
                    {
                        GhostEntry entry = ParseEntry(child);
                        if (entry != null) list.Add(entry);
                    }

                    Debug.Log($"[GhostRepository] LoadCandidates tier={targetTier}±{bandSize} → {list.Count}개");
                    callback?.Invoke(list);
                });
        }

        // Firebase 스냅샷 1개를 GhostEntry로 변환. 파싱 실패 시 null 반환.
        private GhostEntry ParseEntry(DataSnapshot child)
        {
            try
            {
                return JsonUtility.FromJson<GhostEntry>(child.GetRawJsonValue());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GhostRepository] 파싱 실패 key={child.Key}: {e.Message}");
                return null;
            }
        }
    }
}