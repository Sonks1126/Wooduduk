using Firebase.Database;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.DataSO;
using Wooduduk.Data.Result;
using Wooduduk.Data.Service;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase;
using Wooduduk.Network.Firebase.Leaderboard;
using Wooduduk.Network.Firebase.Matchmaking;
using Wooduduk.Player;
using Wooduduk.Slot;
using Wooduduk.Slot.Machine;

namespace Wooduduk.Network
{
    public class GameController : MonoBehaviour
    {
        [SerializeField] private DataManagerSO _dataManager;
        [SerializeField] private RoomLiveSync _liveSync;
        [SerializeField] private PlayerSurvival _playerSurvival;
        [SerializeField] private SlotmachineController _slotmachineController;

        [SerializeField] private float _resultDelay = 3f;

        private RunContext _runContext;
        private SlotMachine _slotMachine;
        private SlotScoreCalculator _scoreCalculator;
        private bool _rankReceived;
        private bool _gameEnded;
        private bool _ranksWritten;

        public bool IsGameActive => _slotMachine != null && !_gameEnded;
        public bool IsGameEnded => _gameEnded;
        public bool RankReceived => _rankReceived;
        public RunContext CurrentRunContext => _runContext;
        public RoomLiveSync LiveSync => _liveSync;
        public SlotMachine CurrentSlotMachine => _slotMachine;

        // ★ 타임아웃 시에도 확정 처리를 하기 위한 보관 필드
        private RunResultData _pendingResult;
        private RunProgress _pendingRun;
        private int _provisionalRank;

        // P2: 이탈 감지 + 호스트 마이그레이션 + last-man 종료
        private readonly HashSet<string> _originalRealPlayers = new HashSet<string>(); // 시작 시 non-ghost 로스터
        private readonly HashSet<string> _leaverWritten = new HashSet<string>();       // 이탈 death 대필한 uid(멱등)
        private int _totalPlayers;                                                       // 시작 시 총원 N(등수 기준, 고정)
        private bool _lifecycleSubscribed;                                               // players/deaths 구독 1회 가드


        private void OnEnable()
        {
            SlotEvent.OnDeath += HandleDeath;
        }
        private void OnDisable()
        {
            SlotEvent.OnDeath -= HandleDeath;
        }

        public void StartMultiGame()
        {
            if (_slotmachineController != null)
                _slotmachineController.SetAutoRevive(false);

            var ctx = RoomContext.Instance;
            if (!ctx.IsInMultiplayer) { Debug.LogError("[GameController] 멀티 컨텍스트 없음"); return; }
            if (ctx.Session.PlayerCount < ctx.Session.MinPlayers)
            {
                Debug.LogError($"[GameController] 인원 부족: {ctx.Session.PlayerCount}/{ctx.Session.MinPlayers}");
                return;
            }

            _rankReceived = false;
            _gameEnded = false;
            _ranksWritten = false;

            long seed = ctx.Session.Seed;
            Debug.Log($"[GameController] 멀티 게임 시작 - Seed: {seed}, 인원: {ctx.Session.PlayerCount}");

            // ★ SlotmachineController를 공유 시드로 재초기화 (클릭 상호작용이 올바른 머신을 씀)
            if (_slotmachineController != null)
            {
                _slotmachineController.Init(seed.ToString(), RunMode.Multi);
                _slotMachine = _slotmachineController.Machine;
            }
            else
            {
                _slotMachine = CreateSlotMachine(seed);
            }

            if (_liveSync != null)
            {
                var survivalConfig = _dataManager._gameBalance._data._survival;
                _scoreCalculator = new SlotScoreCalculator(_dataManager._gameBalance._data._slotScore);
                _liveSync.Initialize(ctx.Session, ctx.Repository, _slotMachine, survivalConfig, _scoreCalculator);
                LoadAndSetGhostEntries(ctx.Session);
                _liveSync.IsEnabled = true;
            }
            else
            {
                Debug.LogWarning("[GameController] RoomLiveSync 가 Inspector 에 연결되지 않았습니다.");
            }

            // P2: 세션 라이프사이클 구독(이탈감지·호스트마이그·last-man 종료) — 모든 클라.
            StartLifecycleWatch(ctx);
        }

