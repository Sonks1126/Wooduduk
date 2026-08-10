using UnityEngine;
using Wooduduk.Data.Result;
using Wooduduk.Data.Static;
using Wooduduk.Data.Tier;
using Wooduduk.Network.Firebase;
using Wooduduk.Network.Firebase.Leaderboard;
using Wooduduk.Slot;

namespace Wooduduk.Data.Service
{
    public enum RunMode
    {
        Single,
        Multi
    }

    public static class RunEndService
    {
        /// <summary>
        /// 기존 호출 호환용 메서드
        /// UserManager.Instance.CurrentUserData에서 유저를 가져와 처리
        /// </summary>
        public static RunResultData EndRun(RunProgress run, SlotScoreCalculator score, RunMode mode)
        {
            UserData user = UserManager.Instance != null ? UserManager.Instance.CurrentUserData : null;

            return EndRun(run, score, mode, user);
        }

        /// <summary>
        /// 런 종료 진입점
        /// user를 명시적으로 넘겨 싱글/멀티 모두 사용
        /// - Single: 즉시 결과 계산 가능하므로 RunResultData 반환
        /// - Multi : 서버 RPC 비동기 처리이므로 즉시 결과를 알 수 없어 null 반환
        /// </summary>
        public static RunResultData EndRun(RunProgress run, SlotScoreCalculator score, RunMode mode, UserData user)
        {
            if (run == null || score == null || user == null) { /* log */ return null; }

            // ★ 공통: 고스트 저장은 모드 무관
            SaveGhost(run, score, user);

            switch (mode)
            {
                case RunMode.Single:
                    // 싱글: 로컬에서 결과 계산
                    RunResultData result = SingleRunResultService.Process(run, score, user);

                    return result;

                case RunMode.Multi:
                    // 멀티: 임시 결과만. 확정은 GameController가 등수 수신 후
                    // MultiRunResultService.ConfirmRank 호출
                    return null; // 또는 provisionalRank를 못 구하므로 여기선 안 만들고 컨트롤러에서 

                default:
                    Debug.LogError($"[RunEndService] 알 수 없는 RunMode: {mode}");
                    return null;
            }
        }
        public static void SaveGhost(RunProgress run, SlotScoreCalculator score, UserData user)
        {
            // 유저 데이터 없으면 저장 불가
            if (user == null)
            {
                Debug.LogWarning("[RunEndService] UserData 없음 — 고스트 저장 생략");
                return;
            }

            // 점수 계산기 없으면 저장 불가
            if (score == null)
            {
                Debug.LogWarning("[RunEndService] SlotScoreCalculator 없음 — 고스트 저장 생략");
                return;
            }

            // TierRunResult 빌드
            TierRunResult tierResult = TierRunResultBuilder.Build(run, score);
            if (tierResult == null)
            {
                Debug.LogWarning("[RunEndService] TierRunResult 빌드 실패 — 고스트 저장 생략");
                return;
            }

            // 고스트 저장 (유저 티어 기준)
            new GhostRepository().Save(tierResult, user._axeTier);

            Debug.Log($"[RunEndService] 고스트 저장 완료: userId={user._userId}, tier={user._axeTier}");
        }
    }
}