using System;
using UnityEngine;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wooduduk.Editor
{
    public partial class SaveManagerDebugWindow : EditorWindow, IHasCustomMenu
    {
        [MenuItem("Tools/우드득 디버그 툴")]
        public static void Open()
        {
            var window = GetWindow<SaveManagerDebugWindow>("🔥 우드득 디버그");
            window.minSize = new Vector2(720, 840);
            window.Show();
        }

        private enum Tab
        {
            Firebase, RunEnd, Shop, Leaderboard, Matchmaking,
            BalanceViewer, SurvivalSim, FirebaseExplorer,
            GhostStock, GhostSurvival, GhostRace
        }

        private Tab _currentTab = Tab.Firebase;
        private Vector2 _scrollPos;

        // ── 비밀 상점 발견 여부 ──
        private const string PREF_SECRET_SHOP = "Wooduduk.Debug.SecretShopUnlocked";

        // ── 탭 표시 여부 (상단 탭바 ON/OFF) ──
        private const string PREF_SHOW_BALANCE_TAB = "Wooduduk.Debug.ShowBalanceTab";
        private const string PREF_SHOW_SURVIVALSIM_TAB = "Wooduduk.Debug.ShowSurvivalSimTab";
        private const string PREF_SHOW_GHOSTSTOCK_TAB = "Wooduduk.Debug.ShowGhoststockTab";
        private const string PREF_SHOW_GHOSTSURVIVAL_TAB = "Wooduduk.Debug.ShowGhostSurvivalTab";
        private const string PREF_SHOW_GHOSTRACE_TAB = "Wooduduk.Debug.ShowGhostRaceTab";
        // ── 상태 ──
        private bool _secretShopUnlocked;
        private bool _showBalanceTab;
        private bool _showSurvivalSimTab;
        private bool _showGhostStockTab;
        private bool _showGhostSurvivalTab;
        private bool _showGhostRaceTab;

        // ── 구슬 클릭 카운터 ──
        private int _secretTapCount;
        private double _lastSecretTapTime;

        private bool AnySecretTabOpen =>
            _showBalanceTab ||
            _showSurvivalSimTab ||
            _showGhostStockTab ||
            _showGhostSurvivalTab ||
            _showGhostRaceTab;

        // ── 우클릭 메뉴 ──
        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddDisabledItem(new GUIContent("🔐 비밀 탭 관리"));
            menu.AddSeparator("");

            if (!_secretShopUnlocked)
            {
                menu.AddDisabledItem(new GUIContent("🔒 비밀 상점 미발견"));
                return;
            }

            menu.AddItem(new GUIContent("📊 밸런스 뷰어"), _showBalanceTab, () =>
                ToggleSecretTab(ref _showBalanceTab, PREF_SHOW_BALANCE_TAB, Tab.BalanceViewer));

            menu.AddItem(new GUIContent("🌡️ 생존 시뮬레이터"), _showSurvivalSimTab, () =>
                ToggleSecretTab(ref _showSurvivalSimTab, PREF_SHOW_SURVIVALSIM_TAB, Tab.SurvivalSim));

            menu.AddItem(new GUIContent("📈 고스트 주식"), _showGhostStockTab, () =>
                ToggleSecretTab(ref _showGhostStockTab, PREF_SHOW_GHOSTSTOCK_TAB, Tab.GhostStock));

            menu.AddItem(new GUIContent("👻 고스트 서바이벌"), _showGhostSurvivalTab, () =>
                ToggleSecretTab(ref _showGhostSurvivalTab, PREF_SHOW_GHOSTSURVIVAL_TAB, Tab.GhostSurvival));

            menu.AddItem(new GUIContent("🏇 고스트 경마"), _showGhostRaceTab, () =>
                ToggleSecretTab(ref _showGhostRaceTab, PREF_SHOW_GHOSTRACE_TAB, Tab.GhostRace));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("✨ 비밀 상점 보기"), false, () =>
            {
                _currentTab = Tab.Shop;
            });
        }

        private void ToggleSecretTab(ref bool show, string prefKey, Tab targetTab)
        {
            show = !show;
            EditorPrefs.SetBool(prefKey, show);

            if (show)
            {
                _currentTab = targetTab;
            }
            else if (_currentTab == targetTab)
            {
                _currentTab = Tab.Shop;
            }
        }

        // ── 기타 필드 ──
        private string _statusMsg = "🟢 대기 중";
        private MessageType _statusType = MessageType.Info;
        private float _statusTime;
        private string _goldInput = "100";
        private string _scoreInput = "1500";
        private string _hpInput = "10";
        private float _simHP = 100f;
        private float _simMaxHP = 100f;
        private bool _isInvincible = false;
        private string _runSurvival = "30";
        private string _runFireLog = "5";
        private string _runSettle = "3";
        private string _runBankedWood = "50";
        private string _tierTestScore = "100000";
        private int _tokenPrice = 100;
        private int _weaponGachaCost = 3;
        private int _skinGachaCost = 2;
        private int _duplicateRefundGold = 50;
        private string _tokenBuyCount = "10";

        private readonly string[] _weaponPool = { "W001", "W002", "W003", "W004", "W005", "W006", "W007", "W008" };
        private readonly string[] _skinPool = { "RS001", "RS002", "RS003", "RS004", "RS005" };
        private string _lastGachaResult = "";

        private readonly KeyCode[] _konamiCode = {
            KeyCode.UpArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.DownArrow,
            KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.LeftArrow, KeyCode.RightArrow
        };
        private int _konamiIndex;

        private Color _colGreen, _colBlue, _colYellow, _colRed, _colPurple, _colOrange;
        private GUIStyle _styleBox, _styleBoxGreen, _styleBoxRed, _styleBoxBlue, _styleBoxPurple, _styleBoxOrange;
        private bool _stylesReady;
        private Texture2D _texBlue, _texPurple, _texOrange, _texGreen;

        // ── Lifecycle ──
        private void OnEnable()
        {
            EditorApplication.update += AutoRepaint;
            _stylesReady = false;

            _secretShopUnlocked = EditorPrefs.GetBool(PREF_SECRET_SHOP, false);

            _showBalanceTab = EditorPrefs.GetBool(PREF_SHOW_BALANCE_TAB, false);
            _showSurvivalSimTab = EditorPrefs.GetBool(PREF_SHOW_SURVIVALSIM_TAB, false);
            _showGhostStockTab = EditorPrefs.GetBool(PREF_SHOW_GHOSTSTOCK_TAB, false);
            _showGhostSurvivalTab = EditorPrefs.GetBool(PREF_SHOW_GHOSTSURVIVAL_TAB, false);
            _showGhostRaceTab = EditorPrefs.GetBool(PREF_SHOW_GHOSTRACE_TAB, false);

            Data.Service.RunResultUIService.OnResultShown += OnMultiResultReceived;
        }

        private void OnDisable()
        {
            EditorApplication.update -= AutoRepaint;
            Data.Service.RunResultUIService.OnResultShown -= OnMultiResultReceived;
            FbExplorerCleanup();
        }

        private float _lastRepaint;
        private void AutoRepaint()
        {
            if (!Application.isPlaying) return;
            if (Time.realtimeSinceStartup - _lastRepaint < 0.1f) return;
            _lastRepaint = Time.realtimeSinceStartup;
            Repaint();
        }

        // ── OnGUI ──
        private void OnGUI()
        {
            BuildStyles();
            var e = Event.current;

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == _konamiCode[_konamiIndex])
                {
                    _konamiIndex++;
                    if (_konamiIndex >= _konamiCode.Length)
                    {
                        _konamiIndex = 0;
                        UnlockAllSecretTabs();
                        SetStatus("🎮 치트코드 발동! 모든 비밀 탭 무료 해금!", MessageType.Info);
                    }
                }
                else _konamiIndex = 0;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("▶ Play Mode에서만 동작합니다", MessageType.Warning);
                return;
            }
            if (UserManager.Instance == null || UserManager.Instance.CurrentUserData == null)
            {
                EditorGUILayout.HelpBox("Firebase 로그인 대기 중...", MessageType.Info);
                return;
            }

            var user = UserManager.Instance.CurrentUserData;

            // 접근 제어: 해금 && 표시 둘 다 켜져 있어야 접근 가능
            if (!_showBalanceTab && _currentTab == Tab.BalanceViewer)
                _currentTab = Tab.Shop;

            if (!_showSurvivalSimTab && _currentTab == Tab.SurvivalSim)
                _currentTab = Tab.Shop;

            if (!_showGhostStockTab && _currentTab == Tab.GhostStock)
                _currentTab = Tab.Shop;

            if (!_showGhostSurvivalTab && _currentTab == Tab.GhostSurvival)
                _currentTab = Tab.Shop;

            if (!_showGhostRaceTab && _currentTab == Tab.GhostRace)
                _currentTab = Tab.Shop;


            DrawStickyDashboard(user);
            EditorGUILayout.Space(6);
            DrawTabs();
            EditorGUILayout.Space(6);
            DrawStatusBar();
            EditorGUILayout.Space(6);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_currentTab)
            {
                case Tab.Firebase: DrawFirebaseTab(user); break;
                case Tab.RunEnd: DrawRunEndTab(user); break;
                case Tab.Shop: DrawShopTab(user); break;
                case Tab.Leaderboard: DrawLeaderboardTab(user); break;
                case Tab.Matchmaking: DrawMatchmakingTab(user); break;
                case Tab.FirebaseExplorer: DrawFirebaseExplorerTab(user); break;
                case Tab.BalanceViewer: if (_showBalanceTab) DrawBalanceViewerTab(user); break;
                case Tab.SurvivalSim: if (_showSurvivalSimTab) DrawSurvivalSimTab(user); break;
                case Tab.GhostStock: if (_showGhostStockTab) DrawGhostStockTab(user); break;
                case Tab.GhostSurvival: if (_showGhostSurvivalTab) DrawGhostSurvivalTab(user); break;
                case Tab.GhostRace: if (_showGhostRaceTab) DrawGhostRaceTab(user); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ── 공통 헬퍼 ──

        private void UnlockAllSecretTabs()
        {
            _secretShopUnlocked = true;

            _showBalanceTab = true;
            _showSurvivalSimTab = true;
            _showGhostStockTab = true;
            _showGhostSurvivalTab = true;
            _showGhostRaceTab = true;

            EditorPrefs.SetBool(PREF_SECRET_SHOP, true);

            EditorPrefs.SetBool(PREF_SHOW_BALANCE_TAB, true);
            EditorPrefs.SetBool(PREF_SHOW_SURVIVALSIM_TAB, true);
            EditorPrefs.SetBool(PREF_SHOW_GHOSTSTOCK_TAB, true);
            EditorPrefs.SetBool(PREF_SHOW_GHOSTSURVIVAL_TAB, true);
            EditorPrefs.SetBool(PREF_SHOW_GHOSTRACE_TAB, true);
        }

        private void Save(UserData user) => SaveManager.Instance.SaveUserData(user);

        private void DrawHPBar()
        {
            float ratio = _simHP / _simMaxHP;
            Rect r = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(r, ratio, $"체력 {_simHP:F0} / {_simMaxHP:F0}");
        }

        private void DrawBox(GUIStyle style, string title, Action body)
        {
            EditorGUILayout.BeginVertical(style);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            body?.Invoke();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        // ── 기존 유틸 함수들 유지 (여기서는 제거됨):
        // DrawSecretVaultTab() → 삭제 완료

        private void SetStatus(string msg, MessageType type)
        {
            _statusMsg = msg;
            _statusType = type;
            _statusTime = Time.realtimeSinceStartup;
            Debug.Log($"[DebugTool] {msg}");
            Repaint();
        }

        private string GetSymbolIcon(string key) => key switch
        {
            "swing" => "🪓",
            "perfect" => "⭐",
            "acorn" => "🌰",
            "lucky" => "🍀",
            "bulbbatta" => "🔥",
            "knot" => "🟤",
            "worm" => "🐛",
            "makgeolli" => "🍶",
            _ => "❓"
        };

        private string GetSymbolName(string key) => key switch
        {
            "swing" => "스윙",
            "perfect" => "퍼펙트",
            "acorn" => "도토리",
            "lucky" => "행운",
            "bulbbatta" => "불빠따",
            "knot" => "옹이",
            "worm" => "좀벌레",
            "makgeolli" => "막걸리",
            _ => "?"
        };

        private int SafeScore(UserData u, int i) => u._bestScores != null && u._bestScores.Length > i ? u._bestScores[i] : 0;
        private int ParseInt(string v, int f = 0) => int.TryParse(v, out int r) ? r : f;
        private float ParseFloat(string v, float f = 0f) => float.TryParse(v, out float r) ? r : f;

        private Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}