        // ── P2: 세션 라이프사이클 감시(모든 클라 구독) ─────────────────────────────
        // 이탈감지(호스트 death 대필) + 로스터 동기화(IsHost 마이그레이션) + last-man 종료(SubscribeDeaths).
        private void StartLifecycleWatch(RoomContext ctx)
        {
            if (_lifecycleSubscribed || ctx.Session == null) return;
            _lifecycleSubscribed = true;

            _totalPlayers = ctx.Session.PlayerCount; // 시작 총원 N(고정, 이후 이탈 제거와 무관하게 등수 기준)
            _originalRealPlayers.Clear();
            _leaverWritten.Clear();
            foreach (var kvp in ctx.Session.Players)
                if (kvp.Value != null && !kvp.Value.isGhost) _originalRealPlayers.Add(kvp.Key);

            string roomId = ctx.Session.RoomId;
            string myUid = ctx.Session.MyUid;

            // 로스터 감시 — 이탈감지 + IsHost 마이그레이션(기존 SubscribePlayers 재사용).
            ctx.Repository?.SubscribePlayers(roomId, players => OnRosterChanged(players, ctx));

            // 사망 감시 — 시작 시점부터(기존엔 내 사망 후에만) → last-man을 남이 죽어도 감지.
            ctx.Repository?.SubscribeDeaths(roomId, snapshot =>
                OnDeathsUpdated(snapshot, roomId, myUid, _totalPlayers, ctx));

            // 내 등수 확정 구독 — 세션 시작 시(죽든 살아남든 종료 시 여기로). 생존자(승자)도 커버.
            ctx.Repository?.SubscribeRank(roomId, myUid, rank => OnMyRankConfirmed(rank, ctx));

            Debug.Log($"[GameController] P2 라이프사이클 구독 시작 (총원 N={_totalPlayers}, real={_originalRealPlayers.Count}).");
        }

        // 로스터 변화: (a) session.Players 동기화 → 결정론 IsHost 자동 재계산(호스트 마이그레이션).
        //             (b) 지금 내가 IsHost면 이탈한 real 플레이어의 death를 대필(멱등).
        private void OnRosterChanged(Dictionary<string, RoomPlayerData> players, RoomContext ctx)
        {
            if (_gameEnded || players == null || ctx.Session == null) return;

            var current = new HashSet<string>(players.Keys);

            // (a) 빠진 uid 제거 → IsHost(최저uid) 재계산 = 마이그레이션.
            var toRemove = new List<string>();
            foreach (var uid in ctx.Session.Players.Keys)
                if (!current.Contains(uid)) toRemove.Add(uid);
            foreach (var uid in toRemove) ctx.Session.RemovePlayer(uid);
            if (toRemove.Count > 0)
                Debug.Log($"[GameController] 로스터 변화 {toRemove.Count}명 이탈 → IsHost={ctx.Session.IsHost}(마이그레이션 반영).");

            // (b) 호스트만: 이탈한 real 플레이어 death 대필(이미 대필했으면 스킵 = 멱등).
            if (!ctx.Session.IsHost) return;
            foreach (var uid in _originalRealPlayers)
            {
                if (current.Contains(uid)) continue;        // 아직 접속중
                if (_leaverWritten.Contains(uid)) continue; // 이미 대필
                _leaverWritten.Add(uid);
                long lastScore = 0;
                if (_liveSync != null && _liveSync.LiveCache.TryGetValue(uid, out var live)) lastScore = live.score;
                ctx.Repository?.WriteDeath(ctx.Session.RoomId, uid, lastScore);
                Debug.Log($"[GameController] (호스트) 이탈 감지 → death 대필: {uid} (마지막 점수 {lastScore}).");
            }
        }

        // ★ 싱글플레이어 게임 시작용 (필요시 추가)
        public void StartSingleGame()
        {
            _rankReceived = false;
            _gameEnded = false;
            _ranksWritten = false;

            _slotMachine = CreateSlotMachine(Random.Range(0, 99999));
            // 싱글은 카운트다운 없이 즉시 시작 → 체온 바로 드레인(멀티만 코디네이터가 지연 세팅).
            if (_runContext != null) _runContext.Run.Started = true;
            _scoreCalculator = new SlotScoreCalculator(_dataManager._gameBalance._data._slotScore);
        }

