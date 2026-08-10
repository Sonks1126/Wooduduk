using UnityEngine;
using Wooduduk.Network;

namespace Wooduduk.Network.Firebase.Matchmaking
{
    public class MatchmakingDebugger : MonoBehaviour
    {
        [SerializeField] private MatchmakingFlow _matchmakingFlow;
        [SerializeField] private GameController _gameController;

        [Header("디버그 GUI 설정")]
        [SerializeField] private bool _showDebugGUI = true;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;

        [Header("GUI 위치/크기 설정")]
        [SerializeField] private float _panelWidth = 380f;
        [SerializeField] private float _panelHeight = 500f;
        [SerializeField] private float _marginX = 10f;
        [SerializeField] private float _marginY = 10f;

        [Header("로그 설정")]
        [SerializeField] private float _logHeight = 120f;
        [SerializeField] private int _maxVisibleLogs = 20;
        [SerializeField] private bool _autoScrollLog = true;

        private Vector2 _logScroll;
        private int _lastLogCount;
        private float _countdownRemaining;
        private bool _isLoading;
        private float _loadingRemaining;

        private void Awake()
        {
            if (_matchmakingFlow == null)
                _matchmakingFlow = FindAnyObjectByType<MatchmakingFlow>();
            if (_gameController == null)
                _gameController = FindAnyObjectByType<GameController>();
        }

        private void OnEnable()
        {
            if (_matchmakingFlow == null) return;
            _matchmakingFlow.OnCountdownTick += t => _countdownRemaining = t;
            _matchmakingFlow.OnLoadingStarted += d => { _isLoading = true; _loadingRemaining = d; };
            _matchmakingFlow.OnLoadingFinished += () => { _isLoading = false; _loadingRemaining = 0f; };
        }

        private void OnDisable()
        {
            if (_matchmakingFlow == null) return;
            _matchmakingFlow.OnCountdownTick -= t => _countdownRemaining = t;
            _matchmakingFlow.OnLoadingStarted -= d => { _isLoading = true; _loadingRemaining = d; };
            _matchmakingFlow.OnLoadingFinished -= () => { _isLoading = false; _loadingRemaining = 0f; };
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                _showDebugGUI = !_showDebugGUI;

            if (_isLoading)
                _loadingRemaining = Mathf.Max(0f, _loadingRemaining - Time.deltaTime);
        }

        private void OnGUI()
        {
            if (!_showDebugGUI) return;

            float panelHeight = Mathf.Min(_panelHeight, Screen.height - _marginY * 2f);
            float posX = _marginX;
            float posY = Screen.height - panelHeight - _marginY;
            if (posY < _marginY) posY = _marginY;

            GUILayout.BeginArea(new Rect(posX, posY, _panelWidth, panelHeight), GUI.skin.box);

            DrawHeader();
            GUILayout.Space(6);

            DrawMatchStatus();
            GUILayout.Space(8);

            if (_gameController != null && _gameController.IsGameActive)
            {
                DrawPlaySection();
                GUILayout.Space(8);
            }

            DrawFooterActions();
            GUILayout.Space(8);

            DrawLogs();

            GUILayout.EndArea();
        }

        // ── 헤더 ──────────────────────────────────────────
        private void DrawHeader()
        {
            GUILayout.Label($"== 멀티 디버그  [{GetPhaseLabel()}] ==");
        }

        private string GetPhaseLabel()
        {
            if (_gameController != null && _gameController.IsGameEnded) return "ENDED";
            if (_gameController != null && _gameController.IsGameActive) return "PLAYING";
            if (_isLoading) return $"LOADING {_loadingRemaining:F1}s";
            if (_matchmakingFlow != null && _matchmakingFlow.IsCountingDown) return $"START {_countdownRemaining:F0}s";
            if (_matchmakingFlow != null && _matchmakingFlow.IsMatched) return "MATCHED ✓";
            if (_matchmakingFlow != null && _matchmakingFlow.IsSearching) return "SEARCHING...";
            if (_matchmakingFlow != null && _matchmakingFlow.HasRoom) return "ROOM";
            return "IDLE";
        }

