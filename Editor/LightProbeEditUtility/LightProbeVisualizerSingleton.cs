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

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace WF.Tool.World.LightProbeEdit
{
    internal class LightProbeVisualizerSingleton : ScriptableSingleton<LightProbeVisualizerSingleton>
    {
        public void UpdateScale(LightProbeVisualizer vis)
        {
            var scale = new Vector3(vis.meshScale, vis.meshScale, vis.meshScale);
            foreach (var go in GetGeneratedObjects(vis))
            {
                if (go != null)
                {
                    go.transform.localScale = scale;
                }
            }
        }

        public void ClearMarkers(LightProbeVisualizer vis)
        {
            foreach (var go in GetGeneratedObjects(vis))
            {
                DestroyImmediate(go);
            }
        }

        public void UpdateMarkers(LightProbeVisualizer vis, int mode)
        {
            ClearMarkers(vis);
            var prefab = vis.prefabPositionMarker;
            if (mode == 1)
            {
                prefab = vis.prefabBakeVisualizer;
            }
            CreateMarkers(vis, prefab);
            UpdateScale(vis);
        }

        private void CreateMarkers(LightProbeVisualizer vis, GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }
            Undo.RegisterFullObjectHierarchyUndo(vis, "LightProbe Visualization");
            foreach (var p in GetAllLightProbeWorldPos())
            {
                var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (go == null)
                {
                    continue;
                }
                Undo.RegisterCreatedObjectUndo(go, "LightProbe Visualization");
                go.name = "auto generated";
                go.transform.position = p;
                go.transform.parent = vis.transform;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.hideFlags = HideFlags.NotEditable | HideFlags.HideInHierarchy;
                }
            }
        }

        private List<GameObject> GetGeneratedObjects(LightProbeVisualizer vis)
        {
            var root = vis.gameObject.transform;
            var result = new List<GameObject>();
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != null && child.name == "auto generated")
                {
                    result.Add(child.gameObject);
                }
            }
            return result;
        }

        private List<Vector3> GetAllLightProbeWorldPos()
        {
            var wsProbes = new List<Vector3>();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    foreach (var go in scene.GetRootGameObjects())
                    {
                        foreach (var lpg in go.GetComponentsInChildren<LightProbeGroup>()) // active only
                        {
                            foreach (var p in lpg.probePositions)
                            {
                                wsProbes.Add(lpg.transform.TransformPoint(p));
                            }
                        }
                    }
                }
            }
            return wsProbes;
        }
    }
}

#endif
