using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Network.Firebase.Matchmaking; // RoomContext, RoomSession, RoomPlayerData

namespace Wooduduk.Network
{
    // ─────────────────────────────────────────────────────────────────────────────
    // MultiSeatSpawner — 멀티 매치마다 상대 좌석(2P~4P) 표시 쉘을 관리.
    //
    // 두 가지 모드 (인스펙터 필드 연결로 자동 분기):
    //   · 고정 모드  — _fixedSeats(씬에 미리 배치한 좌석) 연결 시: SetActive+Bind / SetActive(false).
    //   · 앵커 모드  — _fixedSeats 비우고 _opponentSeatPrefab + _opponentAnchors 연결 시: 런타임 Instantiate/Destroy.
    //
    // 무엇을 하나:
    //   · 멀티 진입을 자가 감지(RoomContext.IsInMultiplayer 폴링) → 상대 쉘 활성/스폰.
    //   · 멀티 종료 감지 → 쉘 비활성/파괴.
    //   · 1P(나)는 기존 씬 리그 그대로 → 스포너는 "상대만" 다룬다(MyUid 제외).
    //
    // 왜 자가구동:
    //   · MultiStatusBoard와 동일 패턴. GameController 등 기존 코드 수정 0.
    //   · 좌석 배정은 로컬 표현 — 각 클라가 자기를 1P로 보고 상대만 채운다(전역 합의 아님).
    //   · 인원 부족분은 MatchmakingFlow의 고스트 백필(_allowGhostFill)이 session.Players에 채워넣으므로,
    //     여기선 실/고스트 구분 없이 그냥 바인드한다(고스트도 RoomLiveSync.SimulateGhosts가 시뮬).
    //
    // [씬 배치 — 고정 모드] (MultiScene, 수동)
    //   1) 빈 GameObject "MultiSeatSpawner" (또는 프리팹 인스턴스) → 이 스크립트 부착.
    //   2) OpponentSeat.prefab(표시전용 쉘) 3개를 2P/3P/4P 월드좌표에 배치 → 기본 비활성.
    //   3) 그 3개를 _fixedSeats 에 순서대로 연결. (_opponentSeatPrefab/_opponentAnchors 는 비움)
    //   4) MatchmakingFlow._desiredPlayerCount = 4 (고스트 백필로 항상 4자리).
    //   5) 플레이 → 매칭 진입 시 좌석 활성 + 바인드.
    //
    // [씬 배치 — 앵커 모드] (Chunghui_DevMulti 등, 기존)
    //   _fixedSeats 비움 + _opponentSeatPrefab + _opponentAnchors(빈 앵커) 연결 → 런타임 스폰.
    //
    // 주의: 로스터는 매치 시작 시 방이 닫혀 고정 → 배치는 진입 시 1회. 이후 각 좌석은 자체 폴링으로 갱신.
    // ─────────────────────────────────────────────────────────────────────────────
    [DisallowMultipleComponent]
    public class MultiSeatSpawner : MonoBehaviour
    {
        #region --- 인스펙터 ---

        [Header("좌석 모드 (둘 중 하나만 연결)")]
        [Tooltip("고정 모드: 씬에 미리 배치한 상대 좌석(기본 비활성). 연결되면 앵커 대신 이걸 SetActive+Bind. 비우면 앵커 모드.")]
        [SerializeField] private OpponentSeatView[] _fixedSeats;

        [Header("앵커 모드 (고정 모드 안 쓸 때)")]
        [Tooltip("상대 좌석 표시 쉘 프리팹(OpponentSeatView 부착). 앵커 모드 전용.")]
        [SerializeField] private OpponentSeatView _opponentSeatPrefab;

        [Tooltip("상대 좌석 위치 앵커. 최대 3개(1P는 기존 리그라 제외). 앵커 모드 전용.")]
        [SerializeField] private Transform[] _opponentAnchors;

        [Header("참조 (선택)")]
        [Tooltip("LiveSync 접근용. 비우면 FindAnyObjectByType로 자동 탐색.")]
        [SerializeField] private GameController _gameController;

        [Header("설정")]
        [Tooltip("멀티 진입/종료 감지 폴링 주기(초).")]
        [SerializeField] private float _pollInterval = 0.33f;

        #endregion

        #region --- 내부 상태 ---

        private readonly List<OpponentSeatView> _spawned = new List<OpponentSeatView>();
        private bool _seatsSpawned;
        private float _timer;

        // _fixedSeats가 하나라도 연결되면 고정 모드.
        private bool FixedMode => _fixedSeats != null && _fixedSeats.Length > 0;

        #endregion

        // 고정 좌석은 씬에서 켜져 있어도 멀티 진입 전엔 숨긴다(단일플레이/대기 중 안 보이게).
        private void Awake()
        {
            if (!FixedMode) return;
            for (int i = 0; i < _fixedSeats.Length; i++)
                if (_fixedSeats[i] != null) _fixedSeats[i].gameObject.SetActive(false);
            Debug.Log($"[MultiSeatSpawner] 고정 모드 — 좌석 {_fixedSeats.Length}개 대기(비활성).");
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < _pollInterval) return;
            _timer = 0f;

