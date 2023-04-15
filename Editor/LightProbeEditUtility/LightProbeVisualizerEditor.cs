/*
 *  The MIT License
 *
 *  Copyright 2022 whiteflare.
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

namespace WF.Utillty.LightProbeEdit
{
   [CustomEditor(typeof(LightProbeVisualizer))]
    public class LightProbeVisualizerEditor : Editor
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

    public class LightProbeVisualizerWindow : EditorWindow
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
    }
}

#endif
