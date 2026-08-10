using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wooduduk.Network.Firebase.Matchmaking
{
    public class MatchmakingFlow : MonoBehaviour
    {
        // 매칭 성사 후 게임 시작 방식. InPlace=현재 씬에서 직호출(단일 씬), LoadScene=멀티 씬 로드(부트스트랩이 시작).
        public enum StartMode { InPlace, LoadScene }

        [Header("게임 제어")]
        [SerializeField] private GameController _gameController;

        [Header("매칭 설정")]
        [SerializeField] private int _desiredPlayerCount = 2;
        [SerializeField] private bool _allowGhostFill = true;
        [SerializeField] private float _ghostFillTimeout = 8f;

        [Header("자동 시작 설정")]
        [SerializeField] private bool _autoStartAfterMatch = true;
        [SerializeField] private float _autoStartDelay = 3f;
        [SerializeField] private float _loadingDuration = 2f;

        [Header("게임 시작 방식")]
        [Tooltip("InPlace=현재 씬에서 GameController.StartMultiGame 직호출(단일 씬, 기본). LoadScene=멀티 씬을 로드(그 씬의 MultiGameBootstrap이 시작). GameSession은 여러 씬 공유 프리팹이라 기본은 InPlace(기존 유지) — 씬 전환 원하는 씬(예: Chunghui_DevScene) 인스턴스에서만 LoadScene으로 오버라이드.")]
        [SerializeField] private StartMode _startMode = StartMode.InPlace;
        [Tooltip("LoadScene 모드에서 로드할 멀티 씬 이름. Build Settings에 등록돼 있어야 함.")]
        [SerializeField] private string _multiSceneName = "Chunghui_DevMulti";

        private IMatchSource _matchSource;
        private readonly List<string> _debugLogs = new();
        private Coroutine _autoStartCoroutine;

        // 카운트 변경 시에만 이벤트 발행하기 위한 캐시
        private int _lastEmittedCount = -1;

        // ★ 자동 시작 중복 방지 플래그
        private bool _autoStartTriggered = false;

        public float AutoStartRemainingTime { get; private set; }
        public bool IsCountingDown { get; private set; }
        public bool IsLoading { get; private set; }

        public event Action<int, int> OnCountChanged;
        public event Action OnMatchSuccess;
        public event Action OnMatchFailed;
        public event Action<float> OnCountdownTick;
        public event Action<float> OnLoadingStarted;
        public event Action OnLoadingFinished;

        public IReadOnlyList<string> DebugLogs => _debugLogs;

        public int DesiredPlayerCount
        {
            get => _desiredPlayerCount;
            set => _desiredPlayerCount = Mathf.Clamp(value, 1, 4);
        }

        public bool AllowGhostFill
        {
            get => _allowGhostFill;
            set => _allowGhostFill = value;
        }

        public bool AutoStartAfterMatch
        {
            get => _autoStartAfterMatch;
            set => _autoStartAfterMatch = value;
        }

        public float GhostFillTimeout
        {
            get => _ghostFillTimeout;
            set => _ghostFillTimeout = Mathf.Max(0f, value);
        }

        public bool IsMatched
        {
            get
            {
                if (_matchSource is IRoomMatchSource room)
                    return room.IsMatched;
                return false;
            }
        }

        public bool IsSearching => _matchSource != null && !IsMatched;

        public bool HasRoom
        {
            get
            {
                var s = CurrentSession;
                return s != null && !string.IsNullOrEmpty(s.RoomId);
            }
        }

        // ★ 게임 진행 중인지 (카운트다운/로딩/게임 시작 후)
        public bool IsGameStarting => _autoStartTriggered;

        public RoomSession CurrentSession
        {
            get
            {
                if (_matchSource is FirebaseRoomMatchSource fb)
                    return fb.Session;
                return null;
            }
        }

        private FirebaseRoomMatchSource FirebaseSource => _matchSource as FirebaseRoomMatchSource;

        private void AddLog(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _debugLogs.Add(line);
            if (_debugLogs.Count > 80) _debugLogs.RemoveAt(0);
            Debug.Log($"[MatchmakingFlow] {line}");
        }

        public void ClearLogs() => _debugLogs.Clear();

        public void BeginMatch()
        {
            if (_matchSource != null)
            {
                AddLog("이미 매칭 중이라 BeginMatch 무시");
                return;
            }

            _autoStartTriggered = false; // ★ 초기화

            // NRE 방지 가드: CurrentUserData null이면 아래 user._userId 접근서 크래시.
            //   원인 = 로그인/유저데이터 로드 전 매칭 시작, 또는 씬전환 시 UserManager 미영속(DDOL 그룹자식 경고).
            var user = UserManager.Instance != null ? UserManager.Instance.CurrentUserData : null;
            if (user == null)
            {
                AddLog("❌ 매칭 실패 — 유저 데이터 없음(로그인/로드 전). 로비 경유 후 재시도.");
                Debug.LogWarning("[MatchmakingFlow] BeginMatch 중단: UserManager.CurrentUserData == null. " +
                                 "MultiScene 단독 테스트면 로비→멀티 경유 필요(로그인/유저 로드), 또는 UserManager DDOL 미영속 확인.");
                return;
            }
            AddLog("매칭 찾기 시작");
            AddLog($"설정: 목표={_desiredPlayerCount}, 고스트허용={_allowGhostFill}, 타임아웃={_ghostFillTimeout:F1}s");

            var source = new FirebaseRoomMatchSource(
                user._userId,
                user._userNick,
                user._axeTier,
                user._gamesPlayed
            )
            {
                DesiredPlayerCount = _desiredPlayerCount,
                AllowGhostFill = _allowGhostFill,
                GhostFillTimeout = _ghostFillTimeout
            };

            _matchSource = source;
            _matchSource.OnMatchReady += HandleMatchReady;
            _matchSource.OnMatchFailed += HandleMatchFailed;

            source.BeginMatch();
        }

        public void CancelMatch()
        {
            _lastEmittedCount = -1;
            if (_matchSource == null)
            {
                AddLog("취소할 매칭 없음");
                return;
            }

            AddLog("매칭 취소/정리");

            StopAutoStart();
            _autoStartTriggered = false;

            _matchSource.OnMatchReady -= HandleMatchReady;
            _matchSource.OnMatchFailed -= HandleMatchFailed;
            _matchSource.CancelMatch();
            _matchSource = null;
        }

        private void StopAutoStart()
        {
            if (_autoStartCoroutine != null)
            {
                StopCoroutine(_autoStartCoroutine);
                _autoStartCoroutine = null;
            }
            IsCountingDown = false;
            IsLoading = false;
            AutoStartRemainingTime = 0f;
        }

        private void Update()
        {
            if (_matchSource is IRoomMatchSource roomMatch)
            {
                roomMatch.Tick(Time.deltaTime);
                var session = roomMatch.Session;
                if (session != null)
                {
                    int count = session.PlayerCount;

                    // 매 프레임 발행하면 OnPlayersChanged의 Clear() 순간 0이 찍혀 UI가 깜빡임
                    // 실제 값이 바뀔 때만 발행
                    if (count != _lastEmittedCount)
                    {
                        _lastEmittedCount = count;
                        OnCountChanged?.Invoke(count, _desiredPlayerCount);
                    }

                    if (_autoStartAfterMatch && !_autoStartTriggered && HasRoom &&
                        count >= _desiredPlayerCount)
                    {
                        TriggerAutoStart();
                    }
                }
            }
        }

        // ★ 자동 시작 트리거 (중복 방지)
        private void TriggerAutoStart()
        {
            if (_autoStartTriggered) return;
            _autoStartTriggered = true;

            // ★ 방을 닫아서 더 이상 못 들어오게 막음
            CloseRoom();

            if (_autoStartCoroutine != null)
                StopCoroutine(_autoStartCoroutine);

            _autoStartCoroutine = StartCoroutine(AutoStartSequence());
        }

        // ★ 방 닫기: state를 starting으로 바꿔서 매칭 검색에서 제외
        private void CloseRoom()
        {
            var fb = FirebaseSource;
            if (fb != null && fb.Session != null && !string.IsNullOrEmpty(fb.Session.RoomId))
            {
                fb.Repository.SetRoomState(fb.Session.RoomId, "starting", fb.MyTier);
                AddLog($"🔒 방 닫음 (state=starting) → 추가 입장 차단");
            }
        }

        private void HandleMatchReady(long seed)
        {
            AddLog($"매칭 성공! Seed={seed}");

            if (_matchSource is FirebaseRoomMatchSource firebaseSource)
            {
                var session = firebaseSource.Session;

                AddLog($"RoomContext 세팅: {session.RoomId}");
                AddLog($"최종 인원: {session.PlayerCount}/{session.MaxPlayers}");

                foreach (var kvp in session.Players)
                {
                    var p = kvp.Value;
                    AddLog($"참가자: {(p.isGhost ? "Ghost" : "Player")} / {p.nick} / T{p.tier}");
                }

                RoomContext.Instance.SetMatchResult(firebaseSource.Session, firebaseSource.Repository);
            }

            OnMatchSuccess?.Invoke();

            // ★ 여기서도 자동 시작 (이벤트 경로)
            if (_autoStartAfterMatch && !_autoStartTriggered)
            {
                TriggerAutoStart();
            }
            else if (!_autoStartAfterMatch)
            {
                AddLog("수동 시작 대기 중... [강제 시작] 버튼을 누르세요.");
            }
        }

        private void HandleMatchFailed()
        {
            AddLog("매칭 실패");
            OnMatchFailed?.Invoke();
            StopAutoStart();
        }

        private IEnumerator AutoStartSequence()
        {
            // ── 1단계: 인원 1차 확인 ──
            var session = CurrentSession;
            if (session == null || session.PlayerCount < _desiredPlayerCount)
            {
                AddLog($"❌ 인원 부족으로 시작 취소: {session?.PlayerCount ?? 0}/{_desiredPlayerCount}");
                StopAutoStart();
                _autoStartTriggered = false; // ★ 재시도 가능하게
                yield break;
            }

            // ── 2단계: 카운트다운 ──
            AddLog("🔥 자동 시작 카운트다운 시작!");
            IsCountingDown = true;
            AutoStartRemainingTime = _autoStartDelay;

            while (AutoStartRemainingTime > 0f)
            {
                AddLog($"게임 시작까지 {AutoStartRemainingTime:F0}초...");
                OnCountdownTick?.Invoke(AutoStartRemainingTime);

                yield return new WaitForSeconds(1f);
                AutoStartRemainingTime -= 1f;
            }

            IsCountingDown = false;
            AddLog("✅ 카운트다운 완료");

            // ── 3단계: 로딩 ──
            IsLoading = true;
            AddLog($"로딩 시작: {_loadingDuration}초");
            OnLoadingStarted?.Invoke(_loadingDuration);

            float loadTime = _loadingDuration;
            while (loadTime > 0f)
            {
                loadTime -= Time.deltaTime;
                yield return null;
            }

            IsLoading = false;

            // ── 4단계: 게임 시작 ──
            AddLog("🎮 게임 시작!");

            // state를 playing으로 최종 전환
            var fb = FirebaseSource;

            fb.Repository.SetRoomState(fb.Session.RoomId, "playing", fb.MyTier);

            // HandleMatchReady가 아직 안 왔을 경우를 대비해 직접 세팅
            if (!RoomContext.Instance.IsInMultiplayer)
            {
                RoomContext.Instance.SetMatchResult(fb.Session, fb.Repository);
            }

            // 게임 시작 — 모드에 따라 인플레이스 호출 or 멀티 씬 로드.
            // (SetMatchResult가 위에서 끝나 seed/session/Repository는 RoomContext에 적재됨 → 정적이라 씬 넘어가도 유지)
            if (_startMode == StartMode.LoadScene)
            {
                if (string.IsNullOrEmpty(_multiSceneName))
                {
                    Debug.LogError("[MatchmakingFlow] _multiSceneName 비어있음 — 멀티 씬 로드 불가. 씬 이름 설정 또는 _startMode=InPlace로.");
                }
                else
                {
                    AddLog($"멀티 씬 로드: {_multiSceneName}");
                    SceneManager.LoadSceneAsync(_multiSceneName);
                }
            }
            else // InPlace — 기존 단일 씬 경로(회귀 0)
            {
                _gameController?.StartMultiGame();
            }

            OnLoadingFinished?.Invoke();

            _autoStartCoroutine = null;
        }

        // ── 디버그용 메서드들 ──

        public bool DebugAddFakePlayer()
        {
            var fb = FirebaseSource;
            if (fb == null || fb.Session == null || string.IsNullOrEmpty(fb.Session.RoomId))
            {
                AddLog("DebugAddFakePlayer 실패: 활성 Room 없음");
                return false;
            }

            // ★ 이미 인원 충족이면 추가 차단
            if (fb.Session.PlayerCount >= _desiredPlayerCount)
            {
                AddLog($"❌ 이미 인원 충족 ({fb.Session.PlayerCount}/{_desiredPlayerCount}) → 플레이어 추가 차단");
                return false;
            }

            string shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var player = new RoomPlayerData
            {
                uid = $"debug_player_{shortId}",
                nick = $"DebugPlayer_{shortId}",
                tier = UserManager.Instance.CurrentUserData._axeTier,
                ready = true,
                isGhost = false
            };

            fb.Repository.JoinRoom(fb.Session.RoomId, player);
            AddLog($"디버그 플레이어 추가: {player.nick}");
            return true;
        }

        public bool DebugAddGhost()
        {
            var fb = FirebaseSource;
            if (fb == null || fb.Session == null || string.IsNullOrEmpty(fb.Session.RoomId))
            {
                AddLog("DebugAddGhost 실패: 활성 Room 없음");
                return false;
            }

            // ★ 이미 인원 충족이면 추가 차단
            if (fb.Session.PlayerCount >= _desiredPlayerCount)
            {
                AddLog($"❌ 이미 인원 충족 ({fb.Session.PlayerCount}/{_desiredPlayerCount}) → 고스트 추가 차단");
                return false;
            }

            string shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var ghost = new RoomPlayerData
            {
                uid = $"debug_ghost_{shortId}",
                nick = $"AI_{shortId}",
                tier = UserManager.Instance.CurrentUserData._axeTier,
                ready = true,
                isGhost = true
            };

            fb.Repository.JoinRoom(fb.Session.RoomId, ghost);
            AddLog($"디버그 고스트 추가: {ghost.nick}");
            return true;
        }

        public bool DebugForceStart()
        {
            if (!_autoStartTriggered)
            {
                TriggerAutoStart();
                AddLog("▶ 수동 강제 시작!");
                return true;
            }
            AddLog("이미 시작 진행 중");
            return false;
        }

        private void OnApplicationQuit()
        {
            // 유니티 에디터 중지 또는 앱 종료 시 방을 즉시 삭제.
            // Firebase onDisconnect는 수 분간 지연될 수 있어서 다음 세션에서 잔존 방이 생기는 문제를 방지.
            var fb = _matchSource as FirebaseRoomMatchSource;
            if (fb?.Session != null && !string.IsNullOrEmpty(fb.Session.RoomId))
            {
                fb.Repository.DeleteRoom(fb.Session.RoomId);
                Debug.Log("[MatchmakingFlow] 앱 종료 — 방 즉시 삭제");
            }
        }

        private void OnDestroy()
        {
            if (_matchSource is IRoomMatchSource room && !room.IsMatched)
            {
                _matchSource.CancelMatch();
            }

            StopAutoStart();
        }
    }
}