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

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Packages.MapToolbox
{
    [RequireComponent(typeof(AddOrRemovable))]
    public class OperationArea : WayTypeBase<OperationArea>, IAddOrRemoveTarget
    {
        public const int CornerCount = 4;
        public float width = 2f;
        public float length = 5f;
        [System.NonSerialized] internal CargoArea cargoArea;

        protected override void Start()
        {
            base.Start();
            LineRenderer.startWidth = LineRenderer.endWidth = 0.2f;
            LineRenderer.startColor = LineRenderer.endColor = Color.cyan;
        }
        public void OnAdd()
        {
            RemoveLoopNodesRef();
            if (Way.Nodes.Count < CornerCount)
            {
                AddNextPoint(GetClickedPoint());
            }
            UseLoopNodesRef();
            UpdateRenderer();
        }
        public void OnRemove()
        {
            RemoveLoopNodesRef();
            RemoveLastNode();
            UseLoopNodesRef();
            UpdateRenderer();
        }
        public void MouseEnterInspector() { }
        internal void RegenerateFromSharedEdge(Vector3 sharedMid, Vector3 heading, Vector3 perp)
        {
            if (width > length) length = width;
            float hw = width;
            float hl = length * 0.5f;
            var nodes = Way.Nodes.Where(_ => _ != null).ToList();
            if (nodes.Count > 1 && nodes.First().Equals(nodes.Last()))
                nodes.RemoveAt(nodes.Count - 1);
            if (nodes.Count < 4) return;
            nodes[0].Position = sharedMid + heading * (-hl);
            nodes[1].Position = sharedMid + heading * (+hl);
            nodes[2].Position = sharedMid + heading * (+hl) + perp * (+hw);
            nodes[3].Position = sharedMid + heading * (-hl) + perp * (+hw);
            UpdateRenderer();
        }
        private void RemoveLoopNodesRef()
        {
            if (Way.Nodes.Count > 2 && Way.Nodes.First().Equals(Way.Nodes.Last()))
            {
                Way.Nodes.RemoveLast();
            }
        }
        private void UseLoopNodesRef()
        {
            if (Way.Nodes.Count > 2 && !Way.Nodes.First().Equals(Way.Nodes.Last()))
            {
                Way.Nodes.Add(Way.Nodes.First());
            }
        }
    }
    [CustomEditor(typeof(OperationArea))]
    class OperationAreaEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                var target = (OperationArea)this.target;
                if (target.cargoArea != null)
                {
                    if (target.cargoArea.vehicle_parking != null)
                    {
                        target.cargoArea.vehicle_parking.length = target.length;
                    }
                    target.cargoArea.RegenerateAll();
                }
            }
        }
    }
}