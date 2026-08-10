using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase.Leaderboard;

namespace Wooduduk.Editor
{
    public partial class SaveManagerDebugWindow
    {
        private enum StockState { Idle, Loading, Trading, Closed }
        private StockState _stockState = StockState.Idle;

        // ── 종목 성향 ──
        private enum StockType
        {
            Stable,   // 안정형: 변동 작음, 배당 확률 높음
            Growth,   // 성장형: 중간 변동, 추세 유지
            Volatile  // 투기형: 변동 큼, 작전세력 대상
        }

        private static readonly string[] _stockTypeLabels = { "🛡안정", "📈성장", "🎰투기" };
        private static readonly Color[] _stockTypeColors =
        {
            new Color(0.3f, 0.6f, 1f),
            new Color(0.2f, 0.9f, 0.4f),
            new Color(1f, 0.4f, 0.3f)
        };

        private class StockEntry
        {
            public GhostEntry Ghost;
            public StockType Type;
            public float Price;
            public float InitialPrice;
            public int OwnedShares;
            public float LastChange;
            public bool IsDelisted;
            public List<float> PriceHistory = new();

            // ── 신규 필드 ──
            public float Volatility;       // 0.05~0.25 (성향별 차등)
            public int Streak;             // 연속 상승(+) 또는 하락(-) 카운트
            public int Volume;             // 거래량 (뉴스 대상일수록 증가)
            public bool CircuitBreaker;    // 서킷브레이커 발동 중
            public bool PumpActive;        // 작전세력 펌프 중
            public int PumpRoundsLeft;     // 펌프 잔여 라운드

            public float TotalInvested;    // 총 매수 금액 (평단가 계산용)
        }

        private List<StockEntry> _stocks = new();
        private List<GhostEntry> _stockPool = new();

        private int _stockRound;
        private const int STOCK_MAX_ROUNDS = 12;
        private string _stockHeadline = "";
        private float _stockNewsTimer;
        private float _stockNewsInterval = 3f;
        private int _stockTradeAmount = 1;
        private int _stockStartGold;
        private System.Random _stockRng;
        private double _lastStockTickTime;

        // 보유 요약 접기/펴기
        private bool _showPortfolio = true;

        private static readonly (string template, string icon, float min, float max)[] _stockNews =
        {
            ("{nick}, 불빠따 연속 발동!",     "🔥",  0.15f,  0.35f),
            ("{nick}, 퍼펙트 히트 신기록!",   "⭐",  0.20f,  0.45f),
            ("{nick}, 행운의 연속!",          "🍀",  0.25f,  0.50f),
            ("{nick}, 도토리 대풍년!",        "🌰",  0.10f,  0.22f),
            ("{nick}, 저체온증 위기!",        "🥶", -0.35f, -0.15f),
            ("{nick}, 좀벌레 습격!",          "🐛", -0.28f, -0.10f),
            ("{nick}, 옹이 연속 출현!",       "🟤", -0.20f, -0.08f),
            ("{nick}, 장작 재고 고갈!",       "💀", -0.40f, -0.20f),
            ("{nick}, 막걸리로 판단력 저하?", "🍶", -0.12f,  0.12f),
            ("{nick}, 비밀 전략 공개!",       "📊",  0.18f,  0.38f),
        };

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 진입점
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawGhostStockTab(UserData user)
        {
            DrawStockCloseButton();

            switch (_stockState)
            {
                case StockState.Idle:
                case StockState.Loading:
                    DrawStockIdle(user); break;
                case StockState.Trading:
                    DrawStockTrading(user); break;
                case StockState.Closed:
                    DrawStockClosed(user); break;
            }
        }

