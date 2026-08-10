using System;
using UnityEngine;
using Wooduduk.Data.Result;

namespace Wooduduk.Data.Service
{
    public static class RunResultUIService
    {
        public static Action<RunResultData> OnResultShown;
        public static event Action<RunResultData> OnShow;
        // 비동기 등수 확정 후 결과창 업데이트용
        public static event Action<int> OnRankUpdated;

        public static void Show(RunResultData result)
        {
            if (result == null)
            {
                Debug.LogWarning("[RunResultUIService] result가 null입니다.");
                return;
            }

            Debug.Log(
                $"[RunResultUI] 결과 표시 / " +
                $"Score={result._finalScore}, " +
                $"Gold={result._earnedGold}, " +
                $"Rank={result._rank}, " +
                $"Survival={result._survivalTime:F1}, " +
                $"Settle={result._settlementCount}, " +
                $"MaxCombo={result._maxCombo}");

            OnResultShown?.Invoke(result);

            // TODO: 실제 결과창 연결
            OnShow?.Invoke(result);
        }

        public static void UpdateRank(int rank)
        {
            OnRankUpdated?.Invoke(rank);
        }
    }
}