        private SlotMachine CreateSlotMachine(long seed)
        {
            var gb = _dataManager._gameBalance._data;
            _runContext = RunContextFactory.CreateDefault(seed.ToString(), gb._slotEconomy, gb._survival);

            var slotMachine = SlotMachineFactory.CreateDefault(_runContext, _dataManager);

            var survival = _playerSurvival ?? FindAnyObjectByType<PlayerSurvival>();
            if (survival != null)
            {
                survival.Bind(_runContext);
                survival.enabled = true;
                // P1: run.Started를 여기서 세팅하지 않는다 → 멀티는 MultiSessionCoordinator가 카운트다운 종료 시
                //     전원 동시에 Started=true(체온 동시 드레인). 싱글은 StartSingleGame이 즉시 세팅.
                //     (코디네이터 없는 멀티 dev씬은 첫 스핀 SlotMachine.Spin이 켬 → 폴백)
                Debug.Log("[GameController] PlayerSurvival 연결 완료 → 체온 드레인 시작 시점은 호출부 결정(P1).");
            }
            else
            {
                Debug.LogError("[GameController] ❌ PlayerSurvival 을 찾을 수 없습니다!");
            }

            return slotMachine;
        }

        public SpinOutcome Spin()
        {
            if (_slotMachine == null)
            {
                Debug.LogError("[GameController] SlotMachine 이 없습니다.");
                return default;
            }
            return _slotMachine.Spin();
        }

        private void LoadAndSetGhostEntries(RoomSession session)
        {
            var ghostUids = new List<string>();
            foreach (var kvp in session.Players)
                if (kvp.Value.isGhost) ghostUids.Add(kvp.Key);

            if (ghostUids.Count == 0) return;

            var entries = session.GhostCandidates;
            if (entries == null || entries.Count == 0)
            {
                Debug.Log("[GameController] 고스트 엔트리 없음 → 랜덤 시뮬레이션");
                return;
            }

            var map = new Dictionary<string, GhostEntry>();
            for (int i = 0; i < ghostUids.Count && i < entries.Count; i++)
            {
                map[ghostUids[i]] = entries[i];
                Debug.Log($"[GameController] 고스트 매핑: {ghostUids[i]} → {entries[i]._nick} (점수:{entries[i]._score})");
            }

            _liveSync?.SetGhostEntries(map);
        }

        // =========================================================
        // ★ 핵심 수정: HandleDeath 분기 (싱글/멀티 공용)
        // =========================================================
        private void HandleDeath(RunProgress run)
        {
            if (_gameEnded) return;

            var gb = _dataManager._gameBalance._data;
            var calculator = _scoreCalculator ?? new SlotScoreCalculator(gb._slotScore);
            var user = UserManager.Instance.CurrentUserData;

            if (RoomContext.Instance.IsInMultiplayer)
                HandleMultiplayerDeath(run, calculator, user);
            else
                HandleSingleplayerDeath(run, calculator, user);
        }

        // ─── 싱글: 데이터는 EndRun(Single)에 전부 위임, UI만 여기서 ───
        private void HandleSingleplayerDeath(RunProgress run, SlotScoreCalculator calculator, UserData user)
        {
            // 데이터: 결과 생성 + 저장 + 고스트 저장 (티어 X — EndRun 내부에 티어 없음)
            var result = RunEndService.EndRun(run, calculator, RunMode.Single, user);

            // UI: 공통 헬퍼 사용
            if (result != null)
                ShowResultUI(result, _resultDelay);

            // ★ EndGame() 호출하지 않음! (싱글은 SlotmachineController가 부활로 이어감)
        }

