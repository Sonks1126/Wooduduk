using UnityEditor;
using UnityEngine;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase;
using Wooduduk.Tier;

namespace Wooduduk.Editor
{
    public partial class SaveManagerDebugWindow
    {
        private void BuildStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _colGreen = new Color(0.2f, 1f, 0.4f);
            _colBlue = new Color(0.3f, 0.7f, 1f);
            _colYellow = new Color(1f, 0.9f, 0.2f);
            _colRed = new Color(1f, 0.4f, 0.4f);
            _colPurple = new Color(0.7f, 0.4f, 1f);
            _colOrange = new Color(1f, 0.65f, 0.2f);

            _styleBox = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(14, 14, 10, 12),
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            _styleBoxGreen = new GUIStyle(_styleBox);
            _styleBoxGreen.normal.textColor = _colGreen;

            _styleBoxRed = new GUIStyle(_styleBox);
            _styleBoxRed.normal.textColor = _colRed;

            _styleBoxBlue = new GUIStyle(_styleBox);
            _styleBoxBlue.normal.textColor = _colBlue;

            _styleBoxPurple = new GUIStyle(_styleBox);
            _styleBoxPurple.normal.textColor = _colPurple;

            _styleBoxOrange = new GUIStyle(_styleBox);
            _styleBoxOrange.normal.textColor = _colOrange;

            _texBlue = MakeTex(new Color(_colBlue.r, _colBlue.g, _colBlue.b, 0.15f));
            _texPurple = MakeTex(new Color(_colPurple.r, _colPurple.g, _colPurple.b, 0.15f));
            _texOrange = MakeTex(new Color(_colOrange.r, _colOrange.g, _colOrange.b, 0.15f));
            _texGreen = MakeTex(new Color(_colGreen.r, _colGreen.g, _colGreen.b, 0.15f));
        }

        private void DrawStickyDashboard(UserData user)
        {
            var box = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(14, 14, 10, 10)
            };

            string shortId = user._userId.Length > 16
                ? user._userId.Substring(0, 16) + "…"
                : user._userId;

            string tierBadge = TierService.Instance != null
                ? $"{TierService.Instance.CurrentRank} (LP {TierService.Instance.CurrentLp})"
                : "No Tier";

            string top3 =
                $"1위: {SafeScore(user, 0):N0}  /  2위: {SafeScore(user, 1):N0}  /  3위: {SafeScore(user, 2):N0}";

            EditorGUILayout.BeginVertical(box);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"👤  {user._userNick}",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 },
                GUILayout.MaxWidth(280));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                $"({shortId})",
                new GUIStyle(EditorStyles.miniLabel) { fontSize = 11 });
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            EditorGUILayout.LabelField(
                $"💰 {user._gold:N0} G   🪙 {user._token:N0} 토큰   🏆 {tierBadge}",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });

            EditorGUILayout.LabelField(
                $"🏅 {top3}",
                new GUIStyle(EditorStyles.miniLabel) { fontSize = 12 });

            EditorGUILayout.EndVertical();
        }

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();

            if (DrawTab("💾 저장", Tab.Firebase, _colBlue, _texBlue))
                _currentTab = Tab.Firebase;

            if (DrawTab("🎮 런종료", Tab.RunEnd, _colPurple, _texPurple))
                _currentTab = Tab.RunEnd;

            if (DrawTab("🛒 상점", Tab.Shop, _colOrange, _texOrange))
                _currentTab = Tab.Shop;

            if (DrawTab("🏆 리더보드", Tab.Leaderboard, _colYellow, _texBlue))
                _currentTab = Tab.Leaderboard;

            if (DrawTab("🎯 매칭", Tab.Matchmaking, _colGreen, _texGreen))
                _currentTab = Tab.Matchmaking;

            if (DrawTab("📡 DB", Tab.FirebaseExplorer, _colRed, _texOrange))
                _currentTab = Tab.FirebaseExplorer;

            if (_showBalanceTab)
            {
                if (DrawTab("📊 밸런스", Tab.BalanceViewer, _colBlue, _texBlue))
                    _currentTab = Tab.BalanceViewer;
            }

            if (_showSurvivalSimTab)
            {
                if (DrawTab("🌡️ 생존", Tab.SurvivalSim, _colOrange, _texOrange))
                    _currentTab = Tab.SurvivalSim;
            }

            if (_showGhostStockTab)
            {
                if (DrawTab("📈 주식", Tab.GhostStock, _colPurple, _texPurple))
                    _currentTab = Tab.GhostStock;
            }

            if (_showGhostSurvivalTab)
            {
                if (DrawTab("👻 생존", Tab.GhostSurvival, _colPurple, _texPurple))
                    _currentTab = Tab.GhostSurvival;
            }

            if (_showGhostRaceTab)
            {
                if (DrawTab("🏇 경마", Tab.GhostRace, _colPurple, _texPurple))
                    _currentTab = Tab.GhostRace;
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool DrawTab(string label, Tab tab, Color col, Texture2D tex)
        {
            bool active = _currentTab == tab;

            var style = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 13,
                fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
                fixedHeight = 32
            };

            if (active)
            {
                style.normal.textColor = col;
                style.normal.background = tex;
            }

            return GUILayout.Button(label, style);
        }

        // ★ [수정 1] 상단 알림창이 절대 두 줄로 구겨지지 않고 에디터 양 끝으로 시원하게 펼쳐지도록 wordWrap을 풀고 가로 확장을 적용했습니다.
        private void DrawStatusBar()
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(16, 16, 8, 8),
                wordWrap = false // 한 줄로만 뻗어 나가게 강제 고정
            };

            if (Time.realtimeSinceStartup - _statusTime > 3f)
                style.normal.textColor = _colYellow;
            else if (_statusType == MessageType.Error)
                style.normal.textColor = _colRed;
            else if (_statusType == MessageType.Warning)
                style.normal.textColor = _colOrange;
            else
                style.normal.textColor = _colGreen;

            // Layout 너비 제약을 지우고 ExpandWidth를 True로 부여하여 창의 전체 가로 너비를 넓게 씁니다.
            EditorGUILayout.SelectableLabel(_statusMsg, style, GUILayout.Height(36f), GUILayout.ExpandWidth(true));
        }
    }
}