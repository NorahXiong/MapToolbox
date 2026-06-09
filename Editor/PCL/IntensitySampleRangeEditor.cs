#region License
/******************************************************************************
* Copyright 2018-2021 The AutoCore Authors. All Rights Reserved.
* 
* Licensed under the GNU Lesser General Public License, Version 3.0 (the "License"); 
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
* 
* https://www.gnu.org/licenses/lgpl-3.0.html
* 
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*****************************************************************************/
#endregion


using AutoCore.MapToolbox.PCL;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AutoCore.MapToolbox.Editor.PCL
{
    class IntensitySampleRangeEditor : SettingsProvider
    {
        public static float min = Externs.IntensityPrecentMin;
        public static float max = Externs.IntensityPrecentMax;
        public static float width = Externs.DefaultWidth;
        public static float speed_limit = Externs.DefaultSpeedLimit;
        public IntensitySampleRangeEditor(string path, SettingsScope scopes) : base(path, scopes) { }
        [SettingsProvider] static SettingsProvider Project() => new IntensitySampleRangeEditor("Project/MapToolbox", SettingsScope.Project);
        public override void OnGUI(string searchContext)
        {
            base.OnGUI(searchContext);
            var newMin = min;
            var newMax = max;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.MinMaxSlider("Intensity range to color", ref newMin, ref newMax, 0, 100);
            EditorGUILayout.LabelField($"    {newMin:f3}%-{newMax:f3}%");
            EditorGUILayout.EndHorizontal();
            if (newMin != min || newMax != max)
            {
                min = newMin;
                max = newMax;
                SaveSettings();
            }
            width = EditorGUILayout.FloatField("Default width", width);
            speed_limit = EditorGUILayout.FloatField("Default speed Limit", speed_limit);
            EditorGUILayout.HelpBox("After changing intensity range, click \"Reimport All PCD Files\" then re-add the .pcd asset to the scene. Check Unity Console for debug logs.", MessageType.Info);
            if (GUILayout.Button("Reimport All PCD Files"))
            {
                SaveSettings();
                EditorApplication.delayCall += ReimportAllPcd;
            }
        }
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            base.OnActivate(searchContext, rootElement);
            min = Externs.IntensityPrecentMin;
            max = Externs.IntensityPrecentMax;
            width = Externs.DefaultWidth;
            speed_limit = Externs.DefaultSpeedLimit;
        }
        public override void OnDeactivate()
        {
            base.OnDeactivate();
            SaveSettings();
        }
        void SaveSettings()
        {
            Externs.IntensityPrecentMin = min;
            Externs.IntensityPrecentMax = max;
            Externs.DefaultWidth = width;
            Externs.DefaultSpeedLimit = speed_limit;
            PlayerPrefs.Save();
        }
        static void ReimportAllPcd()
        {
            var assetsPath = Application.dataPath.TrimEnd('/', '\\');
            var pcdFiles = Directory.GetFiles(assetsPath, "*.pcd", SearchOption.AllDirectories);
            Debug.Log($"[MapToolbox] Reimporting {pcdFiles.Length} .pcd files with IntensityPrecentMin={Externs.IntensityPrecentMin}, IntensityPrecentMax={Externs.IntensityPrecentMax}");
            foreach (var file in pcdFiles)
            {
                var assetPath = "Assets" + file.Substring(assetsPath.Length).Replace('\\', '/');
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[MapToolbox] Reimported: {assetPath}");
            }
        }
    }
}