            var ctx = RoomContext.Instance;
            bool inMulti = ctx != null && ctx.IsInMultiplayer && ctx.Session != null;

            if (inMulti && !_seatsSpawned) SpawnSeats(ctx.Session);
            else if (!inMulti && _seatsSpawned) DespawnSeats();
        }

        // 상대(=MyUid 제외) 플레이어를 좌석에 배치. 모드에 따라 고정 바인드 or 앵커 스폰.
        private void SpawnSeats(RoomSession session)
        {
            RoomLiveSync liveSync = ResolveLiveSync();
            if (liveSync == null)
            {
                // GameController.StartMultiGame 전이면 아직 LiveSync 준비 전일 수 있음 → 재시도(플래그 안 세움).
                Debug.LogWarning("[MultiSeatSpawner] LiveSync 못 찾음 — 이번 폴 스킵(다음에 재시도).");
                return;
            }

            int count = FixedMode ? SpawnFixed(session, liveSync) : SpawnAnchored(session, liveSync);
            _seatsSpawned = true;
            Debug.Log($"[MultiSeatSpawner] 상대 좌석 {count}개 {(FixedMode ? "고정 바인드" : "스폰")} (세션 {session.PlayerCount}명).");
        }

        // 고정 모드: 미리 배치된 _fixedSeats를 활성화 + 바인드. 반환 = 바인드한 좌석 수.
        private int SpawnFixed(RoomSession session, RoomLiveSync liveSync)
        {
            string myUid = session.MyUid;
            int seat = 0;

            foreach (var kvp in session.Players)
            {
                if (seat >= _fixedSeats.Length) break;      // 좌석 다 참
                var p = kvp.Value;
                if (p == null || p.uid == myUid) continue;   // 나(1P) 제외

                var view = _fixedSeats[seat];
                if (view == null)
                {
                    Debug.LogWarning($"[MultiSeatSpawner] 고정좌석[{seat}] 비어있음 — 스킵.");
                    seat++;
                    continue;
                }

                view.gameObject.SetActive(true);
                view.Bind(p.uid, p.nick, liveSync);
                seat++;
            }
            return seat;
        }

        // 앵커 모드(기존): 프리팹을 앵커에 스폰 + 바인드. 반환 = 스폰한 좌석 수.
        private int SpawnAnchored(RoomSession session, RoomLiveSync liveSync)
        {
            if (_opponentSeatPrefab == null)
            {
                Debug.LogWarning("[MultiSeatSpawner] (앵커 모드) OpponentSeat 프리팹 미할당 — 스폰 스킵.");
                return 0;
            }
            if (_opponentAnchors == null || _opponentAnchors.Length == 0)
            {
                Debug.LogWarning("[MultiSeatSpawner] (앵커 모드) 앵커 미할당 — 스폰 스킵.");
                return 0;
            }

            string myUid = session.MyUid;
            int seat = 0;

            foreach (var kvp in session.Players)
            {
                if (seat >= _opponentAnchors.Length) break; // 앵커 다 참
                var p = kvp.Value;
                if (p == null || p.uid == myUid) continue;    // 나(1P) 제외

                Transform anchor = _opponentAnchors[seat];
                if (anchor == null)
                {
                    Debug.LogWarning($"[MultiSeatSpawner] 앵커[{seat}] 비어있음 — 이 좌석 스킵.");
                    seat++;
                    continue;
                }

                OpponentSeatView view = Instantiate(_opponentSeatPrefab, anchor);
                view.transform.localPosition = Vector3.zero;
                view.transform.localRotation = Quaternion.identity;
                view.Bind(p.uid, p.nick, liveSync);

                _spawned.Add(view);
                seat++;
            }
            return _spawned.Count;
        }

        private void DespawnSeats()
        {
            if (FixedMode)
            {
                // 고정: 파괴 안 하고 숨김(재사용).
                for (int i = 0; i < _fixedSeats.Length; i++)
                    if (_fixedSeats[i] != null) _fixedSeats[i].gameObject.SetActive(false);
            }
            else
            {
                // 앵커: 스폰본 파괴.
                for (int i = 0; i < _spawned.Count; i++)
                    if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
                _spawned.Clear();
            }

            _seatsSpawned = false;
            Debug.Log($"[MultiSeatSpawner] 상대 좌석 정리 ({(FixedMode ? "고정 비활성" : "스폰 파괴")}, 멀티 종료).");
        }

        private RoomLiveSync ResolveLiveSync()
        {
            if (_gameController != null && _gameController.LiveSync != null)
                return _gameController.LiveSync;

            var found = FindAnyObjectByType<GameController>();
            return found != null ? found.LiveSync : null;
        }
    }
}
