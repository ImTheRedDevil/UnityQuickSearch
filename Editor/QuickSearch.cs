﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

namespace com.virtulope.quicksearch.Editor
{
    public class QuickSearch : EditorWindow
    {
        private const float WindowWidthPercent = 0.45f;
        private const float WindowHeightPercent = 0.40f;
        private const int ResultSpacing = 3;
        private const int ResultHeight = 18;
        private const float ResultFileNameWidthPercentage = 0.4f;

        private static GUIStyle _searchBoxStyle, _resultStyle, _pathLabelStyle;

        private string _searchText = "";

        private List<string> _searchResults = new();
        private List<string> _history = new();
        
        private bool _showHistoryOnEnable;

        private int _currentSelectedIndex;

        private Vector2 _scrollPosition = Vector2.zero;

        private float VisibleScrollHeight => position.height - _searchBoxStyle.fixedHeight - 3;

        private float CompleteResultHeight => ResultHeight + ResultSpacing + 5;
        
        [MenuItem("Tools/QuickSearch %t")]
        public static void OpenSearch()
        {
            var window = GetWindow<QuickSearch>(true, "Quick Search");
            window.titleContent = null;
            var mainWindow = EditorGUIUtility.GetMainWindowPosition();
            var width = mainWindow.width * WindowWidthPercent;
            var height = mainWindow.height * WindowHeightPercent;
            window.position = new Rect(mainWindow.center.x - width / 2f, mainWindow.center.y - height / 2f, 0, 0);
            window.maxSize = new Vector2(width, height);
            window.minSize = new Vector2(width, height);
            window.Show();
        }

        public void OnEnable()
        {
            if (QuickSearchSingleton.instance.Search != null)
            {
                _searchText = QuickSearchSingleton.instance.Search;
            }
            
            if (QuickSearchSingleton.instance.History != null)
            {
                _history = QuickSearchSingleton.instance.History;
            }

            _showHistoryOnEnable = true;
        }

        public void OnDisable()
        {
            QuickSearchSingleton.instance.Search = _searchText;
            QuickSearchSingleton.instance.History = _history;
        }

        public void OnGUI()
        {
            if (_searchBoxStyle == null) {
                InitStyles();
            }
            HandleKeyboardButtons();
            
            GUI.SetNextControlName("searchbar");
            EditorGUI.BeginChangeCheck();
            
            _searchText = GUILayout.TextField(_searchText, _searchBoxStyle);
            EditorGUILayout.Space();
            
            if (EditorGUI.EndChangeCheck())
            {
                _currentSelectedIndex = 0;
                _showHistoryOnEnable = false;
                _searchResults.Clear();
                if (_searchText != "")
                {
                    var searchResults = AssetDatabase.FindAssets(_searchText, new[] { "Assets/" });
                    searchResults = searchResults.Where(sr => !AssetDatabase.IsValidFolder(AssetDatabase.GUIDToAssetPath(sr))).ToArray();
                    _searchResults.AddRange(searchResults);
                    _searchResults = _searchResults.OrderBy(r=>
                    {
                        long score = 0;
                        FuzzySearch.FuzzyMatch(_searchText, Path.GetFileName(AssetDatabase.GUIDToAssetPath(r)), ref score);
                        return score;
                    }).Reverse().ToList();
                }
            }

            if (_searchText == "" || _showHistoryOnEnable)
            {
                _showHistoryOnEnable = false;
                _searchResults = _history.ToList();
            }

            ShowResults(_searchResults.ToList());

            GUI.FocusControl("searchbar");
        }

        private void ShowResults(List<string> results)
        {
            if (_currentSelectedIndex >= results.Count)
                _currentSelectedIndex = results.Count - 1;

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            for (var i = 0; i < results.Count; i++)
            {
                if (_currentSelectedIndex == i)
                {
                    GUI.backgroundColor = Color.green;
                }
                
                var assetPath = AssetDatabase.GUIDToAssetPath(results[i]);
                
                ShowResult(assetPath);
                
                GUI.backgroundColor = Color.white;
            }
            GUILayout.EndScrollView();
        }

        private void ShowResult(string assetPath)
        {
            GUILayout.BeginHorizontal(_resultStyle, GUILayout.Height(ResultHeight));

            var image = AssetDatabase.GetCachedIcon(assetPath);
            GUILayout.Label(image, GUILayout.Height(ResultHeight), GUILayout.Width(ResultHeight + 5));
            
            GUILayout.Label(Path.GetFileName(assetPath), GUILayout.Height(ResultHeight), GUILayout.Width(ResultFileNameWidthPercentage * position.width));
            
            GUILayout.Label(assetPath, _pathLabelStyle, GUILayout.Height(ResultHeight));
            
            GUILayout.EndHorizontal();
        }

        private void ButtonPressed(string result)
        {
            AddToHistory(result);

            var assetPath = AssetDatabase.GUIDToAssetPath(result);
            var mainObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
            Selection.activeObject = mainObject;
            
            AssetDatabase.OpenAsset(mainObject);

            Close();
        }

        private void AddToHistory(string result)
        {
            if (_history.Contains(result))
            {
                _history.RemoveAll(item => item == result);
            }
            
            _history = _history.Prepend(result).ToList();
            if (_history.Count > 25)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }

        private void HandleKeyboardButtons()
        {
            var currentEvent = Event.current;

            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            switch (currentEvent.keyCode)
            {
                case KeyCode.Return:
                    ReturnKeyPressed();
                    currentEvent.Use();
                    break;
                case KeyCode.Tab:
                case KeyCode.DownArrow:
                    _currentSelectedIndex++;
                    SetScrollToCurrentSelected();
                    currentEvent.Use();
                    break;
                case KeyCode.UpArrow:
                    if (_currentSelectedIndex > 0)
                    {
                        _currentSelectedIndex--;
                    }
                    SetScrollToCurrentSelected();
                    currentEvent.Use();
                    break;
                case KeyCode.Escape:
                    Close();
                    currentEvent.Use();
                    break;
            }
        }

        private void ReturnKeyPressed()
        {
            if (_searchResults.Count > 0)
            {
                if (_currentSelectedIndex >= _searchResults.Count)
                    _currentSelectedIndex = 0;
                    
                ButtonPressed(_searchResults[_currentSelectedIndex]);
            }
        }

        private void SetScrollToCurrentSelected()
        {
            var maxResults = (int)Mathf.Floor(VisibleScrollHeight / CompleteResultHeight);

            var scroll = (_currentSelectedIndex + 1) - maxResults;
            if (scroll > 0 && scroll * CompleteResultHeight > _scrollPosition.y)
            {
                _scrollPosition = new Vector2(0f, scroll * CompleteResultHeight);
            }

            if (_scrollPosition.y > _currentSelectedIndex * CompleteResultHeight)
            {
                _scrollPosition = new Vector2(0f, _currentSelectedIndex * CompleteResultHeight);
            }
        }

        private static void InitStyles() {
            _searchBoxStyle = new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(0, 0, 0, 0)
            };
            _searchBoxStyle.fixedHeight = _searchBoxStyle.lineHeight * 1.6f;

            _resultStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(0, 0, 0, ResultSpacing),
            };

            _pathLabelStyle = new GUIStyle(GUI.skin.label)
            {
                normal =
                {
                    textColor = Color.gray
                }
            };
        }
        
        public class QuickSearchSingleton : ScriptableSingleton<QuickSearchSingleton>
        {
            public string Search { get; set; }
            public List<string> History { get; set; }
        }
    }
}