        // ── 매칭 상태 ──────────────────────────────────────
        private void DrawMatchStatus()
        {
            GUILayout.Label("-- 매칭 상태 --");

            if (_matchmakingFlow == null)
            {
                GUILayout.Label("MatchmakingFlow 없음");
                return;
            }

            var session = _matchmakingFlow.CurrentSession;
            bool matched = _matchmakingFlow.IsMatched || (_gameController != null && _gameController.IsGameActive);

            GUILayout.Label(matched ? "● 매칭됨" : "○ 대기중");

            if (session != null && !string.IsNullOrEmpty(session.RoomId))
            {
                GUILayout.Label($"Room: {Short(session.RoomId)}  |  Seed: {session.Seed}");
                GUILayout.Label($"Players: {session.PlayerCount}  |  Host: {session.IsHost}");
            }
            else
            {
                GUILayout.Label("Room: -");
            }
        }

        // ── 게임 중 플레이어 실시간 ────────────────────────
        private void DrawPlaySection()
        {
            GUILayout.Label("-- 플레이어 실시간 --");

            var session = _matchmakingFlow?.CurrentSession;
            var liveSync = _gameController.LiveSync;

            // 내 슬롯머신 직접 읽기
            var slot = _gameController.CurrentSlotMachine;
            if (slot != null)
            {
                var run = slot.Run;
                string myUid = session?.MyUid ?? "?";
                GUILayout.Label($"[나] {Short(myUid)}");
                GUILayout.Label($"  스핀:{run.SpinIndex}  점수:{run.BankedWood}  콤보:{run.Combo}(최대:{run.MaxCombo})");
                GUILayout.Label($"  체온:{run.BodyTemp:F1}°  생존:{run.SurvivalSeconds:F0}s  {(slot.IsAlive ? "생존" : "사망")}");
            }

            // 상대 / 고스트 라이브 캐시
            if (liveSync != null && liveSync.LiveCache != null)
            {
                string myUid = session?.MyUid ?? "";

                foreach (var kvp in liveSync.LiveCache)
                {
                    if (kvp.Key == myUid) continue; // 내 건 위에서 이미 표시

                    bool isGhost = kvp.Key.StartsWith("ghost_");
                    string tag = isGhost ? "[고스트]" : "[상대]";
                    var d = kvp.Value;

                    GUILayout.Space(4);
                    GUILayout.Label($"{tag} {Short(kvp.Key)}");
                    GUILayout.Label($"  스핀:{d.spinIndex}  점수:{d.score}  콤보:{d.combo}");
                    GUILayout.Label($"  체온:{d.temperature:F1}°  {(d.alive ? "생존" : "사망")}");
                }
            }
        }

        // ── 하단 액션 ──────────────────────────────────────
        private void DrawFooterActions()
        {
            if (_gameController != null && (_gameController.IsGameActive || _gameController.IsGameEnded))
            {
                if (GUILayout.Button("게임 강제 종료"))
                    _gameController.EndGame();
            }
        }

        // ── 로그 ───────────────────────────────────────────
        private void DrawLogs()
        {
            if (_matchmakingFlow == null) return;

            GUILayout.BeginHorizontal();
            GUILayout.Label("-- 로그 --");
            if (GUILayout.Button("지우기", GUILayout.Width(55)))
                _matchmakingFlow.ClearLogs();
            GUILayout.EndHorizontal();

            var logs = _matchmakingFlow.DebugLogs;

            if (_autoScrollLog && logs.Count != _lastLogCount)
            {
                _logScroll.y = float.MaxValue;
                _lastLogCount = logs.Count;
            }

            _logScroll = GUILayout.BeginScrollView(_logScroll, GUI.skin.box, GUILayout.Height(_logHeight));

            int start = Mathf.Max(0, logs.Count - _maxVisibleLogs);
            for (int i = start; i < logs.Count; i++)
                GUILayout.Label(logs[i]);

            GUILayout.EndScrollView();
        }

        private string Short(string text, int length = 8)
        {
            if (string.IsNullOrEmpty(text)) return "-";
            return text.Length <= length ? text : text.Substring(0, length);
        }
    }
}