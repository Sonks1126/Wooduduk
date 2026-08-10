using UnityEditor;
using UnityEngine;
using Wooduduk.Data.Service;
using Wooduduk.Data.Static;
using Wooduduk.Network;
using Wooduduk.Network.Firebase.Matchmaking;
using Wooduduk.Slot;
using Wooduduk.Slot.Machine;

namespace Wooduduk.Editor
{
    public partial class SaveManagerDebugWindow
    {
        private Vector2 _roomDebugScrollPos;
        private Vector2 _roomDebugLogScrollPos;

        private bool _roomDebugLastMatched;

        private void DrawMatchmakingTab(UserData user)
        {
            _roomDebugScrollPos = EditorGUILayout.BeginScrollView(_roomDebugScrollPos);

            DrawMatchmakingControls(user);
            EditorGUILayout.Space(6);

            DrawMatchmakingMatchState();
            EditorGUILayout.Space(6);

            DrawMatchmakingContext();
            EditorGUILayout.Space(6);

            DrawMatchmakingLiveAndGameStatus();

            EditorGUILayout.EndScrollView();
        }

        private void DrawMatchmakingControls(UserData user)
        {
            DrawBox(_styleBoxOrange, "🧪 Firebase Room 매칭 테스트 컨트롤", () =>
            {
                var flow = FindAnyObjectByType<MatchmakingFlow>();

                if (flow == null)
                {
                    var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                    style.normal.textColor = _colRed;
                    EditorGUILayout.LabelField("❌ 씬에 MatchmakingFlow가 없습니다.", style);
                    EditorGUILayout.LabelField("GameSession 오브젝트에 MatchmakingFlow 컴포넌트를 붙여주세요.");
                    return;
                }

                // 매칭 상태 전환 감지 → 알림
                if (flow.IsMatched && !_roomDebugLastMatched)
                {
                    SetStatus("✅ Firebase Room 매칭 완료 → 게임 시작됨", MessageType.Info);
                }
                _roomDebugLastMatched = flow.IsMatched;

                EditorGUILayout.LabelField(
                    $"👤 현재 유저: {user._userNick} / Tier {user._axeTier} / {user._userId}",
                    EditorStyles.miniLabel);

                EditorGUILayout.Space(6);

                GUI.enabled = !flow.IsSearching && !flow.IsMatched;

                flow.DesiredPlayerCount = EditorGUILayout.IntSlider(
                    "목표 인원",
                    flow.DesiredPlayerCount,
                    1,
                    4);

                flow.AllowGhostFill = EditorGUILayout.Toggle(
                    "자동 고스트 백필",
                    flow.AllowGhostFill);

                flow.GhostFillTimeout = EditorGUILayout.FloatField(
                    "고스트 백필 대기 시간",
                    flow.GhostFillTimeout);

                GUI.enabled = true;

                EditorGUILayout.Space(8);

                var oldColor = GUI.backgroundColor;

                EditorGUILayout.BeginHorizontal();

                // 매칭 찾기 버튼
                GUI.enabled = !flow.IsSearching && !flow.IsMatched;
                GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
                if (GUILayout.Button("🔍 매칭 찾기 / 방 생성", GUILayout.Height(34)))
                {
                    flow.BeginMatch();
                    SetStatus("🔍 Firebase Room 매칭 찾기 시작", MessageType.Info);
                }

                // 취소/정리 버튼
                GUI.enabled = flow.IsSearching || flow.IsMatched || RoomContext.Instance.IsInMultiplayer;
                GUI.backgroundColor = new Color(1f, 0.45f, 0.35f);
                if (GUILayout.Button("■ 취소 / 정리", GUILayout.Height(34), GUILayout.Width(120)))
                {
                    // 방 직접 삭제 (matchSource 상태 무관하게)
                    var session = flow.CurrentSession;
                    if (session != null && !string.IsNullOrEmpty(session.RoomId))
                    {
                        var repo = new Wooduduk.Network.Firebase.Matchmaking.RoomRepository();
                        repo.DeleteRoom(session.RoomId);
                    }

                    flow.CancelMatch();
                    RoomContext.Instance.Clear();
                    _roomDebugLastMatched = false;
                    SetStatus("■ Firebase Room 매칭 취소/정리 + 방 삭제", MessageType.Warning);
                }

                if (_lastMultiResult != null)
                {
                    GUI.backgroundColor = Color.gray;
                    if (GUILayout.Button("🗑 결과 지우기", GUILayout.Height(28), GUILayout.Width(110)))
                        _lastMultiResult = null;
                    GUI.backgroundColor = oldColor;
                }

                GUI.enabled = true;
                GUI.backgroundColor = oldColor;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(6);
                EditorGUILayout.BeginHorizontal();

                var curSession = flow.CurrentSession;
                bool isFull = curSession != null && curSession.PlayerCount >= flow.DesiredPlayerCount;

                // ★ 방 있고, 매칭 안됐고, 인원 안 찼고, 시작 안 했을 때만
                bool canAddMember = flow.HasRoom
                    && !flow.IsMatched
                    && !flow.IsGameStarting   // ★ 시작 중이면 차단
                    && !isFull;               // ★ 가득 차면 차단

                GUI.enabled = canAddMember;
                GUI.backgroundColor = new Color(0.4f, 1f, 0.5f);
                if (GUILayout.Button("👤 플레이어 추가", GUILayout.Height(28)))
                {
                    bool ok = flow.DebugAddFakePlayer();
                    SetStatus(ok ? "👤 디버그 플레이어 추가" : "❌ 인원 충족/시작 중 - 추가 불가",
                        ok ? MessageType.Info : MessageType.Error);
                }

                GUI.backgroundColor = new Color(0.7f, 0.6f, 1f);
                if (GUILayout.Button("👻 고스트 추가", GUILayout.Height(28)))
                {
                    bool ok = flow.DebugAddGhost();
                    SetStatus(ok ? "👻 디버그 고스트 추가" : "❌ 인원 충족/시작 중 - 추가 불가",
                        ok ? MessageType.Info : MessageType.Error);
                }

                // 강제 시작은 방 있고 시작 안 했을 때
                GUI.enabled = flow.HasRoom && !flow.IsGameStarting;
                GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
                if (GUILayout.Button("▶ 강제 시작", GUILayout.Height(28)))
                {
                    bool ok = flow.DebugForceStart();
                    SetStatus(ok ? "▶ 강제 시작 요청" : "❌ 이미 시작 중", ok ? MessageType.Info : MessageType.Error);
                }

                GUI.enabled = true;
                GUI.backgroundColor = oldColor;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(6);

                EditorGUILayout.HelpBox(
                    "테스트 흐름: [매칭 찾기] → RoomId 생성 확인 → [플레이어 추가] 또는 [고스트 추가] → 인원 충족 → playing 전환 → 게임 시작 확인",
                    MessageType.Info);
            });
        }

