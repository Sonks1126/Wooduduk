using System;

namespace Wooduduk.Network.Firebase.Matchmaking
{
    /// <summary>
    /// 게임 플레이 중 실시간으로 동기화되는 플레이어 상태입니다.
    /// 각 플레이어는 자신의 데이터만 RTDB에 Write합니다.
    /// 2~4Hz 주기로 throttle 됩니다.
    /// </summary>
    [Serializable]
    public class RoomLiveData
    {
        /// <summary>현재 점수</summary>
        public long score;

        /// <summary>현재 콤보</summary>
        public int combo;

        /// <summary>현재 체온</summary>
        public float temperature;

        /// <summary>생존 여부</summary>
        public bool alive = true;

        /// <summary>현재 스핀 횟수(진행도)</summary>
        public int spinIndex;

        /// <summary>모닥불 켜짐 여부(FireBurnSeconds > 0). ON/OFF만 표시(남은시간/횟수 아님).</summary>
        public bool fireLit;
    }
}