        // ─── 멀티플레이어 전용 (등수 확정 후 티어 적용) ─────────────────────────────────────
        private void HandleMultiplayerDeath(RunProgress run, SlotScoreCalculator calculator, UserData user)
        {
            var ctx = RoomContext.Instance;
            string myUid = ctx.Session.MyUid;
            string roomId = ctx.Session.RoomId;
            int totalPlayers = ctx.Session.PlayerCount;
            long myScore = calculator.MultiScore(run);

            // 1. 임시 등수 계산
            int provisionalRank = 1;
            if (_liveSync != null)
            {
                foreach (var kvp in _liveSync.LiveCache)
                {
                    if (kvp.Key == myUid) continue;
                    if (kvp.Value.alive || kvp.Value.score > myScore) provisionalRank++;
                }
            }

            // 2. 임시 결과 생성 (저장 X, 티어 X)
            var result = MultiRunResultService.CreateProvisional(run, calculator, user, provisionalRank);

            // 타임아웃 대비 보관
            _pendingResult = result;
            _pendingRun = run;
            _provisionalRank = provisionalRank;

            // 3. UI 표시 (임시 등수로)
            ShowResultUI(result, delaySeconds: _resultDelay);

            // 4. 네트워크 — 내 사망 기록만. (사망 감시 SubscribeDeaths는 StartLifecycleWatch에서 세션 시작 시 구독.)
            ctx.Repository?.WriteDeath(roomId, myUid, myScore);

            ctx.Repository?.SubscribeRank(roomId, myUid, rank =>
            {
                if (_rankReceived) return;
                _rankReceived = true;

                // ★ 확정: 골드 + 티어 + 저장 (여기서만 티어 적용!)
                MultiRunResultService.ConfirmRank(result, run, calculator, user, rank, _totalPlayers);
                RunResultUIService.UpdateRank(rank);

                ctx.Repository?.UnsubscribeDeaths(roomId);
                ctx.Repository?.UnsubscribeRank(roomId, myUid);

                if (ctx.Session != null && ctx.Session.IsHost)
                    StartCoroutine(DelayedEndGame(3f));
                else
                    EndGame();
            });

            StartCoroutine(EndGameTimeout(roomId, myUid, 60f));
        }

        // =========================================================
        // ★ 공통: 결과 데이터 생성 로직 추출
        // =========================================================
        private RunResultData BuildRunResult(RunProgress run, SlotScoreCalculator calculator, int initialRank)
        {
            var user = UserManager.Instance.CurrentUserData;
            long rawScore = calculator.MultiScore(run);

            return new RunResultData
            {
                _userId = user._userId,
                _userNick = user._userNick,
                _finalScore = (int)rawScore,
                _maxCombo = run.MaxCombo,
                _survivalTime = run.SurvivalSeconds,
                _settlementCount = run.SettleCount,
                _rank = initialRank,
                _rawScore = rawScore,
                _earnedGold = (int)calculator.MultiCurrency(run, initialRank)
            };
        }

        // =========================================================
        // ★ 공통: UI 표시 로직 외부화 (딜레이 조절 가능)
        // =========================================================
        private void ShowResultUI(RunResultData result, float delaySeconds)
        {
            if (delaySeconds > 0f)
                StartCoroutine(ShowResultDelayed(result, delaySeconds));
            else
                RunResultUIService.Show(result);
        }

        private IEnumerator ShowResultDelayed(RunResultData result, float delay)
        {
            yield return new WaitForSeconds(delay);
            RunResultUIService.Show(result);
        }

        private void OnDeathsUpdated(DataSnapshot snapshot, string roomId, string myUid,
            int totalPlayers, RoomContext ctx)
        {
            if (!snapshot.Exists) return;

            // P2: 종료 조건 = 생존 ≤1명(전원-1 사망). totalPlayers=시작 총원(고정) 기준.
            if ((int)snapshot.ChildrenCount < totalPlayers - 1) return;
            if (_ranksWritten) return;
            _ranksWritten = true;

            var deaths = new List<(string uid, long time)>();
            foreach (var child in snapshot.Children)
            {
                long time = 0;
                var timeNode = child.Child("time");
                if (timeNode.Exists)
                    long.TryParse(timeNode.Value.ToString(), out time);
                deaths.Add((child.Key, time));
            }

            deaths.Sort((a, b) => a.time.CompareTo(b.time)); // 시각 오름차순(먼저 죽은 순)

            // P2 등수: 사망자 = N-i (첫 사망 i=0 → N위 꼴찌, 늦게 죽을수록 상위) + 생존자(로스터-사망)=1위.
            var ranks = new Dictionary<string, int>();
            var deadSet = new HashSet<string>();
            for (int i = 0; i < deaths.Count; i++)
            {
                ranks[deaths[i].uid] = totalPlayers - i;
                deadSet.Add(deaths[i].uid);
            }
            if (ctx.Session != null)
                foreach (var uid in ctx.Session.Players.Keys)
                    if (!deadSet.Contains(uid)) ranks[uid] = 1; // last-man 생존자 = 1위

            ctx.Repository?.WriteRanks(roomId, ranks);
            Debug.Log($"[GameController] 등수 확정(생존≤1) — 사망 {deaths.Count}/{totalPlayers}, 랭크 {ranks.Count}개 기록.");
        }

