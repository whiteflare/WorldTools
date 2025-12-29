/*
 *  The MIT License
 *
 *  Copyright 2021-2026 whiteflare.
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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace WF.Tool.World.Lightmap
{
    internal class LightmapControlPanel : EditorWindow
    {
        [MenuItem("Tools/whiteflare/Lightmap ControlPanel", priority = 19)]
        public static void Menu_LightmapControlPanel()
        {
            Lightmap.LightmapControlPanel.ShowWindow();
        }

        public static void ShowWindow()
        {
            GetWindow<LightmapControlPanel>("Lightmap Control Panel");
        }

        public TreeViewState treeViewState;
        private MeshListView treeView;
        private GameObject rootObject;
        private FilteringMode filter = FilteringMode.None;
        private bool onlyActiveObject = false;

        private void OnEnable()
        {
            if (treeViewState == null)
            {
                treeViewState = new TreeViewState();
            }
            treeView = new MeshListView(treeViewState);
            UpdateTreeView();
        }

        private void OnHierarchyChange()
        {
            UpdateTreeView();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            rootObject = EditorGUILayout.ObjectField("Root Object", rootObject, typeof(GameObject), true) as GameObject;
            EditorGUILayout.Space();
            filter = (FilteringMode)EditorGUILayout.EnumPopup("Filter", filter);
            EditorGUILayout.Space();
            onlyActiveObject = EditorGUILayout.Toggle("Only Active Object", onlyActiveObject);
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                UpdateTreeView();
            }

            EditorGUILayout.Space();

            treeView.OnGUI(GUILayoutUtility.GetRect(0, float.MaxValue, 0, float.MaxValue));
        }

        private LtTreeViewItem[] UpdateMeshInfos(GameObject root)
        {
            var rootObjects = new List<GameObject>();

            if (root == null)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded)
                    {
                        scene.GetRootGameObjects(rootObjects);
                    }
                }
            }
            else
            {
                rootObjects.Add(root);
            }

            var result = rootObjects.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true));
            result = result.Where(IsNotHide);
            if (onlyActiveObject)
            {
                result = result.Where(mr => mr.gameObject.activeInHierarchy && mr.enabled);
            }
            switch (filter)
            {
                case FilteringMode.OnlyContributeGI:
                    result = result.Where(mr => IsContributeGI(mr));
                    break;
                case FilteringMode.OnlyLightmapped:
                    result = result.Where(mr => IsLightmapped(mr));
                    break;
                case FilteringMode.NotContributeGI:
                    result = result.Where(mr => !IsContributeGI(mr));
                    break;
                case FilteringMode.NotLightmapped:
                    result = result.Where(mr => !IsLightmapped(mr));
                    break;
            }
            return result.Distinct().Select(mr => new LtTreeViewItem(mr)).ToArray();
        }

        private bool IsContributeGI(MeshRenderer mr)
        {
            return mr != null && GameObjectUtility.AreStaticEditorFlagsSet(mr.gameObject, StaticEditorFlags.ContributeGI);
        }

        private bool IsLightmapped(MeshRenderer mr)
        {
            return IsContributeGI(mr) && mr.receiveGI == ReceiveGI.Lightmaps && 0 < mr.scaleInLightmap;
        }

        private bool IsNotHide(Component cmp)
        {
            if (cmp == null)
            {
                return true;
            }
            if (cmp.hideFlags != HideFlags.None)
            {
                return false;
            }
            if (cmp.gameObject.hideFlags != HideFlags.None)
            {
                return false;
            }
            return cmp is Transform t ? IsNotHide(t.parent) : IsNotHide(cmp.transform);
        }

        private void UpdateTreeView()
        {
            treeView.items = UpdateMeshInfos(rootObject);
            treeView.SortItems();
            treeView.Reload();
        }

        internal enum FilteringMode
        {
            None,
            OnlyContributeGI,
            OnlyLightmapped,
            NotContributeGI,
            NotLightmapped,
        }

        const int COL_ObjectName = 0;
        const int COL_MeshName = 1;
        const int COL_BoundsSize = 2;
        const int COL_ObjectLayer = 3;
        const int COL_CastShadows = 4;
        const int COL_ReceiveShadows = 5;
        const int COL_ContributeGI = 6;
        const int COL_ReceiveGI = 7;
        const int COL_ScaleInLightmap = 8;
        const int COL_StitchSeams = 9;
        const int COL_LightmapIndex = 10;
        const int COL_AreaWidth = 11;
        const int COL_AreaHeight = 12;
        const int COL_AreaSize = 13;
        const int COL_LightmapSize = 14;
        const int COL_HasUV2 = 15;
        const int COL_GenerateLightmapUVs = 16;

        internal class LtTreeViewItem
        {
            public readonly MeshRenderer renderer;
            public readonly GameObject gameObject;
            public readonly Mesh mesh;

            public LtTreeViewItem(MeshRenderer renderer)
            {
                this.renderer = renderer;
                this.gameObject = renderer.gameObject;

                var filter = gameObject.GetComponent<MeshFilter>();
                this.mesh = filter == null ? null : filter.sharedMesh;
            }

            public object GetValue(int idx)
            {
                if (renderer == null || gameObject == null)
                {
                    return null; // 途中でobjectがdestroyされた場合は何もしない
                }
                switch (idx)
                {
                    case COL_ObjectName:
                        return gameObject.name;
                    case COL_MeshName:
                        return mesh != null ? mesh.name : null;
                    case COL_BoundsSize:
                        return getRendererBoundsSize();

                    case COL_ObjectLayer:
                        return gameObject.layer;
                    case COL_CastShadows:
                        return renderer.shadowCastingMode;
                    case COL_ReceiveShadows:
                        return renderer.receiveShadows;
                    case COL_ContributeGI:
                        return isLightmapStatic();

                    case COL_ReceiveGI:
                        return isLightmapStatic() ? renderer.receiveGI : (ReceiveGI?)null;

                    case COL_ScaleInLightmap:
                        return isLightmapped() ? renderer.scaleInLightmap : (float?)null;
                    case COL_StitchSeams:
                        return isLightmapped() ? renderer.stitchLightmapSeams : (bool?)null;

                    case COL_LightmapIndex:
                        return getLightmapIndex();
                    case COL_LightmapSize:
                        return getLightmapSize();
                    case COL_AreaSize:
                        return getAreaSize(AreaInfoType.Size);
                    case COL_AreaWidth:
                        return getAreaSize(AreaInfoType.Width);
                    case COL_AreaHeight:
                        return getAreaSize(AreaInfoType.Height);

                    case COL_HasUV2:
                        return mesh != null ? mesh.uv2.Length != 0 : (bool?)null;
                    case COL_GenerateLightmapUVs:
                        return getGenerateSecondaryUV();

                    default:
                        return null;
                }
            }

            internal float? getRendererBoundsSize()
            {
                if (renderer == null)
                {
                    return null;
                }
                var es = renderer.bounds.extents * 2;
                return Mathf.Round(Mathf.Sqrt(Mathf.Max(es.x * es.y, es.y * es.z, es.z * es.x)) * 100) / 100;
            }

            /// <summary>
            /// ContributeGI が設定されているかどうか
            /// </summary>
            /// <returns></returns>
            internal bool isLightmapStatic()
            {
                return GameObjectUtility.AreStaticEditorFlagsSet(gameObject, StaticEditorFlags.ContributeGI);
            }

            /// <summary>
            /// ContributeGI かつ ReceiveGI.Lightmaps かどうか
            /// </summary>
            /// <returns></returns>
            internal bool isLightmapped()
            {
                return isLightmapStatic() && renderer.receiveGI == ReceiveGI.Lightmaps;
            }

            internal int? getLightmapIndex()
            {
                if (!isLightmapped())
                {
                    return null;
                }
                if (mesh == null)
                { // mesh はこのメソッドでは使わないけど他項目と条件だけ合わせておく
                    return null;
                }

                var idxLm = renderer.lightmapIndex;
                if (idxLm < 0 || 65533 < idxLm || LightmapSettings.lightmaps.Length <= idxLm)
                {
                    return null;
                }
                return idxLm;
            }

            internal bool? getGenerateSecondaryUV()
            {
                if (mesh == null)
                {
                    return null;
                }
                var path = AssetDatabase.GetAssetPath(mesh);
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }
                var importer = ModelImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    return null;
                }
                return importer.generateSecondaryUV;
            }

            internal int? getAreaSize(AreaInfoType type)
            {
                var lightmap = getLightmapData();
                if (lightmap == null || lightmap.lightmapColor == null)
                {
                    return null;
                }

                // ライトマップのUVはUV2優先、無ければUV1
                if (mesh == null)
                {
                    return null;
                }
                var uv = mesh.uv2.Length != 0 ? mesh.uv2 : mesh.uv;
                if (uv.Length == 0)
                {
                    return null;
                }

                var so = renderer.lightmapScaleOffset;
                var uv_min = new Vector2(uv.Select(i => i.x).Min() * so.x + so.z, uv.Select(i => i.y).Min() * so.y + so.w);
                var uv_max = new Vector2(uv.Select(i => i.x).Max() * so.x + so.z, uv.Select(i => i.y).Max() * so.y + so.w);

                var width = Mathf.Abs(uv_max.x - uv_min.x) * lightmap.lightmapColor.width;
                var height = Mathf.Abs(uv_max.y - uv_min.y) * lightmap.lightmapColor.height;

                switch (type)
                {
                    case AreaInfoType.Width:
                        return Mathf.RoundToInt(width);
                    case AreaInfoType.Height:
                        return Mathf.RoundToInt(height);
                    default:
                        return Mathf.RoundToInt(Mathf.Sqrt(width * height));
                }
            }

            internal enum AreaInfoType
            {
                Width, Height, Size,
            }

            internal LightmapData getLightmapData()
            {
                var idxLm = getLightmapIndex();
                if (idxLm == null)
                {
                    return null;
                }
                return LightmapSettings.lightmaps[idxLm.Value];
            }

            internal int? getLightmapSize()
            {
                var lightmap = getLightmapData();
                if (lightmap == null || lightmap.lightmapColor == null)
                {
                    return null;
                }
                return lightmap.lightmapColor.width;
            }
        }

        internal class MeshListView : TreeView
        {
            public LtTreeViewItem[] items = new LtTreeViewItem[0];

            public MeshListView(TreeViewState state) : base(state, NewHeader())
            {
                multiColumnHeader.sortingChanged += OnSortingChanged;
            }

            private static MultiColumnHeader NewHeader()
            {
                var column = new List<MultiColumnHeaderState.Column>();
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Game Object", "Game Object"),
                    width = 160,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Mesh", "Mesh"),
                    width = 90,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Bounds Size", "Bounds Size"),
                    width = 50,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Layer", "Layer"),
                    width = 90,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Cast Shadows", "Cast Shadows"),
                    width = 90,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Receive Shadows", "Receive Shadows"),
                    width = 30,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Contribute GI", "Contribute GI"),
                    width = 30,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Receive GI", "Receive GI"),
                    width = 90,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Scale In Lightmap", "Scale In Lightmap"),
                    width = 90,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Stitch Seams", "Stitch Seams"),
                    width = 30,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Lightmap Index", "Lightmap Index"),
                    width = 30,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Area Width", "Area Width"),
                    width = 50,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Area Height", "Area Height"),
                    width = 50,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Area Size", "Area Size"),
                    width = 50,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Lightmap Width", "Lightmap Width"),
                    width = 50,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Has UV2", "Has UV2"),
                    width = 50,
                });
                column.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("GenerateLightmapUVs", "GenerateLightmapUVs"),
                    width = 50,
                });

                var state = new MultiColumnHeaderState(column.ToArray());
                var header = new MultiColumnHeader(state);

                return header;
            }

            protected override TreeViewItem BuildRoot()
            {
                var id = 0;
                var root = new TreeViewItem { id = id++, depth = -1, displayName = "Root" };

                var list = new List<TreeViewItem>();
                foreach (var item in items)
                {
                    list.Add(new ExTreeViewItem { id = id++, depth = 0, displayName = item.renderer.name, info = item });
                }

                SetupParentsAndChildrenFromDepths(root, list);
                return root;
            }

            protected override void SelectionChanged(IList<int> selectedIds)
            {
                Selection.objects = getSelectedGameObjects();
            }

            private ExTreeViewItem[] getSelectedTreeViewItem(ExTreeViewItem current = null)
            {
                // TreeView から今選択されている ExTreeViewItem を取得
                var items = GetSelection().Select(id => FindItem(id, rootItem) as ExTreeViewItem)
                    .Where(item => item != null)
                    .ToArray();

                // もし current が null ではなく、Selection に入っていないならば、かわりに current のみを対象にする
                if (current != null && !items.Contains(current))
                {
                    return new ExTreeViewItem[] { current };
                }
                return items;
            }

            protected MeshRenderer[] getSelectedMeshRenderers(ExTreeViewItem current = null)
            {
                return getSelectedTreeViewItem(current)
                    .Where(item => item.info.renderer != null)
                    .Select(item => item.info.renderer)
                    .ToArray();
            }

            protected GameObject[] getSelectedGameObjects(ExTreeViewItem current = null)
            {
                return getSelectedTreeViewItem(current)
                    .Where(item => item.info.gameObject != null)
                    .Select(item => item.info.gameObject)
                    .ToArray();
            }

            protected Mesh[] getSelectedMeshes(ExTreeViewItem current = null)
            {
                return getSelectedTreeViewItem(current)
                    .Where(item => item.info.mesh != null)
                    .Select(item => item.info.mesh)
                    .ToArray();
            }

            protected GameObject[] getSelectedModels(ExTreeViewItem current = null)
            {
                return getSelectedMeshes(current)
                    .Select(mesh => AssetDatabase.GetAssetPath(mesh))
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Select(path => AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject)
                    .Where(imp => imp != null).ToArray();
            }

            protected Texture2D[] getSelectedLightmaps(ExTreeViewItem current = null)
            {
                return getSelectedTreeViewItem(current)
                    .Select(item => item.info.getLightmapData()).Where(lmd => lmd != null)
                    .SelectMany(lmd => new Texture2D[] { lmd.lightmapColor, lmd.lightmapDir, lmd.shadowMask })
                    .Where(tex => tex != null).Distinct().ToArray();
            }

            protected override void ContextClickedItem(int id)
            {
                var ev = Event.current;
                ev.Use();

                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Select GameObjects"), false, () =>
                {
                    var select = getSelectedGameObjects();
                    if (select.Length == 0)
                    {
                        return;
                    }
                    Selection.objects = select;
                });
                menu.AddItem(new GUIContent("Select Meshes"), false, () =>
                {
                    var select = getSelectedMeshes();
                    if (select.Length == 0)
                    {
                        return;
                    }
                    Selection.objects = select;
                });
                menu.AddItem(new GUIContent("Select Model Import Settings"), false, () =>
                {
                    var select = getSelectedModels();
                    if (select.Length == 0)
                    {
                        return;
                    }
                    Selection.objects = select;
                });
                menu.AddItem(new GUIContent("Select Lightmaps"), false, () =>
                {
                    var select = getSelectedLightmaps();
                    if (select.Length == 0)
                    {
                        return;
                    }
                    Selection.objects = select;
                });
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Set ScaleInLightmap/x 2.0"), false, () => SetScaleInLightmap(2.0f));
                menu.AddItem(new GUIContent("Set ScaleInLightmap/x 0.5"), false, () => SetScaleInLightmap(0.5f));
                menu.ShowAsContext();
            }

            private void SetScaleInLightmap(float x)
            {
                var targets = GetSelection().Select(id => FindItem(id, rootItem) as ExTreeViewItem)
                                    .Where(item => item != null).Select(item => item.info)
                                    .Where(info => info.renderer != null && info.gameObject != null)
                                    .Where(info => info.isLightmapStatic() && info.renderer.receiveGI == ReceiveGI.Lightmaps)
                                    .Select(info => info.renderer)
                                    .ToArray();
                if (targets.Length != 0)
                {
                    Undo.RecordObjects(targets, "Set ScaleInLightmap");
                    foreach (var r in targets)
                    {
                        r.scaleInLightmap *= x;
                    }
                }
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                var item = (ExTreeViewItem)args.item;
                var info = item.info;

                for (int i = 0; i < args.GetNumVisibleColumns(); i++)
                {
                    if (info.renderer == null || info.gameObject == null)
                    {
                        continue; // 途中でobjectがdestroyされた場合は何もしない
                    }
                    var isActive = info.gameObject.activeInHierarchy && info.renderer.enabled;

                    var cellRect = args.GetCellRect(i);
                    CenterRectUsingSingleLineHeight(ref cellRect);

                    int idx = args.GetColumn(i);
                    switch (idx)
                    {
                        case COL_ScaleInLightmap:
                            if (!info.isLightmapStatic())
                            {
                                continue;
                            }
                            if (info.renderer.receiveGI != ReceiveGI.Lightmaps)
                            {
                                continue;
                            }
                            DrawCellGUI(
                                () => EditorGUI.DelayedFloatField(cellRect, info.renderer.scaleInLightmap),
                                () => getSelectedMeshRenderers(item),
                                "change ScaleInLightmap", (t, v) => t.scaleInLightmap = v);
                            break;

                        case COL_ObjectLayer:
                            DrawCellGUI(
                                () => EditorGUI.LayerField(cellRect, info.gameObject.layer),
                                () => getSelectedGameObjects(item),
                                "change GameObject Layer", (t, v) => t.layer = v);
                            break;

                        case COL_ReceiveGI:
                            if (!info.isLightmapStatic())
                            {
                                continue;
                            }
                            DrawCellGUI(
                                () => (ReceiveGI)EditorGUI.EnumPopup(cellRect, info.renderer.receiveGI),
                                () => getSelectedMeshRenderers(item),
                                "change ReceiveGI", (t, v) => t.receiveGI = v);
                            break;

                        case COL_CastShadows:
                            DrawCellGUI(
                                () => (ShadowCastingMode)EditorGUI.EnumPopup(cellRect, info.renderer.shadowCastingMode),
                                () => getSelectedMeshRenderers(item),
                                "change CastShadows", (t, v) => t.shadowCastingMode = v);
                            break;

                        case COL_ReceiveShadows:
                            DrawCellGUI(
                                () => EditorGUI.ToggleLeft(cellRect, "", info.renderer.receiveShadows),
                                () => getSelectedMeshRenderers(item),
                                "change ReceiveShadows", (t, v) => t.receiveShadows = v);
                            break;

                        case COL_ContributeGI:
                            DrawCellGUI(
                                () => EditorGUI.ToggleLeft(cellRect, "", info.isLightmapStatic()),
                                () => getSelectedGameObjects(item),
                                "change Lightmap static", (t, v) =>
                                {
                                    var flags = GameObjectUtility.GetStaticEditorFlags(t);
                                    if (v)
                                    {
                                        flags |= StaticEditorFlags.ContributeGI;
                                    }
                                    else
                                    {
                                        flags &= ~StaticEditorFlags.ContributeGI;
                                    }
                                    GameObjectUtility.SetStaticEditorFlags(t, flags);
                                });
                            break;

                        case COL_StitchSeams:
                            if (!info.isLightmapStatic())
                            {
                                continue;
                            }
                            if (info.renderer.receiveGI != ReceiveGI.Lightmaps)
                            {
                                continue;
                            }
                            DrawCellGUI(
                                () => EditorGUI.ToggleLeft(cellRect, "", info.renderer.stitchLightmapSeams),
                                () => getSelectedMeshRenderers(item),
                                "change StitchSeams", (t, v) => t.stitchLightmapSeams = v);
                            break;

                        default:
                            // 非アクティブのときはラベル系を Disabled にして区別できるようにする
                            using (new EditorGUI.DisabledGroupScope(!isActive))
                            {
                                GUI.Label(cellRect, "" + info.GetValue(idx));
                            }
                            break;
                    }
                }
            }

            private void DrawCellGUI<V, T>(Func<V> gui, Func<T[]> target, string undoName, Action<T, V> setter) where T : UnityEngine.Object
            {
                EditorGUI.BeginChangeCheck();

                // GUI表示
                var ret = gui();

                // 変更されているなら
                if (EditorGUI.EndChangeCheck())
                {
                    // ターゲット取得
                    var targets = target();

                    // Undo
                    Undo.RecordObjects(targets, undoName);

                    // 変更
                    foreach (var t in targets)
                    {
                        setter(t, ret);
                        EditorUtility.SetDirty(t);
                    }
                }
            }

            public void SortItems()
            {
                var idx = multiColumnHeader.sortedColumnIndex;
                if (idx < 0)
                {
                    return;
                }
                items = (multiColumnHeader.IsSortedAscending(idx) ?
                    items.OrderBy(mci => mci.GetValue(idx)) :
                    items.OrderByDescending(mci => mci.GetValue(idx))).ToArray();
            }

            void OnSortingChanged(MultiColumnHeader header)
            {
                SortItems();
                Reload();
            }

            internal class ExTreeViewItem : TreeViewItem
            {
                public LtTreeViewItem info;
            }
        }
    }
}

#endif
