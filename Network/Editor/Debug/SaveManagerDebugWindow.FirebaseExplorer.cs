using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wooduduk.Data.Static;

namespace Wooduduk.Editor
{
    public partial class SaveManagerDebugWindow
    {
        #region Fields

        private string _fbPath = "rooms";
        private string _fbPathInput = "rooms";
        private DataSnapshot _fbSnapshot;
        private bool _fbLoading;
        private string _fbError;
        private bool _fbLiveOn;
        private EventHandler<ValueChangedEventArgs> _fbLiveHandler;
        private DataSnapshot _fbLiveSnapshot;
        private readonly Dictionary<string, bool> _fbFoldouts = new();
        private string _fbDeleteConfirm;
        private Vector2 _fbTreeScroll;
        private int _fbRowIndex;

        // ★ "users"를 소문자로 정식 퀵 경로에 추가
        private static readonly string[] FbQuickPaths = { "rooms", "Users", "ghosts", "leaderboard" };
        private const float FbBtnHeight = 24f;
        private const float FbIndentPerDepth = 16f;
        private const float FbTreeMaxHeight = 420f;

        private GUIStyle _fbFoldoutStyleCache;
        private GUIStyle _fbKeyStyleCache;
        private GUIStyle _fbValueStyleCache;

        #endregion

        #region Draw - Root

        private void DrawFirebaseExplorerTab(UserData user)
        {
            DrawFbControls(user);
            EditorGUILayout.Space(6);
            DrawFbContent(user);
        }

        private void DrawFbContent(UserData user)
        {
            if (_fbLiveOn && _fbLiveSnapshot != null)
            {
                DrawFbTree(_fbLiveSnapshot, "🔴 LIVE");
            }
            else if (_fbLoading)
            {
                DrawFbCenteredMessage("⏳ 로딩 중...", _colYellow, EditorStyles.boldLabel);
            }
            else if (!string.IsNullOrEmpty(_fbError))
            {
                DrawFbErrorBox(_fbError, user);
            }
            else if (_fbSnapshot != null)
            {
                DrawFbTree(_fbSnapshot, "📸 스냅샷");
            }
            else
            {
                DrawFbCenteredMessage("경로를 입력하고 📥 조회 또는 ▶ LIVE를 눌러주세요.", Color.gray, EditorStyles.miniLabel);
            }
        }

        #endregion

        #region Draw - Controls

        private void DrawFbControls(UserData user)
        {
            DrawBox(_styleBoxBlue, "📡 Firebase 탐색기", () =>
            {
                DrawFbQuickPathButtons(user);
                EditorGUILayout.Space(4);
                DrawFbPathInputRow();
                EditorGUILayout.Space(4);
                DrawFbBreadcrumb();
            });
        }

