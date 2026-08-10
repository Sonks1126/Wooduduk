using UnityEngine;
using Wooduduk.Data.Result;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase;
using Wooduduk.Slot;
using Wooduduk.Tier;

namespace Wooduduk.Data.Service
{
    /// <summary>
    /// 멀티 플레이 런 종료 처리 (2단계)
    /// 1단계 CreateProvisional : 사망 직후 임시 등수 기준 결과 생성 (UI 선표시용)
    /// 2단계 ConfirmRank       : 서버 등수 확정 후 골드/티어/저장 최종 처리
    /// </summary>
    public static class MultiRunResultService
    {
        // ── 1단계: 사망 직후 (임시) ──
        public static RunResultData CreateProvisional(
            RunProgress run, SlotScoreCalculator score, UserData user, int provisionalRank)
        {
            if (run == null || score == null || user == null)
            {
                Debug.LogWarning("[MultiRunResultService] 유효하지 않은 인자");
                return null;
            }

            long rawScore = score.MultiScore(run);

            var result = new RunResultData
            {
                _userId = user._userId,
                _userNick = user._userNick,
                _finalScore = (int)rawScore,
                _maxCombo = run.MaxCombo,
                _survivalTime = run.SurvivalSeconds,
                _settlementCount = run.SettleCount,
                _rank = provisionalRank,
                _rawScore = rawScore,
                _earnedGold = (int)score.MultiCurrency(run, provisionalRank)
            };

            // ★ 여기서는 저장/티어 적용 안 함 — 아직 확정이 아니므로!
            Debug.Log($"[MultiRunResultService] 임시 결과 생성: rank={provisionalRank}, score={rawScore}");
            return result;
        }

        // ── 2단계: 등수 확정 후 (최종) ──
        public static void ConfirmRank(RunResultData result, RunProgress run, SlotScoreCalculator score, UserData user, int confirmedRank, int playerCount)
        {
            if (result == null || run == null || score == null || user == null) return;

            result._rank = confirmedRank;
            result._earnedGold = (int)score.MultiCurrency(run, confirmedRank);

            // ★ 티어: 멀티는 순위(rank+playerCount)로 판정 → TierCalculator가 상위 절반 성공(+LP)/하위 실패(-LP).
            TierService.Instance?.ApplyRun(new RunResult((int)score.MultiScore(run), confirmedRank, playerCount));

            RunResultSaveService.Save(result, user);

            Debug.Log($"[MultiRunResultService] 확정 완료: rank={confirmedRank}, gold={result._earnedGold}, " +
                      $"tier={TierService.Instance?.CurrentRank}");
        }
    }
}