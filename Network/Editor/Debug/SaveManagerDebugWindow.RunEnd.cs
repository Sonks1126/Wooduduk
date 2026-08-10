using UnityEditor;
using UnityEngine;
using Wooduduk.Data.Result;
using Wooduduk.Data.Service;
using Wooduduk.Data.Static;
using Wooduduk.Network;
using Wooduduk.Slot;
using Wooduduk.Tier;

namespace Wooduduk.Editor
{
    public partial class SaveManagerDebugWindow
    {
        // 마지막 멀티 결과
        private RunResultData _lastMultiResult = null;
        private bool _multiResultPending = false;

        private void OnMultiResultReceived(RunResultData result)
        {
            _lastMultiResult = result;
            _multiResultPending = false;
            Repaint();
        }

        private void DrawRunEndTab(UserData user)
        {
            DrawTierCard(user);

            DrawBox(_styleBoxPurple, "🎮 가상 런 입력", () =>
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("⏱ 생존", GUILayout.Width(50));
                _runSurvival = EditorGUILayout.TextField(_runSurvival, GUILayout.Width(50));
                GUILayout.Space(12);
                EditorGUILayout.LabelField("🔥 모닥불", GUILayout.Width(60));
                _runFireLog = EditorGUILayout.TextField(_runFireLog, GUILayout.Width(50));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("📦 정산", GUILayout.Width(50));
                _runSettle = EditorGUILayout.TextField(_runSettle, GUILayout.Width(50));
                GUILayout.Space(12);
                EditorGUILayout.LabelField("🪵 장작", GUILayout.Width(60));
                _runBankedWood = EditorGUILayout.TextField(_runBankedWood, GUILayout.Width(50));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(8);

                // [싱글] 기존 버튼
                var old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                if (GUILayout.Button("▶  RunEnd 전체 실행 (싱글)", GUILayout.Height(34)))
                    TestRunEndPipeline(user); // ★ user 전달
                GUI.backgroundColor = old;

                EditorGUILayout.Space(4);

                // [멀티] GameController 경로로 실제 죽음 이벤트 발동
                GUI.backgroundColor = new Color(0.8f, 0.4f, 1f);
                if (GUILayout.Button("🌐 사망 이벤트 발동 (멀티 파이프라인 테스트)", GUILayout.Height(34)))
                    TestMultiDeathEvent();
                GUI.backgroundColor = old;
            });

            // 멀티 결과 대기/표시
            if (_multiResultPending)
                DrawMultiResultPending();
            else if (_lastMultiResult != null)
                DrawMultiResult(_lastMultiResult);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 멀티 결과 패널
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawMultiResultPending()
        {
            DrawBox(_styleBoxPurple, "🌐 멀티 결과 대기 중...", () =>
            {
                var s = new GUIStyle(EditorStyles.miniLabel) { fontSize = 12 };
                s.normal.textColor = _colYellow;
                EditorGUILayout.LabelField("서버 RPC 응답 대기 중. 서버가 연결되어 있으면 곧 표시됩니다.", s);
                EditorGUILayout.LabelField("서버가 없으면 콘솔 로그에서 결과를 확인하세요.", s);
            });
        }

        private void DrawMultiResult(RunResultData result)
        {
            DrawBox(_styleBoxGreen, "🌐 마지막 멀티 결과 (서버 수신)", () =>
            {
                var bold = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                bold.normal.textColor = _colYellow;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"🏆 최종 점수: {result._finalScore:N0}", bold, GUILayout.Width(200));
                EditorGUILayout.LabelField($"💰 획득 골드: {result._earnedGold:N0}G", bold, GUILayout.Width(160));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                var infoStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 12 };
                infoStyle.normal.textColor = _colGreen;

                string rankText = result._rank > 0 ? $"{result._rank}위" : "집계 중 (0)";
                EditorGUILayout.LabelField($"📊 순위: {rankText}", infoStyle, GUILayout.Width(120));
                EditorGUILayout.LabelField($"⏱ 생존: {result._survivalTime:F1}s", infoStyle, GUILayout.Width(120));
                EditorGUILayout.LabelField($"📦 정산: {result._settlementCount}회", infoStyle, GUILayout.Width(120));
                EditorGUILayout.LabelField($"⚡ 최대 콤보: {result._maxCombo}", infoStyle, GUILayout.Width(120));
                EditorGUILayout.EndHorizontal();

                if (result._rank == 0)
                {
                    EditorGUILayout.Space(4);
                    var warn = new GUIStyle(EditorStyles.miniLabel) { fontSize = 11, wordWrap = true };
                    warn.normal.textColor = _colRed;
                    EditorGUILayout.LabelField(
                        "⚠️ 순위가 0인 이유: SessionManager.EndSessionServer()가 아직 구현되지 않아 서버에서 최종 순위를 전달하지 않습니다. (할 일 #2)",
                        warn);
                }
            });
        }

        private void TestRunEndPipeline(UserData user)
        {
            if (user == null)
            {
                SetStatus("❌ UserData가 없습니다. 로그인을 확인하세요.", MessageType.Error);
                return;
            }

            // SlotScoreConfig는 [Serializable] class이므로 new로 생성
            var cfg = new SlotScoreConfig();
            var score = new SlotScoreCalculator(cfg);

            var mockRun = new RunProgress
            {
                SurvivalSeconds = ParseFloat(_runSurvival),
                FireLogCount = ParseInt(_runFireLog),
                SettleCount = ParseInt(_runSettle),
                BankedWood = ParseInt(_runBankedWood),
                IsAlive = false
            };

            int bLp = TierService.Instance != null ? TierService.Instance.CurrentLp : 0;

            // ★ user 파라미터 명시적 전달
            var result = RunEndService.EndRun(mockRun, score, RunMode.Single, user);

            if (result != null)
            {
                int delta = (TierService.Instance != null ? TierService.Instance.CurrentLp : 0) - bLp;
                SetStatus($"RunEnd! 점수:{result._finalScore:N0}  LP:{delta:+#;-#;0}", MessageType.Info);
            }
            else
            {
                SetStatus("RunEnd 실패", MessageType.Error);
            }
        }

        // 실제 SlotEvent.RaiseDeath → GameController.HandleDeath 경로를 타는 통합 테스트.
        // RunEndService.EndRun(Multi)는 케이스가 없으므로 이쪽 경로로만 검증해야 함.
        private void TestMultiDeathEvent()
        {
            var gc = FindAnyObjectByType<GameController>();
            if (gc == null)
            {
                SetStatus("❌ 씬에 GameController 없음 — 게임 씬에서 실행하세요.", MessageType.Error);
                return;
            }

            var mockRun = new RunProgress
            {
                SurvivalSeconds = ParseFloat(_runSurvival),
                FireLogCount = ParseInt(_runFireLog),
                SettleCount = ParseInt(_runSettle),
                BankedWood = ParseInt(_runBankedWood),
                IsAlive = false
            };

            _lastMultiResult = null;
            _multiResultPending = true;
            RunResultUIService.OnResultShown = null;
            RunResultUIService.OnResultShown += OnMultiResultReceived;

            SlotEvent.RaiseDeath(mockRun);
            SetStatus("🌐 SlotEvent.RaiseDeath 발동 → GameController 경로 테스트 중...", MessageType.Info);
        }
    }
}