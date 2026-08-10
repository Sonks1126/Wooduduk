using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase.Leaderboard;

namespace Wooduduk.Editor
{
    public partial class SaveManagerDebugWindow
    {
        private enum SurvivalState { Idle, Loading, Playing, Event, GameOver }
        private SurvivalState _survState = SurvivalState.Idle;

        private class SurvivorData
        {
            public string Name;
            public int Day = 1;
            public int MaxDay = 30;

            public int Hp = 100;
            public int MaxHp = 100;
            public int Hunger = 100;
            public int Thirst = 100;
            public int Warmth = 100;
            public int Sanity = 100;

            public int CurrentHour = 6;
            public int Fatigue = 0;

            public int Wood, Food, Water, Cloth, Medicine, Scrap;

            public bool HasShelter, HasFirepit, HasWaterFilter, HasWeapon;
            public int ShelterLevel, WeaponLevel;

            // 🔥 모닥불 연료 (시간)
            public int FireHours;

            public List<GhostCompanion> Companions = new();
            public int MonstersKilled, EventsSurvived, TotalGoldEarned;
        }

        private class GhostCompanion
        {
            public GhostEntry Ghost;
            public string Role;
            public int Skill;
            public int Morale = 80;
            public bool IsInjured;
            // 👥 오늘의 배치 (채집/사냥/경비/휴식)
            public string Assignment = "휴식";
        }

        private SurvivorData _surv;
        private System.Random _survRng = new();
        private string _survLog = "";
        private List<string> _survLogLines = new();
        private Vector2 _survLogScroll;

        private string _survEventTitle = "";
        private string _survEventDesc = "";
        private List<(string label, System.Action action)> _survChoices = new();
        private List<GhostEntry> _survGhostPool = new();

        // 🌤 날씨 (오늘/내일)
        private string _survWeather = "☀ 맑음";
        private int _survWeatherEffect;
        private string _survTomorrowWeather = "알 수 없음";
        private int _survTomorrowWeatherEffect;

        private int _restHours = 1;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 진입
        private void DrawGhostSurvivalTab(UserData user)
        {
            DrawSurvCloseButton();
            switch (_survState)
            {
                case SurvivalState.Idle: case SurvivalState.Loading: DrawSurvIdle(user); break;
                case SurvivalState.Playing: DrawSurvPlaying(user); break;
                case SurvivalState.Event: DrawSurvEvent(user); break;
                case SurvivalState.GameOver: DrawSurvGameOver(user); break;
            }
        }

        private void DrawSurvCloseButton()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.5f, 0.1f, 0.1f);
            if (GUILayout.Button("✕ 닫기", GUILayout.Width(70), GUILayout.Height(22)))
            {
                _showGhostSurvivalTab = false;
                EditorPrefs.SetBool(PREF_SHOW_GHOSTSURVIVAL_TAB, false);
                _currentTab = Tab.Shop;
                _survState = SurvivalState.Idle;
            }
            GUI.backgroundColor = old;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void DrawSurvIdle(UserData user)
        {
            DrawBox(_styleBoxPurple, "🏕 고스트 서바이벌", () =>
            {
                var ts = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                ts.normal.textColor = _colYellow;
                EditorGUILayout.LabelField("30일간 생존하라!", ts);
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField($"💰 보유 골드: {user._gold:N0} G", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                var info = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
                EditorGUILayout.LabelField("• 안전/위험 행동을 골라 하이리스크 하이리턴", info);
                EditorGUILayout.LabelField("• 모닥불은 장작을 계속 넣어야 유지됩니다", info);
                EditorGUILayout.LabelField("• 야간 활동은 위험합니다", info);
                EditorGUILayout.LabelField("• 동료에게 매일 임무를 지정하세요", info);
                EditorGUILayout.LabelField("• 시작 비용: 100G / 30일 생존 시 보상 지급", info);
                EditorGUILayout.Space(8);
                bool loading = _survState == SurvivalState.Loading;
                var old = GUI.backgroundColor;
                GUI.backgroundColor = user._gold >= 100 ? new Color(0.1f, 0.7f, 0.3f) : Color.gray;
                GUI.enabled = !loading && user._gold >= 100;
                if (GUILayout.Button(loading ? "⏳ 세계 생성 중..." : "🏕 생존 시작! (-100G)", GUILayout.Height(42)))
                    StartSurvival(user);
                GUI.enabled = true; GUI.backgroundColor = old;
            });
        }

        private void StartSurvival(UserData user)
        {
            if (user._gold < 100) return;
            user._gold -= 100; Save(user);
            _survState = SurvivalState.Loading;
            var repo = new GhostRepository();
            repo.LoadCandidates(user._axeTier, entries =>
            {
                _survRng = new System.Random();
                _survGhostPool = entries != null && entries.Count > 0 ? new List<GhostEntry>(entries) : new List<GhostEntry>();
                while (_survGhostPool.Count < 10)
                    _survGhostPool.Add(new GhostEntry { _nick = $"유령{_survGhostPool.Count + 1}호", _score = 300 + _survRng.Next(100, 1500), _tier = user._axeTier });
                for (int i = _survGhostPool.Count - 1; i > 0; i--)
                { int j = _survRng.Next(i + 1); (_survGhostPool[i], _survGhostPool[j]) = (_survGhostPool[j], _survGhostPool[i]); }

                _surv = new SurvivorData { Name = "생존자", Wood = 3, Food = 5, Water = 5 };
                _survLogLines = new List<string>();
                AddSurvLog("🏕 Day 1 — 아침 6시, 당신은 숲에서 깨어났습니다.");
                AddSurvLog("⏰ 행동을 신중하게 고르세요. 안전과 위험 중 선택입니다.");
                RollWeather();
                RollTomorrowWeather();
                _survState = SurvivalState.Playing;
                Repaint();
            });
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 플레이 UI
        private void DrawSurvPlaying(UserData user)
        {
            bool isNight = IsNight();
            string timeIcon = isNight ? "🌙" : "☀️";
            DrawBox(_styleBoxPurple, $"🏕 Day {_surv.Day}/{_surv.MaxDay}  {timeIcon} {_surv.CurrentHour:D2}:00  {_survWeather}", () =>
            {
                string fatigueWarn = _surv.Fatigue >= 100 ? " (⚠ 강제 수면!)" : _surv.Fatigue > 70 ? " (⚠ 위험)" : _surv.Fatigue > 40 ? " (피로)" : "";
                var fStyle = new GUIStyle(EditorStyles.boldLabel);
                fStyle.normal.textColor = _surv.Fatigue >= 100 ? _colRed : _surv.Fatigue > 70 ? _colOrange : _surv.Fatigue > 40 ? _colYellow : _colGreen;
                EditorGUILayout.LabelField($"😴 피로도: {_surv.Fatigue}/100{fatigueWarn}", fStyle);

                // 🌦 내일 날씨 예보
                var forecastStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.7f, 0.7f, 1f) } };
                EditorGUILayout.LabelField($"🔮 내일 예보: {_survTomorrowWeather}", forecastStyle);

                var timeRect = EditorGUILayout.GetControlRect(false, 16);
                float timeRatio = Mathf.Clamp01((float)_surv.CurrentHour / 24f);
                EditorGUI.DrawRect(timeRect, new Color(0.1f, 0.1f, 0.15f));
                Color timeColor = isNight ? new Color(0.3f, 0.3f, 0.8f, 0.6f) : new Color(0.8f, 0.6f, 0.1f, 0.6f);
                EditorGUI.DrawRect(new Rect(timeRect.x, timeRect.y, timeRect.width * timeRatio, timeRect.height), timeColor);
                var tLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                tLabel.normal.textColor = Color.white;
                EditorGUI.LabelField(timeRect, $"{_surv.CurrentHour:D2}:00 / 24:00", tLabel);
                EditorGUILayout.Space(4);

                DrawSurvBar("❤ HP", _surv.Hp, _surv.MaxHp, _colRed);
                DrawSurvBar("🍖 배고픔", _surv.Hunger, 100, _surv.Hunger > 50 ? _colGreen : _surv.Hunger > 20 ? _colYellow : _colRed);
                DrawSurvBar("💧 갈증", _surv.Thirst, 100, _surv.Thirst > 50 ? new Color(0.3f, 0.6f, 1f) : _surv.Thirst > 20 ? _colYellow : _colRed);
                DrawSurvBar("🌡 체온", _surv.Warmth, 100, _surv.Warmth > 60 ? _colGreen : _surv.Warmth > 30 ? _colYellow : _colRed);
                DrawSurvBar("🧠 정신력", _surv.Sanity, 100, _surv.Sanity > 60 ? new Color(0.7f, 0.5f, 1f) : _surv.Sanity > 30 ? _colYellow : _colRed);
            });

