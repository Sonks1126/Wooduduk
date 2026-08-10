using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase;

namespace Wooduduk.Editor
{
    public partial class SaveManagerDebugWindow
    {
        // ★ 정리 1: 사용하지 않는 가격 변수 및 Refund 함수 삭제됨

        private void DrawShopTab(UserData user)
        {
            // ── 재화 ──
            DrawBox(_styleBoxOrange, "💎 현재 재화", () =>
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"💰 골드: {user._gold:N0}", EditorStyles.boldLabel, GUILayout.Width(180));
                EditorGUILayout.LabelField($"🪙 토큰: {user._token:N0}", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
            });

            // ── 토큰 구매 ──
            DrawBox(_styleBoxBlue, $"🪙 토큰 구매 (1토큰 = {_tokenPrice}G)", () =>
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("수량", GUILayout.Width(35));
                _tokenBuyCount = EditorGUILayout.TextField(_tokenBuyCount, GUILayout.Width(55));
                int count = ParseInt(_tokenBuyCount);
                int totalCost = count * _tokenPrice;
                EditorGUILayout.LabelField($"= {totalCost:N0}G", EditorStyles.miniLabel, GUILayout.Width(80));
                if (GUILayout.Button("🪙 구매", GUILayout.Width(75), GUILayout.Height(22)))
                    TestBuyTokens(user);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("빠른구매", EditorStyles.miniLabel, GUILayout.Width(55));
                if (GUILayout.Button("1개", EditorStyles.miniButtonLeft, GUILayout.Width(44))) _tokenBuyCount = "1";
                if (GUILayout.Button("5개", EditorStyles.miniButtonMid, GUILayout.Width(44))) _tokenBuyCount = "5";
                if (GUILayout.Button("10개", EditorStyles.miniButtonMid, GUILayout.Width(44))) _tokenBuyCount = "10";
                if (GUILayout.Button("50개", EditorStyles.miniButtonRight, GUILayout.Width(44))) _tokenBuyCount = "50";
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            });

            // ── 무기 가챠 ──
            DrawBox(_styleBoxPurple, $"🗡 무기 랜덤 뽑기 ({_weaponGachaCost} 토큰)", () =>
            {
                string owned = user._ownedWeaponIds != null && user._ownedWeaponIds.Count > 0
                    ? string.Join(", ", user._ownedWeaponIds) : "(없음)";
                EditorGUILayout.LabelField($"보유: [{owned}]", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"장착: {(string.IsNullOrEmpty(user._equippedWeaponId) ? "(없음)" : user._equippedWeaponId)}", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                var old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.6f, 0.4f, 1f);
                if (GUILayout.Button($"🎲 무기 뽑기! ({_weaponGachaCost} 토큰)", GUILayout.Height(34)))
                    TestGachaWeapon(user);
                GUI.backgroundColor = old;
            });

            // ── 스킨 가챠 ──
            DrawBox(_styleBoxGreen, $"🎨 룰렛 스킨 뽑기 ({_skinGachaCost} 토큰)", () =>
            {
                string owned = user._rouletteSkinIds != null && user._rouletteSkinIds.Count > 0
                    ? string.Join(", ", user._rouletteSkinIds) : "(없음)";
                EditorGUILayout.LabelField($"보유: [{owned}]", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"장착: {(string.IsNullOrEmpty(user._equippedRouletteSkinId) ? "(없음)" : user._equippedRouletteSkinId)}", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                var old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.5f);
                if (GUILayout.Button($"🎲 스킨 뽑기! ({_skinGachaCost} 토큰)", GUILayout.Height(34)))
                    TestGachaSkin(user);
                GUI.backgroundColor = old;
            });

            if (!string.IsNullOrEmpty(_lastGachaResult))
                DrawBox(_styleBoxOrange, "🎰 마지막 뽑기 결과", () => DrawWrappedShopMessage(_lastGachaResult));

