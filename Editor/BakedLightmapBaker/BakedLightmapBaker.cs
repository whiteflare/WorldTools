/*
 *  The MIT License
 *
 *  Copyright 2022-2024 whiteflare.
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
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace WF.Tool.World.BakeLmapBaker
{
    internal class BakedLightmapBaker : EditorWindow
    {
        private const string MENU_NAME = "GameObject/LightmapUVをMeshにベイクする";

        [MenuItem(MENU_NAME, priority = 10)]
        public static void Menu()
        {
            var window = GetWindow<BakedLightmapBaker>("BakedLightmap Baker");
            window.targets = Selection.GetFiltered<GameObject>(SelectionMode.Editable | SelectionMode.ExcludePrefab);
        }

        [MenuItem(MENU_NAME, true)]
        public static bool Menu_Validate()
        {
            return Selection.GetFiltered<GameObject>(SelectionMode.Editable | SelectionMode.ExcludePrefab).Length != 0;
        }

        public GameObject[] targets = { };
        public DefaultAsset folder = null;

        public void OnEnable()
        {
            var so = new SerializedObject(this);
            so.Update();
            so.FindProperty(nameof(targets)).isExpanded = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public void OnGUI()
        {
            var so = new SerializedObject(this);
            so.Update();

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty(nameof(targets)), new GUIContent("ターゲットオブジェクト"), true);
            if (EditorGUI.EndChangeCheck())
            {
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorGUILayout.Space();

            folder = EditorGUILayout.ObjectField(new GUIContent("Asset 保存先 Folder"), folder, typeof(DefaultAsset), false) as DefaultAsset;

            EditorGUILayout.Space();

            if (BlueButton("Bake", !targets.Any(t => t != null)))
            {
                // フォルダ未指定ならば指定
                if (SelectFolderIfNotSpec())
                {
                    // 実行
                    Execute();
                }
            }
        }

        protected static bool BlueButton(string text, bool disabled = false)
        {
            var oldColor = GUI.color;

            using (new EditorGUI.DisabledGroupScope(disabled))
            {
                GUI.color = new Color(0.75f, 0.75f, 1f);
                bool exec = GUI.Button(EditorGUILayout.GetControlRect(), text);
                GUI.color = oldColor;
                return exec;
            }
        }

        private bool SelectFolderIfNotSpec()
        {
            if (folder != null)
            {
                return true;
            }

            var folderPath = EditorUtility.SaveFolderPanel("アセット保存先フォルダの指定", "Assets", "");
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return false; // 保存先キャンセル
            }

            if (!folderPath.StartsWith(Application.dataPath, System.StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.LogWarning("アセット保存先フォルダは Assets 配下を指定してください。");
                return false;
            }

            folderPath = "Assets" + folderPath.Substring(Application.dataPath.Length);
            AssetDatabase.ImportAsset(folderPath); // SaveFolderPanel内で新規作成したフォルダは Import しないと Load できない
            folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);

            return true;
        }

        private string folderPath = null;
        private LightmapData[] lightmaps = { };
        private Dictionary<Material, Dictionary<int, Material>> remapMaterials = new Dictionary<Material, Dictionary<int, Material>>();

        private void Execute()
        {
            InitializeExecute();
            if (targets.Length == 0)
            {
                return;
            }

            // ライトマップのコピー
            this.lightmaps = CopyLightmaps();
            if (this.lightmaps.Length == 0)
            {
                return;
            }

            // GameObjectのコピー
            foreach (var rootGo in targets)
            {
                foreach (var mf in rootGo.GetComponentsInChildren<MeshFilter>())
                {
                    CopyAndBake(mf.gameObject);
                }
            }

            // 終わったら掃除する
            InitializeExecute();
        }

        private void InitializeExecute()
        {
            this.targets = targets.Where(t => t != null).ToArray();
            this.folderPath = AssetDatabase.GetAssetPath(folder);
            this.lightmaps = new LightmapData[0];
            this.remapMaterials.Clear();
        }

        private LightmapData[] CopyLightmaps()
        {
            var result = new LightmapData[LightmapSettings.lightmaps.Length];

            // lightmapIndex の回収
            var indexes = targets.SelectMany(go => go.GetComponentsInChildren<MeshRenderer>())
                .Where(mr => IsLightmapped(mr.gameObject))
                .Select(mr => mr.lightmapIndex)
                .Distinct().OrderBy(i => i);
            if (indexes.Any(idx => idx < 0 || result.Length <= idx))
            {
                EditorUtility.DisplayDialog("BakedLightmapBaker", "ライトベイクの済んでいないMeshRendererがあります。\n先に Generate Lightings を実行してください。", "OK");
                return new LightmapData[0];
            }

            // ライトマップのコピー
            foreach (var idx in indexes)
            {
                var src = LightmapSettings.lightmaps[idx];
                var dst = new LightmapData();
                dst.lightmapColor = CopyAndLoadAsset(folderPath, src.lightmapColor);
                result[idx] = dst;
            }

            return result;
        }

        private void CopyAndBake(GameObject go)
        {
            if (!IsLightmapped(go) || !IsLightmapAssigned(go))
            {
                return;
            }

            // Undo登録
            Undo.RegisterFullObjectHierarchyUndo(go, "BakedLightmap Baking");

            var mfilter = go.GetComponent<MeshFilter>();
            var mrenderer = go.GetComponent<MeshRenderer>();

            // メッシュの複製とUV2のベイク
            mfilter.sharedMesh = CopyAndBakeUV2(mfilter.sharedMesh, mrenderer.lightmapScaleOffset);
            EditorUtility.SetDirty(mfilter);

            // ContributeGI の解除
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) & ~StaticEditorFlags.ContributeGI);
            EditorUtility.SetDirty(go);

            // マテリアルにライトマップを設定
            mrenderer.sharedMaterials = CopyAndBakeLightmap(mrenderer.sharedMaterials, mrenderer.lightmapIndex);
            mrenderer.lightmapIndex = -1; // 本物のライトマップはクリア
            EditorUtility.SetDirty(mrenderer);
        }

        /// <summary>
        /// MeshをコピーしてUV2をベイクする
        /// </summary>
        /// <param name="oldMesh"></param>
        /// <param name="lightmapScaleOffset"></param>
        /// <returns></returns>
        private Mesh CopyAndBakeUV2(Mesh oldMesh, Vector4 lightmapScaleOffset)
        {
            var newMesh = Instantiate(oldMesh);
            newMesh.name = oldMesh.name;

            // UV2ベイク
            {
                var oldUV2 = oldMesh.uv2;
                var newUV2 = new Vector2[oldUV2.Length];
                for (int i = 0; i < newUV2.Length; i++)
                {
                    newUV2[i] = oldUV2[i] * new Vector2(lightmapScaleOffset.x, lightmapScaleOffset.y) + new Vector2(lightmapScaleOffset.z, lightmapScaleOffset.w);
                }
                newMesh.SetUVs(1, newUV2);
            }
            // meshアセットの保存
            return CreateAndLoadAsset(folderPath, newMesh, "asset");

            // meshアセットは複数GO間での共通化はしない。GOによってlightmapScaleOffsetが異なるため共通化できない。
        }

        /// <summary>
        /// MaterialをコピーしてLightmapをベイクする
        /// </summary>
        /// <param name="mats"></param>
        /// <param name="lightmapIndex"></param>
        /// <returns></returns>
        private Material[] CopyAndBakeLightmap(Material[] mats, int lightmapIndex)
        {
            var result = new Material[mats.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var oldMat = mats[i];
                if (!remapMaterials.TryGetValue(oldMat, out var newMatMap))
                {
                    newMatMap = new Dictionary<int, Material>();
                    remapMaterials[oldMat] = newMatMap;
                }
                if (!newMatMap.TryGetValue(lightmapIndex, out var newMat))
                {
                    // 変換可能ならば変換する
                    newMat = SetLightmapToMaterial(oldMat, lightmapIndex);
                    newMatMap[lightmapIndex] = newMat;
                }
                result[i] = newMat;
            }
            return result;
        }

        /// <summary>
        /// 旧マテリアルからライトマップを割り当てた新マテリアルを作成する
        /// </summary>
        /// <param name="oldMat"></param>
        /// <param name="lightmapIndex"></param>
        /// <returns></returns>
        private Material SetLightmapToMaterial(Material oldMat, int lightmapIndex)
        {
            var lightmapColor = lightmaps[lightmapIndex].lightmapColor;
            string sn = oldMat.shader.name;
            if (sn == "Standard" || sn == "Standard (Specular setup)" || sn == "Autodesk Interactive" || sn.StartsWith("Silent/Filamented"))
            {
                var newMat = new Material(oldMat);
                newMat.name = string.Format("Lightmap-{0}_{1}", lightmapIndex, newMat.name);
                newMat.SetTexture("_DetailAlbedoMap", lightmapColor);
                newMat.SetTextureScale("_DetailAlbedoMap", Vector2.one);
                newMat.SetTextureOffset("_DetailAlbedoMap", Vector2.zero);
                newMat.EnableKeyword("_DETAIL_MULX2");
                newMat.SetInt("_UVSec", 1);
                return CreateAndLoadAsset(folderPath, newMat, "mat");
            }
            else if (new Regex(@".*_UnToon_.*").IsMatch(sn) && oldMat.HasProperty("_OcclusionMap"))
            {
                var newMat = new Material(oldMat);
                newMat.name = string.Format("Lightmap-{0}_{1}", lightmapIndex, newMat.name);
                newMat.SetTexture("_OcclusionMap", lightmapColor);
                newMat.EnableKeyword("_AO_ENABLE");
                newMat.SetInt("_AO_Enable", 1);
                newMat.SetInt("_AO_UVType", 1);
                newMat.SetFloat("_GL_LevelMin", 1);
                return CreateAndLoadAsset(folderPath, newMat, "mat");
            }
            else
            {
                Debug.LogWarningFormat(oldMat, "ライトマップ割当変換に対応していないシェーダを持つマテリアルです: {0}", oldMat);
            }
            return oldMat;
        }

        private static T CreateAndLoadAsset<T>(string folderPath, T asset, string ext) where T : Object
        {
            // まだ保存されていないアセット
            var newPath = string.Format("{0}/{1}.{2}", folderPath, asset.name, ext);
            // パス作成
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
            // 保存
            AssetDatabase.CreateAsset(asset, newPath);
            // ロードしなおし
            return AssetDatabase.LoadAssetAtPath<T>(newPath);
        }

        private static T CopyAndLoadAsset<T>(string folderPath, T asset) where T : Object
        {
            var path = AssetDatabase.GetAssetPath(asset);

            // 保存されてるアセットから作るパス
            var newPath = string.Format("{0}/{1}", folderPath, Regex.Replace(path, @"^.*[\\/]", ""));
            // パス作成
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
            // コピー
            AssetDatabase.CopyAsset(path, newPath);
            // ロードしなおし
            return AssetDatabase.LoadAssetAtPath<T>(newPath);
        }

        private static bool IsLightmapped(GameObject go)
        {
            if (!go.activeInHierarchy)
            {
                return false;
            }
            if (!GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.ContributeGI))
            {
                return false;
            }
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                return false;
            }
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled || mr.receiveGI != ReceiveGI.Lightmaps || mr.scaleInLightmap == 0)
            {
                return false;
            }
            return true;
        }

        private static bool IsLightmapAssigned(GameObject go)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                return false;
            }
            return 0 <= mr.lightmapIndex && mr.lightmapIndex <= 65533;
        }
    }
}

#endif
