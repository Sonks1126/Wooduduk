using System;
using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Matchmaking;
using Wooduduk.Network.Firebase.Leaderboard;
using Wooduduk.Tier;

namespace Wooduduk.Network.Firebase.Matchmaking
{
    public class FirebaseRoomMatchSource : IRoomMatchSource
    {
        #region --- 설정 (외부에서 제어 가능) ---

        /// <summary>게임에 참여할 총 인원 (실제 + 고스트 포함)</summary>
        public int DesiredPlayerCount { get; set; } = 2;

        /// <summary>고스트로 빈자리를 채울지 여부</summary>
        public bool AllowGhostFill { get; set; } = true;

        /// <summary>고스트를 채우기까지 기다리는 시간 (초)</summary>
        public float GhostFillTimeout { get; set; } = 4f;

        #endregion

        #region --- 상태 ---

        private enum Phase { Idle, Searching, Waiting, Matched, Failed }
        private Phase _phase = Phase.Idle;

        public bool IsMatched => _phase == Phase.Matched;
        public bool IsFailed => _phase == Phase.Failed;

        private List<GhostEntry> _ghostCandidates = new();

        private readonly GhostRepository _ghostRepo = new GhostRepository();

        #endregion

        #region --- 의존성 ---

        private readonly RoomRepository _repo;
        private readonly RoomSession _session;
        private readonly string _myUid;
        private readonly string _myNick;
        private readonly int _myTier;
        private readonly int _myGamesPlayed;

        public int MyTier => _myTier;

        #endregion

        #region --- 내부 상태 ---

        private float _waitTimer;
        private bool _ghostFilled;

        #endregion

        #region --- 이벤트 ---

        public event Action<long> OnMatchReady;
        public event Action OnMatchFailed;

        #endregion

        public FirebaseRoomMatchSource(string myUid, string myNick, int myTier, int myGamesPlayed)
        {
            _repo = new RoomRepository();
            _session = new RoomSession();

            _myUid = myUid;
            _myNick = myNick;
            _myTier = myTier;
            _myGamesPlayed = myGamesPlayed;
        }

        public RoomSession Session => _session;
        public RoomRepository Repository => _repo;

        #region ===== Begin / Cancel =====

        public void BeginMatch()
        {
            if (_phase != Phase.Idle) return;
            _phase = Phase.Searching;
            _waitTimer = 0f;
            _ghostFilled = false;
            _session.MinPlayers = DesiredPlayerCount;
            _session.MaxPlayers = DesiredPlayerCount;

            // 고스트 후보 로드는 방 탐색과 무관하게 병렬로 시작
            _ghostRepo.LoadCandidates(_myTier, entries =>
            {
                _ghostCandidates = entries ?? new List<GhostEntry>();
                _session.GhostCandidates = _ghostCandidates;
            });

            // 이전 세션 잔존 방 정리 후 탐색 시작
            // (유니티 에디터 중지 등으로 onDisconnect가 아직 처리 안 됐을 경우 대비)
            _repo.CleanupMyPreviousRoom(_myUid, () =>
                _repo.FindWaitingRoomByTier(_myTier, OnFindWaitingRoom)
            );
        }

        public void CancelMatch()
        {
            if (_phase == Phase.Idle) return;

            string roomId = _session.RoomId;
            if (!string.IsNullOrEmpty(roomId))
            {
                _repo.UnsubscribeAll(roomId);
                _repo.LeaveRoom(roomId, _myUid);

                if (_session.IsHost && _session.RealPlayerCount <= 1)
                    _repo.DeleteRoom(roomId);
            }

            _session.Clear();
            _phase = Phase.Idle;
        }

        #endregion

        #region ===== 방 탐색 / 생성 =====

        private void OnFindWaitingRoom(string roomId)
        {
            if (_phase != Phase.Searching) return;

            if (string.IsNullOrEmpty(roomId))
            {
                CreateNewRoom();
                return;
            }

            _repo.GetRoom(roomId, room =>
            {
                if (_phase != Phase.Searching) return;

                if (room == null || room.state != "waiting" || string.IsNullOrEmpty(room.hostUid))
                {
                    CreateNewRoom();
                    return;
                }

                JoinExistingRoom(roomId, room.hostUid);
            });
        }


        private void CreateNewRoom()
        {
            string roomId = _repo.GenerateRoomId();

            var roomData = new RoomData
            {
                state = "waiting",
                stateAndTier = $"waiting_{_myTier}",
                hostUid = _myUid
            };

            var playerData = new RoomPlayerData
            {
                uid = _myUid,
                nick = _myNick,
                tier = _myTier,
                ready = true,
                isGhost = false
            };

            // ★ 원자적 생성: 방 + 내 플레이어 동시 기록
            _repo.CreateRoomWithPlayer(roomId, roomData, playerData, success =>
            {
                if (_phase != Phase.Searching) return;
                if (!success) { FailMatch(); return; }

                EnterRoomAsHost(roomId); // 구독만 (이미 player 등록됨)
            });
        }

        /// <summary>
        /// 새 방 호스트용: JoinRoom 없이 구독만 시작
        /// </summary>
        private void EnterRoomAsHost(string roomId)
        {
            _session.Initialize(roomId, "waiting", _myUid);

            _repo.SubscribePlayers(roomId, OnPlayersChanged, OnRoomDeleted);
            _repo.SubscribeState(roomId, OnStateChanged);

            _phase = Phase.Waiting;
            _waitTimer = 0f;
        }

