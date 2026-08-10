using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase.Leaderboard;

namespace Wooduduk.Editor
{
    public partial class SaveManagerDebugWindow
    {
        private enum RaceState { Idle, Picking, Racing, Result }
        private RaceState _raceState = RaceState.Idle;

        private class GhostRacer
        {
            public GhostEntry Ghost;
            public int Speed, Focus, Luck, Stamina, Temperament;
            public float Progress, CurrentSpeed, BurstTimer, StunTimer, FatigueLevel;
            public string StatusText;
            public Color StatusColor;
            public bool Finished;
            public float FinishTime;
            public int FinalRank;
            public Color Color;
            public string Emoji;
            public Vector2 TrackPos;
        }

        private List<GhostRacer> _racers = new();
        private int _selectedRacerIndex = -1;
        private int _betAmount = 100;
        private float _raceTimer;
        private float _raceSpeed = 1f;
        private int _raceWinnerIndex = -1;
        private int _winStreak;
        private List<string> _raceCommentary = new();
        private Vector2 _raceCommentScroll;
        private Vector2 _raceDetailScroll;
        private int _raceLastCommentCount;
        private float _raceLastEventTime;
        private bool _raceFinished;
        private System.Random _raceRng = new();

        private Rect _trackRect;
        private float _trackCenterX, _trackCenterY;
        private float _trackOuterRX, _trackOuterRY;
        private float _trackInnerRX, _trackInnerRY;

        private static readonly string[] RacerEmojis = { "🐎", "🦄", "🐲", "🦅", "🐺" };
        private static readonly Color[] RacerColors = {
            new(1f, 0.4f, 0.4f), new(0.4f, 0.6f, 1f), new(0.4f, 0.9f, 0.4f),
            new(1f, 0.85f, 0.3f), new(0.8f, 0.5f, 1f)
        };

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 진입
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawGhostRaceTab(UserData user)
        {
            if (_raceState == RaceState.Racing)
            {
                float dt = Mathf.Clamp(Time.deltaTime, 0f, 0.1f) * _raceSpeed;
                _raceTimer += dt;
                TickRace(user, dt);
                Repaint();
            }

            DrawRaceCloseButton();

            switch (_raceState)
            {
                case RaceState.Idle: DrawRaceIdle(user); break;
                case RaceState.Picking: DrawRacePicking(user); break;
                case RaceState.Racing: DrawRaceRacing(user); break;
                case RaceState.Result: DrawRaceResult(user); break;
            }
        }

