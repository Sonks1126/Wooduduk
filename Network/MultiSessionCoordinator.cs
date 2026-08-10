using System;
using System.Collections;
using UnityEngine;
using Wooduduk.Network.Firebase.Matchmaking; // RoomContext
using Wooduduk.Slot;                          // RunContext/RunProgress (var 체인 안전)
using Wooduduk.Slot.Machine;                  // SlotmachineController

namespace Wooduduk.Network
{
    // ─────────────────────────────────────────────────────────────────────────────
    // MultiSessionCoordinator — 멀티 세션 "서버시간 동기 시작"(P1).
    //
    // 무엇을 하나:
    //   · MultiScene 진입 후(StartMultiGame 완료 감지) 카운트다운 시작.
    //   · 호스트가 rooms/{roomId}/startAt(절대 서버시각 ms)을 기록, 전원이 자기 serverTimeOffset로
    //     그 절대시각에 맞춰 카운트다운 → 전원 "같은 순간" 동시 발화.
    //   · 발화 시: run.Started=true(체온 동시 드레인 시작) + 스핀 잠금 해제.
    //   · 카운트다운 중: 스핀 봉인(SlotmachineController.SetSpinLocked(true)).
    //
    // 왜 이렇게:
    //   · 체온 드레인은 PlayerSurvival이 run.Started 게이팅 → Started를 동일 절대시각에 켜면 전원 동시 드레인.
    //   · 스핀 잠금은 run.Started 게이팅 대신 별도 플래그(단일플레이 데드락 방지).
    //   · GameController.CreateSlotMachine은 더 이상 Started를 즉시 켜지 않음(P1) → 여기서 켠다.
    //
    // [씬 배치 사용법] (MultiScene, 수동)
    //   1) 빈 GameObject "MultiSessionCoordinator" → 이 스크립트 부착.
    //   2) (선택) _gameController / _slotmachineController 연결(비우면 FindAnyObjectByType 자동).
    //   3) _countdownSeconds = 5~10 (로드 편차 흡수 위해 넉넉히).
    //   4) 카운트다운 UI 배선(선택): OnCountdownStarted(남은초) 이벤트를 HUD에 연결.
    //        예) coordinator.OnCountdownStarted += s => InGameHUDController.Instance.ShowCountdownEvent("게임 시작까지 {0}", s, "시작!");
    //
    // 안전: 멀티 아님/Repository 없음 → 아무것도 안 함(단독 씬 안전). RoomContext(정적)는 씬 전환 넘어와 있음.
    // ─────────────────────────────────────────────────────────────────────────────
    [DisallowMultipleComponent]
    public class MultiSessionCoordinator : MonoBehaviour
    {
        #region --- 인스펙터 ---

        [Header("참조 (선택 — 비우면 자동 탐색)")]
        [SerializeField] private GameController _gameController;
        [SerializeField] private SlotmachineController _slotmachineController;

        [Header("설정")]
        [Tooltip("카운트다운 길이(초). 로드 시점 편차 흡수 위해 넉넉히(5~10).")]
        [SerializeField] private float _countdownSeconds = 7f;

        #endregion

        // UI 배선용. OnCountdownStarted(남은초) → ShowCountdownEvent 등에 연결. OnSessionStarted = 발화 완료.
        public event Action<float> OnCountdownStarted;
        public event Action OnSessionStarted;

        #region --- 내부 상태 ---

        private bool _begun;          // 카운트다운 절차 시작됨(1회)
        private bool _started;         // 세션 발화 완료(1회)
        private long _serverOffsetMs;  // .info/serverTimeOffset (로컬 → 서버 시각 보정)
        private string _roomId;

        #endregion

        private void Update()
        {
            if (_begun) return;

            var ctx = RoomContext.Instance;
            if (ctx == null || !ctx.IsInMultiplayer || ctx.Session == null || ctx.Repository == null) return;

            var gc = ResolveGameController();
            // StartMultiGame 완료 대기. 멀티는 SlotmachineController.Init 경로라 _runContext가 아니라
            // CurrentSlotMachine에 머신/런이 잡힌다 → CurrentSlotMachine으로 준비 판정(양쪽 경로 커버).
            if (gc == null || gc.CurrentSlotMachine == null) return;

            _begun = true;
            BeginCountdown(ctx);
        }