        /// <summary>
        /// 기존 방 참가자용: JoinRoom + 구독
        /// </summary>
        private void JoinExistingRoom(string roomId, string hostUid)
        {
            _session.Initialize(roomId, "waiting", _myUid);

            _repo.JoinRoom(roomId, new RoomPlayerData
            {
                uid = _myUid,
                nick = _myNick,
                tier = _myTier,
                ready = true,
                isGhost = false
            });

            _repo.SubscribePlayers(roomId, OnPlayersChanged, OnRoomDeleted);
            _repo.SubscribeState(roomId, OnStateChanged);

            _phase = Phase.Waiting;
            _waitTimer = 0f;
        }

        private void OnRoomDeleted()
        {
            if (_phase != Phase.Waiting) return;

            string oldRoomId = _session.RoomId;

            // ★ players가 비었어도 방 루트가 살아있으면 삭제가 아님
            _repo.GetRoom(oldRoomId, room =>
            {
                if (_phase != Phase.Waiting) return;

                if (room != null)
                {
                    // 방은 살아있는데 내 player 노드만 날아간 것
                    // (onDisconnect 지연 발화 or 쓰기 거부 롤백)
                    Debug.LogWarning($"[MatchSource] players 비었지만 방 존재 → 재입장: {oldRoomId}");
                    _repo.JoinRoom(oldRoomId, new RoomPlayerData
                    {
                        uid = _myUid,
                        nick = _myNick,
                        tier = _myTier,
                        ready = true,
                        isGhost = false
                    });
                    return;
                }

                // 진짜 삭제됨 → 재탐색
                _repo.UnsubscribeAll(oldRoomId);
                _session.Clear();
                _phase = Phase.Searching;
                _waitTimer = 0f;
                _ghostFilled = false;
                _repo.FindWaitingRoomByTier(_myTier, OnFindWaitingRoom);
            });
        }

        #endregion

        #region ===== 인원 감지 =====

        private void OnPlayersChanged(System.Collections.Generic.Dictionary<string, RoomPlayerData> players)
        {
            if (_phase != Phase.Waiting) return;

            _session.Players.Clear();
            foreach (var p in players.Values)
                _session.AddOrUpdatePlayer(p);

            TryStartIfReady();
        }

        private void OnStateChanged(string state)
        {
            _session.SetState(state);

            if (state == "playing" && _phase == Phase.Waiting)
            {
                CompleteMatch();
            }
        }

        #endregion

        #region ===== Tick + 고스트 백필 =====

        public void Tick(float deltaTime)
        {
            if (_phase != Phase.Waiting) return;

            _waitTimer += deltaTime;

            // 조건: 고스트 채우기 허용 + 타임아웃 + 내가 호스트 + 아직 고스트 안 채움
            if (AllowGhostFill &&
                _waitTimer >= GhostFillTimeout &&
                !_ghostFilled &&
                _session.IsHost)
            {
                FillWithGhosts();
            }
        }

        private void FillWithGhosts()
        {
            _ghostFilled = true;

            int need = DesiredPlayerCount - _session.PlayerCount;
            if (need <= 0) return;

            var svc = MatchmakingService.Instance;
            if (svc == null)
            {
                FillWithGhostsFallback(need);
                return;
            }

            var self = new MatchQuery(_myUid, (AxeTier)_myTier, _myGamesPlayed);

            svc.RequestPool(self, _session.Seed, pool =>
            {
                int toFill = DesiredPlayerCount - _session.PlayerCount;
                var opponents = pool.Opponents;

                for (int i = 0; i < toFill; i++)
                {
                    string nick = (opponents != null && i < opponents.Count)
                        ? opponents[i]._nick
                        : $"AI플레이어{i + 1}";

                    _repo.JoinRoom(_session.RoomId, new RoomPlayerData
                    {
                        uid = $"ghost_{_session.RoomId}_{i}",
                        nick = nick,
                        tier = _myTier,
                        ready = true,
                        isGhost = true
                    });
                }

                _session.GhostCandidates = opponents ?? new List<GhostEntry>();
            });
        }

        // 폴백 (MatchmakingService 없을 때 기존 방식)
        private void FillWithGhostsFallback(int need)
        {
            Debug.Log($"[MatchSource] 폴백: 고스트 {need}명 백필");
            for (int i = 0; i < need; i++)
                _repo.JoinRoom(_session.RoomId, CreateGhostPlayer(i));
        }

        private RoomPlayerData CreateGhostPlayer(int index)
        {
            // 로드된 후보 있으면 실제 닉 사용, 없으면 폴백
            string nick = (index < _ghostCandidates.Count)
                ? _ghostCandidates[index]._nick
                : $"AI플레이어{index + 1}";

            return new RoomPlayerData
            {
                uid = $"ghost_{_session.RoomId}_{index}",  // uid는 기존 그대로
                nick = nick,
                tier = _myTier,
                ready = true,
                isGhost = true
            };
        }

        #endregion

        #region ===== 매칭 완료 판단 =====

        private void TryStartIfReady()
        {
            if (_phase != Phase.Waiting) return;
            if (!_session.IsReady) return;

            // ★★ 호스트가 인원 충족 시 → OnMatchReady만 발행 ★★
            // state 변경은 MatchmakingFlow가 담당 (자동 시작 시퀀스에서)
            if (_session.IsHost)
            {
                CompleteMatch();
            }
        }

        // CompleteMatch는 state를 직접 바꾸지 않고 매칭 완료만 알림
        private void CompleteMatch()
        {
            if (_phase == Phase.Matched) return; // 중복 방지
            _phase = Phase.Matched;
            Debug.Log($"[MatchSource] 매칭 완료! 방={_session.RoomId}, Seed={_session.Seed}, 인원={_session.PlayerCount}");
            OnMatchReady?.Invoke(_session.Seed);
        }

        private void FailMatch()
        {
            _phase = Phase.Failed;
            OnMatchFailed?.Invoke();
        }

        #endregion
    }
}