        private void DrawMatchmakingMatchState()
        {
            DrawBox(_styleBoxOrange, "🔍 [1단계] 매칭 진행 상태 (MatchmakingFlow)", () =>
            {
                var flow = FindAnyObjectByType<MatchmakingFlow>();

                if (flow == null)
                {
                    EditorGUILayout.LabelField("씬에 MatchmakingFlow 없음");
                    return;
                }

                var session = flow.CurrentSession;

                if (session == null || string.IsNullOrEmpty(session.RoomId))
                {
                    EditorGUILayout.LabelField("상태",
                        flow.IsSearching ? "🔍 대기방 탐색 중 / 방 생성 중" : "⚪ Idle - [매칭 찾기]를 누르세요.");
                    DrawRoomDebugLogs(flow);
                    return;
                }

                // ★★★ 카운트다운 강조 표시 ★★★
                if (flow.IsCountingDown)
                {
                    var cdBox = new GUIStyle(EditorStyles.helpBox)
                    {
                        fontSize = 22,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        padding = new RectOffset(10, 10, 14, 14)
                    };
                    cdBox.normal.textColor = Color.red;

                    EditorGUILayout.LabelField(
                        $"🔥 게임 시작까지 {flow.AutoStartRemainingTime:F0}초",
                        cdBox);
                    EditorGUILayout.Space(6);
                }
                else if (flow.IsLoading)
                {
                    var loadBox = new GUIStyle(EditorStyles.helpBox)
                    {
                        fontSize = 18,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        padding = new RectOffset(10, 10, 12, 12)
                    };
                    loadBox.normal.textColor = _colYellow;

                    EditorGUILayout.LabelField("⏳ 로딩 중...", loadBox);
                    EditorGUILayout.Space(6);
                }

                string statusText = flow.IsMatched
                    ? "✅ 매칭 완료 / Playing"
                    : "⏳ Room 생성됨 / 인원 대기 중";

                var statusStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                statusStyle.normal.textColor = flow.IsMatched ? _colGreen : _colYellow;

                EditorGUILayout.LabelField("상태", statusText, statusStyle);
                EditorGUILayout.LabelField("RoomId", session.RoomId);
                EditorGUILayout.LabelField("State", string.IsNullOrEmpty(session.State) ? "(null)" : session.State);
                EditorGUILayout.LabelField("Seed", session.Seed.ToString());
                EditorGUILayout.LabelField("IsHost", session.IsHost ? "👑 Host" : "👤 Guest");

                EditorGUILayout.Space(4);

                EditorGUILayout.LabelField("목표 인원", $"{flow.DesiredPlayerCount}명");
                EditorGUILayout.LabelField("현재 인원", $"{session.PlayerCount} / {session.MaxPlayers}");
                EditorGUILayout.LabelField("실제 플레이어", $"{session.RealPlayerCount}명");
                EditorGUILayout.LabelField("고스트 수", $"{session.PlayerCount - session.RealPlayerCount}명");

                // ── 인원 충족 여부 ──
                EditorGUILayout.Space(4);

                var canStart = session.PlayerCount >= flow.DesiredPlayerCount;
                var canStartStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                canStartStyle.normal.textColor = canStart ? _colGreen : _colRed;

                EditorGUILayout.LabelField(
                    "게임 시작 가능",
                    canStart ? "✅ 인원 충족 - 시작 가능" : $"❌ 인원 부족 ({session.PlayerCount}/{flow.DesiredPlayerCount})",
                    canStartStyle);

                if (flow.AutoStartAfterMatch && !flow.IsCountingDown && !flow.IsLoading)
                {
                    var autoStartStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 11 };
                    autoStartStyle.normal.textColor = canStart ? _colGreen : _colYellow;
                    EditorGUILayout.LabelField(
                        $"자동 시작: {(canStart ? "✅ 준비 완료" : "⏳ 인원 대기 중")}",
                        autoStartStyle);
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("방 참가자 현황:", EditorStyles.boldLabel);

                if (session.Players.Count == 0)
                {
                    EditorGUILayout.LabelField("아직 참가자 없음");
                }
                else
                {
                    foreach (var kvp in session.Players)
                    {
                        var p = kvp.Value;

                        string tag = p.isGhost ? "👻 Ghost" : "👤 Player";
                        string me = kvp.Key == session.MyUid ? " (나)" : "";
                        string uidShort = string.IsNullOrEmpty(p.uid)
                            ? "(uid 없음)"
                            : p.uid.Substring(0, Mathf.Min(12, p.uid.Length));

                        var rowStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 12 };
                        rowStyle.normal.textColor = p.isGhost ? _colPurple : _colGreen;

                        EditorGUILayout.LabelField(
                            $" - {tag} {p.nick}{me} | T{p.tier} | Ready={p.ready} | {uidShort}",
                            rowStyle);
                    }
                }

                DrawRoomDebugLogs(flow);
            });