        private void DrawStockCloseButton()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.5f, 0.1f, 0.1f);
            if (GUILayout.Button("✕ 닫기", GUILayout.Width(70), GUILayout.Height(22)))
            {
                _showGhostStockTab = false;
                EditorPrefs.SetBool(PREF_SHOW_GHOSTSTOCK_TAB, false);
                _currentTab = Tab.Firebase;
                _stockState = StockState.Idle;
            }
            GUI.backgroundColor = old;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [1] Idle
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawStockIdle(UserData user)
        {
            DrawBox(_styleBoxPurple, "📈 고스트 주식시장", () =>
            {
                var ts = new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                ts.normal.textColor = _colYellow;
                EditorGUILayout.LabelField("Firebase 고스트로 주식 투자!", ts);
                EditorGUILayout.Space(6);

                EditorGUILayout.LabelField($"💰 보유 골드: {user._gold:N0} G", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                var info = new GUIStyle(EditorStyles.miniLabel);
                info.normal.textColor = Color.gray;
                EditorGUILayout.LabelField($"• 뉴스 {STOCK_MAX_ROUNDS}회 후 장 마감  • 보유 주식 자동 청산", info);
                EditorGUILayout.LabelField("• 종목마다 성향이 다릅니다 (🛡안정 / 📈성장 / 🎰투기)", info);
                EditorGUILayout.LabelField("• 작전세력, 서킷브레이커, 배당 이벤트 발생", info);
                EditorGUILayout.Space(8);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("뉴스 간격", GUILayout.Width(60));
                _stockNewsInterval = EditorGUILayout.Slider(_stockNewsInterval, 1f, 10f);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(6);

                bool loading = _stockState == StockState.Loading;
                var old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.1f, 0.7f, 0.3f);
                GUI.enabled = !loading;
                if (GUILayout.Button(loading ? "⏳ 종목 불러오는 중..." : "🔔 장 시작!", GUILayout.Height(42)))
                    LoadStocks(user);
                GUI.enabled = true;
                GUI.backgroundColor = old;
            });
        }

