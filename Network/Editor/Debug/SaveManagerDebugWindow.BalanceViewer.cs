using UnityEditor;
using UnityEngine;
using Wooduduk.Data.DataSO;
using Wooduduk.Data.Static;

namespace Wooduduk.Editor
{
    public partial class SaveManagerDebugWindow
    {
        private void DrawBalanceViewerTab(UserData user)
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
                _showBalanceTab = false;
                EditorPrefs.SetBool(PREF_SHOW_BALANCE_TAB, false);
                _currentTab = Tab.Firebase;
                SetStatus("📊 밸런스 뷰어 닫힘", MessageType.Info);
            }
            GUI.backgroundColor = old;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            var gb = dm._gameBalance._data;

            DrawBox(_styleBoxBlue, "🌡️ 생존 설정 (SurvivalConfig)", () =>
            {
                var c = gb._survival;
                BvRow("최대 체온", c._maxBodyTemp.ToString("F1"));
                BvRow("기본 드레인 /s", c._drainBasePerSecond.ToString("F2"));
                BvRow("정산당 드레인 추가", c._drainPerSettleStep.ToString("F2"));
                BvRow("위험 임계값", c._criticalThreshold.ToString("F1"));
                BvRow("틱 간격 (s)", c._tickSeconds.ToString("F3"));
                BvRow("장작당 연소시간 (s)", c._burnSecondsPerLog.ToString("F1"));
                BvRow("최대 연소 누적 (s)", c._maxBurnSeconds.ToString("F1"));
                BvRow("연소 중 회복 /s", c._warmHealPerSecond.ToString("F2"));
            });

            DrawBox(_styleBoxOrange, "🎰 슬롯 경제 (SlotEconomyConfig)", () =>
            {
                var c = gb._slotEconomy;
                BvRow("기본 장작/히트", c._baseWoodPerHit.ToString());
                BvRow("콤보 곡선 밴드 수", (c._comboCurve != null ? c._comboCurve.Count : 0).ToString());
                BvRow("콤보10 배율", (c.ComboMultBp(10) / 10000f).ToString("F1") + "x");
                BvRow("콤보50 배율", (c.ComboMultBp(50) / 10000f).ToString("F1") + "x");
                BvRow("콤보100 배율", (c.ComboMultBp(100) / 10000f).ToString("F1") + "x");
                BvRow("콤보500 배율", (c.ComboMultBp(500) / 10000f).ToString("F1") + "x");
                BvRow("도토리 장작/개", c._acornWoodEach.ToString());
                BvRow("모닥불 기본비용", c._fireFeedBaseCost.ToString());
                BvRow("모닥불 비용 증가/정산", c._fireFeedCostStep.ToString());
                BvRow("스핀 기본비용", c._spinBaseCost.ToString());
                BvRow("스핀 비용 증가/정산", c._spinCostStep.ToString());
                BvRow("카드 기본비용", c._cardBaseCost.ToString());
                BvRow("카드 비용 증가", c._cardCostStep.ToString());
            });

            DrawBox(_styleBoxGreen, "🏆 점수 설정 (SlotScoreConfig)", () =>
            {
                var c = gb._slotScore;
                BvRow("장작 점수 BP", c._bankedWoodScoreBp.ToString("N0"));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("생존 구간 점수", EditorStyles.boldLabel);
                foreach (var band in c._survivalBands)
                    EditorGUILayout.LabelField($"  {band._minSeconds}초 이상 → {band._points}점",
                        EditorStyles.miniLabel);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("등수 점수", EditorStyles.boldLabel);
                for (int i = 0; i < c._rankPoints.Count; i++)
                    EditorGUILayout.LabelField($"  {i + 1}위 → {c._rankPoints[i]}점",
                        EditorStyles.miniLabel);
            });
        }

        private void BvRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(200));
            var s = new GUIStyle(EditorStyles.boldLabel);
            s.normal.textColor = _colYellow;
            EditorGUILayout.LabelField(value, s);
            EditorGUILayout.EndHorizontal();
        }

        private DataManagerSO FindDataManager()
        {
            var guids = AssetDatabase.FindAssets("t:DataManagerSO");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<DataManagerSO>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}