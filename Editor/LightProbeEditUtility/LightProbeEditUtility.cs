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

using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace WF.Tool.World.LightProbeEdit
{
    internal class LightProbeEditUtility
    {
        private const string PATH_ROOT = "GameObject/ライトプローブ編集/";
        private const string PATH_CREATE_LINE = PATH_ROOT + "作成/Line";
        private const string PATH_CREATE_CAGE = PATH_ROOT + "作成/Cage";
        private const string PATH_CREATE_BOX = PATH_ROOT + "作成/Box";
        private const string PATH_CREATE_PLANE = PATH_ROOT + "作成/Plane";
        private const string PATH_CREATE_CYLINDER = PATH_ROOT + "作成/Cylinder";
        private const string PATH_CLRTRANSFORM = PATH_ROOT + "Transform変更/ワールド原点へ";
        private const string PATH_CENTERING = PATH_ROOT + "Transform変更/プローブの中央へ";
        private const string PATH_COMBINE = PATH_ROOT + "統合";
        private const string PATH_MERGEPROBE = PATH_ROOT + "プローブを距離でマージ";
        private const string PATH_FALLPROBE = PATH_ROOT + "コライダーに落下させる";

        #region 作成系

        [MenuItem(PATH_CREATE_LINE, false, 11)]
        public static void Menu_CreateLine()
        {
            var selected = Selection.GetTransforms(SelectionMode.Editable);
            if (selected.Length <= 0)
            {
                selected = new Transform[] { null };
            }
            if (!CreateProbeDialog.ShowDialog(CreateProbeType.Line, out var x, out var y, out var z, out var origin))
            {
                return;
            }
            CreateProbeBox(selected, x, 1, 1, origin, (xedge, yedge, zedge) => true);
        }

        [MenuItem(PATH_CREATE_CAGE, false, 12)]
        public static void Menu_CreateCage()
        {
            var selected = Selection.GetTransforms(SelectionMode.Editable);
            if (selected.Length <= 0)
            {
                selected = new Transform[] { null };
            }
            if (!CreateProbeDialog.ShowDialog(CreateProbeType.Cage, out var x, out var y, out var z, out var origin))
            {
                return;
            }
            CreateProbeBox(selected, x, y, z, origin, (xedge, yedge, zedge) => 2 <= (xedge ? 1 : 0) + (yedge ? 1 : 0) + (zedge ? 1 : 0));
            // XYZ全て端 or XYZのどれか2つが端のときに出力するとCageになる
        }

        [MenuItem(PATH_CREATE_BOX, false, 13)]
        public static void Menu_CreateBox()
        {
            var selected = Selection.GetTransforms(SelectionMode.Editable);
            if (selected.Length <= 0)
            {
                selected = new Transform[] { null };
            }
            if (!CreateProbeDialog.ShowDialog(CreateProbeType.Box, out var x, out var y, out var z, out var origin))
            {
                return;
            }
            CreateProbeBox(selected, x, y, z, origin, (xedge, yedge, zedge) => xedge || yedge || zedge);
            // XYZいずれかが端のときに出力するとBoxになる
        }

        [MenuItem(PATH_CREATE_PLANE, false, 14)]
        public static void Menu_CreatePlane()
        {
            var selected = Selection.GetTransforms(SelectionMode.Editable);
            if (selected.Length <= 0)
            {
                selected = new Transform[] { null };
            }
            if (!CreateProbeDialog.ShowDialog(CreateProbeType.Plane, out var x, out var y, out var z, out var origin))
            {
                return;
            }
            CreateProbeBox(selected, x, 1, y, origin, (xedge, yedge, zedge) => true);
            // y = 1 とすることでPlaneになる
        }

        [MenuItem(PATH_CREATE_CYLINDER, false, 15)]
        public static void Menu_CreateCylinder()
        {
            var selected = Selection.GetTransforms(SelectionMode.Editable);
            if (selected.Length <= 0)
            {
                selected = new Transform[] { null };
            }
            if (!CreateProbeDialog.ShowDialog(CreateProbeType.Cylinder, out var x, out var y, out var z, out var origin))
            {
                return;
            }
            CreateProbeCylinder(selected, x, y, origin);
        }

        private static void Reset(Transform t)
        {
            // Transform.Reset はVRCSDKが定義する拡張メソッドなので利用しない
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        private static void CreateProbeBox(Transform[] selected, int x, int y, int z, CreateProbeOrigin origin, System.Func<bool, bool, bool, bool> cond)
        {
            var created = new List<GameObject>();
            foreach (var t in selected)
            {
                var go = new GameObject();
                go.name = "Light Probe Group";
                go.transform.parent = t;
                Reset(go.transform);
                go.transform.SetAsLastSibling();
                created.Add(go);
                Undo.RegisterCreatedObjectUndo(go, "Create LightProbeGroup");
                var lpg = go.AddComponent<LightProbeGroup>();
                var newp = new List<Vector3>();
                var xp = BetweenLinear(x, origin);
                var yp = BetweenLinear(y, origin);
                var zp = BetweenLinear(z, origin);
                for (int i = 0; i < xp.Length; i++)
                {
                    bool xedge = i == 0 || i == xp.Length - 1;
                    for (int j = 0; j < yp.Length; j++)
                    {
                        bool yedge = j == 0 || j == yp.Length - 1;
                        for (int k = 0; k < zp.Length; k++)
                        {
                            bool zedge = k == 0 || k == zp.Length - 1;
                            if (cond(xedge, yedge, zedge))
                            {
                                newp.Add(new Vector3(xp[i], yp[j], zp[k]));
                            }
                        }
                    }
                }
                lpg.probePositions = newp.ToArray();
            }
            Selection.objects = created.ToArray();
        }

        private static void CreateProbeCylinder(Transform[] selected, int x, int y, CreateProbeOrigin origin)
        {
            var created = new List<GameObject>();
            foreach (var t in selected)
            {
                var go = new GameObject();
                go.name = "Light Probe Group";
                go.transform.parent = t;
                Reset(go.transform);
                go.transform.SetAsLastSibling();
                created.Add(go);
                Undo.RegisterCreatedObjectUndo(go, "Create LightProbeGroup");
                var lpg = go.AddComponent<LightProbeGroup>();
                var newp = new List<Vector3>();
                var xp = BetweenRound(x);
                var yp = BetweenLinear(y, origin);
                for (int i = 0; i < xp.Length; i++)
                {
                    var rad = Mathf.Deg2Rad * xp[i];
                    for (int j = 0; j < yp.Length; j++)
                    {
                        newp.Add(new Vector3(Mathf.Cos(rad), yp[j], Mathf.Sin(rad)));
                    }
                }
                lpg.probePositions = newp.ToArray();
            }
            Selection.objects = created.ToArray();
        }

        private static float[] BetweenLinear(int count, CreateProbeOrigin origin)
        {
            if (count <= 1)
            {
                return new float[] { 0 };
            }
            switch(origin)
            {
                case CreateProbeOrigin.Corner:
                    return Between(0, +2, count).ToArray();
                default:
                    return Between(-1, +1, count).ToArray();
            }
        }

        private static float[] BetweenRound(int count)
        {
            if (count <= 1)
            {
                return new float[] { 0 };
            }
            return Between(0, 360, count + 1, excludeTo: true).ToArray();
        }

        private static IEnumerable<float> Between(float from, float to, int count, bool excludeFrom = false, bool excludeTo = false)
        {
            if (count == 0)
            {
                // 何も出力しない
                yield break;
            }
            else if (count == 1)
            {
                // カウント1のときはfromとtoの中点を出力する
                if (!excludeFrom && !excludeTo)
                    yield return (from + to) / 2;
                else
                    yield break;
            }
            else
            {
                // それ以上のときはfrom～toを出力する
                float len = to - from;
                count--;
                if (!excludeFrom)
                {
                    yield return from;
                }
                for (int i = 1; i < count; i++)
                {
                    yield return from + len / count * i;
                }
                if (!excludeTo)
                {
                    yield return to; // 最後は計算せずに出力して誤差を打ち消す
                }
            }
        }

        #endregion

        #region 編集系

        [MenuItem(PATH_COMBINE, true)]
        public static bool Validate_DoubleLpg()
        {
            if (Selection.activeObject == null)
            {
                return false;
            }
            return 2 <= GetSelectedLPGCount();
        }

        [MenuItem(PATH_MERGEPROBE, true)]
        [MenuItem(PATH_CLRTRANSFORM, true)]
        [MenuItem(PATH_FALLPROBE, true)]
        [MenuItem(PATH_CENTERING, true)]
        public static bool Validate_AnyLpg()
        {
            if (Selection.activeObject == null)
            {
                return false;
            }
            return 1 <= GetSelectedLPGCount();
        }

        /// <summary>
        /// 統合
        /// </summary>
        [MenuItem(PATH_COMBINE, false, 16)]
        public static void Menu_LpgCombine()
        {
            var selected = GetSelectedLPG();
            if (selected.Length <= 1)
            {
                return;
            }
            // 収集
            var probes = new List<Vector3>();
            foreach (var lpg in selected)
            {
                probes.AddRange(ObjectToWorldPosition(lpg.probePositions, lpg.transform));
            }
            // 設定
            var first = selected[0];
            Undo.RecordObject(first, "LightProbe Combine");
            first.probePositions = WorldToObjectPosition(probes.ToArray(), first.transform);
            EditorUtility.SetDirty(first);
            // 結合した他は削除する
            foreach (var lpg in selected.Skip(1))
            {
                Undo.DestroyObjectImmediate(lpg);
            }
            Debug.LogFormat(first, "{0} 個の LightProbeGroup を {1} に統合", selected.Length - 1, first);
        }

        /// <summary>
        /// Transform変更/ワールド原点へ
        /// </summary>
        [MenuItem(PATH_CLRTRANSFORM, false, 17)]
        public static void Menu_LpgToWorldOrigin()
        {
            var selected = GetSelectedLPG();
            // Undo
            Undo.RecordObjects(MergeTransform(selected), "LightProbe Move To World Origin");

            // いったんWorldPositionを設定
            foreach (var lpg in selected)
            {
                lpg.probePositions = ObjectToWorldPosition(lpg.probePositions, lpg.transform);
            }
            // Transformをリセット
            foreach (var lpg in selected)
            {
                Reset(lpg.transform);
            }
            // リセット後のTransformにてLocalPositionを計算しなおして再設定
            foreach (var lpg in selected)
            {
                lpg.probePositions = WorldToObjectPosition(lpg.probePositions, lpg.transform);
            }
            Debug.LogFormat("{0} 個の LightProbeGroup の Transform をワールド原点へ", selected.Length);
        }

        /// <summary>
        /// Transform変更/プローブの中央へ
        /// </summary>
        [MenuItem(PATH_CENTERING, false, 18)]
        public static void Menu_LpgToProbeCenter()
        {
            var selected = GetSelectedLPG();
            // Undo
            Undo.RecordObjects(MergeTransform(selected), "LightProbe Move To Probe Center");

            // いったんWorldPositionを設定
            foreach (var lpg in selected)
            {
                lpg.probePositions = ObjectToWorldPosition(lpg.probePositions, lpg.transform);
            }
            // Transformをリセット
            foreach (var lpg in selected)
            {
                var minPos = lpg.probePositions.Aggregate((a, b) => new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z)));
                var maxPos = lpg.probePositions.Aggregate((a, b) => new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z)));
                lpg.transform.position = new Vector3((minPos.x + maxPos.x) / 2, (minPos.y + maxPos.y) / 2, (minPos.z + maxPos.z) / 2); // ワールド座標
                lpg.transform.rotation = Quaternion.identity;
                lpg.transform.localScale = new Vector3(
                    Mathf.Abs(maxPos.x - minPos.x) < 0.01f ? 1 : (maxPos.x - minPos.x) / 2,
                    Mathf.Abs(maxPos.y - minPos.y) < 0.01f ? 1 : (maxPos.y - minPos.y) / 2,
                    Mathf.Abs(maxPos.z - minPos.z) < 0.01f ? 1 : (maxPos.z - minPos.z) / 2);
            }
            // リセット後のTransformにてLocalPositionを計算しなおして再設定
            foreach (var lpg in selected)
            {
                lpg.probePositions = WorldToObjectPosition(lpg.probePositions, lpg.transform);
            }
            Debug.LogFormat("{0} 個の LightProbeGroup の Transform をプローブ中央へ", selected.Length);
        }

        /// <summary>
        /// プローブを距離でマージ
        /// </summary>
        [MenuItem(PATH_MERGEPROBE, false, 19)]
        public static void Menu_LpgMergeByDistance()
        {
            if (!MergeByDistanceDialog.ShowDialog(out var dist))
            {
                return;
            }

            var selected = GetSelectedLPG();
            Undo.RecordObjects(selected, "LightProbe Merge by Distance");
            foreach (var lpg in selected)
            {
                var wp = ObjectToWorldPosition(lpg.probePositions, lpg.transform);
                int cntBefore = wp.Length;
                wp = MergeByDistance(wp, dist);
                int cntAfter = wp.Length;
                lpg.probePositions = WorldToObjectPosition(wp, lpg.transform);
                Debug.LogFormat(lpg, "{0} のプローブを距離でマージ: {1} 個 -> {2} 個", lpg, cntBefore, cntAfter);
                EditorUtility.SetDirty(lpg);
            }
        }

        /// <summary>
        /// コライダーに落下させる
        /// </summary>
        [MenuItem(PATH_FALLPROBE, false, 20)]
        public static void Menu_LpgFallProbe()
        {
            var selected = GetSelectedLPG();
            if (selected.Length <= 0)
            {
                return;
            }
            if (!FallProbeDialog.ShowDialog(out var direction, out var maxDistance, out var margin, out var nohit))
            {
                return;
            }
            Undo.RecordObjects(selected, "LightProbe Falling");
            // 収集
            foreach (var lpg in selected)
            {
                var pb = ObjectToWorldPosition(lpg.probePositions, lpg.transform);
                var newpb = new List<Vector3>();
                for (int i = 0; i < pb.Length; i++)
                {
                    if (Physics.Raycast(pb[i], direction, out var hit, maxDistance))
                    {
                        // 当たった場所にマージン分を加算した場所を新座標とする
                        newpb.Add(hit.point - direction * margin);
                    }
                    else
                    {
                        // ヒットしなかったら
                        switch(nohit)
                        {
                            case 1: // 移動する
                                newpb.Add(pb[i] + direction * (maxDistance - margin));
                                break;
                            case 2: // 削除する
                                break;
                            default: // 何もしない
                                newpb.Add(pb[i]);
                                break;
                        }
                    }
                }
                lpg.probePositions = WorldToObjectPosition(newpb.ToArray(), lpg.transform);
                EditorUtility.SetDirty(lpg);
            }
            Debug.LogFormat("{0} 個の LightProbeGroup をコライダーに落下", selected.Length);
        }

        private static Vector3[] ObjectToWorldPosition(Vector3[] array, Transform t)
        {
            return array.Select(t.TransformPoint).ToArray();
        }

        private static Vector3[] WorldToObjectPosition(Vector3[] array, Transform t)
        {
            return array.Select(t.InverseTransformPoint).ToArray();
        }

        private static Object[] MergeTransform(LightProbeGroup[] lpgs)
        {
            var result = new List<Object>();
            result.AddRange(lpgs);
            result.AddRange(lpgs.Select(lpg => lpg.transform));
            return result.ToArray();
        }

        private static int GetSelectedLPGCount()
        {
            return Selection.GetFiltered<LightProbeGroup>(SelectionMode.Editable).Length;
        }

        private static LightProbeGroup[] GetSelectedLPG()
        {
            // 選択されているものを取得
            var result = new List<LightProbeGroup>(Selection.GetFiltered<LightProbeGroup>(SelectionMode.Editable));
            // もしLPGの子に選択されていないLPGがいるならば、それも対象に含める
            result.AddRange(result.SelectMany(lpg => lpg.GetComponentsInChildren<LightProbeGroup>(true)).ToArray());
            // Hierarchyの表示順にソート
            result.Sort(CompareInHierarchy);
            // 単一化して返却
            return result.Distinct().ToArray();
        }

        private static int CompareInHierarchy(Component x, Component y)
        {
            var xa = GetDeepSiblingIndex(x.transform);
            var ya = GetDeepSiblingIndex(y.transform);
            var mx = System.Math.Min(xa.Length, ya.Length);
            for (int i = 0; i < mx; i++)
            {
                var c = xa[i].CompareTo(ya[i]);
                if (c != 0)
                {
                    return c;
                }
            }
            return xa.Length.CompareTo(ya.Length);
        }

        private static int[] GetDeepSiblingIndex(Transform t)
        {
            var result = new List<int>();
            while (t != null)
            {
                result.Insert(0, t.GetSiblingIndex());
                t = t.parent;
            }
            return result.ToArray();
        }

        private static Vector3[] MergeByDistance(Vector3[] src, float dist)
        {
            return src.Distinct(new Vector3NearByComparer(dist)).ToArray();
        }

        class Vector3NearByComparer : IEqualityComparer<Vector3>
        {
            private readonly float dist2;

            public Vector3NearByComparer(float distance)
            {
                this.dist2 = distance * distance;
            }

            public bool Equals(Vector3 x, Vector3 y)
            {
                return (x - y).sqrMagnitude < dist2;
            }

            public int GetHashCode(Vector3 product)
            {
                return 0;
            }
        }

        #endregion
    }

    internal enum CreateProbeType
    {
        Line,
        Cage,
        Box,
        Plane,
        Cylinder,
    }

    internal enum CreateProbeOrigin
    {
        Center,
        Corner,
    }

    internal class CreateProbeDialog : EditorWindow
    {
        private static readonly Vector2 SIZE = new Vector2(320, 120);

        CreateProbeType mode = CreateProbeType.Line;
        CreateProbeOrigin origin = CreateProbeOrigin.Center;
        int x = 2;
        int y = 2;
        int z = 2;
        bool ok = false;

        public static bool ShowDialog(CreateProbeType mode, out int x, out int y, out int z, out CreateProbeOrigin origin)
        {
            var window = CreateInstance<CreateProbeDialog>();
            window.titleContent = new GUIContent("LightProbeGroupを作成");
            // サイズと位置を調整
            var position = new Vector2((Screen.currentResolution.width - SIZE.x) / 2, (Screen.currentResolution.height - SIZE.y) / 2);
            window.minSize = SIZE;
            window.position = new Rect(position, SIZE);
            // 返却値の初期化
            window.mode = mode;
            window.ok = false;
            // 表示
            window.ShowModalUtility();
            // 返却
            x = window.x;
            y = window.y;
            z = window.z;
            origin = window.origin;
            return window.ok;
        }

        public void OnGUI()
        {
            switch(mode)
            {
                case CreateProbeType.Line:
                    x = Mathf.Max(2, EditorGUILayout.IntField("X", x));
                    break;
                case CreateProbeType.Plane:
                case CreateProbeType.Cylinder:
                    x = Mathf.Max(2, EditorGUILayout.IntField("X", x));
                    y = Mathf.Max(2, EditorGUILayout.IntField("Y", y));
                    break;
                case CreateProbeType.Cage:
                case CreateProbeType.Box:
                    x = Mathf.Max(2, EditorGUILayout.IntField("X", x));
                    y = Mathf.Max(2, EditorGUILayout.IntField("Y", y));
                    z = Mathf.Max(2, EditorGUILayout.IntField("Z", z));
                    break;
            }
            origin = (CreateProbeOrigin) EditorGUILayout.EnumPopup("原点", origin);
 
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("OK"))
            {
                ok = true;
                Close();
            }
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    internal class MergeByDistanceDialog : EditorWindow
    {
        private static readonly Vector2 SIZE = new Vector2(320, 80);

        float value = 0.1f;
        bool ok = false;

        public static bool ShowDialog(out float value)
        {
            var window = CreateInstance<MergeByDistanceDialog>();
            window.titleContent = new GUIContent("プローブを距離でマージ");
            // サイズと位置を調整
            var position = new Vector2((Screen.currentResolution.width - SIZE.x) / 2, (Screen.currentResolution.height - SIZE.y) / 2);
            window.minSize = SIZE;
            window.position = new Rect(position, SIZE);
            // 返却値の初期化
            window.ok = false;
            // 表示
            window.ShowModalUtility();
            // 返却
            value = window.value;
            return window.ok;
        }

        public void OnGUI()
        {
            value = EditorGUILayout.FloatField("距離", value);
            value = value < 0 ? 0 : value;
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("OK"))
            {
                ok = true;
                Close();
            }
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    internal class FallProbeDialog : EditorWindow
    {
        private static readonly Vector2 SIZE = new Vector2(320, 120);

        int direction = 0;
        float maxDistance = 100;
        float margin = 0.05f;
        int nohit = 0;
        bool ok = false;

        public static bool ShowDialog(out Vector3 direction, out float maxDistance, out float margin, out int nohit)
        {
            var window = CreateInstance<FallProbeDialog>();
            window.titleContent = new GUIContent("プローブを落下させる");
            // サイズと位置を調整
            var position = new Vector2((Screen.currentResolution.width - SIZE.x) / 2, (Screen.currentResolution.height - SIZE.y) / 2);
            window.minSize = SIZE;
            window.position = new Rect(position, SIZE);
            // 返却値の初期化
            window.ok = false;
            // 表示
            window.ShowModalUtility();
            // 返却
            switch(window.direction)
            {
                default: // -Y
                    direction = new Vector3(0, -1, 0);
                    break;
                case 1: // +Y
                    direction = new Vector3(0, +1, 0);
                    break;
                case 2: // -X
                    direction = new Vector3(-1, 0, 0);
                    break;
                case 3: // +X
                    direction = new Vector3(+1, 0, 0);
                    break;
                case 4: // -Z
                    direction = new Vector3(0, 0, -1);
                    break;
                case 5: // +Z
                    direction = new Vector3(0, 0, +1);
                    break;
            }
            maxDistance = window.maxDistance;
            margin = window.margin;
            nohit = window.nohit;
            return window.ok;
        }

        public void OnGUI()
        {
            direction = EditorGUILayout.Popup("方向", direction, new string[] { "-Y", "+Y", "-X", "+X", "-Z", "+Z" });
            margin = Mathf.Max(0, EditorGUILayout.FloatField("マージン", margin));
            maxDistance = Mathf.Max(0, EditorGUILayout.FloatField("最大射程", maxDistance));
            nohit = EditorGUILayout.Popup("Hitしなかったとき", nohit, new string[] { "移動しない", "移動する", "削除する" });

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("OK"))
            {
                ok = true;
                Close();
            }
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}

#endif