        private void DrawFbQuickPathButtons(UserData user)
        {
            EditorGUILayout.BeginHorizontal();

            // 기본 퀵 버튼들 (rooms, Users, ghosts, leaderboard)
            foreach (var quickPath in FbQuickPaths)
            {
                bool isActive = _fbPath == quickPath || _fbPath.StartsWith(quickPath + "/");
                // "Users" 버튼만 관리자 느낌이 나도록 보라/핑크톤으로 강조
                Color color = quickPath == "Users"
                    ? new Color(0.8f, 0.3f, 0.7f)
                    : (isActive ? new Color(0.15f, 0.85f, 0.5f) : new Color(0.25f, 0.6f, 1f));

                WithColor(color, () =>
                {
                    string label = quickPath == "Users" ? "👑 유저목록(Users)" : quickPath;
                    if (GUILayout.Button(label, GUILayout.Height(FbBtnHeight)))
                        SetFbPath(quickPath);
                });
            }

            // ★ 본인 UID 데이터로 바로 이동 (소문자 users 완벽 적용)
            if (user != null && !string.IsNullOrEmpty(user._userId))
            {
                WithColor(new Color(1f, 0.65f, 0.15f), () =>
                {
                    if (GUILayout.Button("👤 내 데이터", GUILayout.Height(FbBtnHeight)))
                        SetFbPath($"Users/{user._userId}");
                });
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFbPathInputRow()
        {
            EditorGUILayout.BeginHorizontal();

            _fbPathInput = EditorGUILayout.TextField(_fbPathInput, GUILayout.Height(FbBtnHeight));

            WithColor(new Color(0.3f, 0.8f, 1f), () =>
            {
                if (GUILayout.Button("📥 조회", GUILayout.Width(70), GUILayout.Height(FbBtnHeight)))
                    FbFetch(_fbPathInput);
            });

            if (!_fbLiveOn && _fbSnapshot != null)
            {
                WithColor(new Color(0.6f, 0.6f, 0.6f), () =>
                {
                    if (GUILayout.Button("🔄", GUILayout.Width(30), GUILayout.Height(FbBtnHeight)))
                        FbFetch(_fbPath);
                });
            }

            WithColor(_fbLiveOn ? _colRed : new Color(0.2f, 0.9f, 0.4f), () =>
            {
                string liveLabel = _fbLiveOn ? "⏹ LIVE 중" : "▶ LIVE";
                if (GUILayout.Button(liveLabel, GUILayout.Width(80), GUILayout.Height(FbBtnHeight)))
                    ToggleFbLive(_fbPathInput);
            });

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFbBreadcrumb()
        {
            EditorGUILayout.BeginHorizontal();

            var segments = _fbPath.Split('/');
            string accumulated = "";
            var crumbStyle = new GUIStyle(EditorStyles.miniButton) { fontSize = 10 };

            EditorGUILayout.LabelField("📍", GUILayout.Width(18));

            for (int i = 0; i < segments.Length; i++)
            {
                if (string.IsNullOrEmpty(segments[i])) continue;

                accumulated = string.IsNullOrEmpty(accumulated) ? segments[i] : $"{accumulated}/{segments[i]}";
                string targetPath = accumulated;

                if (GUILayout.Button(segments[i], crumbStyle, GUILayout.ExpandWidth(false)))
                    SetFbPath(targetPath);

                if (i < segments.Length - 1)
                    EditorGUILayout.LabelField("/", GUILayout.Width(8));
            }

            GUILayout.FlexibleSpace();

            if (_fbLiveOn)
            {
                var liveStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = _colRed },
                    fontStyle = FontStyle.Bold
                };
                EditorGUILayout.LabelField("🔴 실시간 구독 중", liveStyle, GUILayout.Width(110));
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Draw - Tree

        private void DrawFbTree(DataSnapshot snap, string label)
        {
            DrawBox(_styleBoxGreen, $"🌲 {label} · 총 {snap.ChildrenCount}개", () =>
            {
                if (!snap.Exists)
                {
                    DrawFbCenteredMessage("(데이터 없음)", Color.gray, EditorStyles.miniLabel);
                    return;
                }

                _fbRowIndex = 0;
                _fbTreeScroll = EditorGUILayout.BeginScrollView(_fbTreeScroll, GUILayout.MaxHeight(FbTreeMaxHeight));
                DrawFbNode(snap, _fbPath, 0);
                EditorGUILayout.EndScrollView();
            });
        }

        private void DrawFbNode(DataSnapshot snap, string fullPath, int depth)
        {
            if (!snap.Exists) return;

            if (snap.HasChildren)
                DrawFbFolderNode(snap, fullPath, depth);
            else
                DrawFbLeafNode(snap, fullPath, depth);
        }

        private void DrawFbFolderNode(DataSnapshot snap, string fullPath, int depth)
        {
            if (!_fbFoldouts.ContainsKey(fullPath))
                _fbFoldouts[fullPath] = depth < 2;

            var rowRect = EditorGUILayout.BeginHorizontal();
            DrawFbZebraBackground(rowRect);
            GUILayout.Space(depth * FbIndentPerDepth);

            var foldoutStyle = GetFbFoldoutStyle(depth);

            _fbFoldouts[fullPath] = EditorGUILayout.Foldout(
                _fbFoldouts[fullPath],
                $"📁 {snap.Key}  ({snap.ChildrenCount})",
                true, foldoutStyle);

            GUILayout.FlexibleSpace();
            DrawFbDeleteBtn(fullPath);
            EditorGUILayout.EndHorizontal();

            if (_fbFoldouts[fullPath])
            {
                foreach (var child in snap.Children)
                    DrawFbNode(child, $"{fullPath}/{child.Key}", depth + 1);
            }
        }

        private void DrawFbLeafNode(DataSnapshot snap, string fullPath, int depth)
        {
            var rowRect = EditorGUILayout.BeginHorizontal();
            DrawFbZebraBackground(rowRect);
            GUILayout.Space(depth * FbIndentPerDepth);

            var keyStyle = GetFbKeyStyle();
            EditorGUILayout.LabelField(snap.Key, keyStyle, GUILayout.Width(Mathf.Max(60, 160 - depth * FbIndentPerDepth)));

            string val = snap.Value?.ToString() ?? "null";
            var valueStyle = GetFbValueStyle(val);
            EditorGUILayout.LabelField(val, valueStyle);

            GUILayout.FlexibleSpace();
            DrawFbDeleteBtn(fullPath);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFbZebraBackground(Rect rowRect)
        {
            if (Event.current.type != EventType.Repaint) return;

            if (_fbRowIndex % 2 == 0)
                EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.035f));

            _fbRowIndex++;
        }

        #endregion

        #region Draw - Shared UI Bits

        private void DrawFbDeleteBtn(string path)
        {
            if (_fbDeleteConfirm == path)
            {
                WithColor(_colRed, () =>
                {
                    if (GUILayout.Button("⚠️ 삭제 확정", GUILayout.Width(80), GUILayout.Height(18)))
                    {
                        FbDelete(path);
                        _fbDeleteConfirm = null;
                    }
                });
                WithColor(Color.gray, () =>
                {
                    if (GUILayout.Button("취소", GUILayout.Width(40), GUILayout.Height(18)))
                        _fbDeleteConfirm = null;
                });
            }
            else
            {
                WithColor(new Color(0.5f, 0.15f, 0.15f), () =>
                {
                    if (GUILayout.Button("🗑", GUILayout.Width(26), GUILayout.Height(18)))
                        _fbDeleteConfirm = path;
                });
            }
        }

        private void DrawFbCenteredMessage(string text, Color color, GUIStyle baseStyle)
        {
            var style = new GUIStyle(baseStyle) { normal = { textColor = color }, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(text, style);
            EditorGUILayout.Space(8);
        }

        private void DrawFbErrorBox(string message, UserData user)
        {
            bool isPermissionDenied = message.IndexOf("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0;

            var style = new GUIStyle(EditorStyles.helpBox)
            {
                wordWrap = true,
                normal = { textColor = _colRed }
            };
            EditorGUILayout.LabelField($"❌ {message}", style);

            if (!isPermissionDenied) return;

            EditorGUILayout.Space(4);

            var hintStyle = new GUIStyle(EditorStyles.helpBox) { wordWrap = true };
            EditorGUILayout.LabelField(
                "🔒 이 노드는 보안 규칙상 전체 조회가 금지되어 있습니다.\n" +
                "(예: Users/$uid 구조는 관리자 계정 또는 본인 UID만 읽기 허용)\n" +
                "관리자 UID 등록이 안 되어 있다면 아래 버튼으로 본인 데이터만 조회하세요.",
                hintStyle);

            if (user != null && !string.IsNullOrEmpty(user._userId))
            {
                EditorGUILayout.Space(2);
                WithColor(new Color(1f, 0.65f, 0.15f), () =>
                {
                    // ★ 여기도 소문자 Users 로 완벽 수정
                    if (GUILayout.Button("👤 내 데이터로 대신 조회", GUILayout.Height(FbBtnHeight)))
                        SetFbPath($"Users/{user._userId}");
                });
            }
        }

        private void WithColor(Color color, Action drawAction)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = color;
            drawAction?.Invoke();
            GUI.backgroundColor = old;
        }

        private GUIStyle GetFbFoldoutStyle(int depth)
        {
            _fbFoldoutStyleCache ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            _fbFoldoutStyleCache.normal.textColor = depth switch
            {
                0 => _colGreen,
                1 => _colBlue,
                _ => Color.white
            };
            return _fbFoldoutStyleCache;
        }

        private GUIStyle GetFbKeyStyle()
        {
            _fbKeyStyleCache ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = _colBlue } };
            return _fbKeyStyleCache;
        }

        private GUIStyle GetFbValueStyle(string val)
        {
            _fbValueStyleCache ??= new GUIStyle(EditorStyles.miniLabel);
            _fbValueStyleCache.normal.textColor = val switch
            {
                "null" => Color.gray,
                "true" => _colGreen,
                "false" => _colRed,
                _ => _colYellow
            };
            return _fbValueStyleCache;
        }

        #endregion

        #region Actions

        private void SetFbPath(string path)
        {
            _fbPathInput = path;
            FbFetch(path);
        }

        private void FbFetch(string path)
        {
            _fbPath = path;
            _fbSnapshot = null;
            _fbError = null;
            _fbLoading = true;
            _fbDeleteConfirm = null;

            FirebaseDatabase.DefaultInstance.RootReference
                .Child(path)
                .GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    _fbLoading = false;

                    if (task.IsFaulted || task.IsCanceled)
                    {
                        _fbError = task.Exception?.GetBaseException().Message ?? "알 수 없는 오류";
                        Repaint();
                        return;
                    }

                    _fbSnapshot = task.Result;
                    Repaint();
                });
        }