        // ── P2: 내 등수 확정(죽었든 살아남았든 종료 시). ─────────────────────────────
        // 죽었으면 _pendingResult(HandleMultiplayerDeath서 세팅), 생존자(승자)면 현재 run으로 결과 새로 생성.
        private void OnMyRankConfirmed(int rank, RoomContext ctx)
        {
            if (_rankReceived) return;
            _rankReceived = true;

            var calculator = _scoreCalculator ?? new SlotScoreCalculator(_dataManager._gameBalance._data._slotScore);
            var user = UserManager.Instance.CurrentUserData;
            var run = _pendingRun ?? _slotMachine?.Run;   // 생존자는 pending 없음 → 현재 머신 run

            var result = _pendingResult;
            if (result == null && run != null)            // 생존자(승자): 결과 새로 생성 + 표시
            {
                result = MultiRunResultService.CreateProvisional(run, calculator, user, rank);
                ShowResultUI(result, 0f);
            }
            if (result != null && run != null)
                MultiRunResultService.ConfirmRank(result, run, calculator, user, rank, _totalPlayers); // 골드/티어/저장 1회

            RunResultUIService.UpdateRank(rank);

            string roomId = ctx.Session != null ? ctx.Session.RoomId : null;
            string myUid = ctx.Session != null ? ctx.Session.MyUid : null;
            if (roomId != null)
            {
                ctx.Repository?.UnsubscribeDeaths(roomId);
                if (myUid != null) ctx.Repository?.UnsubscribeRank(roomId, myUid);
            }

            if (ctx.Session != null && ctx.Session.IsHost)
                StartCoroutine(DelayedEndGame(3f));
            else
                EndGame();
        }

        private IEnumerator DelayedEndGame(float delay)
        {
            yield return new WaitForSeconds(delay);
            EndGame();
        }

        private IEnumerator EndGameTimeout(string roomId, string myUid, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_rankReceived) yield break;
            _rankReceived = true;

            var ctx = RoomContext.Instance;
            ctx.Repository?.UnsubscribeDeaths(roomId);
            ctx.Repository?.UnsubscribeRank(roomId, myUid);

            // ★ 등수 못 받았어도 임시 등수로 확정 처리 (안 하면 결과가 영영 저장 안 됨!)
            if (_pendingResult != null && _pendingRun != null)
            {
                var calculator = _scoreCalculator ?? new SlotScoreCalculator(_dataManager._gameBalance._data._slotScore);
                MultiRunResultService.ConfirmRank(
                    _pendingResult, _pendingRun, calculator,
                    UserManager.Instance.CurrentUserData, _provisionalRank, _totalPlayers);
            }

            Debug.LogWarning("[GameController] 등수 수신 타임아웃 → 임시 등수로 확정 종료");
            EndGame();
        }

        public void EndGame()
        {
            if (_gameEnded) return;
            _gameEnded = true;

            _liveSync?.StopSync();

            var ctx = RoomContext.Instance;
            if (ctx.IsInMultiplayer && ctx.Session != null && ctx.Session.IsHost)
            {
                ctx.Repository?.DeleteRoom(ctx.Session.RoomId);
            }

            if (_playerSurvival != null) _playerSurvival.enabled = false;
            else
            {
                var ps = FindAnyObjectByType<PlayerSurvival>();
                if (ps != null) ps.enabled = false;
            }

            RoomContext.Instance.Clear();
        }
    }
}