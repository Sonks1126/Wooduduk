using UnityEditor;
using UnityEngine;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase;
using Wooduduk.Tier;

namespace Wooduduk.Editor
{
    public partial class SaveManagerDebugWindow
    {
        private void DrawFirebaseTab(UserData user)
        {
            DrawTierCard(user);

            DrawBox(_styleBoxBlue, "💰 골드 / 🎬 점수 조작", () =>
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("골드", GUILayout.Width(35));
                _goldInput = EditorGUILayout.TextField(_goldInput, GUILayout.Width(70));
                if (GUILayout.Button("➕", GUILayout.Width(32), GUILayout.Height(22))) TestAddGold(user);
                if (GUILayout.Button("➖", GUILayout.Width(32), GUILayout.Height(22))) TestSubGold(user);
                GUILayout.Space(6);
                if (GUILayout.Button("+100", EditorStyles.miniButtonLeft, GUILayout.Width(45))) _goldInput = "100";
                if (GUILayout.Button("+1천", EditorStyles.miniButtonMid, GUILayout.Width(45))) _goldInput = "1000";
                if (GUILayout.Button("+1만", EditorStyles.miniButtonRight, GUILayout.Width(45))) _goldInput = "10000";
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("점수", GUILayout.Width(35));
                _scoreInput = EditorGUILayout.TextField(_scoreInput, GUILayout.Width(70));
                if (GUILayout.Button("💾 TOP3 저장", GUILayout.Width(100), GUILayout.Height(22))) TestSaveScore(user);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            });

            DrawBox(_styleBoxGreen, "❤️ 체력 시뮬레이션", () =>
            {
                DrawHPBar();
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("값", GUILayout.Width(20));
                _hpInput = EditorGUILayout.TextField(_hpInput, GUILayout.Width(50));
                if (GUILayout.Button("회복", GUILayout.Width(50), GUILayout.Height(22))) TestAddHP();
                if (GUILayout.Button("피해", GUILayout.Width(50), GUILayout.Height(22))) TestSubHP();
                GUILayout.Space(8);
                if (GUILayout.Button(_isInvincible ? "🛡 ON" : "🛡 OFF", GUILayout.Width(60), GUILayout.Height(22)))
                {
                    _isInvincible = !_isInvincible;
                    SetStatus(_isInvincible ? "무적 활성화" : "무적 해제", MessageType.Info);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            });

            DrawBox(_styleBoxRed, "🗑 초기화", () =>
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("로컬 초기화", GUILayout.Width(100), GUILayout.Height(26)))
                {
                    if (EditorUtility.DisplayDialog("확인", "로컬 데이터를 0으로.", "실행", "취소"))
                        TestResetAll(user);
                }
                var old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("⚠️ DB 삭제+재생성", GUILayout.Width(160), GUILayout.Height(26)))
                {
                    if (EditorUtility.DisplayDialog("경고", "Firebase 노드 통째 삭제.", "삭제", "취소"))
                        TestFirebaseWipe(user);
                }
                GUI.backgroundColor = old;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            });
        }

        private void DrawTierCard(UserData user)
        {
            DrawBox(_styleBoxBlue, "🏆 티어 진단", () =>
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("📦 DB", GUILayout.Width(40));
                EditorGUILayout.LabelField($"{(AxeTier)user._axeTier} (LP {user._tierLp})", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                if (TierService.Instance == null)
                {
                    EditorGUILayout.HelpBox("❌ TierService 없음", MessageType.Error);
                    return;
                }

                var s = TierService.Instance.CurrentState;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("🧠 메모리", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{s.Rank} (LP {s.Lp})", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                if (user._axeTier != (int)s.Rank || user._tierLp != s.Lp)
                    EditorGUILayout.HelpBox("⚠️ DB ≠ 메모리", MessageType.Warning);

                EditorGUILayout.Space(3);
                EditorGUILayout.BeginHorizontal();
                _tierTestScore = EditorGUILayout.TextField(_tierTestScore, GUILayout.Width(80));
                if (GUILayout.Button("ApplyRun", GUILayout.Width(85), GUILayout.Height(22))) TestTierApplyRun(user);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            });
        }

        private void TestAddGold(UserData user)
        {
            if (!int.TryParse(_goldInput, out int amt) || amt <= 0)
            {
                SetStatus("숫자 오류", MessageType.Error);
                return;
            }
            user._gold += amt;
            Save(user);
            SetStatus($"골드 +{amt:N0} (현재 {user._gold:N0})", MessageType.Info);
        }

        private void TestSubGold(UserData user)
        {
            if (!int.TryParse(_goldInput, out int amt) || amt <= 0)
            {
                SetStatus("숫자 오류", MessageType.Error);
                return;
            }
            int b = user._gold;
            user._gold = Mathf.Max(0, user._gold - amt);
            Save(user);
            SetStatus($"골드 -{b - user._gold:N0} (현재 {user._gold:N0})", MessageType.Info);
        }

        private void TestSaveScore(UserData user)
        {
            if (!int.TryParse(_scoreInput, out int score) || score <= 0)
            {
                SetStatus("점수 오류", MessageType.Error);
                return;
            }
            int o1 = SafeScore(user, 0), o2 = SafeScore(user, 1), o3 = SafeScore(user, 2);
            user._latestScore = score;
            bool nr = false;
            if (score > o1) { user._bestScores[2] = o2; user._bestScores[1] = o1; user._bestScores[0] = score; nr = true; }
            else if (score > o2) { user._bestScores[2] = o2; user._bestScores[1] = score; }
            else if (score > o3) { user._bestScores[2] = score; }
            Save(user);
            SetStatus(nr ? $"🎉 신기록 {score:N0}! 1위" : $"{score:N0}점 저장", MessageType.Info);
        }

        private void TestAddHP()
        {
            if (!float.TryParse(_hpInput, out float a) || a <= 0) return;
            _simHP = Mathf.Min(_simMaxHP, _simHP + a);
        }

        private void TestSubHP()
        {
            if (!float.TryParse(_hpInput, out float a) || a <= 0 || _isInvincible) return;
            _simHP = Mathf.Max(0, _simHP - a);
            if (_simHP <= 0) SetStatus("💀 사망!", MessageType.Error);
        }

        private void TestResetAll(UserData user)
        {
            user._gold = 0;
            user._token = 0;
            user._latestScore = 0;
            user._bestScores = new int[3];
            _simHP = 100f;
            _isInvincible = false;
            Save(user);
            SetStatus("로컬 초기화 완료", MessageType.Info);
        }

        private void TestFirebaseWipe(UserData user)
        {
            SetStatus("🔥 DB 삭제 중...", MessageType.Warning);
            SaveManager.Instance.DeleteAndRecreate(user._userId);
        }

        private void TestTierApplyRun(UserData user)
        {
            if (TierService.Instance == null) return;
            if (int.TryParse(_tierTestScore, out int s))
                TierService.Instance.ApplyRun(new Wooduduk.Tier.RunResult(s));
        }
    }
}