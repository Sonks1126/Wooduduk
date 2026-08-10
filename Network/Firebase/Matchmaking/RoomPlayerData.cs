using System;

namespace Wooduduk.Network.Firebase.Matchmaking
{
    /// <summary>
    /// 방에 참가한 플레이어의 기본 정보입니다.
    /// 게임 시작 전 한 번만 등록되는 데이터입니다.
    /// </summary>
    [Serializable]
    public class RoomPlayerData
    {
        /// <summary>플레이어 UID</summary>
        public string uid;

        /// <summary>닉네임</summary>
        public string nick;

        /// <summary>현재 티어</summary>
        public int tier;

        /// <summary>게임 준비 완료 여부</summary>
        public bool ready;

        /// <summary>고스트(AI) 여부</summary>
        public bool isGhost;
    }
}