        private void ToggleFbLive(string path)
        {
            if (_fbLiveOn)
            {
                FbExplorerCleanup();
                SetStatus("⏹ LIVE 구독 중단", MessageType.Info);
                return;
            }

            _fbPath = path;
            _fbLiveOn = true;
            _fbLiveHandler = (_, args) =>
            {
                if (!args.Snapshot.Exists) return;
                _fbLiveSnapshot = args.Snapshot;
                Repaint();
            };

            FirebaseDatabase.DefaultInstance.RootReference
                .Child(path).ValueChanged += _fbLiveHandler;

            SetStatus($"🔴 LIVE 구독 시작: /{path}", MessageType.Info);
        }

        private void FbDelete(string path)
        {
            FirebaseDatabase.DefaultInstance.RootReference
                .Child(path)
                .RemoveValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        SetStatus($"❌ 삭제 실패: /{path}", MessageType.Error);
                        return;
                    }

                    SetStatus($"🗑 삭제 완료: /{path}", MessageType.Info);

                    if (!_fbLiveOn)
                        FbFetch(_fbPath);
                });
        }

        public void FbExplorerCleanup()
        {
            if (_fbLiveHandler != null)
            {
                FirebaseDatabase.DefaultInstance.RootReference
                    .Child(_fbPath).ValueChanged -= _fbLiveHandler;
                _fbLiveHandler = null;
            }

            _fbLiveOn = false;
            _fbLiveSnapshot = null;
        }

        #endregion
    }
}