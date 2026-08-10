using System;

namespace Wooduduk.Network.Firebase.Matchmaking
{
    public interface IMatchSource
    {
        event Action<long> OnMatchReady; // 매칭 완료 및 공유 Seed 전달
        event Action OnMatchFailed;      // 매칭 실패

        void BeginMatch();
        void CancelMatch();
    }
}