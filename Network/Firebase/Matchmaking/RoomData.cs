using System;

namespace Wooduduk.Network.Firebase.Matchmaking
{
    /// <summary>
    /// Room 자체의 정보입니다.
    /// 플레이어 정보와 별도로 관리됩니다.
    /// Seed는 roomId에서 결정론적으로 계산되므로 저장하지 않습니다.
    /// </summary>
    [Serializable]
    public class RoomData
    {
        /// <summary>
        /// 현재 방 상태
        /// waiting → 플레이어 대기
        /// playing → 게임 진행 중
        /// finished → 게임 종료
        /// </summary>
        public string state;

        // Firebase RTDB는 쿼리를 한 필드만 할 수 있어서
        // "state=waiting AND tier=3" 같은 복합 조건이 불가능함.
        // 그래서 "waiting_3" 처럼 두 값을 합친 복합키 필드를 만들어서
        // 이 하나로 쿼리함.
        public string stateAndTier;

        public string hostUid;
    }
}