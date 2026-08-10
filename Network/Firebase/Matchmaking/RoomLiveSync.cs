using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Network.Firebase.Leaderboard;
using Wooduduk.Network.Firebase.Matchmaking;
using Wooduduk.Player;
using Wooduduk.Slot;
using Wooduduk.Slot.Machine;

namespace Wooduduk.Network
{
    public class RoomLiveSync : MonoBehaviour
    {
        [Header("동기화 설정")]
        [SerializeField] private float _syncInterval = 0.33f;

        private SurvivalConfig _survivalConfig;
        private RoomSession _session;
        private RoomRepository _repo;
        private SlotMachine _slotMachine;
        private SlotScoreCalculator _scoreCalculator;
        private float _syncTimer;
        private bool _isRunning;
        private bool _subscribed;

        private Dictionary<string, GhostEntry> _ghostEntries = new();
        // ★ 모든 플레이어의 최신 라이브 데이터 (uid → data)
        private readonly Dictionary<string, RoomLiveData> _liveCache = new();
        public IReadOnlyDictionary<string, RoomLiveData> LiveCache => _liveCache;

        // ★ 디버그 윈도우용 프로퍼티
        public bool IsSyncing => _isRunning;

        public bool IsEnabled
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
                if (!_isRunning)
                {
                    _syncTimer = 0f;
                    Debug.Log("[RoomLiveSync] 비활성화됨");
                }
                else
                {
                    Debug.Log("[RoomLiveSync] 활성화됨 ✅");
                    TrySubscribe(); // 켜질 때 구독 시작
                }
            }
        }

        public void Initialize(RoomSession session, RoomRepository repository, SlotMachine slotMachine, SurvivalConfig survivalConfig, SlotScoreCalculator scoreCalculator)
        {
            _session = session;
            _repo = repository;
            _slotMachine = slotMachine;
            _survivalConfig = survivalConfig;
            _scoreCalculator = scoreCalculator;

            _syncTimer = 0f;
            _isRunning = true;
            _liveCache.Clear();

            TrySubscribe();

            Debug.Log($"[RoomLiveSync] 초기화 완료 및 동기화 시작 ✅ (Room: {session.RoomId})");
        }

        private void TrySubscribe()
        {
            if (_subscribed || _repo == null || _session == null) return;
            if (string.IsNullOrEmpty(_session.RoomId)) return;

            // ★ 상대방 라이브 데이터 구독 시작
            _repo.SubscribeLive(_session.RoomId, OnLiveReceived);
            _subscribed = true;
            Debug.Log("[RoomLiveSync] 상대 라이브 구독 시작");
        }

        // ★ Firebase에서 실시간 데이터가 올 때마다 호출됨
        private void OnLiveReceived(Dictionary<string, RoomLiveData> all)
        {
            if (all == null) return;

            foreach (var kvp in all)
            {
                _liveCache[kvp.Key] = kvp.Value;
            }
        }

        public void StopSync()
        {
            _isRunning = false;

            if (_subscribed && _repo != null && _session != null)
            {
                _repo.UnsubscribeLive(_session.RoomId);
                _subscribed = false;
            }

            _slotMachine = null;
            Debug.Log("[RoomLiveSync] 동기화 중단됨 🛑");
        }

        private void Update()
        {
            // 세션이나 레포가 없으면 초기화가 안 된 것이므로 실행 안 함
            if (!_isRunning || _slotMachine == null || _session == null || _repo == null) return;

            _syncTimer += Time.deltaTime;
            if (_syncTimer < _syncInterval) return;
            _syncTimer = 0f;

            SendMyLiveData();
            SimulateGhosts(); // ★ 고스트 시뮬레이션 추가
        }

        private void SendMyLiveData()
        {
            if (string.IsNullOrEmpty(_session.RoomId) || string.IsNullOrEmpty(_session.MyUid)) return;

            var run = _slotMachine.Run;

            var liveData = new RoomLiveData
            {
                score = _scoreCalculator != null ? _scoreCalculator.MultiScore(run) : run.BankedWood,
                combo = run.Combo,
                temperature = run.BodyTemp,
                alive = _slotMachine.IsAlive,
                spinIndex = run.SpinIndex,
                fireLit = run.FireBurnSeconds > 0f  // 모닥불 연소시간 남으면 ON
            };

            // ★ 내 데이터도 캐시에 즉시 반영 (디버그 창에서 바로 보이게)
            _liveCache[_session.MyUid] = liveData;

            try
            {
                _repo.UpdateLive(_session.RoomId, _session.MyUid, liveData);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoomLiveSync] Live 전송 실패: {e.Message}");
            }
        }

        /// <summary>
        /// [디버그용] 특정 UID의 최신 실시간 데이터를 반환합니다.
        /// 내 데이터는 SlotMachine에서 직접 읽어서 최신 상태를 보장합니다.
        /// </summary>
        public RoomLiveData GetPlayerLiveData(string uid)
        {
            // 내 데이터: SlotMachine에서 직접 읽음
            if (_session != null && uid == _session.MyUid && _slotMachine != null)
            {
                var run = _slotMachine.Run;
                return new RoomLiveData
                {
                    // ★ SendMyLiveData와 동일 규칙으로 맞춤(불일치 제거). score=MultiScore, fireLit 누락 복구.
                    score = _scoreCalculator != null ? _scoreCalculator.MultiScore(run) : run.BankedWood,
                    combo = run.Combo,
                    temperature = run.BodyTemp,
                    alive = _slotMachine.IsAlive,
                    spinIndex = run.SpinIndex,
                    fireLit = run.FireBurnSeconds > 0f   // ★ 누락 복구 — 내 패널 모닥불 표시
                };
            }

            // 상대/고스트: 캐시에서 읽음
            if (_liveCache.TryGetValue(uid, out var live))
            {
                return live;
            }

            // 데이터 없음
            return new RoomLiveData
            {
                alive = true,
                score = 0,
                combo = 0,
                temperature = 36.5f,
                spinIndex = 0
            };
        }

        // 외부에서 고스트 데이터 주입
        public void SetGhostEntries(Dictionary<string, GhostEntry> entries)
        {
            _ghostEntries = entries ?? new Dictionary<string, GhostEntry>();
            Debug.Log($"[RoomLiveSync] 고스트 엔트리 {_ghostEntries.Count}개 주입됨");
        }

        // ★ 고스트 로컬 시뮬레이션
        private void SimulateGhosts()
        {
            if (_session == null) return;

            // ★ 세션 GO(run.Started) 전엔 고스트 점수·체온 진행 정지. 실플레이어와 동일하게 카운트다운 후부터 시작.
            //   초기 캐시 엔트리는 대기 중에도 만들어 좌석/보드에 정상 초기값(0점/풀체온) 표시.
            bool started = _slotMachine != null && _slotMachine.Run != null && _slotMachine.Run.Started;

            foreach (var kvp in _session.Players)
            {
                var p = kvp.Value;
                if (!p.isGhost) continue;

                // 캐시에 없으면 초기 데이터 생성
                if (!_liveCache.TryGetValue(p.uid, out var ghostLive))
                {
                    ghostLive = new RoomLiveData
                    {
                        alive = true,
                        score = 0,
                        // SO에서 MaxBodyTemp 읽기, 없으면 100f
                        temperature = _survivalConfig?._maxBodyTemp ?? 100f
                    };
                    _liveCache[p.uid] = ghostLive;
                }

                if (!started) continue;      // ★ GO 전엔 진행 정지(초기값만 유지) — 대기시간 전 점수상승 버그 픽스
                if (!ghostLive.alive) continue;

                // 체온 감소 — 플레이어와 동일한 드레인
                float drain = _survivalConfig?._drainBasePerSecond ?? 4f;
                ghostLive.temperature = Mathf.Max(0, ghostLive.temperature - (drain * _syncInterval));

                // 점수 증가 — GhostEntry 있으면 목표 기반, 없으면 랜덤
                if (_ghostEntries.TryGetValue(p.uid, out var entry))
                {
                    // 목표 생존시간 60초 가정 → 초당 목표 점수 역산
                    float targetPerSecond = entry._score / 60f;
                    ghostLive.score += Mathf.RoundToInt(targetPerSecond * _syncInterval);

                    // 콤보도 maxCombo 기반으로 서서히 증가
                    int targetCombo = Mathf.Clamp(entry._maxCombo, 1, 15);
                    ghostLive.combo = Mathf.Min(ghostLive.combo + 1, targetCombo);
                }
                else
                {
                    // GhostEntry 없을 때 폴백 — 적당한 랜덤
                    ghostLive.score += Mathf.RoundToInt(Random.Range(20f, 60f) * _syncInterval);
                    ghostLive.combo = Mathf.Min(ghostLive.combo + 1, 10);
                }

                ghostLive.spinIndex++;

                // 체온 0 → 사망
                if (ghostLive.temperature <= 0f)
                {
                    ghostLive.alive = false;
                    // 호스트만 기록 — 모든 클라가 쓰면 중복됨
                    if (_session.IsHost)
                        _repo?.WriteDeath(_session.RoomId, p.uid, ghostLive.score);
                    Debug.Log($"[RoomLiveSync] 고스트 {p.nick} 사망!");
                }
            }
        }
    }
}