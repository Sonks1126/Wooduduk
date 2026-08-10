using System;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase;
using Wooduduk.Slot;

namespace Wooduduk.Data.Tier
{
    public static class TierRunResultBuilder
    {
        // 멀티 전용. _rawScore = 멀티 최종 점수
        // 티어 승강 점수(S+R)는 순위 확정 후 TierService.ApplyRun(MultiTierScore)로 별도 적용.
        public static TierRunResult Build(RunProgress run, SlotScoreCalculator score)
        {
            if (run == null || score == null)
            {
                UnityEngine.Debug.LogWarning("[TierRunResultBuilder] run/score null");
                return null;
            }

            UserData user = UserManager.Instance?.CurrentUserData;

            long raw = score.MultiScore(run);                 // S + W (순위 무관)
            int safeScore = raw > int.MaxValue ? int.MaxValue : (int)raw;

            return new TierRunResult
            {
                _userId = user?._userId ?? string.Empty,
                _userNick = user?._userNick ?? string.Empty,
                _rawScore = safeScore,
                _settlementCount = run.SettleCount,
                _maxCombo = run.MaxCombo, 
                _timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
    }
}