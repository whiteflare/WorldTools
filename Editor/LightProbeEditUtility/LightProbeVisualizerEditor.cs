/*
 *  The MIT License
 *
 *  Copyright 2022-2026 whiteflare.
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
 *  to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
 *  and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 *  IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
 *  TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

namespace WF.Tool.World.LightProbeEdit
{
   [CustomEditor(typeof(LightProbeVisualizer))]
    internal class LightProbeVisualizerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var oldColor = GUI.color;

            var vis = target as LightProbeVisualizer;

            EditorGUI.BeginChangeCheck();
            vis.meshScale = EditorGUILayout.Slider("スケール", vis.meshScale, 0, 5);
            if (EditorGUI.EndChangeCheck())
            {
                LightProbeVisualizerSingleton.instance.UpdateScale(vis);
                EditorUtility.SetDirty(vis);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledGroupScope(PrefabUtility.IsPartOfPrefabAsset(target)))
            {
                GUI.color = new Color(0.75f, 0.75f, 1f);

                Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 1.2f);
                rect.width = rect.width / 2 - 1;
                bool execPosMark = GUI.Button(rect, "位置にマーク");
                rect.x += rect.width + 2;
                bool execShowSH9 = GUI.Button(rect, "ベイク結果を可視化");

                GUI.color = oldColor;
                if (execPosMark || execShowSH9)
                {
                    LightProbeVisualizerSingleton.instance.UpdateMarkers(vis, execPosMark ? 0 : 1);
                }
                if (GUI.Button(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 1.2f), "クリア"))
                {
                    LightProbeVisualizerSingleton.instance.ClearMarkers(vis);
                }
            }

            EditorGUILayout.Space();
            if (GUI.Button(EditorGUILayout.GetControlRect(), "ツールウィンドウを開く"))
            {
                LightProbeVisualizerWindow.OpenWindow(vis);
            }
        }
    }

    internal class LightProbeVisualizerWindow : EditorWindow
    {
        public static void OpenWindow(LightProbeVisualizer target = null)
        {
            var window = GetWindow<LightProbeVisualizerWindow>("LightProbeVisualizer");
            window.vis = target;
        }

        public LightProbeVisualizer vis;

        public void OnGUI()
        {
            var oldColor = GUI.color;

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledGroupScope(vis != null))
            {
                vis = EditorGUILayout.ObjectField("LightProbeVisualizer", vis, typeof(LightProbeVisualizer), true) as LightProbeVisualizer;
            }

            EditorGUILayout.Space();
            if (vis != null)
            {
                EditorGUI.BeginChangeCheck();
                vis.meshScale = EditorGUILayout.Slider("スケール", vis.meshScale, 0, 5);
                if (EditorGUI.EndChangeCheck())
                {
                    LightProbeVisualizerSingleton.instance.UpdateScale(vis);
                    EditorUtility.SetDirty(vis);
                }

                EditorGUILayout.Space();
                using (new EditorGUI.DisabledGroupScope(PrefabUtility.IsPartOfPrefabAsset(vis)))
                {
                    GUI.color = new Color(0.75f, 0.75f, 1f);

                    Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 1.2f);
                    rect.width = rect.width / 2 - 1;
                    bool execPosMark = GUI.Button(rect, "位置にマーク");
                    rect.x += rect.width + 2;
                    bool execShowSH9 = GUI.Button(rect, "ベイク結果を可視化");

                    GUI.color = oldColor;
                    if (execPosMark || execShowSH9)
                    {
                        LightProbeVisualizerSingleton.instance.UpdateMarkers(vis, execPosMark ? 0 : 1);
                    }
                    if (GUI.Button(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 1.2f), "クリア"))
                    {
                        LightProbeVisualizerSingleton.instance.ClearMarkers(vis);
                    }
                }
            }
        }

        [MenuItem("GameObject/ライトプローブ編集/ライトプローブ可視化ツール",priority = 31)]
        public static void CreateInstanceIntoScene()
        {
            var path = AssetDatabase.GUIDToAssetPath("465e70e1d7f2bb34191559573aa3a5ec");
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("LightProbeVisualizer: Not Found LightProbeVisualizer.prefab");
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("LightProbeVisualizer: LightProbeVisualizer.prefab Instantiation Failed.");
                return;
            }
            var instance = (GameObject) PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetAsLastSibling();
            Selection.activeGameObject = instance;
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate LightProbeVisualizer");
        }
    }
}

#endif
