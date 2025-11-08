using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BoothImportAssistant.UI
{
    /// <summary>
    /// UnityPackageインポート選択ダイアログ
    /// </summary>
    public class PackageImportDialog : EditorWindow
    {
        private List<string> packagePaths;
        private Dictionary<string, bool> packageSelections;
        private Action<List<string>> onImport;
        private Vector2 scrollPosition;

        public static void ShowDialog(List<string> packages, Action<List<string>> callback)
        {
            var window = GetWindow<PackageImportDialog>(true, "UnityPackageをインポート");
            window.packagePaths = packages;
            window.onImport = callback;
            window.packageSelections = new Dictionary<string, bool>();
            
            // すべてデフォルトで選択
            foreach (string package in packages)
            {
                window.packageSelections[package] = true;
            }
            
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("以下のUnityPackageをインポートしますか？", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            foreach (string packagePath in packagePaths)
            {
                EditorGUILayout.BeginHorizontal();
                
                string fileName = Path.GetFileName(packagePath);
                bool isSelected = packageSelections.ContainsKey(packagePath) && packageSelections[packagePath];
                
                // チェックボックス
                bool newSelection = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                packageSelections[packagePath] = newSelection;
                
                // ファイル名とサイズ
                if (File.Exists(packagePath))
                {
                    FileInfo fileInfo = new FileInfo(packagePath);
                    long fileSizeMB = fileInfo.Length / 1024 / 1024;
                    EditorGUILayout.LabelField($"{fileName} ({fileSizeMB} MB)", 
                        newSelection ? EditorStyles.label : EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField(fileName, EditorStyles.miniLabel);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(10);
            
            // ボタン
            EditorGUILayout.BeginHorizontal();
            
            // すべて選択/解除
            int selectedCount = packageSelections.Values.Count(v => v);
            if (GUILayout.Button(selectedCount == packagePaths.Count ? "すべて解除" : "すべて選択", 
                GUILayout.Height(30)))
            {
                bool selectAll = selectedCount != packagePaths.Count;
                foreach (string package in packagePaths)
                {
                    packageSelections[package] = selectAll;
                }
            }
            
            GUILayout.FlexibleSpace();
            
            // キャンセル
            if (GUILayout.Button("キャンセル", GUILayout.Height(30), GUILayout.Width(100)))
            {
                Close();
            }
            
            // インポート
            GUI.enabled = selectedCount > 0;
            if (GUILayout.Button("選択したものをインポート", GUILayout.Height(30), GUILayout.Width(180)))
            {
                List<string> selectedPackages = new List<string>();
                foreach (var kvp in packageSelections)
                {
                    if (kvp.Value)
                    {
                        selectedPackages.Add(kvp.Key);
                    }
                }
                
                if (onImport != null)
                {
                    onImport(selectedPackages);
                }
                
                Close();
            }
            
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
        }
    }
}