            // 자원 + 시설
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            DrawResourceLabel("🪵", "나무", _surv.Wood); DrawResourceLabel("🍖", "음식", _surv.Food);
            DrawResourceLabel("💧", "물", _surv.Water); DrawResourceLabel("🧵", "천", _surv.Cloth);
            DrawResourceLabel("💊", "약", _surv.Medicine); DrawResourceLabel("🔩", "부품", _surv.Scrap);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            var facStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = _colYellow } };
            if (_surv.HasShelter) EditorGUILayout.LabelField($"🏠쉘터Lv{_surv.ShelterLevel}", facStyle, GUILayout.Width(70));
            if (_surv.HasFirepit)
            {
                var fireStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = _surv.FireHours > 0 ? _colOrange : Color.gray } };
                EditorGUILayout.LabelField($"🔥모닥불({_surv.FireHours}h)", fireStyle, GUILayout.Width(85));
            }
            if (_surv.HasWaterFilter) EditorGUILayout.LabelField("🚰정수기", facStyle, GUILayout.Width(55));
            if (_surv.HasWeapon) EditorGUILayout.LabelField($"⚔무기Lv{_surv.WeaponLevel}", facStyle, GUILayout.Width(65));
            EditorGUILayout.EndHorizontal();

            // 👥 동료 + 배치
            if (_surv.Companions.Count > 0)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("👥 동료 배치", EditorStyles.miniBoldLabel);
                foreach (var c in _surv.Companions)
                {
                    EditorGUILayout.BeginHorizontal();
                    var cs = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = c.IsInjured ? _colRed : c.Morale < 30 ? _colYellow : _colGreen } };
                    EditorGUILayout.LabelField($"{c.Ghost._nick}({c.Role}) M:{c.Morale}", cs, GUILayout.Width(150));

                    DrawAssignmentBtn(c, "채집");
                    DrawAssignmentBtn(c, "사냥");
                    DrawAssignmentBtn(c, "경비");
                    DrawAssignmentBtn(c, "휴식");
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
            DrawSurvActions(user);
            EditorGUILayout.Space(4);
            DrawSurvLog();
        }

        private void DrawAssignmentBtn(GhostCompanion c, string assignment)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = c.Assignment == assignment ? new Color(0.3f, 0.7f, 0.4f) : new Color(0.3f, 0.3f, 0.3f);
            if (GUILayout.Button(assignment, EditorStyles.miniButton, GUILayout.Width(40)))
            {
                c.Assignment = assignment;
                AddSurvLog($"  👥 {c.Ghost._nick} → {assignment} 배치");
            }
            GUI.backgroundColor = old;
        }

        private void DrawSurvBar(string label, int current, int max, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(65));
            var rect = EditorGUILayout.GetControlRect(false, 14, GUILayout.Width(130));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.15f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01((float)current / max), rect.height), color);
            var vs = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            vs.normal.textColor = Color.white;
            EditorGUI.LabelField(rect, $" {current}", vs);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawResourceLabel(string icon, string name, int amount)
        {
            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = amount > 0 ? Color.white : Color.gray } };
            EditorGUILayout.LabelField($"{icon}{amount}", style, GUILayout.Width(40));
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 행동 시스템 (안전/위험 분리)
        private void DrawSurvActions(UserData user)
        {
            var old = GUI.backgroundColor;
            bool canAct = _surv.Fatigue < 100;

            if (_surv.Fatigue >= 100)
            {
                EditorGUILayout.HelpBox("⚠ 피로도가 최대입니다! 무조건 잠을 자야 합니다.", MessageType.Warning);
                GUI.backgroundColor = new Color(0.5f, 0.2f, 0.6f);
                if (GUILayout.Button("🌙 강제 취침 (6시간)", GUILayout.Height(36))) DoSurvSleep(user);
                GUI.backgroundColor = old;
                return;
            }

            // === 1행: 나무 (안전/위험) ===
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.25f, 0.6f, 0.3f);
            if (GUILayout.Button("🪵 가지 줍기\n1h 안전", GUILayout.Height(40))) DoActionGatherTwig(user);
            GUI.backgroundColor = new Color(0.5f, 0.4f, 0.1f);
            if (GUILayout.Button("🌲 벌목\n2h 대량/위험", GUILayout.Height(40))) DoActionChopTree(user);
            GUI.backgroundColor = new Color(0.25f, 0.6f, 0.3f);
            if (GUILayout.Button("🍓 열매 채집\n1h 안전", GUILayout.Height(40))) DoActionBerry(user);
            GUI.backgroundColor = new Color(0.7f, 0.2f, 0.1f);
            if (GUILayout.Button("🐗 큰 사냥\n2h 대량/고위험", GUILayout.Height(40))) DoActionBigHunt(user);
            GUI.backgroundColor = old;
            EditorGUILayout.EndHorizontal();

            // === 2행: 물/탐색 ===
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.5f, 0.9f);
            if (GUILayout.Button("💧 물 긷기\n1h 안전", GUILayout.Height(40))) DoActionFetchWater(user);

            bool isRain = _survWeatherEffect == 2;
            GUI.backgroundColor = isRain ? new Color(0.2f, 0.6f, 1f) : Color.gray;
            GUI.enabled = isRain;
            if (GUILayout.Button("🌧 빗물 받기\n1h 대량(비)", GUILayout.Height(40))) DoActionRainwater(user);
            GUI.enabled = true;

            GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
            if (GUILayout.Button("🔍 주변 탐색\n1h 보통", GUILayout.Height(40))) DoSurvExplore(user, false);
            GUI.backgroundColor = new Color(0.4f, 0.3f, 0.6f);
            if (GUILayout.Button("🌑 야간 탐색\n1h 희귀/위험", GUILayout.Height(40))) DoSurvExplore(user, true);
            GUI.backgroundColor = old;
            EditorGUILayout.EndHorizontal();

            // === 3행: 즉시 소비 / 건설 / 장작 ===
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = _surv.Food > 0 ? new Color(0.8f, 0.5f, 0.2f) : Color.gray;
            if (GUILayout.Button($"🍖 먹기 (즉시)", GUILayout.Height(36))) DoSurvEat();
            GUI.backgroundColor = _surv.Water > 0 ? new Color(0.2f, 0.5f, 0.9f) : Color.gray;
            if (GUILayout.Button($"💧 마시기 (즉시)", GUILayout.Height(36))) DoSurvDrink();
            GUI.backgroundColor = _surv.HasFirepit && _surv.Wood > 0 ? new Color(0.9f, 0.5f, 0.1f) : Color.gray;
            if (GUILayout.Button($"🔥 장작 넣기\n(나무1→불+3h)", GUILayout.Height(36))) DoSurvAddFire();
            GUI.backgroundColor = _surv.Wood >= 3 ? new Color(0.6f, 0.4f, 0.2f) : Color.gray;
            if (GUILayout.Button($"🔨 건설 (2시간)", GUILayout.Height(36))) DoSurvBuild(user);
            GUI.backgroundColor = _surv.Medicine > 0 && _surv.Hp < _surv.MaxHp ? new Color(0.2f, 0.8f, 0.4f) : Color.gray;
            if (GUILayout.Button($"💊 치료 (즉시)", GUILayout.Height(36))) DoSurvHeal();
            GUI.backgroundColor = old;
            EditorGUILayout.EndHorizontal();

            // === 4행: 휴식 / 취침 ===
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            _restHours = EditorGUILayout.IntSlider("휴식 시간", _restHours, 1, 4);
            GUI.backgroundColor = new Color(0.3f, 0.3f, 0.6f);
            if (GUILayout.Button($"😴 휴식 ({_restHours}시간)", GUILayout.Height(36))) DoSurvRest(_restHours);
            GUI.backgroundColor = new Color(0.5f, 0.2f, 0.6f);
            if (GUILayout.Button($"🌙 취침 (6시간)\n→ 다음 날", GUILayout.Height(36))) DoSurvSleep(user);
            GUI.backgroundColor = old;
            EditorGUILayout.EndHorizontal();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 시간/컨디션 핵심
        private bool IsNight() => _surv.CurrentHour >= 18 || _surv.CurrentHour < 6;

        private float GetConditionYieldMod()
        {
            float mod = 1f;
            if (_surv.Hunger <= 15) mod -= 0.2f;
            else if (_surv.Hunger <= 30) mod -= 0.1f;
            if (_surv.Thirst <= 15) mod -= 0.25f;
            else if (_surv.Thirst <= 30) mod -= 0.1f;
            if (_surv.Sanity <= 20) mod -= 0.2f;
            return Mathf.Clamp(mod, 0.3f, 1.2f);
        }

        private float GetFatigueYieldMod() => _surv.Fatigue > 70 ? 0.5f : _surv.Fatigue > 40 ? 0.8f : 1.0f;

        private void AdvanceTime(int hours, UserData user)
        {
            _surv.CurrentHour += hours;
            bool dayPassed = false;
            while (_surv.CurrentHour >= 24)
            {
                _surv.CurrentHour -= 24;
                _surv.Day++;
                dayPassed = true;
                // 내일 날씨 → 오늘로
                _survWeather = _survTomorrowWeather;
                _survWeatherEffect = _survTomorrowWeatherEffect;
                RollTomorrowWeather();
                AddSurvLog($"\n🌅 Day {_surv.Day} — {_survWeather}  (내일: {_survTomorrowWeather})");
            }

            bool isNight = IsNight();
            int fatigueBonus = 0;
            if (_surv.Hunger <= 30) fatigueBonus += 2;
            if (_surv.Thirst <= 30) fatigueBonus += 3;
            int fatigueGain = (isNight ? hours * 12 : hours * 8) + fatigueBonus * hours;
            _surv.Fatigue = Mathf.Min(100, _surv.Fatigue + fatigueGain);

            _surv.Hunger = Mathf.Max(0, _surv.Hunger - hours * 3);
            _surv.Thirst = Mathf.Max(0, _surv.Thirst - hours * 4);

            // 🔥 모닥불 연료 소진 + 체온 회복
            if (_surv.HasFirepit && _surv.FireHours > 0)
            {
                int burnedHours = Mathf.Min(_surv.FireHours, hours);
                _surv.FireHours -= burnedHours;
                _surv.Warmth = Mathf.Min(100, _surv.Warmth + burnedHours * 3);
                if (_surv.FireHours == 0) AddSurvLog("  🔥 모닥불이 꺼졌습니다!");
            }

            int warmthDrop = _surv.HasShelter ? hours * 1 : hours * 3;
            if (isNight) warmthDrop += hours * 2;
            if (_survWeatherEffect == 1) warmthDrop += hours * 2;
            if (_survWeatherEffect == 3) warmthDrop += hours * 4;
            _surv.Warmth = Mathf.Max(0, _surv.Warmth - warmthDrop);

            if (_surv.Companions.Count == 0) _surv.Sanity = Mathf.Max(0, _surv.Sanity - hours * 2);
            else _surv.Sanity = Mathf.Min(100, _surv.Sanity + hours);

            // 배고픔/갈증 0 시 HP 감소
            if (_surv.Hunger <= 0) { _surv.Hp = Mathf.Max(0, _surv.Hp - hours * 5); AddSurvLog($"  ⚠ 굶주림으로 HP -{hours * 5}"); }
            if (_surv.Thirst <= 0) { _surv.Hp = Mathf.Max(0, _surv.Hp - hours * 8); AddSurvLog($"  ⚠ 탈수로 HP -{hours * 8}"); }

            if (dayPassed && _surv.Day > _surv.MaxDay)
            {
                int reward = 300 + _surv.Day * 30 + _surv.MonstersKilled * 20;
                user._gold += reward; _surv.TotalGoldEarned += reward; Save(user);
                AddSurvLog($"\n🏆 {_surv.MaxDay}일 생존 성공! +{reward}G!");
                _survState = SurvivalState.GameOver;
                return;
            }

            if (_surv.Fatigue >= 100)
            {
                AddSurvLog("⚠ 피로도 한계! 강제 수면합니다.");
                DoSurvSleep(user);
            }
            else CheckSurvDeath(user);
        }

        // 🌑 야간 리스크
        private void MaybeNightRisk(int hours, UserData user)
        {
            if (!IsNight()) return;
            int chance = 10 + hours * 8;
            if (_surv.HasFirepit && _surv.FireHours > 0) chance -= 5;
            if (_surv.HasShelter) chance -= 3;
            chance = Mathf.Clamp(chance, 5, 40);
            if (_survRng.Next(100) >= chance) return;

            int roll = _survRng.Next(100);
            if (roll < 30) { _surv.Sanity = Mathf.Max(0, _surv.Sanity - 10); AddSurvLog("  🌑 어둠 속 기척... 정신력 -10"); }
            else if (roll < 55) { _surv.Warmth = Mathf.Max(0, _surv.Warmth - 10); AddSurvLog("  🥶 밤공기에 체온이 급락! 체온 -10"); }
            else if (roll < 80) { _surv.Hp = Mathf.Max(0, _surv.Hp - 12); AddSurvLog("  🦇 야간 습격! HP -12"); }
            else { _surv.CurrentHour = Mathf.Min(23, _surv.CurrentHour + 1); AddSurvLog("  🌫 길을 잃었다... 1시간 낭비"); }
            CheckSurvDeath(user);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 안전/위험 행동들
        private void DoActionGatherTwig(UserData user)
        {
            if (_surv.CurrentHour + 1 > 24) { AddSurvLog("⚠ 하루를 넘길 수 없습니다."); return; }
            AdvanceTime(1, user); if (_survState == SurvivalState.GameOver) return;
            float mod = GetFatigueYieldMod() * GetConditionYieldMod();
            int gain = Mathf.Max(1, Mathf.RoundToInt((1 + _survRng.Next(2) + GetAssignmentBonus("채집")) * mod));
            _surv.Wood += gain;
            AddSurvLog($"  🪵 가지를 주웠다. 나무 +{gain} (보유: {_surv.Wood})");
            MaybeNightRisk(1, user);
        }

        private void DoActionChopTree(UserData user)
        {
            if (_surv.CurrentHour + 2 > 24) { AddSurvLog("⚠ 하루를 넘길 수 없습니다."); return; }
            AdvanceTime(2, user); if (_survState == SurvivalState.GameOver) return;
            float mod = GetFatigueYieldMod() * GetConditionYieldMod();
            if (_survRng.Next(100) < 15)
            {
                int dmg = 10 + _survRng.Next(15);
                _surv.Hp = Mathf.Max(0, _surv.Hp - dmg);
                AddSurvLog($"  🪓 벌목 중 사고! HP -{dmg}");
            }
            else
            {
                int gain = Mathf.Max(2, Mathf.RoundToInt((4 + _survRng.Next(4) + GetAssignmentBonus("채집")) * mod));
                _surv.Wood += gain;
                AddSurvLog($"  🌲 벌목 성공! 나무 +{gain} (보유: {_surv.Wood})");
            }
            MaybeNightRisk(2, user);
        }

        private void DoActionBerry(UserData user)
        {
            if (_surv.CurrentHour + 1 > 24) { AddSurvLog("⚠ 하루를 넘길 수 없습니다."); return; }
            AdvanceTime(1, user); if (_survState == SurvivalState.GameOver) return;
            float mod = GetFatigueYieldMod() * GetConditionYieldMod();
            int gain = Mathf.Max(1, Mathf.RoundToInt((1 + _survRng.Next(3) + GetAssignmentBonus("채집")) * mod));
            _surv.Food += gain;
            AddSurvLog($"  🍓 열매를 채집했다. 음식 +{gain} (보유: {_surv.Food})");
            MaybeNightRisk(1, user);
        }

        private void DoActionBigHunt(UserData user)
        {
            if (_surv.CurrentHour + 2 > 24) { AddSurvLog("⚠ 하루를 넘길 수 없습니다."); return; }
            AdvanceTime(2, user); if (_survState == SurvivalState.GameOver) return;

            float mod = GetFatigueYieldMod() * GetConditionYieldMod();
            int weaponBonus = _surv.HasWeapon ? _surv.WeaponLevel * 3 : 0;
            int huntBonus = GetAssignmentBonus("사냥");
            int failChance = Mathf.RoundToInt(30 * (IsNight() ? 1.4f : 1.0f));
            if (!_surv.HasWeapon) failChance += 20;

            if (_survRng.Next(100) < failChance)
            {
                int dmg = 20 + _survRng.Next(25);
                _surv.Hp = Mathf.Max(0, _surv.Hp - dmg);
                AddSurvLog($"  🐗 큰 사냥 실패! 맹수 반격 HP -{dmg}");
            }
            else
            {
                int gain = Mathf.Max(3, Mathf.RoundToInt((7 + _survRng.Next(6) + weaponBonus + huntBonus) * mod));
                _surv.Food += gain;
                _surv.MonstersKilled++;
                AddSurvLog($"  🏹 큰 사냥 성공! 음식 +{gain} (보유: {_surv.Food})");
            }
            MaybeNightRisk(2, user);
        }

        private void DoActionFetchWater(UserData user)
        {
            if (_surv.CurrentHour + 1 > 24) { AddSurvLog("⚠ 하루를 넘길 수 없습니다."); return; }
            AdvanceTime(1, user); if (_survState == SurvivalState.GameOver) return;
            float mod = GetFatigueYieldMod() * GetConditionYieldMod();
            int gain = Mathf.Max(1, Mathf.RoundToInt((2 + _survRng.Next(2) + (_surv.HasWaterFilter ? 2 : 0) + GetAssignmentBonus("채집")) * mod));
            _surv.Water += gain;
            AddSurvLog($"  💧 물을 길었다. +{gain} (보유: {_surv.Water})");
            MaybeNightRisk(1, user);
        }

        private void DoActionRainwater(UserData user)
        {
            if (_survWeatherEffect != 2) { AddSurvLog("⚠ 비가 오지 않습니다."); return; }
            if (_surv.CurrentHour + 1 > 24) { AddSurvLog("⚠ 하루를 넘길 수 없습니다."); return; }
            AdvanceTime(1, user); if (_survState == SurvivalState.GameOver) return;
            int gain = 5 + _survRng.Next(4);
            _surv.Water += gain;
            AddSurvLog($"  🌧 빗물을 대량으로 받았다! 물 +{gain} (보유: {_surv.Water})");
            MaybeNightRisk(1, user);
        }

        // 🔥 장작 넣기
        private void DoSurvAddFire()
        {
            if (!_surv.HasFirepit) { AddSurvLog("⚠ 모닥불이 없습니다."); return; }
            if (_surv.Wood <= 0) { AddSurvLog("⚠ 나무가 없습니다."); return; }
            _surv.Wood--;
            _surv.FireHours = Mathf.Min(12, _surv.FireHours + 3);
            AddSurvLog($"  🔥 장작을 넣었다. 불 지속 {_surv.FireHours}시간");
        }

        // 🔍 탐색 (낮/밤 분리)
        private void DoSurvExplore(UserData user, bool nightMode)
        {
            if (_surv.CurrentHour + 1 > 24) { AddSurvLog("⚠ 하루를 넘길 수 없습니다."); return; }
            AdvanceTime(1, user); if (_survState == SurvivalState.GameOver) return;

            float roll = (float)_survRng.NextDouble();
            bool isNight = IsNight() || nightMode;
            float rareBonus = nightMode ? 0.1f : 0f; // 야간 탐색은 희귀 보상 확률 ↑

            if (roll < 0.15f + rareBonus && _survGhostPool.Count > 0 && _surv.Companions.Count < 3)
            {
                var ghost = _survGhostPool[0]; _survGhostPool.RemoveAt(0);
                string[] roles = { "사냥꾼", "채집가", "의사", "경비" };
                string role = roles[_survRng.Next(roles.Length)];
                int skill = Mathf.Clamp((int)(ghost._score / 300f), 1, 10);
                _survEventTitle = $"👻 {ghost._nick} 발견!";
                _survEventDesc = $"숲에서 길을 잃은 {ghost._nick}을(를) 발견했습니다.\n역할: {role} (스킬 {skill})";
                _survChoices = new List<(string, System.Action)>
                {
                    ("👥 동료로 영입!", () => { _surv.Companions.Add(new GhostCompanion { Ghost = ghost, Role = role, Skill = skill }); AddSurvLog($"  👻 {ghost._nick} 합류!"); _survState = SurvivalState.Playing; }),
                    ("❌ 무시하기", () => { AddSurvLog("  ...지나쳤다."); _survState = SurvivalState.Playing; })
                };
                _survState = SurvivalState.Event; return;
            }
            else if (roll < 0.45f + rareBonus)
            {
                int type = _survRng.Next(4);
                int amt = nightMode ? _survRng.Next(2, 5) : _survRng.Next(1, 3);
                if (type == 0) { _surv.Cloth += amt; AddSurvLog($"  🧵 천 +{amt}"); }
                else if (type == 1) { _surv.Medicine += amt; AddSurvLog($"  💊 약 +{amt}"); }
                else if (type == 2) { _surv.Scrap += amt; AddSurvLog($"  🔩 부품 +{amt}"); }
                else { _surv.Food += amt; AddSurvLog($"  🫐 야생 열매 +{amt}"); }
            }
            else if (roll < 0.70f)
            {
                int guardBonus = GetAssignmentBonus("경비");
                _survEventTitle = "🐺 야생 늑대 출현!";
                int wolfDmg = Mathf.RoundToInt((25 + _survRng.Next(15) - guardBonus * 3) * (isNight ? 1.2f : 1.0f));
                wolfDmg = Mathf.Max(10, wolfDmg);
                _survEventDesc = $"늑대 무리!\n도망: HP -{wolfDmg}, 싸움: 위험하지만 보상이...";
                _survChoices = new List<(string, System.Action)>
                {
                    ("🏃 도망", () => { _surv.Hp = Mathf.Max(0, _surv.Hp - wolfDmg); AddSurvLog($"  🐺 도망! HP -{wolfDmg}"); CheckSurvDeath(user); _survState = SurvivalState.Playing; }),
                    ("⚔ 싸움", () => {
                        int weaponPower = _surv.HasWeapon ? _surv.WeaponLevel * 15 : 0;
                        if (_survRng.Next(100) < 35 + weaponPower + guardBonus * 5) {
                            _surv.Food += 4; _surv.Cloth += 2; _surv.MonstersKilled++; AddSurvLog($"  ⚔ 늑대 승! 🍖+4 🧵+2");
                        } else {
                            int dmg = Mathf.RoundToInt((30 + _survRng.Next(20)) * (isNight ? 1.2f : 1.0f)); _surv.Hp = Mathf.Max(0, _surv.Hp - dmg); AddSurvLog($"  💀 패배! HP -{dmg}");
                        }
                        _surv.EventsSurvived++; CheckSurvDeath(user); _survState = SurvivalState.Playing;
                    })
                };
                _survState = SurvivalState.Event; return;
            }
            else { AddSurvLog("  🔍 별다른 것을 찾지 못했다..."); _surv.Sanity = Mathf.Max(0, _surv.Sanity - 5); }

            if (nightMode) MaybeNightRisk(1, user);
        }

        // 즉시 실행
        private void DoSurvEat() { if (_surv.Food <= 0) return; _surv.Food--; _surv.Hunger = Mathf.Min(100, _surv.Hunger + 25); _surv.Hp = Mathf.Min(_surv.MaxHp, _surv.Hp + 2); AddSurvLog("  🍖 음식 섭취 (즉시)"); }
        private void DoSurvDrink() { if (_surv.Water <= 0) return; _surv.Water--; _surv.Thirst = Mathf.Min(100, _surv.Thirst + 30); AddSurvLog("  💧 물 섭취 (즉시)"); }
        private void DoSurvHeal() { if (_surv.Medicine <= 0 || _surv.Hp >= _surv.MaxHp) return; _surv.Medicine--; int heal = 20 + GetAssignmentBonus("의사") * 5; _surv.Hp = Mathf.Min(_surv.MaxHp, _surv.Hp + heal); AddSurvLog($"  💊 치료 HP +{heal} (즉시)"); foreach (var c in _surv.Companions) if (c.IsInjured) { c.IsInjured = false; break; } }

        private void DoSurvRest(int hours)
        {
            if (_surv.CurrentHour + hours > 24) { AddSurvLog("⚠ 하루를 넘길 수 없습니다."); return; }
            AdvanceTime(hours, null); if (_survState == SurvivalState.GameOver) return;
            int warmthGain = (_surv.HasShelter || (_surv.HasFirepit && _surv.FireHours > 0)) ? hours * 10 : hours * 2;
            _surv.Warmth = Mathf.Min(100, _surv.Warmth + warmthGain);
            _surv.Fatigue = Mathf.Max(0, _surv.Fatigue - hours * 20);
            _surv.Sanity = Mathf.Min(100, _surv.Sanity + hours * 5);
            AddSurvLog($"  😴 {hours}시간 휴식! 피로도 -{hours * 20}, 체온 +{warmthGain}");
        }

        private void DoSurvSleep(UserData user)
        {
            AddSurvLog($"\n🌙 취침... (6시간)");
            _surv.CurrentHour += 6;
            if (_surv.CurrentHour >= 24)
            {
                _surv.CurrentHour -= 24; _surv.Day++;
                _survWeather = _survTomorrowWeather; _survWeatherEffect = _survTomorrowWeatherEffect;
                RollTomorrowWeather();
                AddSurvLog($"🌅 Day {_surv.Day} — {_survWeather}");
            }
            _surv.Fatigue = 0;
            _surv.Sanity = Mathf.Min(100, _surv.Sanity + 15);

            if (_surv.HasShelter || (_surv.HasFirepit && _surv.FireHours > 0))
            {
                _surv.Warmth = Mathf.Min(100, _surv.Warmth + 20);
                _surv.Hp = Mathf.Min(_surv.MaxHp, _surv.Hp + 5);
            }
            else
            {
                _surv.Warmth = Mathf.Max(0, _surv.Warmth - 15);
                _surv.Hp = Mathf.Max(0, _surv.Hp - 10);
                AddSurvLog("🥶 쉘터/불 없이 잠들어 저체온증! HP -10");
            }

            if (_surv.HasFirepit) _surv.FireHours = Mathf.Max(0, _surv.FireHours - 6);

            foreach (var c in _surv.Companions)
            {
                if (_surv.Food > 0 && _surv.Water > 0) { _surv.Food--; _surv.Water--; }
                else { c.Morale -= 20; AddSurvLog($"  💔 {c.Ghost._nick} 식량 부족! 모랄 -20"); }
            }

            _surv.Hunger = Mathf.Max(0, _surv.Hunger - 10);
            _surv.Thirst = Mathf.Max(0, _surv.Thirst - 10);

            if (_surv.Day > _surv.MaxDay)
            {
                int reward = 300 + _surv.Day * 30 + _surv.MonstersKilled * 20;
                user._gold += reward; _surv.TotalGoldEarned += reward; Save(user);
                AddSurvLog($"\n🏆 {_surv.MaxDay}일 생존 성공! +{reward}G!");
                _survState = SurvivalState.GameOver; return;
            }

            List<GhostCompanion> toRemove = new();
            foreach (var c in _surv.Companions) if (c.Morale <= 0) { AddSurvLog($"  💔 {c.Ghost._nick} 이탈!"); toRemove.Add(c); }
            foreach (var c in toRemove) _surv.Companions.Remove(c);

            if (_survRng.Next(100) < 35) TriggerNightEvent(user);
            CheckSurvDeath(user);
        }

        private void DoSurvBuild(UserData user)
        {
            if (_surv.CurrentHour + 2 > 24) { AddSurvLog("⚠ 건설은 2시간이 필요합니다."); return; }
            _survEventTitle = "🔨 건설";
            _survEventDesc = "무엇을 건설하시겠습니까?";
            _survChoices = new List<(string, System.Action)>();

            if (!_surv.HasShelter && _surv.Wood >= 5)
                _survChoices.Add(("🏠 쉘터 (나무5, 2h)", () => { _surv.Wood -= 5; AdvanceTime(2, user); _surv.HasShelter = true; _surv.ShelterLevel = 1; AddSurvLog("  🏠 쉘터 건설!"); _survState = SurvivalState.Playing; }));
            if (_surv.HasShelter && _surv.ShelterLevel < 3 && _surv.Wood >= 5 + _surv.ShelterLevel * 3)
                _survChoices.Add(($"🏠 쉘터업글 (나무{5 + _surv.ShelterLevel * 3}, 2h)", () => { int c = 5 + _surv.ShelterLevel * 3; _surv.Wood -= c; AdvanceTime(2, user); _surv.ShelterLevel++; _surv.MaxHp += 10; AddSurvLog($"  🏠 쉘터 Lv{_surv.ShelterLevel}!"); _survState = SurvivalState.Playing; }));
            if (!_surv.HasFirepit && _surv.Wood >= 3)
                _survChoices.Add(("🔥 모닥불 (나무3, 2h)", () => { _surv.Wood -= 3; AdvanceTime(2, user); _surv.HasFirepit = true; _surv.FireHours = 3; AddSurvLog("  🔥 모닥불 설치! (3시간 분량)"); _survState = SurvivalState.Playing; }));
            if (!_surv.HasWaterFilter && _surv.Scrap >= 3 && _surv.Cloth >= 2)
                _survChoices.Add(("🚰 정수기 (부품3+천2, 2h)", () => { _surv.Scrap -= 3; _surv.Cloth -= 2; AdvanceTime(2, user); _surv.HasWaterFilter = true; AddSurvLog("  🚰 정수기 완성!"); _survState = SurvivalState.Playing; }));
            if (!_surv.HasWeapon && _surv.Wood >= 3 && _surv.Scrap >= 2)
                _survChoices.Add(("⚔ 무기 (나무3+부품2, 2h)", () => { _surv.Wood -= 3; _surv.Scrap -= 2; AdvanceTime(2, user); _surv.HasWeapon = true; _surv.WeaponLevel = 1; AddSurvLog("  ⚔ 무기 제작!"); _survState = SurvivalState.Playing; }));
            _survChoices.Add(("← 취소", () => { _survState = SurvivalState.Playing; }));
            _survState = SurvivalState.Event;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 날씨/이벤트
        private void RollWeather()
        {
            int w = _survRng.Next(100);
            if (w < 30) { _survWeather = "☀ 맑음"; _survWeatherEffect = 0; }
            else if (w < 45) { _survWeather = "☁ 흐림"; _survWeatherEffect = 0; }
            else if (w < 65) { _survWeather = "🌧 비"; _survWeatherEffect = 2; }
            else if (w < 85) { _survWeather = "❄ 추움"; _survWeatherEffect = 1; }
            else { _survWeather = "🌪 폭풍"; _survWeatherEffect = 3; }
        }

        private void RollTomorrowWeather()
        {
            int w = _survRng.Next(100);
            if (w < 30) { _survTomorrowWeather = "☀ 맑음"; _survTomorrowWeatherEffect = 0; }
            else if (w < 45) { _survTomorrowWeather = "☁ 흐림"; _survTomorrowWeatherEffect = 0; }
            else if (w < 65) { _survTomorrowWeather = "🌧 비"; _survTomorrowWeatherEffect = 2; }
            else if (w < 85) { _survTomorrowWeather = "❄ 추움"; _survTomorrowWeatherEffect = 1; }
            else { _survTomorrowWeather = "🌪 폭풍"; _survTomorrowWeatherEffect = 3; }
        }

        private void TriggerNightEvent(UserData user)
        {
            int guardBonus = GetAssignmentBonus("경비");
            int roll = _survRng.Next(100);
            if (roll < 45)
            {
                int stolen = Mathf.Max(0, _survRng.Next(4) - guardBonus);
                if (stolen > 0 && _surv.Food > 0) { int lost = Mathf.Min(stolen, _surv.Food); _surv.Food -= lost; AddSurvLog($"  🦝 야간 도둑! 음식 -{lost}"); }
                else if (guardBonus > 0) AddSurvLog("  🛡 경비가 침입자를 막았다!");
            }
            else if (roll < 80)
            {
                int dmg = Mathf.Max(0, 20 + _survRng.Next(15) - guardBonus * 5);
                if (dmg > 0) { _surv.Hp = Mathf.Max(0, _surv.Hp - dmg); AddSurvLog($"  🐺 야간 습격! HP -{dmg}"); }
                else AddSurvLog("  🛡 경비 덕분에 안전한 밤!");
            }
            else { _surv.Sanity = Mathf.Min(100, _surv.Sanity + 10); AddSurvLog("  🌙 별이 예쁜 밤... 정신력 +10"); }
            CheckSurvDeath(user);
        }

        // 👥 Assignment 기반 보너스
        private int GetAssignmentBonus(string assignment)
        {
            int bonus = 0;
            foreach (var c in _surv.Companions)
                if (c.Assignment == assignment && !c.IsInjured && c.Morale > 20)
                    bonus += c.Skill;
            return bonus;
        }

        private void DrawSurvEvent(UserData user)
        {
            DrawBox(_styleBoxPurple, _survEventTitle, () =>
            {
                var ds = new GUIStyle(EditorStyles.label) { fontSize = 12, wordWrap = true };
                ds.normal.textColor = Color.white;
                EditorGUILayout.LabelField(_survEventDesc, ds);
                EditorGUILayout.Space(8);
                var old = GUI.backgroundColor;
                for (int i = 0; i < _survChoices.Count; i++)
                {
                    var (label, action) = _survChoices[i];
                    GUI.backgroundColor = i == _survChoices.Count - 1 ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.2f, 0.5f, 0.8f);
                    if (GUILayout.Button(label, GUILayout.Height(32))) action?.Invoke();
                }
                GUI.backgroundColor = old;
            });
            DrawSurvLog();
        }

        private void CheckSurvDeath(UserData user)
        {
            if (_surv != null && _surv.Hp <= 0) { AddSurvLog("\n💀 사망했습니다..."); _survState = SurvivalState.GameOver; }
        }

        private void DrawSurvGameOver(UserData user)
        {
            bool survived = _surv.Day > _surv.MaxDay;
            DrawBox(survived ? _styleBoxGreen : _styleBoxRed, survived ? "🏆 생존 성공!" : "💀 사망", () =>
            {
                var ts = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                ts.normal.textColor = survived ? _colGreen : _colRed;
                EditorGUILayout.LabelField(survived ? $"🏆 {_surv.MaxDay}일 생존 성공!" : $"💀 Day {_surv.Day}에 사망", ts);
                EditorGUILayout.Space(6);
                var info = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.gray } };
                EditorGUILayout.LabelField($"  생존 일수: {_surv.Day}일", info);
                EditorGUILayout.LabelField($"  처치한 적: {_surv.MonstersKilled}", info);
                EditorGUILayout.LabelField($"  동료: {_surv.Companions.Count}명", info);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"💰 현재 골드: {user._gold:N0}G", EditorStyles.boldLabel);
                EditorGUILayout.Space(8);
                var old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.1f, 0.7f, 0.3f);
                if (GUILayout.Button("🏕 다시 도전! (-100G)", GUILayout.Height(36))) StartSurvival(user);
                GUI.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
                if (GUILayout.Button("처음으로", GUILayout.Height(26))) _survState = SurvivalState.Idle;
                GUI.backgroundColor = old;
            });
            DrawSurvLog();
        }

        private void AddSurvLog(string text) { _survLogLines.Add(text); if (_survLogLines.Count > 80) _survLogLines.RemoveAt(0); _survLog = string.Join("\n", _survLogLines); }
        private void DrawSurvLog()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("📜 기록", EditorStyles.boldLabel);
            _survLogScroll = EditorGUILayout.BeginScrollView(_survLogScroll, EditorStyles.helpBox, GUILayout.Height(140));
            var logStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.8f, 0.85f, 0.9f) } };
            EditorGUILayout.LabelField(_survLog, logStyle, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }
}