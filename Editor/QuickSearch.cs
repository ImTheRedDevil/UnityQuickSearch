using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
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
            GUI.SetNextControlName("searchbar");
            EditorGUI.BeginChangeCheck();
            _searchText = GUILayout.TextField(_searchText);
            if (EditorGUI.EndChangeCheck())
            {
                _showHistoryOnEnable = false;
                _searchResults.Clear();
                if (_searchText != "")
                {
                    var searchResults = AssetDatabase.FindAssets(_searchText, new[] { "Assets/" });
                    _searchResults.AddRange(searchResults);
                    _searchResults = _searchResults.OrderBy(r=> Path.GetFileName(AssetDatabase.GUIDToAssetPath(r))).ToList();
                }
            }

            SpawnButtons(_searchText == "" || _showHistoryOnEnable ? _history.ToList() : _searchResults.ToList());
            
            if (Event.current.keyCode == KeyCode.Return)
            {
                if (_searchResults.Count > 0)
                {
                    ButtonPressed(_searchResults[0]);
                }

                if (_history.Count > 0)
                {
                    ButtonPressed(_history[0]);
                }
            }

            GUI.FocusControl("searchbar");
        }

        private void SpawnButtons(List<string> results)
        {
            foreach (var result in results)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(result);
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }
                
                if (GUILayout.Button(Path.GetFileName(assetPath)))
                {
                    ButtonPressed(result);
                }
            }
        }

        private void ButtonPressed(string result)
        {
            AddToHistory(result);

            var assetPath = AssetDatabase.GUIDToAssetPath(result);
            var mainObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
            Selection.activeObject = mainObject;
            
            OpenExternalAsset(mainObject);

            _window.Close();
        }
        
        private static void OpenExternalAsset(Object asset)
        {
            AssetDatabase.OpenAsset(asset);
        }

        private void AddToHistory(string result)
        {
            _history.Remove(result);
            _history = _history.Prepend(result).ToList();
            if (_history.Count > 25)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }
        
        public class QuickSearchSingleton : ScriptableSingleton<QuickSearchSingleton>
        {
            public string Search { get; set; }
            public List<string> History { get; set; }
        }
    }
}