            // ── 장착 변경 ──
            DrawBox(_styleBoxBlue, "⚔️ 장착 변경", () =>
            {
                if (user._ownedWeaponIds != null && user._ownedWeaponIds.Count > 0)
                {
                    EditorGUILayout.LabelField("무기 장착", EditorStyles.miniLabel);
                    EditorGUILayout.BeginHorizontal();
                    foreach (var wid in user._ownedWeaponIds)
                    {
                        bool eq = user._equippedWeaponId == wid;
                        if (GUILayout.Button(eq ? $"[{wid}]✓" : wid, GUILayout.Width(64), GUILayout.Height(24)))
                        {
                            UserManager.Instance.EquipWeapon(wid);
                            SetStatus($"⚔️ 무기 [{wid}] 장착!", MessageType.Info);
                        }
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(4);

                if (user._rouletteSkinIds != null && user._rouletteSkinIds.Count > 0)
                {
                    EditorGUILayout.LabelField("스킨 장착", EditorStyles.miniLabel);
                    EditorGUILayout.BeginHorizontal();
                    foreach (var sid in user._rouletteSkinIds)
                    {
                        bool eq = user._equippedRouletteSkinId == sid;
                        if (GUILayout.Button(eq ? $"[{sid}]✓" : sid, GUILayout.Width(70), GUILayout.Height(24)))
                        {
                            UserManager.Instance.EquipRouletteSkin(sid);
                            SetStatus($"🎨 스킨 [{sid}] 장착!", MessageType.Info);
                        }
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            });

            // ── 디버그 충전 + 구석 우하단 숨겨진 구슬 버튼 ──
            DrawBox(_styleBoxRed, "🔧 디버그 충전", () =>
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("💰 +5000G", GUILayout.Width(100), GUILayout.Height(28)))
                { user._gold += 5000; Save(user); SetStatus("골드 +5,000", MessageType.Info); }
                if (GUILayout.Button("🪙 +50토큰", GUILayout.Width(100), GUILayout.Height(28)))
                { user._token += 50; Save(user); SetStatus("토큰 +50", MessageType.Info); }
                if (GUILayout.Button("전부 초기화", GUILayout.Width(100), GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog("경고", "상점 데이터를 전부 초기화합니다.", "확인", "취소"))
                    {
                        user._gold = 0; user._token = 0;
                        user._ownedWeaponIds = new List<string> { "W001" };
                        user._equippedWeaponId = "W001";
                        user._rouletteSkinIds = new List<string>();
                        user._equippedRouletteSkinId = "";
                        _lastGachaResult = "";
                        Save(user);
                        SetStatus("상점 초기화 완료", MessageType.Info);
                    }
                }

                GUILayout.FlexibleSpace();

                // ★ 숨겨진 구슬(●) 버튼 우하단 배치 (상점이 열리지 않았을 때만 렌더링)
                if (!_secretShopUnlocked)
                {
                    var dotStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 11,
                        alignment = TextAnchor.LowerRight,
                        normal = { textColor = new Color(0.35f, 0.35f, 0.35f, 0.4f) } // 극도로 연한 회색 처리
                    };

                    if (GUILayout.Button("●", dotStyle, GUILayout.Width(16), GUILayout.Height(26)))
                    {
                        HandleSecretShopTap();
                    }
                }

                EditorGUILayout.EndHorizontal();
            });

            // ── 비밀 상점 영역 ──
            EditorGUILayout.Space(8);
            DrawSecretShopArea(user);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 비밀 상점 코어 시스템
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawSecretShopArea(UserData user)
        {
            if (!_secretShopUnlocked)
            {
                DrawCommandHelper();
                return;
            }

            DrawSecretShop(user);
        }

        // 구슬 클릭 누적 처리 (조용히 연산됨)
        private void HandleSecretShopTap()
        {
            if (EditorApplication.timeSinceStartup - _lastSecretTapTime > 3.0)
                _secretTapCount = 0;

            _secretTapCount++;
            _lastSecretTapTime = EditorApplication.timeSinceStartup;

            if (_secretTapCount >= 7)
            {
                _secretTapCount = 0;
                _secretShopUnlocked = true;
                EditorPrefs.SetBool(PREF_SECRET_SHOP, true);
                SetStatus("✨ 히든 패널을 발견했습니다!", MessageType.Info);
            }
        }

        // 구슬 버튼이 사라진 자리에 들어갈 치트 가이드 커맨드 UI
        private void DrawCommandHelper()
        {
            EditorGUILayout.Space(6);
            var helperBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8)
            };

