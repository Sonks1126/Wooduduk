namespace Wooduduk.Network.Firebase.Matchmaking
{
    /// <summary>
    /// FirebaseRoomMatchSource 등 Room 기반 매칭 소스 전용 확장 인터페이스.
    /// IMatchSource를 상속합니다.
    /// </summary>
    public interface IRoomMatchSource : IMatchSource
    {
        /// <summary>매칭 완료 여부</summary>
        bool IsMatched { get; }

        /// <summary>매칭 실패 여부</summary>
        bool IsFailed { get; }

        /// <summary>현재 세션 (Room 정보)</summary>
        RoomSession Session { get; }

        /// <summary>타임아웃 처리를 위한 매 프레임 호출</summary>
        void Tick(float deltaTime);
    }
}