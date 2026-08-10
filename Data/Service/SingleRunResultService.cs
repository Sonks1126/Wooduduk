using System;
using UnityEngine;
using Wooduduk.Data.Result;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase;
using Wooduduk.Slot;

namespace Wooduduk.Data.Service
{
    /// <summary>
    /// 싱글 플레이 런 종료 처리를 담당
    /// 결과 데이터만 생성하고, UI 표시는 호출하는 쪽에서 담당
    /// </summary>
    public static class SingleRunResultService
    {
        public static RunResultData Process(RunProgress run, SlotScoreCalculator score, UserData user)
        {
            if (run == null || score == null || user == null)
            {
                Debug.LogWarning("[SingleRunResultService] 유효하지 않은 인자입니다.");
                return null;
            }

            RunResultData result = RunResultBuilder.Build(run, score, RunMode.Single);

            if (result == null)
            {
                Debug.LogWarning("[SingleRunResultService] 결과 생성 실패");
                return null;
            }

            // 유저 정보 부여
            result._userId = user._userId;
            result._userNick = user._userNick;

            // 티어/비교용 원점수
            result._rawScore = score.SingleScore(run);

            // 싱글은 등수 개념이 없으므로 0 유지
            result._rank = 0;

            // 싱글 결과 저장
            RunResultSaveService.Save(result, user);


            Debug.Log($"[SingleRunResultService] 싱글 결과 생성 완료 / " +
                      $"User={user._userId}, Score={result._finalScore}, Raw={result._rawScore}");

            return result;
        }
    }
}