            EditorGUILayout.BeginVertical(helperBoxStyle);

            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic,
                richText = true
            };
            labelStyle.normal.textColor = new Color(0.55f, 0.5f, 0.65f);

            // ★ 정리 2: RichText 태그 오타 수정 (</b> -> <b>)
            EditorGUILayout.LabelField("<b>💡 숨겨진 버튼 OR 코나미 코드</b>", labelStyle);

            EditorGUILayout.EndVertical();
        }

        // 비밀 상점 본체
        private void DrawSecretShop(UserData user)
        {
            EditorGUILayout.BeginVertical(_styleBoxPurple);

            // 헤더: 타이틀 + 닫기
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("✨🔮 히든 패널", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            var closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 11 };
            if (GUILayout.Button("✕ 닫기", closeStyle, GUILayout.Width(60), GUILayout.Height(20)))
            {
                // ★ 닫을 시 올 클로즈 (상점 잠금 및 모든 비밀 탭 강제 리셋)
                _secretShopUnlocked = false;
                _secretTapCount = 0;

                _showBalanceTab = false;
                _showSurvivalSimTab = false;
                _showGhostStockTab = false;
                _showGhostSurvivalTab = false;
                _showGhostRaceTab = false;

                // DeleteKey 대신 SetBool(false)로 통일하여 안정성 확보
                EditorPrefs.SetBool(PREF_SECRET_SHOP, false);
                EditorPrefs.SetBool(PREF_SHOW_BALANCE_TAB, false);
                EditorPrefs.SetBool(PREF_SHOW_SURVIVALSIM_TAB, false);
                EditorPrefs.SetBool(PREF_SHOW_GHOSTSTOCK_TAB, false);
                EditorPrefs.SetBool(PREF_SHOW_GHOSTSURVIVAL_TAB, false);
                EditorPrefs.SetBool(PREF_SHOW_GHOSTRACE_TAB, false);

                _currentTab = Tab.Shop;
                SetStatus("🔒 비밀 상점 및 해금되었던 모든 탭이 비활성화 되었습니다.", MessageType.Warning);

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            var infoStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            infoStyle.normal.textColor = Color.gray;
            EditorGUILayout.LabelField("비밀 탭을 자유롭게 활성화/비활성화 할 수 있습니다.", infoStyle);

            EditorGUILayout.Space(6);

            // 탭 열기/닫기 목록
            DrawSecretShopToggleItem("📊 밸런스 뷰어", ref _showBalanceTab, PREF_SHOW_BALANCE_TAB, Tab.BalanceViewer);
            DrawSecretShopToggleItem("🌡️ 생존 시뮬레이터", ref _showSurvivalSimTab, PREF_SHOW_SURVIVALSIM_TAB, Tab.SurvivalSim);
            DrawSecretShopToggleItem("📈 고스트 주식", ref _showGhostStockTab, PREF_SHOW_GHOSTSTOCK_TAB, Tab.GhostStock);
            DrawSecretShopToggleItem("👻 고스트 서바이벌", ref _showGhostSurvivalTab, PREF_SHOW_GHOSTSURVIVAL_TAB, Tab.GhostSurvival);

            // ★ 정리 3: 더블 세미콜론(;;) 오타 수정
            DrawSecretShopToggleItem("🏇 고스트 경마", ref _showGhostRaceTab, PREF_SHOW_GHOSTRACE_TAB, Tab.GhostRace);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        // 아이템 한 줄 (구매 프로세스 없이 바로 토글)
        private void DrawSecretShopToggleItem(
            string label,
            ref bool show,
            string showPref,
            Tab targetTab)
        {
            bool isOpen = show;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var nameStyle = new GUIStyle(EditorStyles.boldLabel);
            nameStyle.normal.textColor = isOpen ? _colGreen : Color.gray;

            EditorGUILayout.LabelField(label, nameStyle, GUILayout.Width(180));

            GUILayout.FlexibleSpace();

            var stateStyle = new GUIStyle(EditorStyles.miniLabel);
            stateStyle.normal.textColor = isOpen ? _colGreen : Color.gray;
            EditorGUILayout.LabelField(isOpen ? "✅ 열림" : "🔒 닫힘", stateStyle, GUILayout.Width(65));

            var old = GUI.backgroundColor;

            if (!isOpen)
            {
                GUI.backgroundColor = new Color(0.15f, 0.65f, 0.25f);

                if (GUILayout.Button("열기", GUILayout.Width(70), GUILayout.Height(24)))
                {
                    show = true;
                    EditorPrefs.SetBool(showPref, true);
                    SetStatus($"{label} 탭이 열렸습니다.", MessageType.Info);
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.7f, 0.25f, 0.25f);

                if (GUILayout.Button("닫기", GUILayout.Width(70), GUILayout.Height(24)))
                {
                    show = false;
                    EditorPrefs.SetBool(showPref, false);

                    if (_currentTab == targetTab)
                        _currentTab = Tab.Shop;

                    SetStatus($"{label} 탭이 닫혔습니다.", MessageType.Warning);
                }
            }

            GUI.backgroundColor = old;

            EditorGUILayout.EndHorizontal();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 기존 유틸
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DrawWrappedShopMessage(string msg)
        {
            var style = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true, fontSize = 13, alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = _colOrange;
            float width = Mathf.Max(420f, EditorGUIUtility.currentViewWidth - 70f);
            float height = Mathf.Clamp(style.CalcHeight(new GUIContent(msg), width), 24f, 90f);
            EditorGUILayout.SelectableLabel(msg, style, GUILayout.Height(height));
        }

        private void TestBuyTokens(UserData user)
        {
            int count = ParseInt(_tokenBuyCount);
            if (count <= 0) { SetStatus("수량을 입력하세요", MessageType.Error); return; }
            int cost = count * _tokenPrice;
            if (!UserManager.Instance.SpendGold(cost)) { SetStatus($"골드 부족! (필요:{cost:N0})", MessageType.Error); return; }
            UserManager.Instance.AddTokens(count);
            _lastGachaResult = $"🪙 토큰 {count}개 구매! -{cost:N0}G / 잔액 {user._gold:N0}G";
            SetStatus(_lastGachaResult, MessageType.Info);
        }

        private void TestGachaWeapon(UserData user)
        {
            if (!UserManager.Instance.SpendTokens(_weaponGachaCost)) { SetStatus("토큰 부족!", MessageType.Error); return; }
            string picked = _weaponPool[Random.Range(0, _weaponPool.Length)];
            UserManager.Instance.AddWeapon(picked, _duplicateRefundGold, out bool isNew);
            _lastGachaResult = isNew ? $"🎉 새 무기 [{picked}] 획득!" : $"🔁 [{picked}] 중복! {_duplicateRefundGold}G 환불";
            SetStatus(_lastGachaResult, isNew ? MessageType.Info : MessageType.Warning);
        }

        private void TestGachaSkin(UserData user)
        {
            if (!UserManager.Instance.SpendTokens(_skinGachaCost)) { SetStatus("토큰 부족!", MessageType.Error); return; }
            string picked = _skinPool[Random.Range(0, _skinPool.Length)];
            UserManager.Instance.AddRouletteSkin(picked, _duplicateRefundGold, out bool isNew);
            _lastGachaResult = isNew ? $"🎉 새 스킨 [{picked}] 획득!" : $"🔁 [{picked}] 중복! {_duplicateRefundGold}G 환불";
            SetStatus(_lastGachaResult, isNew ? MessageType.Info : MessageType.Warning);
        }
    }
}