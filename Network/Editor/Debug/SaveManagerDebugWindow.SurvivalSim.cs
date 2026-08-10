using UnityEditor;
using UnityEngine;
using Wooduduk.Data.Static;
using Wooduduk.Network;

namespace Wooduduk.Editor
{
    public partial class SaveManagerDebugWindow
    {
        private void DrawSurvivalSimTab(UserData user)
        {
            var dm = FindDataManager();
            if (dm == null)
            {
                EditorGUILayout.HelpBox("DataManagerSO를 찾을 수 없습니다.", MessageType.Error);
                return;
            }

            // ── ✕ 닫기 버튼 ──
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
            if (GUILayout.Button("✕ 닫기", GUILayout.Width(70), GUILayout.Height(22)))
            {
                _showSurvivalSimTab = false;
                EditorPrefs.SetBool(PREF_SHOW_SURVIVALSIM_TAB, false);
                _currentTab = Tab.Firebase;
                SetStatus("🌡️ 생존 시뮬레이터 닫힘", MessageType.Info);
            }
            GUI.backgroundColor = old;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            var cfg = dm._gameBalance._data._survival;

            // ── 실시간 체온 바 ──
            DrawBox(_styleBoxBlue, "🌡️ 실시간 체온", () =>
            {
                var gc = FindAnyObjectByType<GameController>();
                var slot = gc?.CurrentSlotMachine;

                if (slot != null)
                {
                    float temp = slot.Run.BodyTemp;
                    float max = slot.Run.MaxBodyTemp > 0f ? slot.Run.MaxBodyTemp : cfg._maxBodyTemp;
                    float ratio = Mathf.Clamp01(temp / max);

                    var barRect = EditorGUILayout.GetControlRect(false, 28);
                    EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f));
                    Color fill = ratio > 0.5f ? _colGreen : ratio > 0.25f ? _colOrange : _colRed;
                    EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width * ratio, barRect.height), fill);
                    var ls = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
                    EditorGUI.LabelField(barRect, $"{temp:F1} / {max:F0}  ({ratio * 100f:F0}%)", ls);

                    EditorGUILayout.Space(4);
                    var ds = new GUIStyle(EditorStyles.miniLabel);
                    ds.normal.textColor = Color.gray;
                    EditorGUILayout.LabelField(
                        $"생존 {slot.Run.SurvivalSeconds:F1}s | 드레인 {cfg._drainBasePerSecond + slot.Run.SettleCount * cfg._drainPerSettleStep:F1}/s | 정산 {slot.Run.SettleCount}회",
                        ds);
                }
                else
                {
                    var s = new GUIStyle(EditorStyles.miniLabel);
                    s.normal.textColor = Color.gray;
                    EditorGUILayout.LabelField("게임 시작 후 표시됩니다.", s);
                }
            });

            // ── 수치 조작 슬라이더 ──
            DrawBox(_styleBoxOrange, "⚙️ 수치 조작 (즉시 적용)", () =>
            {
                cfg._maxBodyTemp = EditorGUILayout.Slider("최대 체온", cfg._maxBodyTemp, 10f, 300f);
                cfg._drainBasePerSecond = EditorGUILayout.Slider("기본 드레인 /s", cfg._drainBasePerSecond, 0.1f, 30f);
                cfg._drainPerSettleStep = EditorGUILayout.Slider("정산당 드레인 추가", cfg._drainPerSettleStep, 0f, 10f);
                cfg._criticalThreshold = EditorGUILayout.Slider("위험 임계값", cfg._criticalThreshold, 5f, 80f);
                cfg._burnSecondsPerLog = EditorGUILayout.Slider("장작당 연소시간 (s)", cfg._burnSecondsPerLog, 1f, 60f);
                cfg._maxBurnSeconds = EditorGUILayout.Slider("최대 연소 누적 (s)", cfg._maxBurnSeconds, 5f, 120f);
                cfg._warmHealPerSecond = EditorGUILayout.Slider("연소 중 회복 /s", cfg._warmHealPerSecond, 0.1f, 20f);

                EditorGUILayout.Space(8);

                var old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("🔄 기본값 초기화", GUILayout.Height(30)))
                {
                    cfg._maxBodyTemp = 100f;
                    cfg._drainBasePerSecond = 4f;
                    cfg._drainPerSettleStep = 1f;
                    cfg._criticalThreshold = 25f;
                    cfg._burnSecondsPerLog = 6f;
                    cfg._maxBurnSeconds = 30f;
                    cfg._warmHealPerSecond = 3f;
                    SetStatus("🔄 기본값으로 초기화됨", MessageType.Info);
                }
                GUI.backgroundColor = old;

                EditorGUILayout.Space(4);
                var ws = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                ws.normal.textColor = Color.gray;
                EditorGUILayout.LabelField(
                    "⚠️ 플레이 모드 종료 시 수치 초기화됩니다. SO에 영구 저장하려면 직접 수정하세요.",
                    ws);
            });
        }
    }
}