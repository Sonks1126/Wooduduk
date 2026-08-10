using UnityEngine;
using UnityEngine.SceneManagement;
using Wooduduk.Network.Firebase.Matchmaking; // RoomContext
using Wooduduk.UI;                            // ResultController (결과창 복귀 버튼)

namespace Wooduduk.Network
{
    // ─────────────────────────────────────────────────────────────────────────────
    // MultiGameBootstrap — 멀티 씬(Chunghui_DevMulti) 진입 시 멀티 게임을 시작하는 트리거.
    //
    // 무엇을 하나:
    //   · 씬 로드 후 Start()에서 RoomContext(정적 싱글턴, 매칭 씬에서 적재됨)를 보고,
    //     멀티 컨텍스트가 있으면 이 씬의 GameController.StartMultiGame()을 1회 호출.
    //   · MatchmakingFlow(LoadScene 모드)가 SceneManager.LoadSceneAsync로 이 씬을 로드하면
    //     seed/session/Repository는 정적 RoomContext로 넘어와 있고, 여기서 게임을 켠다.
    //
    // 왜 필요:
    //   · MatchmakingFlow는 매칭 씬(DevScene)에 있고 씬 전환 시 파괴됨.
    //     실제 게임 GameController는 이 멀티 씬에 있으므로, 전환 후 "시작"을 눌러줄 주체가 필요.
    //
    // [씬 배치 사용법] (Chunghui_DevMulti.unity)
    //   1) 빈 GameObject "MultiGameBootstrap" 생성 → 이 스크립트 부착.
    //   2) (권장) 씬의 GameController를 _gameController에 연결. 비우면 FindAnyObjectByType로 자동 탐색.
    //   3) 매칭 씬 MatchmakingFlow의 _startMode=LoadScene, _multiSceneName=이 씬 이름, Build Settings에 두 씬 등록.
    //
    // 안전:
    //   · 매칭 없이 이 씬을 단독 오픈하면 RoomContext가 비어있음 → 아무것도 안 함(크래시 없음, 로그만).
    //   · _started 가드로 중복 호출 방지.
    // ─────────────────────────────────────────────────────────────────────────────
    [DisallowMultipleComponent]
    public class MultiGameBootstrap : MonoBehaviour
    {
        [Tooltip("이 씬의 GameController. 비우면 FindAnyObjectByType로 자동 탐색.")]
        [SerializeField] private GameController _gameController;

        [Tooltip("멀티 종료 후 결과창 '다시하기/확인' 시 돌아갈 매칭 씬 이름(예: Chunghui_DevScene). Build Settings에 등록돼 있어야 함.")]
        [SerializeField] private string _returnSceneName = "Chunghui_DevScene";

        private bool _started;

        // 결과창 복귀 버튼 구독(정적 이벤트 → 대칭 구독, 씬 파괴 시 자동 해지).
        private void OnEnable()  { ResultController.OnRestartRequested += HandleReturnToMatchmaking; }
        private void OnDisable() { ResultController.OnRestartRequested -= HandleReturnToMatchmaking; }

        // 멀티 종료 후 결과창 확인/다시하기 → 매칭 씬으로 복귀. (EndGame이 이미 RoomContext.Clear 했음)
        private void HandleReturnToMatchmaking()
        {
            if (string.IsNullOrEmpty(_returnSceneName))
            {
                Debug.LogError("[MultiGameBootstrap] _returnSceneName 비어있음 — 복귀 씬 설정 필요.");
                return;
            }
            Debug.Log($"[MultiGameBootstrap] 결과창 복귀 → 씬 로드: {_returnSceneName}");
            SceneManager.LoadScene(_returnSceneName);
        }

        private void Start()
        {
            TryStart();
        }

        private void TryStart()
        {
            if (_started) return;

            var ctx = RoomContext.Instance;
            if (ctx == null || !ctx.IsInMultiplayer)
            {
                // 매칭 없이 씬 단독 실행 등 — 정상 스킵(안전).
                Debug.Log("[MultiGameBootstrap] 멀티 컨텍스트 없음(RoomContext 비어있음) — StartMultiGame 스킵(씬 단독 실행으로 간주).");
                return;
            }

            GameController gc = _gameController != null ? _gameController : FindAnyObjectByType<GameController>();
            if (gc == null)
            {
                Debug.LogError("[MultiGameBootstrap] GameController 못 찾음 — 멀티 게임 시작 불가. 이 씬에 GameController 배치/연결 확인.");
                return;
            }

            _started = true;
            gc.StartMultiGame();
            Debug.Log($"[MultiGameBootstrap] StartMultiGame 호출 완료 (RoomId={ctx.Session.RoomId}, Seed={ctx.Session.Seed}).");
        }
    }
}
