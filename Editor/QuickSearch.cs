using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.virtulope.quicksearch.Editor
{
    public class QuickSearch : EditorWindow
    {
        private const float WindowWidthPercent = 0.45f;
        private const float WindowHeightPercent = 0.40f;

        private string _searchText = "";

        private List<string> _searchResults = new();
        private List<string> _history = new();

        private static EditorWindow _window;

        private bool _showHistoryOnEnable;

        private int _currentSelectedIndex = 0;
        
        [MenuItem("Tools/QuickSearch %t")]
        public static void OpenSearch()
        {
            _window = GetWindow<QuickSearch>(true, "QuickSearch");
            var mainWindow = EditorGUIUtility.GetMainWindowPosition();
            var width = mainWindow.width * WindowWidthPercent;
            var height = mainWindow.height * WindowHeightPercent;
            _window.position = new Rect(mainWindow.center.x - width / 2f, mainWindow.center.y - height / 2f, 0, 0);
            _window.maxSize = new Vector2(width, height);
            _window.minSize = new Vector2(width, height);
            _window.ShowPopup();
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
            HandleKeyboardButtons();
            
            GUI.SetNextControlName("searchbar");
            EditorGUI.BeginChangeCheck();
            _searchText = GUILayout.TextField(_searchText);
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

            SpawnButtons(_searchResults.ToList());

            GUI.FocusControl("searchbar");
        }

        private void SpawnButtons(List<string> results)
        {
            if (_currentSelectedIndex >= results.Count)
                _currentSelectedIndex = results.Count - 1;
            
            for (var i = 0; i < results.Count; i++)
            {
                if (_currentSelectedIndex == i)
                {
                    GUI.backgroundColor = Color.green;
                }
                
                var assetPath = AssetDatabase.GUIDToAssetPath(results[i]);
                if (GUILayout.Button(Path.GetFileName(assetPath)))
                {
                    ButtonPressed(results[i]);
                }
                
                GUI.backgroundColor = Color.white;
            }
        }

        private void ButtonPressed(string result)
        {
            AddToHistory(result);

            var assetPath = AssetDatabase.GUIDToAssetPath(result);
            var mainObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
            Selection.activeObject = mainObject;
            
            AssetDatabase.OpenAsset(mainObject);

            _window.Close();
        }

        private void AddToHistory(string result)
        {
            if (_history.Contains(result) && _history[0] != result)
            {
                _history.Remove(result);
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
                    currentEvent.Use();
                    break;
                case KeyCode.UpArrow:
                    if (_currentSelectedIndex > 0)
                    {
                        _currentSelectedIndex--;
                    }
                    currentEvent.Use();
                    break;
                case KeyCode.Escape:
                    _window.Close();
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
        
        public class QuickSearchSingleton : ScriptableSingleton<QuickSearchSingleton>
        {
            public string Search { get; set; }
            public List<string> History { get; set; }
        }
    }
}