        private void BeginCountdown(RoomContext ctx)
        {
            _roomId = ctx.Session.RoomId;

            // ── 손찬성 요청: 고스트-호스트 안전망 ────────────────────────────────
            // IsHost가 고스트를 제외하므로 정상 상황에선 항상 실플레이어가 호스트다.
            //  (a) 방에 실플레이어가 하나도 없으면(=고아 방) 아무도 startAt을 못 써 전원 무한 대기 →
            //      "고스트만 호스트 후보"인 broken 상태이므로 방을 파괴하고 세션 시작을 중단한다.
            //  (b) 고스트가 '나이브 최소 uid'인 경우는 IsHost 픽스로 실플레이어가 호스트를 승계하므로
            //      파괴하지 않고 경고 로그만 남긴다(솔로+고스트 정상 매치의 절반이 여기 해당 → 파괴 금지).
            if (!ctx.Session.HasRealHost)
            {
                Debug.LogError($"[MultiSessionCoordinator] 실플레이어 0 — 고스트만 남은 고아 방({_roomId}) → 방 파괴, 세션 시작 중단.");
                ctx.Repository.DeleteRoom(_roomId);
                return;
            }
            if (ctx.Session.GhostWouldBeNaiveHost)
                Debug.LogWarning($"[MultiSessionCoordinator] 나이브 선출이면 고스트가 호스트였을 상황 — IsHost 픽스로 실플레이어가 승계, 방 유지({_roomId}).");
            // ─────────────────────────────────────────────────────────────────────

            ResolveSlot()?.SetSpinLocked(true); // 카운트다운 동안 스핀 봉인(즉시)
            Debug.Log("[MultiSessionCoordinator] 카운트다운 준비 — 스핀 잠금, 서버시간 오프셋 조회.");

            ctx.Repository.ReadServerTimeOffset(offset =>
            {
                _serverOffsetMs = (long)offset;
                ctx.Repository.SubscribeStartAt(_roomId, OnStartAt);

                if (ctx.Session.IsHost)
                {
                    long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _serverOffsetMs;
                    long startAtMs = nowMs + (long)(_countdownSeconds * 1000f);
                    ctx.Repository.WriteStartAt(_roomId, startAtMs);
                    Debug.Log($"[MultiSessionCoordinator] (호스트) startAt={startAtMs} 기록 (offset={_serverOffsetMs}ms, 카운트다운 {_countdownSeconds}s).");
                }
                else
                {
                    Debug.Log($"[MultiSessionCoordinator] (비호스트) startAt 구독 대기 (offset={_serverOffsetMs}ms).");
                }
            });
        }

        // startAt 수신(전원) → 절대시각까지 남은 시간 계산 → 카운트다운 후 발화.
        private void OnStartAt(long startAtMs)
        {
            if (_started) return;

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _serverOffsetMs;
            float secLeft = Mathf.Max(0f, (startAtMs - nowMs) / 1000f);
            Debug.Log($"[MultiSessionCoordinator] startAt 수신 → {secLeft:F2}s 후 세션 시작 (startAt={startAtMs}, now={nowMs}).");

            OnCountdownStarted?.Invoke(secLeft); // UI(ShowCountdownEvent 등) 배선용

            StopAllCoroutines();
            StartCoroutine(CountdownThenBegin(secLeft));
        }

        private IEnumerator CountdownThenBegin(float secLeft)
        {
            float t = secLeft;
            while (t > 0f)
            {
                yield return null;
                t -= Time.deltaTime;
            }
            BeginSession();
        }

        // 발화 — 전원 동일 절대시각에 도달: 체온 드레인 ON + 스핀 해제.
        private void BeginSession()
        {
            if (_started) return;
            _started = true;

            // 멀티 실제 런 = CurrentSlotMachine.Run (SlotmachineController.Init가 만든 그 run, PlayerSurvival이 드레인).
            var run = ResolveGameController()?.CurrentSlotMachine?.Run;
            if (run != null) run.Started = true;   // PlayerSurvival이 이걸 게이팅 → 동시 드레인 시작
            else Debug.LogError("[MultiSessionCoordinator] CurrentSlotMachine.Run 없음 — Started 설정 실패(GameController/머신 확인).");

            ResolveSlot()?.SetSpinLocked(false);    // 스핀 해제
            OnSessionStarted?.Invoke();
            Debug.Log("[MultiSessionCoordinator] ▶ 세션 시작 — 체온 드레인 ON, 스핀 해제.");
        }

        private void OnDisable()
        {
            var repo = RoomContext.Instance?.Repository;
            if (repo != null && !string.IsNullOrEmpty(_roomId)) repo.UnsubscribeStartAt(_roomId);
        }

        private GameController ResolveGameController()
        {
            if (_gameController == null) _gameController = FindAnyObjectByType<GameController>();
            return _gameController;
        }

        private SlotmachineController ResolveSlot()
        {
            if (_slotmachineController == null) _slotmachineController = FindAnyObjectByType<SlotmachineController>();
            return _slotmachineController;
        }
    }
}