            // ★★ 카운트다운/로딩 중일 때 실시간 갱신 ★★
            Repaint();
        }

        private void DrawSlotmachineControllerStatus()
        {
            DrawBox(_styleBoxPurple, "🎰 SlotmachineController 상태", () =>
            {
                var smc = FindAnyObjectByType<SlotmachineController>();

                if (smc == null)
                {
                    var s = new GUIStyle(EditorStyles.miniLabel) { fontSize = 11 };
                    s.normal.textColor = Color.gray;
                    EditorGUILayout.LabelField("씬에 SlotmachineController 없음", s);
                    return;
                }

                var run = smc.Run;

                // ── 기본 상태 ──
                EditorGUILayout.BeginHorizontal();
                LbLabel("모드", 40, TextAnchor.MiddleLeft, _colBlue, true);
                LbLabel(smc.IsSpinning ? "🔄 스핀중" : "⚪ 대기", 80, TextAnchor.MiddleLeft);
                LbLabel("체온", 35, TextAnchor.MiddleLeft, _colBlue, true);

                float temp = run != null ? smc.BodyTempNormalized : 0f;
                Color tempCol = temp > 0.5f ? _colGreen : temp > 0.25f ? _colYellow : _colRed;
                LbLabel($"{temp * 100f:F0}%", 50, TextAnchor.MiddleLeft, tempCol);

                LbLabel(run != null && run.IsAlive ? "🟢 생존" : "🔴 사망", 70, TextAnchor.MiddleLeft);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                if (run == null)
                {
                    var ns = new GUIStyle(EditorStyles.miniLabel) { fontSize = 11 };
                    ns.normal.textColor = Color.gray;
                    EditorGUILayout.LabelField("RunProgress 없음 — 아직 Init 전", ns);
                    return;
                }

                // ── Run 수치 ──
                EditorGUILayout.BeginHorizontal();
                LbLabel("스핀", 35, TextAnchor.MiddleLeft, _colBlue, true);
                LbLabel($"{run.SpinIndex}", 40, TextAnchor.MiddleLeft, _colYellow);
                LbLabel("생존", 35, TextAnchor.MiddleLeft, _colBlue, true);
                LbLabel($"{run.SurvivalSeconds:F0}s", 50, TextAnchor.MiddleLeft, _colYellow);
                LbLabel("정산", 35, TextAnchor.MiddleLeft, _colBlue, true);
                LbLabel($"{run.SettleCount}회", 40, TextAnchor.MiddleLeft, _colYellow);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                LbLabel("장작", 35, TextAnchor.MiddleLeft, _colBlue, true);
                LbLabel($"{run.BankedWood}", 50, TextAnchor.MiddleLeft, _colYellow);
                LbLabel("미정산", 45, TextAnchor.MiddleLeft, _colBlue, true);
                LbLabel($"{run.UnsettledWood}", 50, TextAnchor.MiddleLeft, _colYellow);
                LbLabel("콤보", 35, TextAnchor.MiddleLeft, _colBlue, true);
                LbLabel($"x{run.Combo}", 40, TextAnchor.MiddleLeft, _colYellow);
                EditorGUILayout.EndHorizontal();
            });
        }

