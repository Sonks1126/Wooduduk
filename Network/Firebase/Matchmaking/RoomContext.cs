using UnityEngine;

namespace Wooduduk.Network.Firebase.Matchmaking
{
    /// <summary>
    /// 단일 씬에서 매칭 → 게임 데이터를 전달하는 정적 컨텍스트
    /// 어디서든 RoomContext.Instance로 접근 가능
    /// </summary>
    public class RoomContext
    {
        public static readonly RoomContext Instance = new RoomContext();

        public RoomSession Session { get; private set; }
        public RoomRepository Repository { get; private set; }

        public bool IsInMultiplayer => Session != null && !string.IsNullOrEmpty(Session.RoomId);

        private RoomContext() { } // 싱글톤 방어

        /// <summary>
        /// 매칭 완료 후 호출 (MultiplayerController에서 호출)
        /// </summary>
        public void SetMatchResult(RoomSession session, RoomRepository repository)
        {
            Session = session;
            Repository = repository;
            Debug.Log($"[RoomContext] 멀티 세션 설정 완료 - RoomId: {session.RoomId}, Seed: {session.Seed}");
        }

        /// <summary>
        /// 게임 종료 후 반드시 호출 (방 나가기 + 구독 정리)
        /// </summary>
        public void Clear()
        {
            if (IsInMultiplayer && Repository != null)
            {
                Repository.UnsubscribeAll(Session.RoomId);
                Repository.LeaveRoom(Session.RoomId, Session.MyUid);
            }

            Session = null;
            Repository = null;
            Debug.Log("[RoomContext] 멀티 세션 정리 완료");
        }
    }
}