        private void DrawRaceCloseButton()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.5f, 0.1f, 0.1f);
            if (GUILayout.Button("✕ 닫기", GUILayout.Width(70), GUILayout.Height(22)))
            {
                _showGhostRaceTab = false;
                EditorPrefs.SetBool(PREF_SHOW_GHOSTRACE_TAB, false);
                _currentTab = Tab.Shop;
                _raceState = RaceState.Idle;
                _racers.Clear();
            }
            GUI.backgroundColor = old;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [1] 대기
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawRaceIdle(UserData user)
        {
            DrawBox(_styleBoxBlue, "🏇 고스트 레이스", () =>
            {
                var ts = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
                ts.normal.textColor = _colYellow;
                EditorGUILayout.LabelField("🔮 Firebase 고스트들의 타원 트랙 경주!", ts);
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField($"💰 보유 골드: {user._gold:N0}G", EditorStyles.boldLabel);

                if (_winStreak > 0)
                {
                    var ss = new GUIStyle(EditorStyles.boldLabel);
                    ss.normal.textColor = _colOrange;
                    EditorGUILayout.LabelField($"🔥 {_winStreak}연승 중! ×{GetStreakMultiplier():F1}", ss);
                }

                EditorGUILayout.Space(4);
                var info = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, normal = { textColor = Color.gray } };
                EditorGUILayout.LabelField("• 5마리가 타원형 트랙 1바퀴를 달립니다", info);
                EditorGUILayout.LabelField("• 능력치: ⚡속도 🎯집중 🍀운 💪지구 🧘기질", info);
                EditorGUILayout.LabelField("• 강한 말=낮은 배당, 약한 말=높은 배당 (하우스 엣지 10%)", info);
                EditorGUILayout.LabelField("• 3연승 ×1.5 / 5연승 ×1.8 보너스", info); // ★ 개선: 5연승 배당 변경 반영
                EditorGUILayout.Space(8);

                var old = GUI.backgroundColor;
                GUI.backgroundColor = user._gold >= 50 ? new Color(0.2f, 0.6f, 0.2f) : Color.gray;
                GUI.enabled = user._gold >= 50;
                if (GUILayout.Button("🎯 출전마 확인하기", GUILayout.Height(40)))
                {
                    _racers.Clear();
                    GenerateRacers(user);
                    _raceState = RaceState.Picking;
                }
                GUI.enabled = true;
                GUI.backgroundColor = old;
            });
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [2] 선택/베팅
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawRacePicking(UserData user)
        {
            DrawBox(_styleBoxBlue, "🏇 출전마 선택", () =>
            {
                for (int i = 0; i < _racers.Count; i++)
                    DrawRacerCard(i, _racers[i]);

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField($"💰 보유: {user._gold:N0}G", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("배팅", GUILayout.Width(30));
                _betAmount = EditorGUILayout.IntSlider(_betAmount, 50, Mathf.Min(10000, user._gold));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("100", EditorStyles.miniButtonLeft, GUILayout.Width(44))) _betAmount = Mathf.Min(100, user._gold);
                if (GUILayout.Button("500", EditorStyles.miniButtonMid, GUILayout.Width(44))) _betAmount = Mathf.Min(500, user._gold);
                if (GUILayout.Button("1000", EditorStyles.miniButtonMid, GUILayout.Width(44))) _betAmount = Mathf.Min(1000, user._gold);
                if (GUILayout.Button("MAX", EditorStyles.miniButtonRight, GUILayout.Width(44))) _betAmount = Mathf.Min(10000, user._gold);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(8);
                bool canStart = _selectedRacerIndex >= 0 && user._gold >= _betAmount && _betAmount > 0;
                var old = GUI.backgroundColor;
                GUI.backgroundColor = canStart ? new Color(0.9f, 0.5f, 0.1f) : Color.gray;
                GUI.enabled = canStart;
                string btn = _selectedRacerIndex < 0 ? "👆 말을 선택하세요" : $"🏁 출발! ({_betAmount:N0}G → {_racers[_selectedRacerIndex].Ghost._nick})";
                if (GUILayout.Button(btn, GUILayout.Height(42)))
                {
                    user._gold -= _betAmount;
                    Save(user);
                    StartRace();
                }
                GUI.enabled = true;
                GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
                if (GUILayout.Button("← 뒤로", GUILayout.Height(22))) { _raceState = RaceState.Idle; _racers.Clear(); }
                GUI.backgroundColor = old;
            });
        }

