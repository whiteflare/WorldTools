/*
 *  The MIT License
 *
 *  Copyright 2021 whiteflare.
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
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace WF.Utillty
{
    public class MaterialShaderExplorer : EditorWindow
    {
        [MenuItem("Tools/whiteflare/Misc/Material Shader Explorer")]
        public static void EntryMenuTool() {
            GetWindow<MaterialShaderExplorer>("Material Shader Explorer");
        }

        public TreeViewState treeViewState;
        private MaterialShaderListView treeView;

        private static DefaultAsset baseFolder = null;
        private static bool showMaterialPath = false;

        private void OnEnable() {
            if (treeViewState == null) {
                treeViewState = new TreeViewState();
            }
            treeView = new MaterialShaderListView(treeViewState);
            UpdateTreeView();
        }

        private void OnProjectChange() {
            UpdateTreeView();
        }

        private void OnGUI() {
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            baseFolder = (DefaultAsset) EditorGUILayout.ObjectField("Base Folder", baseFolder, typeof(DefaultAsset), false);
            showMaterialPath = EditorGUILayout.Toggle("Show Material Path", showMaterialPath);
            if (EditorGUI.EndChangeCheck()) {
                UpdateTreeView();
            }

            EditorGUILayout.Space();

            treeView.OnGUI(GUILayoutUtility.GetRect(0, float.MaxValue, 0, float.MaxValue));
        }

        private void UpdateTreeView() {
            var search = AssetDatabase.FindAssets("t:Material")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => !string.IsNullOrEmpty(path))
                .Where(path => path.EndsWith(".mat"));

            if (baseFolder != null) {
                var b = AssetDatabase.GetAssetPath(baseFolder) + "/";
                search = search.Where(path => path.StartsWith(b));
            }

            treeView.Items = search.Select(path => AssetDatabase.LoadAssetAtPath<Material>(path))
                .Where(mat => mat != null)
                .Select(mat => new MaterialShaderItem(mat))
                .ToArray();

            treeView.Reload();
        }

        public struct GuidAndShader
        {
            public Shader shader;
            public string guid;
            public string name;
            public string shaderAssetPath;

            public GuidAndShader(Shader shader) {
                this.shader = shader;
                this.name = shader.name;
                var path = AssetDatabase.GetAssetPath(shader);
                this.guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);

                this.shaderAssetPath = AssetDatabase.GetAssetPath(shader);
                if (string.IsNullOrEmpty(this.shaderAssetPath)) {
                    this.shaderAssetPath = shader.name;
                }
            }

            public override bool Equals(object obj) {
                if (obj is GuidAndShader) {
                    return shader.Equals(((GuidAndShader) obj).shader);
                }
                return false;
            }

            public override int GetHashCode() {
                return shader.GetHashCode();
            }
        }

        public class MaterialShaderItem
        {
            public readonly Material material;
            public readonly GuidAndShader shader;
            public readonly string materialAssetPath;

            public MaterialShaderItem(Material mat) {
                this.material = mat;
                this.shader = new GuidAndShader(mat.shader);
                this.materialAssetPath = AssetDatabase.GetAssetPath(mat);
            }
        }

        public class MaterialShaderListView : TreeView
        {
            private MaterialShaderItem[] items = new MaterialShaderItem[0];
            private List<object> idTable = new List<object>();

            public MaterialShaderItem[] Items
            {
                get => items;
                set {
                    this.items = value;
                    idTable.Clear();

                    foreach (var mat in items) {
                        SetExpanded(AddAndGetId(mat.shader.name), true);
                        SetExpanded(AddAndGetId(mat.shader.shader), true);
                        AddAndGetId(mat.material);
                    }
                }
            }

            private int AddAndGetId(object obj) {
                if (!idTable.Contains(obj)) {
                    idTable.Add(obj);
                }
                return idTable.IndexOf(obj);
            }

            public MaterialShaderListView(TreeViewState state) : base(state) {
                this.rowHeight = 20;
            }

            protected override TreeViewItem BuildRoot() {
                var root = new TreeViewItem { id = 99999999, depth = -1, displayName = "Root" };
                return root;
            }

            protected override IList<TreeViewItem> BuildRows(TreeViewItem root) {
                var rows = GetRows() ?? new List<TreeViewItem>();
                rows.Clear();

                foreach (var name in items.Select(mat => mat.shader.name).Distinct().OrderBy(name => name).ToArray()) {
                    var idShaderName = idTable.IndexOf(name);

                    var itemShaderName = new ExTreeViewItem { id = idShaderName, displayName = name, tag = null };
                    root.AddChild(itemShaderName);
                    rows.Add(itemShaderName);

                    if (IsExpanded(idShaderName)) {
                        foreach (var shader in items.Where(mat => mat.shader.name == name).Select(mat => mat.shader).Distinct().OrderBy(shader => shader.guid)) {
                            var idShader = idTable.IndexOf(shader.shader);

                            var mats = items.Where(mat => mat.shader.Equals(shader));

                            var itemShader = new ExTreeViewItem { id = idShader, displayName = shader.shaderAssetPath, tag = shader.shader };
                            itemShaderName.AddChild(itemShader);
                            rows.Add(itemShader);

                            if (IsExpanded(idShader)) {
                                foreach (var mat in mats) {
                                    var idMaterial = idTable.IndexOf(mat.material);
                                    var itemMaterial = new ExTreeViewItem { id = idMaterial, displayName = showMaterialPath ? mat.materialAssetPath : mat.material.name, tag = mat.material };
                                    itemShader.AddChild(itemMaterial);
                                    rows.Add(itemMaterial);
                                }
                            }
                            else {
                                itemShader.children = CreateChildListForCollapsedParent();
                            }
                        }
                    }
                    else {
                        itemShaderName.children = CreateChildListForCollapsedParent();
                    }
                }

                SetupDepthsFromParentsAndChildren(root);

                return rows;
            }

            protected override void RowGUI(RowGUIArgs args) {
                if (args.item is ExTreeViewItem item) {
                    Texture2D texture = null;
                    if (item.tag is Shader) {
                        texture = EditorGUIUtility.Load("Shader Icon") as Texture2D;
                    }
                    else if (item.tag is Material) {
                        texture = EditorGUIUtility.Load("Material Icon") as Texture2D;
                    }

                    // アイコンを描画する
                    if (texture != null) {
                        Rect toggleRect = args.rowRect;
                        toggleRect.x += GetContentIndent(args.item);
                        toggleRect.width = 16f;
                        GUI.DrawTexture(toggleRect, texture);

                        extraSpaceBeforeIconAndLabel = toggleRect.width + 2f;
                    }
                }

                base.RowGUI(args);
            }

            protected override void SelectionChanged(IList<int> selectedIds) {
                var objects = GetSelection().Select(id => FindItem(id, rootItem) as ExTreeViewItem).Select(item => item.tag).Where(tag => tag != null).ToArray();
                if (0 < objects.Length) {
                    Selection.objects = objects;
                }
            }

            protected override void ContextClicked() {


                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Copy Text"), false, () => {
                    string cp = "";
                    foreach (var text in GetSelection().Select(id => FindItem(id, rootItem) as ExTreeViewItem).Select(item => item.displayName)) {
                        cp += text + "\r\n";
                    }
                    EditorGUIUtility.systemCopyBuffer = cp;
                });

                // 選択中のマテリアルを取得
                var selectMaterials = GetSelection().Select(id => FindItem(id, rootItem) as ExTreeViewItem).Select(item => item.tag as Material).Where(mat => mat != null).ToArray();

                // 選択されているマテリアルのシェーダ名が同一ならばシェーダ切り替えメニューを追加する。
                var selectShaderNames = selectMaterials.Select(mat => mat.shader.name).Distinct().ToArray();
                if (selectShaderNames.Length == 1) {
                    menu.AddSeparator("");
                    var sn = selectShaderNames[0];
                    foreach (var shader in items.Select(item => item.shader).Where(shader => shader.name == sn).Distinct().OrderBy(shader => shader.shaderAssetPath).ToArray()) {
                        menu.AddItem(new GUIContent("Change Shader/" + shader.shaderAssetPath.Replace("/", " ")), false, () => {
                            Undo.RecordObjects(selectMaterials, "Change Shader");
                            foreach(var mat in selectMaterials) {
                                mat.shader = shader.shader;
                            }
                        });
                    }
                }

                menu.ShowAsContext();
            }

            class ExTreeViewItem : TreeViewItem
            {
                public UnityEngine.Object tag;
            }
        }
    }
}

#endif
