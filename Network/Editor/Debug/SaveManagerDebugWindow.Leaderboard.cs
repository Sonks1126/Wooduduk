using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wooduduk.Data.Result;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase.Leaderboard;

namespace Wooduduk.Editor
{
    public partial class SaveManagerDebugWindow
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 리더보드 탭 상태
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private List<RankingData> _lbEntries = new List<RankingData>();
        private bool _lbLoading = false;
        private bool _lbLoaded = false;
        private string _lbStatusMsg = "🔘 아직 로드하지 않았습니다.";
        private Vector2 _lbScrollPos;

        // 내 순위
        private int _lbMyRank = -1;
        private int _lbMyScore = 0;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 탭 진입점
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawLeaderboardTab(UserData user)
        {
            DrawLeaderboardControlPanel(user);
            EditorGUILayout.Space(4);

            if (!_lbLoaded)
            {
                EditorGUILayout.HelpBox(
                    "🔄 [Top 100 로드] 버튼을 눌러 리더보드를 불러오세요.",
                    MessageType.Info);
                return;
            }

            if (_lbEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("리더보드에 데이터가 없습니다.", MessageType.Warning);
                return;
            }

            DrawLeaderboardTable(user);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 상단 컨트롤 패널
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawLeaderboardControlPanel(UserData user)
        {
            DrawBox(_styleBoxBlue, "🏆 리더보드", () =>
            {
                // ── 내 순위 정보 ──
                EditorGUILayout.BeginHorizontal();

                var myRankStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                myRankStyle.normal.textColor = _lbMyRank > 0 ? _colGreen : Color.gray;

                EditorGUILayout.LabelField(
                    _lbMyRank > 0
                        ? $"📍 내 순위:  {_lbMyRank}위     최고 점수: {_lbMyScore:N0}"
                        : "📍 내 순위:  미집계",
                    myRankStyle);

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(6);

                // ── 버튼 행 ──
                EditorGUILayout.BeginHorizontal();

                // Top 100 로드
                GUI.enabled = !_lbLoading;
                var old = GUI.backgroundColor;

                GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
                if (GUILayout.Button(
                    _lbLoading ? "⏳ 로드 중..." : "🔄  Top 100 로드",
                    GUILayout.Height(28), GUILayout.Width(130)))
                {
                    LoadTop100(user);
                }

                // 내 순위 조회
                GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
                if (GUILayout.Button("📍  내 순위 조회",
                    GUILayout.Height(28), GUILayout.Width(120)))
                {
                    LoadMyRank(user);
                }

                // 서비스 통합 로드 (LeaderboardService 사용)
                GUI.backgroundColor = new Color(0.7f, 0.4f, 1f);
                if (GUILayout.Button("📦  통합 로드 (Service)",
                    GUILayout.Height(28), GUILayout.Width(150)))
                {
                    LoadViaService(user);
                }

                // 캐시 초기화
                GUI.backgroundColor = new Color(1f, 0.45f, 0.3f);
                if (GUILayout.Button("🗑️  캐시 초기화",
                    GUILayout.Height(28), GUILayout.Width(110)))
                {
                    LeaderboardCache.Clear();
                    _lbEntries.Clear();
                    _lbLoaded = false;
                    _lbMyRank = -1;
                    _lbMyScore = 0;
                    _lbStatusMsg = "🗑️ 캐시 초기화 완료. 다시 로드하세요.";
                    SetStatus("리더보드 캐시 초기화", MessageType.Info);
                }

                GUI.backgroundColor = old;
                GUI.enabled = true;

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // ── 상태 메시지 ──
                var statusStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 12,
                    wordWrap = true
                };
                statusStyle.normal.textColor = _lbLoading ? _colYellow : _colGreen;
                EditorGUILayout.LabelField(_lbStatusMsg, statusStyle);
            });
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 리더보드 테이블
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawLeaderboardTable(UserData user)
        {
            DrawBox(_styleBoxPurple, $"🏅  Top {_lbEntries.Count} 랭킹", () =>
            {
                // 헤더
                DrawLbTableHeader();

                // 구분선
                var line = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(line,
                    new Color(_colPurple.r, _colPurple.g, _colPurple.b, 0.5f));
                EditorGUILayout.Space(2);

                // 스크롤 목록
                _lbScrollPos = EditorGUILayout.BeginScrollView(_lbScrollPos);

                for (int i = 0; i < _lbEntries.Count; i++)
                    DrawLbTableRow(i + 1, _lbEntries[i], user);

                EditorGUILayout.EndScrollView();
            });
        }

        private void DrawLbTableHeader()
        {
            EditorGUILayout.BeginHorizontal();

            LbLabel("순위", 55, TextAnchor.MiddleCenter, _colPurple, true);
            LbLabel("닉네임", 160, TextAnchor.MiddleLeft, _colPurple, true);
            LbLabel("점수", 110, TextAnchor.MiddleRight, _colPurple, true);
            LbLabel("UserID", 160, TextAnchor.MiddleLeft, _colPurple, true);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLbTableRow(int rank, RankingData entry, UserData currentUser)
        {
            if (entry == null) return;

            bool isMe = currentUser != null && entry._userId == currentUser._userId;
            bool isTop3 = rank <= 3;

            // 내 항목 배경 강조
            if (isMe)
            {
                var bgRect = EditorGUILayout.BeginVertical();
                EditorGUI.DrawRect(bgRect,
                    new Color(_colGreen.r, _colGreen.g, _colGreen.b, 0.1f));
            }

            EditorGUILayout.BeginHorizontal();

            // 순위 뱃지
            string badge = rank switch
            {
                1 => "🥇  1위",
                2 => "🥈  2위",
                3 => "🥉  3위",
                _ => $"    {rank}위"
            };

            Color rankColor = rank switch
            {
                1 => new Color(1f, 0.85f, 0.2f),
                2 => new Color(0.8f, 0.8f, 0.8f),
                3 => new Color(0.8f, 0.5f, 0.2f),
                _ => isMe ? _colGreen : new Color(0.75f, 0.75f, 0.75f)
            };

            LbLabel(badge, 55,
                TextAnchor.MiddleCenter, rankColor,
                bold: isTop3 || isMe, fontSize: isTop3 ? 13 : 12);

            // 닉네임 (내 항목이면 ★ 붙임)
            string nick = isMe
                ? $"★  {entry._nickname ?? "Unknown"}"
                : (entry._nickname ?? "Unknown");

            LbLabel(nick, 160,
                TextAnchor.MiddleLeft,
                isMe ? _colGreen : Color.white,
                bold: isMe);

            // 점수
            LbLabel($"{entry._score:N0}", 110,
                TextAnchor.MiddleRight,
                isTop3 ? _colYellow : Color.white,
                bold: isTop3);

            // UserID (짧게)
            string shortId = !string.IsNullOrEmpty(entry._userId) && entry._userId.Length > 14
                ? entry._userId.Substring(0, 14) + "…"
                : entry._userId ?? "-";

            LbLabel(shortId, 160,
                TextAnchor.MiddleLeft,
                new Color(0.55f, 0.55f, 0.55f),
                fontSize: 10);

            EditorGUILayout.EndHorizontal();

            if (isMe)
                EditorGUILayout.EndVertical();

            // 10위마다 얇은 구분선
            if (rank % 10 == 0 && rank < _lbEntries.Count)
            {
                var divRect = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(divRect, new Color(1f, 1f, 1f, 0.06f));
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Firebase 조회 로직
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void LoadTop100(UserData user)
        {
            if (_lbLoading) return;

            _lbLoading = true;
            _lbLoaded = false;
            _lbStatusMsg = "⏳ Top 100 로드 중...";
            Repaint();

            new LeaderboardRepository().LoadTop100(entries =>
            {
                _lbEntries = entries ?? new List<RankingData>();
                _lbLoaded = true;
                _lbLoading = false;
                _lbStatusMsg = $"✅  {_lbEntries.Count}명 로드 완료  ({System.DateTime.Now:HH:mm:ss})";

                // 내 항목 찾기
                if (user != null)
                {
                    int idx = _lbEntries.FindIndex(e => e._userId == user._userId);
                    if (idx >= 0)
                    {
                        _lbMyRank = _lbEntries[idx]._rank;
                        _lbMyScore = _lbEntries[idx]._score;
                    }
                }

                SetStatus($"🏆 리더보드 로드 완료: {_lbEntries.Count}명", MessageType.Info);
                Repaint();
            });
        }

        private void LoadMyRank(UserData user)
        {
            if (user == null)
            {
                SetStatus("❌ UserData가 없습니다.", MessageType.Error);
                return;
            }

            // ★ 이미 Top100이 로드되어 있으면 그 안에서 바로 찾기
            if (_lbLoaded && _lbEntries.Count > 0)
            {
                int idx = _lbEntries.FindIndex(e => e._userId == user._userId);

                if (idx >= 0)
                {
                    _lbMyRank = _lbEntries[idx]._rank;
                    _lbMyScore = _lbEntries[idx]._score;
                    _lbStatusMsg =
                        $"📍 내 순위: {_lbMyRank}위   점수: {_lbMyScore:N0}   " +
                        $"(Top100 기준 / {System.DateTime.Now:HH:mm:ss})";
                    SetStatus($"📍 내 순위: {_lbMyRank}위 / 점수: {_lbMyScore:N0}", MessageType.Info);
                }
                else
                {
                    _lbMyRank = -1;
                    _lbMyScore = 0;
                    _lbStatusMsg =
                        $"📍 Top 100 밖 (현재 로드된 목록에 없음) / " +
                        $"{System.DateTime.Now:HH:mm:ss}";
                    SetStatus("📍 Top 100 밖 — 정확한 순위는 user_ranks 캐시 참고", MessageType.Warning);
                }

                Repaint();
                return;
            }

            // ★ Top100이 없으면 실시간으로 Top100을 먼저 로드 후 찾기
            _lbStatusMsg = "⏳ 내 순위 조회 중 (Top100 실시간 로드)...";
            _lbLoading = true;
            Repaint();

            new LeaderboardRepository().LoadTop100(entries =>
            {
                _lbEntries = entries ?? new List<RankingData>();
                _lbLoaded = true;
                _lbLoading = false;

                int idx = _lbEntries.FindIndex(e => e._userId == user._userId);

                if (idx >= 0)
                {
                    _lbMyRank = _lbEntries[idx]._rank;
                    _lbMyScore = _lbEntries[idx]._score;
                    _lbStatusMsg =
                        $"📍 내 순위: {_lbMyRank}위   점수: {_lbMyScore:N0}   " +
                        $"({System.DateTime.Now:HH:mm:ss})";
                    SetStatus($"📍 내 순위: {_lbMyRank}위 / 점수: {_lbMyScore:N0}", MessageType.Info);
                }
                else
                {
                    // Top 100 밖이면 user_ranks 캐시로 보완
                    new LeaderboardRepository().LoadMyRank(user._userId, (rank, score) =>
                    {
                        _lbMyRank = rank;
                        _lbMyScore = score;
                        _lbStatusMsg = rank > 0
                            ? $"📍 내 순위: {rank}위 (캐시)   점수: {score:N0}   " +
                              $"({System.DateTime.Now:HH:mm:ss})"
                            : $"📍 순위 미집계 (Top100 밖)   ({System.DateTime.Now:HH:mm:ss})";
                        SetStatus(
                            rank > 0
                                ? $"📍 내 순위: {rank}위 (캐시) / 점수: {score:N0}"
                                : "📍 순위 미집계",
                            MessageType.Info);
                        Repaint();
                    });
                }

                Repaint();
            });
        }

        private void LoadViaService(UserData user)
        {
            if (user == null)
            {
                SetStatus("❌ UserData가 없습니다.", MessageType.Error);
                return;
            }

            _lbLoading = true;
            _lbStatusMsg = "⏳ LeaderboardService 통합 로드 중...";
            Repaint();

            // 디버그 창에서는 항상 최신 데이터를 보기 위해 캐시 무효화 후 로드
            LeaderboardCache.Clear();
            LeaderboardEvents.OnLoaded += OnLeaderboardServiceLoaded;
            LeaderboardService.Load(user);
        }

        private void OnLeaderboardServiceLoaded(LeaderboardViewData data)
        {
            // 이벤트 구독 해제 (한 번만 받기)
            LeaderboardEvents.OnLoaded -= OnLeaderboardServiceLoaded;

            _lbLoading = false;

            if (data == null)
            {
                _lbStatusMsg = "❌ 데이터 로드 실패";
                Repaint();
                return;
            }

            _lbEntries = data._top100 ?? new List<RankingData>();
            _lbLoaded = true;

            // 내 순위
            if (data._myRankRecord != null)
            {
                _lbMyRank = data._myRankRecord._rank;
                _lbMyScore = data._myRankRecord._score;
            }

            _lbStatusMsg =
                $"📦  통합 로드 완료  ({_lbEntries.Count}명)   ({System.DateTime.Now:HH:mm:ss})";

            SetStatus($"📦 LeaderboardService 로드 완료: {_lbEntries.Count}명", MessageType.Info);
            Repaint();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 테이블 셀 헬퍼
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void LbLabel(
            string text,
            float width,
            TextAnchor anchor = TextAnchor.MiddleLeft,
            Color? color = null,
            bool bold = false,
            int fontSize = 12)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                fontSize = fontSize,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                alignment = anchor
            };
            style.normal.textColor = color ?? Color.white;

            EditorGUILayout.LabelField(text, style, GUILayout.Width(width));
        }
    }
}