        private void DrawRacerCard(int idx, GhostRacer r)
        {
            bool sel = idx == _selectedRacerIndex;
            float odds = CalculateOdds(r);
            float win = EstimateWinRate(r);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            var old = GUI.backgroundColor;
            GUI.backgroundColor = sel ? new Color(0.3f, 0.7f, 0.3f) : new Color(0.25f, 0.25f, 0.3f);
            if (GUILayout.Button(sel ? $"✅ {r.Emoji} {r.Ghost._nick}" : $"{r.Emoji} {r.Ghost._nick}", GUILayout.Width(145), GUILayout.Height(32)))
                _selectedRacerIndex = sel ? -1 : idx;
            GUI.backgroundColor = old;

            var os = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            os.normal.textColor = odds < 2f ? _colRed : odds < 3.5f ? _colOrange : odds < 6f ? _colYellow : _colGreen;
            EditorGUILayout.LabelField($"×{odds:F1}", os, GUILayout.Width(50));

            var ws = new GUIStyle(EditorStyles.miniLabel);
            ws.normal.textColor = new Color(0.7f, 0.7f, 0.9f);
            EditorGUILayout.LabelField($"{win:F0}%", ws, GUILayout.Width(35));

            if (sel)
            {
                var ps = new GUIStyle(EditorStyles.boldLabel);
                ps.normal.textColor = _colYellow;
                EditorGUILayout.LabelField($"+{Mathf.RoundToInt(_betAmount * odds):N0}G", ps);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawStatBar("⚡", r.Speed, new Color(1f, 0.7f, 0.2f));
            DrawStatBar("🎯", r.Focus, new Color(0.3f, 0.7f, 1f));
            DrawStatBar("🍀", r.Luck, new Color(0.3f, 1f, 0.3f));
            DrawStatBar("💪", r.Stamina, new Color(1f, 0.5f, 0.8f));
            DrawStatBar("🧘", r.Temperament, new Color(0.7f, 0.5f, 1f));
            EditorGUILayout.EndHorizontal();

            int total = r.Speed + r.Focus + r.Luck + r.Stamina + r.Temperament;
            var ts = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
            EditorGUILayout.LabelField($"  총합 {total}/50 | {GetRacerComment(r)}", ts);
            EditorGUILayout.EndVertical();
        }

        private void DrawStatBar(string icon, int val, Color col)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(70));
            var ls = new GUIStyle(EditorStyles.miniLabel) { fontSize = 9, alignment = TextAnchor.MiddleCenter };
            ls.normal.textColor = Color.white;
            EditorGUILayout.LabelField($"{icon}{val}", ls);
            var r = EditorGUILayout.GetControlRect(false, 6);
            EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.15f));
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * (val / 10f), r.height), col);
            EditorGUILayout.EndVertical();
        }

        private string GetRacerComment(GhostRacer r)
        {
            if (r.Speed >= 8 && r.Stamina >= 7) return "🏆 우승 후보";
            if (r.Speed >= 8) return "⚡ 스피드형";
            if (r.Stamina >= 8) return "💪 후반형";
            if (r.Luck >= 8) return "🍀 운빨형";
            if (r.Focus >= 8) return "🎯 안정형";
            if (r.Speed <= 3 && r.Stamina <= 3) return "😰 약체(고배당)";
            int t = r.Speed + r.Focus + r.Luck + r.Stamina + r.Temperament;
            return t >= 38 ? "⭐ 상위" : t >= 28 ? "📊 중위" : "📉 하위";
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [3] 경주
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawRaceRacing(UserData user)
        {
            EditorGUILayout.BeginVertical(_styleBoxBlue);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"🏇 레이스! ⏱{_raceTimer:F1}초", EditorStyles.boldLabel, GUILayout.Width(160));

            // ★ 개선 3: 평균 진행률 -> 선두 진행률
            float maxProgress = _racers.Count > 0 ? _racers.Max(r => r.Progress) : 0f;
            EditorGUILayout.LabelField($"선두 {maxProgress:F0}%", EditorStyles.boldLabel, GUILayout.Width(80));

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("속도", GUILayout.Width(30));
            _raceSpeed = EditorGUILayout.Slider(_raceSpeed, 0.25f, 3f, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            DrawOvalRaceTrack();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);

            DrawRaceLiveStatus();
            EditorGUILayout.Space(4);
            DrawRaceRankings();
            DrawRaceCommentary();
        }

        private void DrawOvalRaceTrack()
        {
            float w = Mathf.Max(440f, EditorGUIUtility.currentViewWidth - 60f);
            float h = w * 0.4f;

            // 레이아웃 영역 확보 (Repaint 조건 밖에 있어야 함)
            Rect area = GUILayoutUtility.GetRect(w, h);

            _trackCenterX = area.x + area.width * 0.5f;
            _trackCenterY = area.y + area.height * 0.5f;
            _trackOuterRX = area.width * 0.5f - 12f;
            _trackOuterRY = area.height * 0.5f - 12f;
            _trackInnerRX = _trackOuterRX - 60f;
            _trackInnerRY = _trackOuterRY - 24f;
            _trackRect = area;

            // ★ 개선 7: 무거운 그리기 연산은 Repaint 이벤트일 때만 실행
            if (Event.current.type == EventType.Repaint)
            {
                // 잔디
                EditorGUI.DrawRect(area, new Color(0.15f, 0.30f, 0.12f));

                // 트랙
                int seg = 100;
                Vector3[] outer = new Vector3[seg + 1];
                Vector3[] inner = new Vector3[seg + 1];
                for (int s = 0; s <= seg; s++)
                {
                    float a = (s / (float)seg) * Mathf.PI * 2f;
                    outer[s] = new Vector3(_trackCenterX + Mathf.Cos(a) * _trackOuterRX, _trackCenterY + Mathf.Sin(a) * _trackOuterRY, 0);
                    inner[s] = new Vector3(_trackCenterX + Mathf.Cos(a) * _trackInnerRX, _trackCenterY + Mathf.Sin(a) * _trackInnerRY, 0);
                }

                Handles.color = new Color(0.35f, 0.28f, 0.2f);
                for (int s = 0; s < seg; s++)
                    Handles.DrawAAPolyLine(2f, new[] { outer[s], inner[s] });

                Handles.color = new Color(1f, 0.95f, 0.8f, 0.5f);
                Handles.DrawAAPolyLine(2.5f, outer);
                Handles.DrawAAPolyLine(2.5f, inner);

                // 레인
                Handles.color = new Color(1f, 1f, 1f, 0.06f);
                for (int lane = 1; lane < 5; lane++)
                {
                    float t = lane / 5f;
                    float rx = Mathf.Lerp(_trackOuterRX - 5f, _trackInnerRX + 5f, t);
                    float ry = Mathf.Lerp(_trackOuterRY - 5f, _trackInnerRY + 5f, t);
                    Vector3[] lp = new Vector3[seg + 1];
                    for (int s = 0; s <= seg; s++)
                    {
                        float a = (s / (float)seg) * Mathf.PI * 2f;
                        lp[s] = new Vector3(_trackCenterX + Mathf.Cos(a) * rx, _trackCenterY + Mathf.Sin(a) * ry, 0);
                    }
                    Handles.DrawAAPolyLine(1f, lp);
                }

                // 결승선
                float sa = Mathf.PI * 1.5f;
                Vector3 so = new Vector3(_trackCenterX + Mathf.Cos(sa) * _trackOuterRX, _trackCenterY + Mathf.Sin(sa) * _trackOuterRY, 0);
                Vector3 si = new Vector3(_trackCenterX + Mathf.Cos(sa) * _trackInnerRX, _trackCenterY + Mathf.Sin(sa) * _trackInnerRY, 0);
                for (int c = 0; c < 8; c++)
                {
                    Handles.color = c % 2 == 0 ? Color.white : Color.black;
                    Handles.DrawAAPolyLine(4f, new[] { Vector3.Lerp(si, so, c / 8f), Vector3.Lerp(si, so, (c + 1) / 8f) });
                }
                var fs = new GUIStyle(EditorStyles.boldLabel) { fontSize = 10 };
                fs.normal.textColor = _colYellow;
                EditorGUI.LabelField(new Rect(so.x - 25, so.y - 18, 65, 16), "🏁 FINISH", fs);

                // 중앙
                EditorGUI.DrawRect(new Rect(_trackCenterX - 40, _trackCenterY - 10, 80, 20), new Color(0.12f, 0.25f, 0.1f));
                var cs = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
                cs.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
                EditorGUI.LabelField(new Rect(_trackCenterX - 40, _trackCenterY - 9, 80, 18), "GHOST", cs);

                // 말
                for (int i = 0; i < _racers.Count; i++)
                {
                    var r = _racers[i];
                    var p = GetTrackPoint(r.Progress, i, _racers.Count);
                    r.TrackPos = p;
                    bool mine = i == _selectedRacerIndex;

                    string emoji = r.Finished ? "🏁" : r.StunTimer > 0 ? "😵" : r.BurstTimer > 0 ? "💨" : r.FatigueLevel > 0.6f ? "😰" : r.Emoji;

                    if (mine)
                    {
                        EditorGUI.DrawRect(new Rect(p.x - 14, p.y - 14, 28, 28), new Color(1f, 1f, 0f, 0.2f));
                    }

                    var es = new GUIStyle(EditorStyles.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                    EditorGUI.LabelField(new Rect(p.x - 12, p.y - 12, 24, 24), emoji, es);

                    var ns = new GUIStyle(EditorStyles.boldLabel) { fontSize = 9, alignment = TextAnchor.MiddleCenter };
                    ns.normal.textColor = mine ? _colYellow : new Color(r.Color.r, r.Color.g, r.Color.b, 0.9f);
                    EditorGUI.LabelField(new Rect(p.x - 40, p.y - 26, 80, 12), $"{r.Ghost._nick} {r.Progress:F0}%", ns);
                }

                // 선두
                var lead = _racers.OrderByDescending(r => r.Progress).First();
                var ls2 = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
                ls2.normal.textColor = _colGreen;
                EditorGUI.LabelField(new Rect(_trackCenterX - 100, area.yMax + 2, 200, 16), $"🏆 선두: {lead.Emoji} {lead.Ghost._nick}", ls2);
            }
        }

        private Vector2 GetTrackPoint(float progress, int idx, int total)
        {
            float t = total > 1 ? (float)idx / (total - 1) : 0.5f;
            float rx = Mathf.Lerp(_trackOuterRX - 8f, _trackInnerRX + 8f, t);
            float ry = Mathf.Lerp(_trackOuterRY - 8f, _trackInnerRY + 8f, t);
            float a = Mathf.PI * 1.5f + (progress / 100f) * Mathf.PI * 2f;
            return new Vector2(_trackCenterX + Mathf.Cos(a) * rx, _trackCenterY + Mathf.Sin(a) * ry);
        }

        private void DrawRaceLiveStatus()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 말 상태", EditorStyles.boldLabel);
            var sorted = _racers.OrderByDescending(r => r.Progress).ToList();
            foreach (var r in sorted)
            {
                bool mine = _racers.IndexOf(r) == _selectedRacerIndex;
                EditorGUILayout.BeginHorizontal();
                var ns = new GUIStyle(EditorStyles.miniLabel) { fontSize = 10, normal = { textColor = mine ? _colYellow : r.Color } };
                string rank = r.Finished ? $"🏁{r.FinalRank}" : r.Emoji;
                EditorGUILayout.LabelField($"{rank} {r.Ghost._nick}", ns, GUILayout.Width(90));

                var ss = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold, normal = { textColor = r.StatusColor } };
                EditorGUILayout.LabelField(r.StatusText, ss, GUILayout.Width(40));
                EditorGUILayout.LabelField($"속도{r.CurrentSpeed:F1}", EditorStyles.miniLabel, GUILayout.Width(50));
                EditorGUILayout.LabelField($"{r.Progress:F0}%", EditorStyles.miniLabel, GUILayout.Width(35));

                // ★ 개선 4: 피로도 막대 + 텍스트 표시
                var fr = EditorGUILayout.GetControlRect(false, 8, GUILayout.Width(40));
                EditorGUI.DrawRect(fr, new Color(0.15f, 0.15f, 0.15f));
                EditorGUI.DrawRect(new Rect(fr.x, fr.y, fr.width * r.FatigueLevel, fr.height),
                    r.FatigueLevel > 0.7f ? _colRed : r.FatigueLevel > 0.4f ? _colYellow : _colGreen);

                var fatStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 8, alignment = TextAnchor.MiddleCenter };
                fatStyle.normal.textColor = Color.white;
                EditorGUI.LabelField(fr, $"{r.FatigueLevel * 100:F0}%", fatStyle);

                string eff = "";
                if (r.BurstTimer > 0) eff += $"💨{r.BurstTimer:F1} ";
                if (r.StunTimer > 0) eff += $"😵{r.StunTimer:F1} ";
                if (string.IsNullOrEmpty(eff)) eff = "—";
                EditorGUILayout.LabelField(eff, EditorStyles.miniLabel, GUILayout.Width(60));

                EditorGUILayout.LabelField(r.Finished ? "GOAL" : $"{100 - r.Progress:F0}m", EditorStyles.miniLabel, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawRaceRankings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var sorted = _racers.OrderByDescending(r => r.Progress).ToList();
            string[] m = { "🥇", "🥈", "🥉", "4️⃣", "5️⃣" };
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < sorted.Count; i++)
            {
                bool mine = _racers.IndexOf(sorted[i]) == _selectedRacerIndex;
                var s = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = mine ? _colYellow : Color.white } };
                EditorGUILayout.LabelField($"{m[i]}{sorted[i].Emoji}{sorted[i].Ghost._nick}", s, GUILayout.Width(120));
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawRaceCommentary()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("📣 실황", EditorStyles.boldLabel);

            bool newAdded = _raceCommentary.Count > _raceLastCommentCount;
            _raceLastCommentCount = _raceCommentary.Count;

            if (newAdded) _raceCommentScroll.y = 999999f;

            _raceCommentScroll = EditorGUILayout.BeginScrollView(_raceCommentScroll, EditorStyles.helpBox, GUILayout.Height(80));
            var st = new GUIStyle(EditorStyles.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.9f, 0.9f, 0.8f) } };
            EditorGUILayout.LabelField(string.Join("\n", _raceCommentary), st, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (newAdded) GUI.changed = true; // ★ 개선 8: 스크롤 강제 갱신
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [4] 결과
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawRaceResult(UserData user)
        {
            // ★ 개선 1: 빈 리스트 및 인덱스 방어
            if (_racers == null || _racers.Count == 0 || _selectedRacerIndex < 0 || _selectedRacerIndex >= _racers.Count)
            {
                _raceState = RaceState.Idle;
                return;
            }

            bool won = _raceWinnerIndex == _selectedRacerIndex;
            float odds = _selectedRacerIndex >= 0 ? CalculateOdds(_racers[_selectedRacerIndex]) : 1f;
            float sm = GetStreakMultiplier();
            int reward = Mathf.RoundToInt(_betAmount * odds * sm);

            DrawBox(won ? _styleBoxGreen : _styleBoxRed, won ? "🎉 적중!" : "💸 빗나감...", () =>
            {
                var ts = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                ts.normal.textColor = won ? _colGreen : _colRed;
                EditorGUILayout.LabelField(won ? $"🏆 +{reward:N0}G!" : $"💀 -{_betAmount:N0}G", ts);
                EditorGUILayout.Space(6);

                var sorted = _racers.OrderBy(r => r.FinalRank).ToList();
                string[] m = { "🥇", "🥈", "🥉", "4️⃣", "5️⃣" };
                for (int i = 0; i < sorted.Count; i++)
                {
                    bool mine = _racers.IndexOf(sorted[i]) == _selectedRacerIndex;
                    bool isW = _racers.IndexOf(sorted[i]) == _raceWinnerIndex;
                    var s = new GUIStyle(EditorStyles.label) { fontSize = 12 };
                    s.normal.textColor = isW ? _colGreen : mine ? _colYellow : Color.gray;
                    string mark = mine ? " ← 내 선택" : "";
                    EditorGUILayout.LabelField($"  {m[i]} {sorted[i].Emoji} {sorted[i].Ghost._nick} ({sorted[i].FinishTime:F1}초, ×{CalculateOdds(sorted[i]):F1}){mark}", s);
                }

                if (won)
                {
                    EditorGUILayout.LabelField($"배당 ×{odds:F1} × 연승 ×{sm:F1} = ×{odds * sm:F1}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"🔥 연승: {_winStreak}회", EditorStyles.boldLabel);
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"💰 골드: {user._gold:N0}G", EditorStyles.boldLabel);
                EditorGUILayout.Space(8);

                var old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.7f, 0.2f);
                if (GUILayout.Button("🔄 다음 레이스", GUILayout.Height(36))) { _racers.Clear(); GenerateRacers(user); _raceState = RaceState.Picking; }
                GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
                // ★ 개선 2: 처음으로 누를 때 선택 인덱스도 초기화
                if (GUILayout.Button("🏠 처음으로", GUILayout.Height(24))) { _raceState = RaceState.Idle; _racers.Clear(); _selectedRacerIndex = -1; }
                GUI.backgroundColor = old;
            });
            DrawRaceCommentary();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 로직
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void GenerateRacers(UserData user)
        {
            if (_racers.Count > 0) return;
            _selectedRacerIndex = -1;
            _raceRng = new System.Random();

            var pool = new List<GhostEntry>();
            if (_survGhostPool != null) pool.AddRange(_survGhostPool);
            while (pool.Count < 5) pool.Add(new GhostEntry { _nick = $"유령{pool.Count + 1}호", _score = 200 + _raceRng.Next(100, 1800) });
            for (int i = pool.Count - 1; i > 0; i--) { int j = _raceRng.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }

            for (int i = 0; i < 5; i++)
            {
                var g = pool[i];
                int sc = Mathf.Max(100, g._score);
                _racers.Add(new GhostRacer
                {
                    Ghost = g,
                    Speed = Mathf.Clamp(2 + sc / 350 + _raceRng.Next(-1, 2), 1, 10),
                    Focus = Mathf.Clamp(2 + _raceRng.Next(0, 7), 1, 10),
                    Luck = Mathf.Clamp(1 + _raceRng.Next(0, 8), 1, 10),
                    Stamina = Mathf.Clamp(2 + sc / 500 + _raceRng.Next(-1, 3), 1, 10),
                    Temperament = Mathf.Clamp(2 + _raceRng.Next(0, 6), 1, 10),
                    Color = RacerColors[i],
                    Emoji = RacerEmojis[i],
                    StatusText = "대기",
                    StatusColor = Color.gray
                });
            }
        }

        // ★ 개선 5: 기질(Temperament) 가중치 1.0 -> 1.5 상향
        private float GetPowerScore(GhostRacer r) => r.Speed * 3f + r.Focus * 1.5f + r.Luck + r.Stamina * 2.5f + r.Temperament * 1.5f;

        private float CalculateOdds(GhostRacer r)
        {
            float total = _racers.Sum(h => GetPowerScore(h));
            if (total <= 0) return 5f;
            float prob = GetPowerScore(r) / total;
            float odds = 0.90f / Mathf.Max(prob, 0.03f);
            return Mathf.Max(1.1f, Mathf.Round(Mathf.Clamp(odds, 1.1f, 15f) * 10f) / 10f);
        }

        private float EstimateWinRate(GhostRacer r)
        {
            float total = _racers.Sum(h => GetPowerScore(h));
            return total > 0 ? Mathf.Clamp(GetPowerScore(r) / total * 100f, 2f, 70f) : 20f;
        }

        // ★ 개선 6: 5연승 보너스 2.0 -> 1.8로 하향 (인플레이션 방지)
        private float GetStreakMultiplier() => _winStreak >= 5 ? 1.8f : _winStreak >= 3 ? 1.5f : 1.0f;

        private void StartRace()
        {
            _raceTimer = 0f;
            _raceWinnerIndex = -1;
            _raceLastEventTime = 0f;
            _raceFinished = false;
            _raceCommentary = new List<string> { "🏁 레이스 시작!" };
            _raceLastCommentCount = 0;

            foreach (var r in _racers)
            {
                r.Progress = 0; r.CurrentSpeed = 0; r.BurstTimer = 0; r.StunTimer = 0;
                r.FatigueLevel = 0; r.Finished = false; r.FinishTime = 0; r.FinalRank = 0;
                r.StatusText = "질주"; r.StatusColor = _colGreen;
            }
            _raceState = RaceState.Racing;
        }

        private void TickRace(UserData user, float dt)
        {
            if (_raceFinished) return;

            int finishCount = 0;
            for (int i = 0; i < _racers.Count; i++)
            {
                var r = _racers[i];
                if (r.Finished) { finishCount++; continue; }
                if (r.StunTimer > 0) { r.StunTimer -= dt; r.CurrentSpeed = 0; r.StatusText = "기절"; r.StatusColor = _colRed; continue; }

                float fatRate = 0.015f + (r.Progress / 100f) * 0.04f;
                fatRate *= (1f - r.Stamina * 0.06f);
                r.FatigueLevel = Mathf.Clamp01(r.FatigueLevel + fatRate * dt);

                float baseSpd = r.Speed * 1.4f;
                float noise = (float)(_raceRng.NextDouble() - 0.5) * (10 - r.Focus) * 0.5f;
                float fatPen = r.FatigueLevel > 0.5f ? 1f - (r.FatigueLevel - 0.5f) * 1.1f : 1f;
                fatPen = Mathf.Max(0.45f, fatPen);

                float spd = (baseSpd + noise) * fatPen;
                if (r.BurstTimer > 0) { spd *= 1.6f; r.BurstTimer -= dt; }

                r.CurrentSpeed = Mathf.Max(0, spd);
                r.Progress = Mathf.Min(100f, r.Progress + r.CurrentSpeed * dt);

                r.StatusText = r.BurstTimer > 0 ? "가속" : r.FatigueLevel > 0.7f ? "피로" : r.CurrentSpeed > baseSpd * 0.9f ? "질주" : "보통";
                r.StatusColor = r.BurstTimer > 0 ? new Color(0.3f, 1f, 1f) : r.FatigueLevel > 0.7f ? _colOrange : r.CurrentSpeed > baseSpd * 0.9f ? _colGreen : Color.white;

                if (r.Progress >= 100f && !r.Finished)
                {
                    r.Finished = true; r.FinishTime = _raceTimer; finishCount++;
                    r.FinalRank = finishCount; r.StatusText = "골인"; r.StatusColor = _colYellow; r.CurrentSpeed = 0;
                    _raceCommentary.Add(_raceWinnerIndex < 0 ? $"🏆 {r.Emoji} {r.Ghost._nick} 1등! ({_raceTimer:F1}초)" : $"  {r.Emoji} {r.Ghost._nick} {r.FinalRank}등");
                    if (_raceWinnerIndex < 0) _raceWinnerIndex = i;
                }
            }

            if (_raceTimer - _raceLastEventTime > 2.5f && !_raceFinished) { _raceLastEventTime = _raceTimer; TriggerRaceEvent(); }
            if (finishCount >= _racers.Count) { _raceFinished = true; SettleRace(user); }
        }

        private void TriggerRaceEvent()
        {
            var active = _racers.Where(r => !r.Finished && r.StunTimer <= 0).ToList();
            if (active.Count == 0) return;
            var r = active[_raceRng.Next(active.Count)];
            int roll = _raceRng.Next(100);
            float luck = r.Luck / 10f;
            float temp = r.Temperament / 10f;

            if (roll < 12) { r.BurstTimer = 2.5f; _raceCommentary.Add($"  💨 {r.Emoji}{r.Ghost._nick} 스퍼트!"); }
            else if (roll < 25) { if (_raceRng.NextDouble() > temp * 0.6f + luck * 0.3f) { r.StunTimer = 1.8f; _raceCommentary.Add($"  😵 {r.Emoji}{r.Ghost._nick} 넘어짐!"); } else { r.BurstTimer = 1.2f; _raceCommentary.Add($"  🧘 {r.Emoji}{r.Ghost._nick} 균형→반격!"); } }
            else if (roll < 35) { float bp = 1.3f + luck * 1.8f; r.BurstTimer = bp; _raceCommentary.Add($"  🍀 {r.Emoji}{r.Ghost._nick} 행운!({bp:F1}초)"); }
            else if (roll < 45) { r.BurstTimer = 1f; r.FatigueLevel = Mathf.Max(0, r.FatigueLevel - 0.15f); _raceCommentary.Add($"  👻 {r.Emoji}{r.Ghost._nick} 유령 통과!"); }
            else if (roll < 55 && r.Stamina >= 7 && r.Progress > 50f) { r.BurstTimer = 2f; r.FatigueLevel = Mathf.Max(0, r.FatigueLevel - 0.2f); _raceCommentary.Add($"  💪 {r.Emoji}{r.Ghost._nick} 지구력 폭발!"); }
            else if (roll < 62) { _raceCommentary.Add("  🌪 돌풍!"); foreach (var h in _racers.Where(h => !h.Finished)) if (_raceRng.NextDouble() > h.Temperament / 12f) h.StunTimer = Mathf.Max(h.StunTimer, 0.45f); }
            if (_raceCommentary.Count > 25) _raceCommentary.RemoveAt(0);
        }

        private void SettleRace(UserData user)
        {
            bool won = _selectedRacerIndex == _raceWinnerIndex;
            if (won) { float odds = CalculateOdds(_racers[_selectedRacerIndex]); float sm = GetStreakMultiplier(); int rw = Mathf.RoundToInt(_betAmount * odds * sm); user._gold += rw; _winStreak++; _raceCommentary.Add($"\n🎉 적중! +{rw:N0}G (×{odds:F1}×{sm:F1})"); }
            else { _winStreak = 0; _raceCommentary.Add($"\n💸 빗나감 -{_betAmount:N0}G"); }
            Save(user);
            _raceState = RaceState.Result;
        }
    }
}