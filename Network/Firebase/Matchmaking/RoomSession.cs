using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Wooduduk.Network.Firebase.Leaderboard;

namespace Wooduduk.Network.Firebase.Matchmaking
{
    /// <summary>
    /// 현재 참가 중인 Room 정보를 메모리에 보관합니다.
    /// Seed는 roomId 해시로 자동 계산되어 모든 클라가 같은 값을 갖습니다.
    /// </summary>
    public class RoomSession
    {
        #region --- Room 정보 ---

        /// <summary>현재 참가 중인 Room ID</summary>
        public string RoomId { get; private set; }

        /// <summary>공유 Seed (roomId 해시로 결정론적 계산)</summary>
        public long Seed { get; private set; }

        /// <summary>Room 상태 (waiting / playing / finished)</summary>
        public string State { get; private set; }

        #endregion

        #region --- Player 정보 ---

        /// <summary>내 UID</summary>
        public string MyUid { get; private set; }

        /// <summary>Room 참가 플레이어</summary>
        public Dictionary<string, RoomPlayerData> Players { get; } = new();
        public List<GhostEntry> GhostCandidates { get; set; } = new();

        #endregion

        #region --- Config ---

        /// <summary>게임 시작에 필요한 최소 인원</summary>
        public int MinPlayers { get; set; } = 2;

        /// <summary>방 최대 인원</summary>
        public int MaxPlayers { get; set; } = 4;

        #endregion

        #region --- 생성 / 초기화 ---

        /// <summary>
        /// Room 정보를 초기화합니다.
        /// Seed는 roomId 해시로 자동 계산됩니다.
        /// </summary>
        public void Initialize(string roomId, string state, string myUid)
        {
            RoomId = roomId;
            Seed = HashRoomIdToSeed(roomId);
            State = state;
            MyUid = myUid;

            Players.Clear();
        }

        /// <summary>세션을 정리합니다.</summary>
        public void Clear()
        {
            RoomId = null;
            Seed = 0;
            State = null;
            MyUid = null;

            Players.Clear();
            GhostCandidates.Clear();
        }

        /// <summary>
        /// roomId를 SHA256 해시 후 long으로 변환합니다.
        /// 같은 roomId는 항상 같은 Seed를 반환합니다.
        /// </summary>
        private static long HashRoomIdToSeed(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
                return 0;

            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(roomId));
            return BitConverter.ToInt64(hash, 0);
        }

        #endregion

        #region --- Player 관리 ---

        public void AddOrUpdatePlayer(RoomPlayerData player)
        {
            if (player == null || string.IsNullOrEmpty(player.uid)) return;
            Players[player.uid] = player;
        }

        public void RemovePlayer(string uid)
        {
            Players.Remove(uid);
        }

        /// <summary>현재 플레이어 수</summary>
        public int PlayerCount => Players.Count;

        /// <summary>실제(고스트 제외) 플레이어 수</summary>
        public int RealPlayerCount
        {
            get
            {
                int count = 0;
                foreach (var p in Players.Values)
                    if (!p.isGhost) count++;
                return count;
            }
        }

        /// <summary>게임 시작 가능 여부</summary>
        public bool IsReady => PlayerCount >= MinPlayers;

        /// <summary>방이 가득 찼는지</summary>
        public bool IsFull => PlayerCount >= MaxPlayers;

        /// <summary>내가 방 첫 입장자인지 (= 방 생성자)</summary>
        public bool IsHost
        {
            get
            {
                if (string.IsNullOrEmpty(MyUid) || Players.Count == 0) return false;
                // ★ 실플레이어(고스트 제외) 중 사전식 최소 uid가 호스트.
                //   고스트를 포함하면 고스트 uid("ghost_...")가 최소로 잡혀 고스트가 "호스트"가 되고,
                //   고스트는 코디네이터(startAt 기록 등) 로직을 안 돌려 실플레이어가 무한 대기 → 게임 시작 불가.
                string first = null;
                foreach (var kvp in Players)
                {
                    if (kvp.Value != null && kvp.Value.isGhost) continue; // 고스트 제외
                    if (first == null || string.CompareOrdinal(kvp.Key, first) < 0)
                        first = kvp.Key;
                }
                return first != null && first == MyUid; // 실플레이어 없으면 false
            }
        }

        /// <summary>
        /// 실호스트(고스트 제외 선출) 존재 여부. false면 방에 실플레이어가 하나도 없는
        /// "고아 방" — 아무도 startAt을 기록하지 못해 전원 무한 대기하므로 파괴 대상.
        /// (= RealPlayerCount &gt; 0 과 동치. 호출부 가독성용 별칭.)
        /// </summary>
        public bool HasRealHost => RealPlayerCount > 0;

        /// <summary>
        /// (진단용) 고스트를 포함해 나이브하게 최소 uid를 뽑으면 그게 고스트인가.
        /// IsHost는 고스트를 제외하므로 실제 선출엔 영향 없음(실플레이어가 승계).
        /// true면 "예전 로직이라면 고스트가 호스트로 잡혔을" 상황 — 로그로만 가시화하고 방은 유지한다
        /// (솔로+고스트 정상 매치의 절반이 여기 해당하므로 파괴하면 정상 매치를 부순다).
        /// </summary>
        public bool GhostWouldBeNaiveHost
        {
            get
            {
                if (Players.Count == 0) return false;
                string first = null;
                bool firstIsGhost = false;
                foreach (var kvp in Players)
                {
                    if (kvp.Value == null) continue;
                    if (first == null || string.CompareOrdinal(kvp.Key, first) < 0)
                    {
                        first = kvp.Key;
                        firstIsGhost = kvp.Value.isGhost;
                    }
                }
                return first != null && firstIsGhost;
            }
        }

        #endregion

        #region --- Room 상태 ---

        public void SetState(string state)
        {
            State = state;
        }

        #endregion
    }
}