        private void DrawRoomDebugLogs(MatchmakingFlow flow)
        {
            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📋 매칭 이벤트 로그", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("로그 지우기", GUILayout.Width(90), GUILayout.Height(20)))
            {
                flow.ClearLogs();
            }

            EditorGUILayout.EndHorizontal();

            var logs = flow.DebugLogs;

            if (logs == null || logs.Count == 0)
            {
                EditorGUILayout.LabelField("로그 없음");
                return;
            }

            _roomDebugLogScrollPos = EditorGUILayout.BeginScrollView(
                _roomDebugLogScrollPos,
                GUILayout.MaxHeight(160));

            var logStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                wordWrap = true
            };

            for (int i = 0; i < logs.Count; i++)
            {
                string log = logs[i];

                if (log.Contains("성공") || log.Contains("시작"))
                    logStyle.normal.textColor = _colGreen;
                else if (log.Contains("실패") || log.Contains("취소"))
                    logStyle.normal.textColor = _colRed;
                else if (log.Contains("고스트"))
                    logStyle.normal.textColor = _colPurple;
                else if (log.Contains("플레이어"))
                    logStyle.normal.textColor = _colBlue;
                else
                    logStyle.normal.textColor = Color.white;

                EditorGUILayout.LabelField(log, logStyle);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawMatchmakingContext()
        {
            DrawBox(_styleBoxGreen, "📦 [2단계] 인게임 세션 상태 (RoomContext)", () =>
            {
                var ctx = RoomContext.Instance;

                if (ctx == null || !ctx.IsInMultiplayer || ctx.Session == null)
                {
                    EditorGUILayout.LabelField("현재 인게임 멀티 세션 없음");
                    EditorGUILayout.LabelField("매칭 완료 후 생성됩니다.");
                    return;
                }

                var s = ctx.Session;

                EditorGUILayout.LabelField("RoomId", s.RoomId);
                EditorGUILayout.LabelField("Seed", s.Seed.ToString());
                EditorGUILayout.LabelField("State", string.IsNullOrEmpty(s.State) ? "(null)" : s.State);
                EditorGUILayout.LabelField("IsHost", s.IsHost ? "👑 방장" : "👤 게스트");
                EditorGUILayout.LabelField("Repository", ctx.Repository != null ? "🟢 연결됨" : "❌ 없음");
            });
        }

        private void DrawMatchmakingLiveAndGameStatus()
        {
            DrawBox(_styleBoxBlue, "🎮 실시간 동기화 및 게임 상태", () =>
            {
                var gc = FindAnyObjectByType<GameController>();
                var slot = gc?.CurrentSlotMachine;
                var session = RoomContext.Instance?.Session;
                var liveSync = FindAnyObjectByType<RoomLiveSync>();

                // ── 1. 동기화 + GameController 상태 ──
                EditorGUILayout.BeginHorizontal();

                string syncStr = (liveSync != null)
                    ? (liveSync.IsSyncing ? "🟢 Syncing" : "⚪ Idle")
                    : "⚪ NoSync";
                var syncStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
                syncStyle.normal.textColor = (liveSync != null && liveSync.IsSyncing) ? _colGreen : Color.gray;
                EditorGUILayout.LabelField($"Sync: {syncStr}", syncStyle, GUILayout.Width(120));

                if (gc != null)
                {
                    string gameStr = gc.IsGameEnded
                        ? "🔴 종료"
                        : gc.IsGameActive ? "🟢 진행중" : "⚪ 대기";
                    string rankStr = gc.RankReceived ? "✅ 등수확정" : "⏳ 등수대기";

                    var gcStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
                    gcStyle.normal.textColor = gc.IsGameEnded ? _colRed : gc.IsGameActive ? _colGreen : Color.gray;
                    EditorGUILayout.LabelField($"Game: {gameStr}", gcStyle, GUILayout.Width(110));

                    var rankStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 11 };
                    rankStyle.normal.textColor = gc.RankReceived ? _colGreen : _colYellow;
                    EditorGUILayout.LabelField(rankStr, rankStyle);
                }
                else
                {
                    var naStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 11 };
                    naStyle.normal.textColor = Color.gray;
                    EditorGUILayout.LabelField("Game: ⚪ GameController 없음", naStyle);
                }

                EditorGUILayout.EndHorizontal();

                // ── 세션이 없으면 더 이상 진행 불가 ──
                if (session == null || liveSync == null)
                {
                    EditorGUILayout.LabelField("세션 또는 LiveSync 없음", EditorStyles.miniLabel);
                    Repaint();
                    return;
                }

                EditorGUILayout.Space(4);

                // ── 2. 방 전체 실시간 표 ──
                EditorGUILayout.LabelField("📊 방 내부 실시간 현황", EditorStyles.boldLabel);

                // 헤더
                EditorGUILayout.BeginHorizontal();
                LbLabel("플레이어", 120, TextAnchor.MiddleLeft, _colBlue, true);
                LbLabel("점수", 70, TextAnchor.MiddleRight, _colBlue, true);
                LbLabel("콤보", 50, TextAnchor.MiddleRight, _colBlue, true);
                LbLabel("체온", 55, TextAnchor.MiddleRight, _colBlue, true);
                LbLabel("생존", 45, TextAnchor.MiddleCenter, _colBlue, true);
                EditorGUILayout.EndHorizontal();

                var line = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(line, new Color(_colBlue.r, _colBlue.g, _colBlue.b, 0.4f));
                EditorGUILayout.Space(2);

                foreach (var kvp in session.Players)
                {
                    string uid = kvp.Key;
                    var p = kvp.Value;
                    bool isMe = uid == session.MyUid;

                    long score;
                    int combo;
                    float temp;
                    bool alive;

                    if (isMe && slot != null)
                    {
                        // ★ 내 데이터: SlotMachine이 있을 때만 직접 읽음
                        var run = slot.Run;
                        score = (int)run.BankedWood;
                        combo = run.Combo;
                        temp = run.BodyTemp;
                        alive = slot.IsAlive;
                    }
                    else
                    {
                        // ★ LiveCache에서 데이터 읽기 (슬롯머신 없어도 표시됨)
                        if (liveSync.LiveCache.TryGetValue(uid, out var live))
                        {
                            score = live.score;
                            combo = live.combo;
                            temp = live.temperature;
                            alive = live.alive;
                        }
                        else
                        {
                            // 데이터가 아직 없으면 기본값 (고스트도 포함)
                            score = 0;
                            combo = 0;
                            temp = 36.5f;
                            alive = true;
                        }
                    }

                    // 색상
                    var rowCol = isMe ? _colOrange : (p.isGhost ? _colPurple : Color.white);
                    string icon = isMe ? "👤" : (p.isGhost ? "👻" : "🙂");
                    string nameLabel = $"{icon} {p.nick}{(isMe ? " (나)" : "")}";

                    EditorGUILayout.BeginHorizontal();
                    LbLabel(nameLabel, 120, TextAnchor.MiddleLeft, rowCol, isMe);
                    LbLabel($"{score:N0}", 70, TextAnchor.MiddleRight, _colYellow);
                    LbLabel($"x{combo}", 50, TextAnchor.MiddleRight);
                    LbLabel($"{temp:F0}°", 55, TextAnchor.MiddleRight);
                    LbLabel(alive ? "🟢" : "🔴", 45, TextAnchor.MiddleCenter);
                    EditorGUILayout.EndHorizontal();
                }

                // ── 3. 내 로컬 상세 (SlotMachine 있을 때만) ──
                if (slot != null)
                {
                    var run = slot.Run;
                    var detailStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 10 };
                    detailStyle.normal.textColor = Color.gray;
                    EditorGUILayout.LabelField(
                        $"[내 로컬] Banked:{run.BankedWood} | Unsettled:{run.UnsettledWood} | " +
                        $"Spin:{run.SpinIndex} | Time:{run.SurvivalSeconds:F0}s | Alive:{slot.IsAlive}",
                        detailStyle);
                }
                else
                {
                    // 슬롯머신 없으면 안내 문구
                    var detailStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 10 };
                    detailStyle.normal.textColor = Color.gray;
                    EditorGUILayout.LabelField(
                        $"[내 로컬] SlotMachine 미생성 - 게임 시작 후 표시됩니다.",
                        detailStyle);
                }
            });

            DrawSlotmachineControllerStatus();
            EditorGUILayout.Space(6);

            DrawBox(_styleBoxRed, "💀 강제 사망 테스트", () =>
            {
                var gc2 = FindAnyObjectByType<GameController>();
                bool canDie = gc2 != null && gc2.IsGameActive;

                EditorGUILayout.HelpBox(
                    "현재 SlotMachine의 Run 데이터로 SlotEvent.RaiseDeath를 발동합니다.\n" +
                    "결과창 표시 / 화면 페이드 / GameController 처리 전체를 테스트할 수 있습니다.",
                    MessageType.None);

                EditorGUILayout.Space(4);

                GUI.enabled = canDie;
                var old = GUI.backgroundColor;
                GUI.backgroundColor = canDie ? new Color(1f, 0.3f, 0.3f) : Color.gray;

                if (GUILayout.Button("💀 지금 죽기 (강제 사망)", GUILayout.Height(34)))
                {
                    var slot = gc2.CurrentSlotMachine;
                    var run = slot?.Run;

                    if (run != null)
                    {
                        RunResultUIService.OnResultShown = null;
                        RunResultUIService.OnResultShown += OnMultiResultReceived;
                        _lastMultiResult = null;
                        _multiResultPending = true;

                        SlotEvent.RaiseDeath(run);
                        SetStatus("💀 강제 사망 발동 — 결과창/페이드 확인하세요", MessageType.Warning);
                    }
                    else
                    {
                        SetStatus("❌ SlotMachine.Run 없음 — 게임이 시작됐는지 확인", MessageType.Error);
                    }
                }

                GUI.backgroundColor = old;
                GUI.enabled = true;

                if (!canDie)
                {
                    var hint = new GUIStyle(EditorStyles.miniLabel) { fontSize = 11 };
                    hint.normal.textColor = Color.gray;
                    EditorGUILayout.LabelField(
                        gc2 == null ? "GameController가 씬에 없습니다." : "게임이 시작된 후 사용 가능합니다.",
                        hint);
                }
            });

            if (_lastMultiResult != null)
            {
                EditorGUILayout.Space(6);
                DrawMultiResult(_lastMultiResult);
            }
            else if (_multiResultPending)
            {
                EditorGUILayout.Space(6);
                DrawMultiResultPending();
            }
            Repaint();
        }
    }
}