        private void LoadStocks(UserData user)
        {
            _stockState = StockState.Loading;
            var repo = new GhostRepository();
            repo.LoadCandidates(user._axeTier, entries =>
            {
                _stockRng = new System.Random();
                var pool = entries != null && entries.Count > 0
                    ? new List<GhostEntry>(entries)
                    : new List<GhostEntry>();

                for (int i = pool.Count - 1; i > 0; i--)
                {
                    int j = _stockRng.Next(i + 1);
                    (pool[i], pool[j]) = (pool[j], pool[i]);
                }

                int target = 10;
                while (pool.Count < target)
                    pool.Add(new GhostEntry
                    {
                        _nick = $"유령{pool.Count + 1}호",
                        _score = 400 + _stockRng.Next(200, 2000),
                        _tier = user._axeTier
                    });

                _stocks = new List<StockEntry>();
                _stockPool = new List<GhostEntry>();

                StockType[] types = { StockType.Stable, StockType.Growth, StockType.Volatile };

                for (int i = 0; i < pool.Count; i++)
                {
                    if (i < 5)
                    {
                        float price = Mathf.Round(Mathf.Max(pool[i]._score * 0.12f, 50f));
                        var type = types[_stockRng.Next(types.Length)];
                        float vol = type switch
                        {
                            StockType.Stable => 0.04f + (float)_stockRng.NextDouble() * 0.06f,
                            StockType.Growth => 0.08f + (float)_stockRng.NextDouble() * 0.10f,
                            StockType.Volatile => 0.15f + (float)_stockRng.NextDouble() * 0.15f,
                            _ => 0.10f
                        };

                        _stocks.Add(new StockEntry
                        {
                            Ghost = pool[i],
                            Type = type,
                            Price = price,
                            InitialPrice = price,
                            Volatility = vol,
                            PriceHistory = new List<float> { price }
                        });
                    }
                    else
                    {
                        _stockPool.Add(pool[i]);
                    }
                }

                _stockRound = 0;
                _stockHeadline = "🔔 장이 열렸습니다! 종목 성향을 확인하고 투자하세요.";
                _stockNewsTimer = _stockNewsInterval;
                _stockTradeAmount = 1;
                _stockStartGold = user._gold;
                _lastStockTickTime = EditorApplication.timeSinceStartup;
                _stockState = StockState.Trading;
                _showPortfolio = true;
                Repaint();
            });
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [2] 거래 중
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawStockTrading(UserData user)
        {
            TickStock(user);

            // ── 상단 정보 ──
            DrawBox(_styleBoxPurple, $"📈 장 진행 중  [{_stockRound}/{STOCK_MAX_ROUNDS}]  대기 {_stockPool.Count}종목", () =>
            {
                var hs = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, wordWrap = true };
                hs.normal.textColor = _colYellow;
                EditorGUILayout.LabelField(_stockHeadline, hs);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(
                    $"💰 {user._gold:N0} G    📰 다음 뉴스: {_stockNewsTimer:F1}초",
                    EditorStyles.boldLabel);

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("거래 수량", GUILayout.Width(60));
                var old = GUI.backgroundColor;
                foreach (int amt in new[] { 1, 3, 5, 10 })
                {
                    GUI.backgroundColor = _stockTradeAmount == amt
                        ? new Color(0.5f, 0.2f, 0.9f)
                        : new Color(0.3f, 0.3f, 0.3f);
                    if (GUILayout.Button($"{amt}", GUILayout.Width(36), GUILayout.Height(22)))
                        _stockTradeAmount = amt;
                }
                GUI.backgroundColor = old;
                EditorGUILayout.EndHorizontal();
            });

            // ── 보유 종목 요약 ──
            DrawPortfolioSummary(user);

            // ── 종목 목록 ──
            foreach (var s in _stocks)
                DrawStockRow(s, user);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 보유 종목 요약 패널
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawPortfolioSummary(UserData user)
        {
            var owned = _stocks.FindAll(s => s.OwnedShares > 0 && !s.IsDelisted);
            if (owned.Count == 0) return;

            int totalValue = 0;
            int totalCost = 0;
            foreach (var s in owned)
            {
                totalValue += (int)(s.Price * s.OwnedShares);
                totalCost += (int)s.TotalInvested;
            }
            int totalPnl = totalValue - totalCost;
            float totalPnlPct = totalCost > 0 ? (float)totalPnl / totalCost * 100f : 0f;
            bool isProfit = totalPnl >= 0;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 헤더 (접기/펴기)
            EditorGUILayout.BeginHorizontal();
            string arrow = _showPortfolio ? "▼" : "▶";
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            headerStyle.normal.textColor = isProfit ? _colGreen : _colRed;

            string pnlSign = isProfit ? "+" : "";
            if (GUILayout.Button(
                $"{arrow} 💼 내 포트폴리오  |  평가 {totalValue:N0}G  |  {pnlSign}{totalPnl:N0}G ({pnlSign}{totalPnlPct:F1}%)  |  {owned.Count}종목",
                headerStyle))
            {
                _showPortfolio = !_showPortfolio;
            }
            EditorGUILayout.EndHorizontal();

            if (_showPortfolio)
            {
                EditorGUILayout.Space(2);

                // 컬럼 헤더
                var colHeader = new GUIStyle(EditorStyles.miniLabel);
                colHeader.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("종목", colHeader, GUILayout.Width(90));
                EditorGUILayout.LabelField("성향", colHeader, GUILayout.Width(45));
                EditorGUILayout.LabelField("수량", colHeader, GUILayout.Width(35));
                EditorGUILayout.LabelField("평단가", colHeader, GUILayout.Width(55));
                EditorGUILayout.LabelField("현재가", colHeader, GUILayout.Width(55));
                EditorGUILayout.LabelField("평가액", colHeader, GUILayout.Width(60));
                EditorGUILayout.LabelField("손익", colHeader, GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();

                // 구분선
                var lineRect = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(lineRect, new Color(0.4f, 0.4f, 0.4f));

                foreach (var s in owned)
                {
                    float avgCost = s.TotalInvested / s.OwnedShares;
                    int evalValue = (int)(s.Price * s.OwnedShares);
                    int pnl = evalValue - (int)s.TotalInvested;
                    float pnlPct = s.TotalInvested > 0 ? pnl / s.TotalInvested * 100f : 0f;
                    bool profit = pnl >= 0;

                    EditorGUILayout.BeginHorizontal();

                    // 종목명
                    var nameStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
                    nameStyle.normal.textColor = Color.white;
                    EditorGUILayout.LabelField(s.Ghost._nick, nameStyle, GUILayout.Width(90));

                    // 성향
                    var typeStyle = new GUIStyle(EditorStyles.miniLabel);
                    typeStyle.normal.textColor = _stockTypeColors[(int)s.Type];
                    EditorGUILayout.LabelField(_stockTypeLabels[(int)s.Type], typeStyle, GUILayout.Width(45));

                    // 수량
                    var qtyStyle = new GUIStyle(EditorStyles.miniLabel);
                    qtyStyle.normal.textColor = _colYellow;
                    EditorGUILayout.LabelField($"{s.OwnedShares}주", qtyStyle, GUILayout.Width(35));

                    // 평단가
                    EditorGUILayout.LabelField($"{avgCost:F0}G", EditorStyles.miniLabel, GUILayout.Width(55));

                    // 현재가
                    var curStyle = new GUIStyle(EditorStyles.miniLabel);
                    curStyle.normal.textColor = s.Price >= avgCost ? _colGreen : _colRed;
                    EditorGUILayout.LabelField($"{s.Price:F0}G", curStyle, GUILayout.Width(55));

                    // 평가액
                    EditorGUILayout.LabelField($"{evalValue:N0}G", EditorStyles.miniLabel, GUILayout.Width(60));

                    // 손익
                    var pnlStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
                    pnlStyle.normal.textColor = profit ? _colGreen : _colRed;
                    string pSign = profit ? "+" : "";
                    EditorGUILayout.LabelField($"{pSign}{pnl:N0}G ({pSign}{pnlPct:F1}%)", pnlStyle, GUILayout.Width(80));

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(2);

                // 총 자산
                int totalAsset = user._gold + totalValue;
                var totalStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
                totalStyle.normal.textColor = _colYellow;
                EditorGUILayout.LabelField(
                    $"  💰 현금 {user._gold:N0}G + 📊 주식 {totalValue:N0}G = 🏦 총 자산 {totalAsset:N0}G",
                    totalStyle);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 종목 행
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawStockRow(StockEntry s, UserData user)
        {
            if (s.IsDelisted)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var ds = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
                ds.normal.textColor = Color.gray;
                EditorGUILayout.LabelField($"🚫 {s.Ghost._nick}  —  거래 불가 (상장폐지)", ds);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
                return;
            }

            float fromInitial = (s.Price - s.InitialPrice) / s.InitialPrice;
            Color priceColor = s.LastChange > 0.005f ? _colGreen
                              : s.LastChange < -0.005f ? _colRed
                              : Color.white;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ── 1행: 이름 + 성향 + 가격 + 보유 + 상태 ──
            EditorGUILayout.BeginHorizontal();

            // 이름
            var nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField(s.Ghost._nick, nameStyle, GUILayout.Width(100));

            // 성향 뱃지
            var typeStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            typeStyle.normal.textColor = _stockTypeColors[(int)s.Type];
            EditorGUILayout.LabelField(_stockTypeLabels[(int)s.Type], typeStyle, GUILayout.Width(45));

            // 가격
            var ps = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            ps.normal.textColor = priceColor;
            string arrow = s.LastChange >= 0 ? "▲" : "▼";
            EditorGUILayout.LabelField(
                $"{s.Price:F0}G {arrow}{Mathf.Abs(s.LastChange) * 100:F1}%", ps, GUILayout.Width(120));

            // 추세
            string streak = s.Streak > 0 ? $"🔥{s.Streak}연속↑" : s.Streak < 0 ? $"❄{Mathf.Abs(s.Streak)}연속↓" : "━";
            var streakStyle = new GUIStyle(EditorStyles.miniLabel);
            streakStyle.normal.textColor = s.Streak > 0 ? _colGreen : s.Streak < 0 ? _colRed : Color.gray;
            EditorGUILayout.LabelField(streak, streakStyle, GUILayout.Width(65));

            GUILayout.FlexibleSpace();

            // 보유
            if (s.OwnedShares > 0)
            {
                var os = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
                os.normal.textColor = _colYellow;
                EditorGUILayout.LabelField($"📦{s.OwnedShares}주", os, GUILayout.Width(50));
            }

            // 특수 상태
            if (s.CircuitBreaker)
            {
                var cb = new GUIStyle(EditorStyles.miniLabel);
                cb.normal.textColor = new Color(1f, 0.5f, 0f);
                EditorGUILayout.LabelField("🚧정지", cb, GUILayout.Width(42));
            }
            if (s.PumpActive)
            {
                var pump = new GUIStyle(EditorStyles.miniLabel);
                pump.normal.textColor = new Color(1f, 0.2f, 0.8f);
                EditorGUILayout.LabelField($"🐋세력({s.PumpRoundsLeft})", pump, GUILayout.Width(60));
            }

            EditorGUILayout.EndHorizontal();

            // ── 2행: 미니 차트 (선 그래프, 콤팩트) ──
            DrawStockLineChart(s.PriceHistory, s.InitialPrice);

            // ── 3행: 매수/매도 ──
            if (!s.CircuitBreaker)
            {
                int buyCost = (int)(s.Price * _stockTradeAmount);
                int sellAmount = Mathf.Min(_stockTradeAmount, s.OwnedShares);
                int sellGain = (int)(s.Price * sellAmount);

                EditorGUILayout.BeginHorizontal();
                var old = GUI.backgroundColor;

                GUI.backgroundColor = user._gold >= buyCost ? new Color(0.1f, 0.7f, 0.3f) : Color.gray;
                GUI.enabled = user._gold >= buyCost;
                if (GUILayout.Button($"매수 -{buyCost:N0}G", GUILayout.Height(22)))
                {
                    user._gold -= buyCost;
                    s.TotalInvested += buyCost;
                    s.OwnedShares += _stockTradeAmount;
                    s.Volume += _stockTradeAmount;
                    Save(user);
                }
                GUI.enabled = true;

                GUI.backgroundColor = s.OwnedShares >= 1 ? new Color(0.8f, 0.2f, 0.2f) : Color.gray;
                GUI.enabled = s.OwnedShares >= 1;
                if (GUILayout.Button($"매도 +{sellGain:N0}G", GUILayout.Height(22)))
                {
                    user._gold += sellGain;
                    // 평단가 기준 투자금 차감
                    float avgCost = s.TotalInvested / s.OwnedShares;
                    s.TotalInvested -= avgCost * sellAmount;
                    s.TotalInvested = Mathf.Max(0, s.TotalInvested);
                    s.OwnedShares -= sellAmount;
                    s.Volume += sellAmount;
                    if (s.OwnedShares == 0) s.TotalInvested = 0;
                    Save(user);
                }
                GUI.enabled = true;
                GUI.backgroundColor = old;
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                var cbMsg = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                cbMsg.normal.textColor = new Color(1f, 0.5f, 0f);
                EditorGUILayout.LabelField("🚧 서킷브레이커 — 이번 뉴스 거래 중지", cbMsg);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 선 그래프 (콤팩트)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawStockLineChart(List<float> history, float baseline)
        {
            if (history.Count < 2) return;

            // 콤팩트: 높이 20, 최대 폭 160px
            float chartWidth = Mathf.Min(160f, EditorGUIUtility.currentViewWidth * 0.35f);
            var rect = EditorGUILayout.GetControlRect(false, 20, GUILayout.Width(chartWidth));
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.12f));

            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in history) { min = Mathf.Min(min, v); max = Mathf.Max(max, v); }
            float range = max - min;
            if (range < 1f) { min -= 0.5f; max += 0.5f; range = max - min; }

            // baseline 라인
            float baseT = Mathf.Clamp01((baseline - min) / range);
            float baseY = rect.y + rect.height - baseT * (rect.height - 2f) - 1f;
            EditorGUI.DrawRect(new Rect(rect.x, baseY, rect.width, 1f), new Color(1f, 1f, 0f, 0.25f));

            // 선 그래프
            float stepX = (rect.width - 4f) / (history.Count - 1);
            for (int i = 1; i < history.Count; i++)
            {
                float t0 = Mathf.Clamp01((history[i - 1] - min) / range);
                float t1 = Mathf.Clamp01((history[i] - min) / range);
                float x0 = rect.x + 2f + (i - 1) * stepX;
                float x1 = rect.x + 2f + i * stepX;
                float y0 = rect.y + rect.height - t0 * (rect.height - 2f) - 1f;
                float y1 = rect.y + rect.height - t1 * (rect.height - 2f) - 1f;

                Color lineColor = history[i] >= baseline ? _colGreen : _colRed;
                DrawLine(x0, y0, x1, y1, lineColor, 1.5f);
            }

            // 마지막 점 강조
            float lastT = Mathf.Clamp01((history[^1] - min) / range);
            float lastY = rect.y + rect.height - lastT * (rect.height - 2f) - 1f;
            float lastX = rect.x + 2f + (history.Count - 1) * stepX;
            Color dotColor = history[^1] >= baseline ? _colGreen : _colRed;
            EditorGUI.DrawRect(new Rect(lastX - 2, lastY - 2, 4, 4), dotColor);
        }

        private void DrawLine(float x0, float y0, float x1, float y1, Color color, float width)
        {
            // EditorGUI에서 사선 그리기 — Handles 사용
            var oldColor = Handles.color;
            Handles.color = color;
            Handles.DrawAAPolyLine(width, new Vector3(x0, y0, 0), new Vector3(x1, y1, 0));
            Handles.color = oldColor;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 뉴스 틱
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void TickStock(UserData user)
        {
            if (_stockState != StockState.Trading) return;

            // Editor 안전 타이머
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastStockTickTime);
            _lastStockTickTime = now;
            dt = Mathf.Clamp(dt, 0f, 0.5f); // 에디터 포커스 잃었다 돌아왔을 때 폭주 방지

            _stockNewsTimer -= dt;
            if (_stockNewsTimer > 0f) return;

            _stockNewsTimer = _stockNewsInterval;
            _stockRound++;

            // 서킷브레이커 해제
            foreach (var st in _stocks)
                st.CircuitBreaker = false;

            // 작전세력 카운트다운
            foreach (var st in _stocks)
            {
                if (!st.PumpActive) continue;
                st.PumpRoundsLeft--;
                if (st.PumpRoundsLeft <= 0)
                {
                    // 세력 이탈 → 급락
                    st.PumpActive = false;
                    float dump = -0.30f - (float)_stockRng.NextDouble() * 0.20f;
                    ApplyPriceChange(st, dump);
                    _stockHeadline = $"💥 [{st.Ghost._nick}] 작전세력 이탈! 대량 매도! ({dump * 100:F0}%)";
                    SetStatus(_stockHeadline, MessageType.Error);

                    // 다른 종목 소폭 변동
                    ApplyAmbientChanges(st);
                    CheckMarketClose(user);
                    return;
                }
            }

            float roll = (float)_stockRng.NextDouble();

            // ── 대폭락 (6%) ──
            if (roll < 0.06f)
            {
                float rate = -0.30f - (float)_stockRng.NextDouble() * 0.20f;
                foreach (var st in _stocks)
                {
                    if (st.IsDelisted) continue;
                    ApplyPriceChange(st, rate * (0.7f + (float)_stockRng.NextDouble() * 0.6f));
                }
                _stockHeadline = $"💥 [긴급] 전 종목 대폭락! 시장 패닉!";
                SetStatus(_stockHeadline, MessageType.Error);
            }
            // ── 서킷브레이커 (5%) ──
            else if (roll < 0.11f)
            {
                var alive = _stocks.FindAll(st => !st.IsDelisted);
                if (alive.Count > 0)
                {
                    var target = alive[_stockRng.Next(alive.Count)];
                    target.CircuitBreaker = true;
                    _stockHeadline = $"🚧 [{target.Ghost._nick}] 서킷브레이커 발동! 거래 일시 중지";
                    SetStatus(_stockHeadline, MessageType.Warning);
                    // 다른 종목은 정상 변동
                    ApplyAmbientChanges(target);
                }
                else FireNormalNews(user);
            }
            // ── 작전세력 펌프 시작 (5%, 투기형 우선) ──
            else if (roll < 0.16f)
            {
                var candidates = _stocks.FindAll(st => !st.IsDelisted && !st.PumpActive);
                // 투기형 종목 있으면 우선
                var volatiles = candidates.FindAll(st => st.Type == StockType.Volatile);
                var pool = volatiles.Count > 0 ? volatiles : candidates;

                if (pool.Count > 0)
                {
                    var target = pool[_stockRng.Next(pool.Count)];
                    target.PumpActive = true;
                    target.PumpRoundsLeft = 2 + _stockRng.Next(2); // 2~3라운드 후 덤프
                    float pump = 0.25f + (float)_stockRng.NextDouble() * 0.35f;
                    ApplyPriceChange(target, pump);
                    _stockHeadline = $"🐋 [{target.Ghost._nick}] 작전세력 매집! 급등! (+{pump * 100:F0}%) ⚠{target.PumpRoundsLeft}뉴스 후 이탈 예정";
                    SetStatus(_stockHeadline, MessageType.Warning);
                    ApplyAmbientChanges(target);
                }
                else FireNormalNews(user);
            }
            // ── 배당 (6%, 안정형 우선) ──
            else if (roll < 0.22f)
            {
                var owned = _stocks.FindAll(st => !st.IsDelisted && st.OwnedShares > 0);
                if (owned.Count > 0)
                {
                    // 안정형이면 배당금 높음
                    var target = owned[_stockRng.Next(owned.Count)];
                    int perShare = target.Type == StockType.Stable
                        ? 15 + _stockRng.Next(25)
                        : 5 + _stockRng.Next(15);
                    int total = perShare * target.OwnedShares;
                    user._gold += total;
                    Save(user);
                    _stockHeadline = $"🌰 [{target.Ghost._nick}] 배당 지급! 주당 {perShare}G × {target.OwnedShares}주 = +{total}G";
                    SetStatus(_stockHeadline, MessageType.Info);
                    ApplyAmbientChanges(target);
                }
                else FireNormalNews(user);
            }
            // ── 강제 상장폐지 (4%) ──
            else if (roll < 0.26f)
            {
                var alive = _stocks.FindAll(st => !st.IsDelisted);
                if (alive.Count > 1)
                {
                    var victim = alive[_stockRng.Next(alive.Count)];
                    int refund = (int)(victim.InitialPrice * 0.1f) * victim.OwnedShares;
                    if (refund > 0) { user._gold += refund; Save(user); }
                    victim.OwnedShares = 0;
                    victim.IsDelisted = true;
                    victim.Price = 0f;
                    victim.TotalInvested = 0;
                    _stockHeadline = $"🚨 [{victim.Ghost._nick}] 강제 상장폐지! 초기가 10%로 청산 (+{refund}G)";
                    SetStatus(_stockHeadline, MessageType.Error);
                }
                else FireNormalNews(user);
            }
            // ── 신규 상장 (6%) ──
            else if (roll < 0.32f && _stockPool.Count > 0)
            {
                var alive = _stocks.FindAll(st => !st.IsDelisted);
                if (alive.Count < 7)
                {
                    var newGhost = _stockPool[0];
                    _stockPool.RemoveAt(0);
                    float ipoPrice = Mathf.Round(Mathf.Max(newGhost._score * 0.12f, 50f));
                    StockType[] types = { StockType.Stable, StockType.Growth, StockType.Volatile };
                    var type = types[_stockRng.Next(types.Length)];
                    float vol = type switch
                    {
                        StockType.Stable => 0.04f + (float)_stockRng.NextDouble() * 0.06f,
                        StockType.Growth => 0.08f + (float)_stockRng.NextDouble() * 0.10f,
                        StockType.Volatile => 0.15f + (float)_stockRng.NextDouble() * 0.15f,
                        _ => 0.10f
                    };
                    _stocks.Add(new StockEntry
                    {
                        Ghost = newGhost,
                        Type = type,
                        Price = ipoPrice,
                        InitialPrice = ipoPrice,
                        Volatility = vol,
                        PriceHistory = new List<float> { ipoPrice }
                    });
                    _stockHeadline = $"📣 [{newGhost._nick}] 신규 상장! {_stockTypeLabels[(int)type]} IPO {ipoPrice:F0}G";
                    SetStatus(_stockHeadline, MessageType.Info);
                }
                else FireNormalNews(user);
            }
            // ── 자진 상장폐지 (4%) ──
            else if (roll < 0.36f)
            {
                var alive = _stocks.FindAll(st => !st.IsDelisted);
                if (alive.Count > 1)
                {
                    var quitter = alive[_stockRng.Next(alive.Count)];
                    int refund = (int)(quitter.Price * quitter.OwnedShares);
                    if (refund > 0) { user._gold += refund; Save(user); }
                    quitter.OwnedShares = 0;
                    quitter.IsDelisted = true;
                    quitter.Price = 0f;
                    quitter.TotalInvested = 0;
                    _stockHeadline = $"📤 [{quitter.Ghost._nick}] 자진 상장폐지. 현재가 전량 매도 (+{refund}G)";
                    SetStatus(_stockHeadline, MessageType.Warning);
                }
                else FireNormalNews(user);
            }
            // ── 일반 뉴스 (64%) ──
            else
            {
                FireNormalNews(user);
            }

            CheckMarketClose(user);
        }

        private void FireNormalNews(UserData user)
        {
            var alive = _stocks.FindAll(st => !st.IsDelisted);
            if (alive.Count == 0) return;

            var target = alive[_stockRng.Next(alive.Count)];
            var (template, icon, newsMin, newsMax) = _stockNews[_stockRng.Next(_stockNews.Length)];

            // ── 성향별 변동률 조정 ──
            float baseChange = newsMin + (float)_stockRng.NextDouble() * (newsMax - newsMin);
            float typeMultiplier = target.Type switch
            {
                StockType.Stable => 0.6f,
                StockType.Growth => 1.0f,
                StockType.Volatile => 1.5f,
                _ => 1f
            };
            float change = baseChange * typeMultiplier;

            // ── 추세 보너스 (성장형: 연속 방향 강화) ──
            if (target.Type == StockType.Growth)
            {
                if ((target.Streak > 0 && change > 0) || (target.Streak < 0 && change < 0))
                    change *= 1f + Mathf.Abs(target.Streak) * 0.08f;
            }

            // ── 평균 회귀 (모든 종목) ──
            float revertStrength = target.Type == StockType.Stable ? 0.20f : 0.12f;
            float revert = (target.InitialPrice - target.Price) / target.InitialPrice * revertStrength;
            change += revert;

            // ── 작전세력 중이면 상승 유지 ──
            if (target.PumpActive && change < 0)
                change = Mathf.Abs(change) * 0.5f;

            ApplyPriceChange(target, change);
            target.Volume += 5 + _stockRng.Next(20);

            string sign = change >= 0 ? $"+{change * 100:F0}%" : $"{change * 100:F0}%";
            _stockHeadline = $"{icon} {template.Replace("{nick}", target.Ghost._nick)} ({sign})";
            SetStatus(_stockHeadline, MessageType.Info);

            // 다른 종목 소폭 변동
            ApplyAmbientChanges(target);
        }

        private void ApplyAmbientChanges(StockEntry except)
        {
            foreach (var st in _stocks)
            {
                if (st == except || st.IsDelisted || st.CircuitBreaker) continue;

                float ambient = ((float)_stockRng.NextDouble() - 0.45f) * st.Volatility * 0.5f;

                // 평균 회귀
                float revert = (st.InitialPrice - st.Price) / st.InitialPrice * 0.08f;
                ambient += revert;

                ApplyPriceChange(st, ambient);
            }
        }

        private void ApplyPriceChange(StockEntry s, float rate)
        {
            // 상한가/하한가 ±35%
            rate = Mathf.Clamp(rate, -0.35f, 0.35f);

            s.LastChange = rate;
            s.Price = Mathf.Max(s.Price * (1f + rate), 5f);
            s.Price = Mathf.Round(s.Price * 10f) / 10f;
            s.PriceHistory.Add(s.Price);
            if (s.PriceHistory.Count > 15) s.PriceHistory.RemoveAt(0);

            // 연속 추세 카운트
            if (rate > 0.005f) s.Streak = s.Streak > 0 ? s.Streak + 1 : 1;
            else if (rate < -0.005f) s.Streak = s.Streak < 0 ? s.Streak - 1 : -1;
            else s.Streak = 0;
        }

        private void CheckMarketClose(UserData user)
        {
            if (_stockRound >= STOCK_MAX_ROUNDS)
                CloseMarket(user);
        }

        private void CloseMarket(UserData user)
        {
            int liquidated = 0;
            foreach (var s in _stocks)
            {
                if (s.IsDelisted || s.OwnedShares == 0) continue;
                int gain = (int)(s.Price * s.OwnedShares);
                user._gold += gain;
                liquidated += gain;
                s.OwnedShares = 0;
                s.TotalInvested = 0;
            }
            Save(user);

            int profit = user._gold - _stockStartGold;
            SetStatus(profit >= 0
                ? $"🔔 장 마감! 청산 {liquidated:N0}G. 📈 수익 +{profit:N0}G"
                : $"🔔 장 마감! 청산 {liquidated:N0}G. 📉 손실 {profit:N0}G",
                MessageType.Info);

            _stockState = StockState.Closed;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [3] 마감
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawStockClosed(UserData user)
        {
            int profit = user._gold - _stockStartGold;
            bool won = profit >= 0;
            float pctReturn = _stockStartGold > 0 ? (float)profit / _stockStartGold * 100f : 0f;

            DrawBox(won ? _styleBoxGreen : _styleBoxRed,
                    won ? "📈 수익 실현!" : "📉 손실 마감", () =>
                    {
                        var ts = new GUIStyle(EditorStyles.boldLabel)
                        { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                        ts.normal.textColor = won ? _colGreen : _colRed;
                        string pSign = won ? "+" : "";
                        EditorGUILayout.LabelField(
                            $"{pSign}{profit:N0}G ({pSign}{pctReturn:F1}%)", ts);

                        EditorGUILayout.Space(6);
                        EditorGUILayout.LabelField($"💰 현재 골드: {user._gold:N0}G", EditorStyles.boldLabel);
                        EditorGUILayout.Space(8);

                        EditorGUILayout.LabelField("📊 최종 시세", EditorStyles.boldLabel);
                        foreach (var s in _stocks)
                        {
                            if (s.IsDelisted)
                            {
                                var gs = new GUIStyle(EditorStyles.label);
                                gs.normal.textColor = Color.gray;
                                EditorGUILayout.LabelField($"  🚫 {s.Ghost._nick} — 상장폐지", gs);
                                continue;
                            }
                            float chg = (s.Price - s.InitialPrice) / s.InitialPrice;
                            var cs = new GUIStyle(EditorStyles.label);
                            cs.normal.textColor = chg >= 0 ? _colGreen : _colRed;
                            string a = chg >= 0 ? "▲" : "▼";
                            EditorGUILayout.LabelField(
                                $"  {_stockTypeLabels[(int)s.Type]} {s.Ghost._nick}  {s.InitialPrice:F0} → {s.Price:F0}G ({a}{Mathf.Abs(chg) * 100:F1}%)", cs);
                        }

                        EditorGUILayout.Space(10);

                        var old = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(0.1f, 0.7f, 0.3f);
                        if (GUILayout.Button("🔔 다시 투자", GUILayout.Height(36)))
                            LoadStocks(user);
                        GUI.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
                        if (GUILayout.Button("처음으로", GUILayout.Height(26)))
                            _stockState = StockState.Idle;
                        GUI.backgroundColor = old;
                    });
        }
    }
}