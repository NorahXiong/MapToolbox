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
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Packages.MapToolbox
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(Relation))]
    public class Lanelet : MonoBehaviour
    {
        public enum SubType
        {
            road,
            crosswalk,
        }
        public SubType subType;
        static Material Material { get; set; }
        Lanelet2Map Lanelet2Map => GetComponentInParent<Lanelet2Map>();
        private void Start()
        {
            if (!Material)
            {
                Material = new Material(AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat"));
                Color color = Color.green;
                color.a = 0.2f;
                Material.SetColor("_Color", color);
            }
            MeshRenderer.sharedMaterial = Material;
            width = Externs.DefaultWidth;
            speed_limit = Externs.DefaultSpeedLimit;
            left.Way.OnNodeMoved -= OnNodeMoved;
            left.Way.OnNodeMoved += OnNodeMoved;
            right.Way.OnNodeMoved -= OnNodeMoved;
            right.Way.OnNodeMoved += OnNodeMoved;
            left.Way.Ref.TryAdd(Relation);
            right.Way.Ref.TryAdd(Relation);
            Relation.Members.TryAdd(left.Way);
            Relation.Members.TryAdd(right.Way);
            left.LineRenderer.loop = loop;
            right.LineRenderer.loop = loop;
            UpdateRenderer();
        }
        private void OnNodeMoved(Node node) => UpdateRenderer();
        public MeshFilter MeshFilter => GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        public MeshRenderer MeshRenderer => GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        public Relation Relation => GetComponent<Relation>() ?? gameObject.AddComponent<Relation>();
        public List<Vector3> CenterPoints { get; set; } = new List<Vector3>();
        public LineThin left;
        public LineThin right;
        public float width;
        public float speed_limit;
        public bool loop = false;
        public enum TurnDirection
        {
            Null,
            Straight,
            Left,
            Right
        }
        public TurnDirection turnDirection = TurnDirection.Null;
        internal Lanelet AddNew() => AddNew(Lanelet2Map);
        internal static Lanelet AddNew(Lanelet2Map map)
        {
            var ret = map.AddChildGameObject<Lanelet>(map.transform.ChildMapId());
            ret.gameObject.RecordUndoCreateGo();
            ret.left = LineThin.AddNew(map);
            ret.right = LineThin.AddNew(map);
            ret.left.Way.Ref.Add(ret.Relation);
            ret.right.Way.Ref.Add(ret.Relation);
            return ret;
        }
        private void RemovePoints()
        {
            OpenLoop();
            if (CenterPoints.Count > 0)
            {
                CenterPoints.RemoveAt(CenterPoints.Count - 1);
            }
            left.RemovePointFinal();
            right.RemovePointFinal();
            CloseLoop();
        }
        private void AddPoints()
        {
            OpenLoop();
            var centerPoint = Utils.MousePointInSceneView;
            centerPoint.y = Utils.GetHeight(centerPoint);
            if (CenterPoints.Count > 1)
            {
                var lastPoint = CenterPoints.Last();
                var leftPoint = centerPoint + Vector3.Cross(centerPoint - lastPoint, Vector3.up).normalized * width / 2;
                var rightPoint = centerPoint + Vector3.Cross(centerPoint - lastPoint, Vector3.down).normalized * width / 2;
                left.AddNextPoint(leftPoint);
                right.AddNextPoint(rightPoint);
            }
            else if (CenterPoints.Count == 1)
            {
                var lastPoint = CenterPoints.Last();
                var leftPoint0 = lastPoint + Vector3.Cross(centerPoint - lastPoint, Vector3.up).normalized * width / 2;
                var rightPoint0 = lastPoint + Vector3.Cross(centerPoint - lastPoint, Vector3.down).normalized * width / 2;
                var leftPoint1 = centerPoint + Vector3.Cross(centerPoint - lastPoint, Vector3.up).normalized * width / 2;
                var rightPoint1 = centerPoint + Vector3.Cross(centerPoint - lastPoint, Vector3.down).normalized * width / 2;
                left.AddNextPoint(leftPoint0);
                right.AddNextPoint(rightPoint0);
                left.AddNextPoint(leftPoint1);
                right.AddNextPoint(rightPoint1);
            }
            CenterPoints.Add(centerPoint);
            CloseLoop();
        }
        private void OpenLoop()
        {
            if (loop)
            {
                RemoveClosingNode(left.Way.Nodes);
                RemoveClosingNode(right.Way.Nodes);
                left.UpdateRenderer();
                right.UpdateRenderer();
            }
        }
        private void CloseLoop()
        {
            if (loop)
            {
                AddClosingNode(left.Way.Nodes);
                AddClosingNode(right.Way.Nodes);
                left.UpdateRenderer();
                right.UpdateRenderer();
            }
        }
        private static void RemoveClosingNode(List<Node> nodes)
        {
            if (nodes.Count > 2 && nodes.First().Equals(nodes.Last()))
                nodes.RemoveAt(nodes.Count - 1);
        }
        private static void AddClosingNode(List<Node> nodes)
        {
            if (nodes.Count > 2 && !nodes.First().Equals(nodes.Last()))
                nodes.Add(nodes.First());
        }
        internal void OnEditorEnable()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
            SceneView.duringSceneGui += DuringSceneGui;
            foreach (var item in Relation.Members)
            {
                if (item is Relation)
                {
                    var re = item.GetComponent<RegulatoryElement>();
                    if (re)
                    {
                        re.drawGizmos = true;
                    }
                }
            }
        }
        internal void OnEditorDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
            if (destroyed)
            {
                return;
            }
            if (gameObject != null)
            {
                SceneVisibilityManager.instance.EnablePicking(gameObject, true);
            }
            foreach (var item in Relation.Members)
            {
                if (item is Relation)
                {
                    var re = item.GetComponent<RegulatoryElement>();
                    if (re)
                    {
                        re.drawGizmos = false;
                    }
                }
            }
        }
        private void OnDestroy() => destroyed = true;
        private void DuringSceneGui(SceneView obj)
        {
            if (EditorUpdate.MouseInSceneView)
            {
                if (EditorUpdate.MouseLeftButtonDownWithCtrl && EditorUpdate.MouseLeftButtonDownWithShift)
                {
                    RemovePoints();
                    UpdateRenderer();
                    SceneVisibilityManager.instance.DisablePicking(gameObject, true);
                }
                else if (EditorUpdate.MouseLeftButtonDownWithCtrl)
                {
                    AddPoints();
                    UpdateRenderer();
                    SceneVisibilityManager.instance.DisablePicking(gameObject, true);
                }
            }
        }
        public enum ReversedMode
        {
            None,
            Left,
            Right,
            All
        }
        public ReversedMode CurrentReversedMode = ReversedMode.None;
        private bool destroyed;

        internal void UpdateRenderer()
        {
            MeshFilter.sharedMesh?.Clear();
            if (left.Way.Nodes.Count > 1 || right.Way.Nodes.Count > 1)
            {
                List<Vector3> leftPoints = left.Way.Nodes.Select(_ => _.Position).ToList();
                List<Vector3> rightPoints = right.Way.Nodes.Select(_ => _.Position).ToList();
                MeshFilter.sharedMesh = LinkLeftRightPointsMesh(leftPoints, rightPoints);
                for (int i = 0; i < 4; i++)
                {
                    if (NeedReUpdateRenderer(ref leftPoints, ref rightPoints))
                    {
                        MeshFilter.sharedMesh.Clear();
                        MeshFilter.sharedMesh = LinkLeftRightPointsMesh(leftPoints, rightPoints);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private Mesh LinkLeftRightPointsMesh(List<Vector3> left, List<Vector3> right)
        {
            Mesh ret = new Mesh
            {
                name = name
            };
            var count = left.Count + right.Count - 2;
            var lastLeft = left.First();
            var lastRight = right.First();
            var lastLeftIndex = 1;
            var lastRightIndex = 0;
            var leftCount = 1;
            var rightCount = 1;
            List<Vector3> points = new List<Vector3> { lastRight, lastLeft };
            List<int> indices = new List<int>();
            for (int i = 0; i < count; i++)
            {
                float dl = 0, dr = 0;
                bool addLeft = true, addRight = true;
                if (left.Count > leftCount)
                {
                    dl = Vector3.Distance(left.ElementAt(leftCount), lastRight);
                }
                else
                {
                    addLeft = false;
                }
                if (right.Count > rightCount)
                {
                    dr = Vector3.Distance(right.ElementAt(rightCount), lastLeft);
                }
                else
                {
                    addRight = false;
                }
                if (addLeft || addRight)
                {
                    indices.Add(lastRightIndex);
                    indices.Add(lastLeftIndex);
                }
                else
                {
                    return ret;
                }
                if (addLeft && addRight)
                {
                    if (dl > dr)
                    {
                        addLeft = false;
                    }
                }
                if (addLeft)
                {
                    lastLeft = left.ElementAt(leftCount++);
                    lastLeftIndex = i + 2;
                    points.Add(lastLeft);
                    indices.Add(lastLeftIndex);
                }
                else
                {
                    lastRight = right.ElementAt(rightCount++);
                    lastRightIndex = i + 2;
                    points.Add(lastRight);
                    indices.Add(lastRightIndex);
                }
            }
            ret.SetVertices(points);
            ret.SetIndices(indices, MeshTopology.Triangles, 0);
            return ret;
        }
        private bool NeedReUpdateRenderer(ref List<Vector3> leftPoints, ref List<Vector3> rightPoints)
        {
            List<Vector3> normals = new List<Vector3>();
            MeshFilter.sharedMesh.RecalculateNormals();
            MeshFilter.sharedMesh.GetNormals(normals);
            if (normals.Count > 1)
            {
                for (int i = 0; i < normals.Count; i++)
                {
                    if (Vector3.Angle(Vector3.up, normals[i]) > 90)
                    {
                        switch (CurrentReversedMode)
                        {
                            case ReversedMode.None:
                                CurrentReversedMode = ReversedMode.Left;
                                leftPoints.Reverse();
                                return true;
                            case ReversedMode.Left:
                                CurrentReversedMode = ReversedMode.Right;
                                leftPoints.Reverse();
                                rightPoints.Reverse();
                                return true;
                            case ReversedMode.Right:
                                CurrentReversedMode = ReversedMode.All;
                                leftPoints.Reverse();
                                return true;
                            case ReversedMode.All:
                                CurrentReversedMode = ReversedMode.None;
                                leftPoints.Reverse();
                                rightPoints.Reverse();
                                return true;
                            default:
                                return false;
                        }
                    }
                }
            }
            return false;
        }
        public bool CanDuplicateLeft => left.Way.Nodes.Count > 1 && left.OnlyUsedBy(Relation);
        public bool CanDuplicateRight => right.Way.Nodes.Count > 1 && right.OnlyUsedBy(Relation);
        public bool CanReverse => true;
        public bool Valide => left.Way.Valide && right.Way.Valide;
        internal void DuplicateLeft()
        {
            if (CanDuplicateLeft)
            {
                var lanelet = Lanelet2Map.AddChildGameObject<Lanelet>(Lanelet2Map.transform.ChildMapId());
                lanelet.gameObject.RecordUndoCreateGo();
                lanelet.left = LineThin.AddNew(Lanelet2Map);
                lanelet.right = left;
                lanelet.left.Way.Ref.Add(lanelet.Relation);
                lanelet.right.Way.Ref.Add(lanelet.Relation);
                lanelet.left.DuplicateNodes(right, left);
                UpdateRenderer();
            }
        }
        internal void DuplicateRight()
        {
            if (CanDuplicateRight)
            {
                var lanelet = Lanelet2Map.AddChildGameObject<Lanelet>(Lanelet2Map.transform.ChildMapId());
                lanelet.gameObject.RecordUndoCreateGo();
                lanelet.right = LineThin.AddNew(Lanelet2Map);
                lanelet.left = right;
                lanelet.left.Way.Ref.Add(lanelet.Relation);
                lanelet.right.Way.Ref.Add(lanelet.Relation);
                lanelet.right.DuplicateNodes(left, right);
                UpdateRenderer();
            }
        }
        internal void Reverse()
        {
            var tmp = right;
            right = left;
            left = tmp;
        }
        internal void ToggleLoop()
        {
            loop = !loop;
            if (loop)
            {
                AddClosingNode(left.Way.Nodes);
                AddClosingNode(right.Way.Nodes);
            }
            else
            {
                RemoveClosingNode(left.Way.Nodes);
                RemoveClosingNode(right.Way.Nodes);
            }
            left.LineRenderer.loop = loop;
            right.LineRenderer.loop = loop;
            left.UpdateRenderer();
            right.UpdateRenderer();
            UpdateRenderer();
        }
        internal void SelectLineThin() => Selection.objects = new[] { left.gameObject, right.gameObject };
        internal void Merge(Lanelet other)
        {
            Undo.RecordObject(this, "Merge Lanelet");
            Undo.RecordObject(left, "Merge Lanelet");
            Undo.RecordObject(right, "Merge Lanelet");
            var otherLeftNodes = other.left.Way.Nodes;
            var otherRightNodes = other.right.Way.Nodes;
            var otherCenterPoints = other.CenterPoints;
            if (otherLeftNodes.Count == 0 || otherRightNodes.Count == 0 || otherCenterPoints.Count == 0)
                return;
            bool append = (otherLeftNodes[0].Equals(left.Way.Nodes.Last()) || otherRightNodes[0].Equals(right.Way.Nodes.Last()));
            bool prepend = (otherLeftNodes.Last().Equals(left.Way.Nodes[0]) || otherRightNodes.Last().Equals(right.Way.Nodes[0]));
            if (append)
            {
                int leftStart = 1;
                int rightStart = (otherRightNodes[0].Equals(right.Way.Nodes.Last())) ? 1 : 0;
                for (int i = leftStart; i < otherLeftNodes.Count; i++)
                {
                    left.Way.Nodes.Add(otherLeftNodes[i]);
                    otherLeftNodes[i].Ref.Add(left.Way);
                }
                for (int i = rightStart; i < otherRightNodes.Count; i++)
                {
                    right.Way.Nodes.Add(otherRightNodes[i]);
                    otherRightNodes[i].Ref.Add(right.Way);
                }
                for (int i = leftStart; i < otherCenterPoints.Count; i++)
                    CenterPoints.Add(otherCenterPoints[i]);
            }
            else if (prepend)
            {
                int leftEnd = otherLeftNodes.Count - 1;
                int rightEnd = (otherRightNodes.Last().Equals(right.Way.Nodes[0])) ? otherRightNodes.Count - 1 : otherRightNodes.Count;
                for (int i = leftEnd - 1; i >= 0; i--)
                {
                    left.Way.Nodes.Insert(0, otherLeftNodes[i]);
                    otherLeftNodes[i].Ref.Add(left.Way);
                }
                for (int i = rightEnd - 1; i >= 0; i--)
                {
                    right.Way.Nodes.Insert(0, otherRightNodes[i]);
                    otherRightNodes[i].Ref.Add(right.Way);
                }
                for (int i = otherCenterPoints.Count - 2; i >= 0; i--)
                    CenterPoints.Insert(0, otherCenterPoints[i]);
            }
            else
            {
                Debug.LogWarning("[Merge] Lanelets are not connected at endpoints");
                return;
            }
            left.UpdateRenderer();
            right.UpdateRenderer();
            UpdateRenderer();
            if (other.left != null)
                Undo.DestroyObjectImmediate(other.left.gameObject);
            if (other.right != null)
                Undo.DestroyObjectImmediate(other.right.gameObject);
            Undo.DestroyObjectImmediate(other.gameObject);
        }
        internal bool CanSplitAt(Node node)
        {
            int idx = left.Way.Nodes.IndexOf(node);
            if (idx < 0) idx = right.Way.Nodes.IndexOf(node);
            return idx >= 2 && idx < left.Way.Nodes.Count - 1 && idx < right.Way.Nodes.Count - 1;
        }
        internal void SplitAt(Node node)
        {
            int idx = left.Way.Nodes.IndexOf(node);
            if (idx < 0) idx = right.Way.Nodes.IndexOf(node);
            if (idx < 2 || idx >= left.Way.Nodes.Count - 1 || idx >= right.Way.Nodes.Count - 1)
            {
                Debug.LogWarning("[Split] Cannot split at 1st, 2nd, or last point");
                return;
            }
            var map = Lanelet2Map;
            var newLanelet = map.AddChildGameObject<Lanelet>(map.transform.ChildMapId());
            newLanelet.gameObject.RecordUndoCreateGo();
            newLanelet.width = width;
            newLanelet.speed_limit = speed_limit;
            LineThin oldLeft = left;
            LineThin oldRight = right;
            Undo.RecordObject(this, "Split Lanelet");
            if (oldLeft.OnlyUsedBy(Relation))
            {
                Undo.RecordObject(oldLeft, "Split Lanelet");
                var nodesToMove = oldLeft.Way.Nodes.GetRange(idx, oldLeft.Way.Nodes.Count - idx);
                oldLeft.Way.Nodes.RemoveRange(idx, nodesToMove.Count);
                newLanelet.left = LineThin.AddNew(map);
                foreach (var n in nodesToMove)
                {
                    n.Ref.Remove(oldLeft.Way);
                    n.Ref.Add(newLanelet.left.Way);
                    newLanelet.left.Way.Nodes.Add(n);
                }
                left = oldLeft;
            }
            else
            {
                newLanelet.left = LineThin.AddNew(map);
                var newLeft = LineThin.AddNew(map);
                for (int i = 0; i < idx; i++)
                    newLeft.Way.Nodes.Add(oldLeft.Way.Nodes[i]);
                for (int i = idx; i < oldLeft.Way.Nodes.Count; i++)
                    newLanelet.left.Way.Nodes.Add(oldLeft.Way.Nodes[i]);
                left = newLeft;
            }
            if (oldRight.OnlyUsedBy(Relation))
            {
                Undo.RecordObject(oldRight, "Split Lanelet");
                var nodesToMove = oldRight.Way.Nodes.GetRange(idx, oldRight.Way.Nodes.Count - idx);
                oldRight.Way.Nodes.RemoveRange(idx, nodesToMove.Count);
                newLanelet.right = LineThin.AddNew(map);
                foreach (var n in nodesToMove)
                {
                    n.Ref.Remove(oldRight.Way);
                    n.Ref.Add(newLanelet.right.Way);
                    newLanelet.right.Way.Nodes.Add(n);
                }
                right = oldRight;
            }
            else
            {
                newLanelet.right = LineThin.AddNew(map);
                var newRight = LineThin.AddNew(map);
                for (int i = 0; i < idx; i++)
                    newRight.Way.Nodes.Add(oldRight.Way.Nodes[i]);
                for (int i = idx; i < oldRight.Way.Nodes.Count; i++)
                    newLanelet.right.Way.Nodes.Add(oldRight.Way.Nodes[i]);
                right = newRight;
            }
            left.Way.Ref.TryAdd(Relation);
            right.Way.Ref.TryAdd(Relation);
            Relation.Members.TryAdd(left.Way);
            Relation.Members.TryAdd(right.Way);
            newLanelet.left.Way.Ref.Add(newLanelet.Relation);
            newLanelet.right.Way.Ref.Add(newLanelet.Relation);
            newLanelet.Relation.Members.Add(newLanelet.left.Way);
            newLanelet.Relation.Members.Add(newLanelet.right.Way);
            if (oldLeft != left && oldLeft != newLanelet.left)
            {
                oldLeft.Way.Ref.Remove(Relation);
                Relation.Members.Remove(oldLeft.Way);
            }
            if (oldRight != right && oldRight != newLanelet.right)
            {
                oldRight.Way.Ref.Remove(Relation);
                Relation.Members.Remove(oldRight.Way);
            }
            left.UpdateRenderer();
            right.UpdateRenderer();
            UpdateRenderer();
            newLanelet.left.UpdateRenderer();
            newLanelet.right.UpdateRenderer();
            newLanelet.UpdateRenderer();
            Selection.activeObject = newLanelet.gameObject;
        }
    }
    [CustomEditor(typeof(Lanelet))]
    [CanEditMultipleObjects]
    class LaneletEditor : Editor
    {
        Lanelet Target => target as Lanelet;
        private void OnEnable()
        {
            if (targets.Length == 1)
                Target.OnEditorEnable();
        }
        private void OnDisable()
        {
            if (targets.Length == 1)
                Target.OnEditorDisable();
        }
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (targets.Length == 2)
            {
                if (GUILayout.Button("Merge Lanelet"))
                {
                    var a = targets[0] as Lanelet;
                    var b = targets[1] as Lanelet;
                    a.Merge(b);
                }
                return;
            }
            Tools.current = Tool.None;
            if (GUILayout.Button("Add Lanelet"))
            {
                Selection.activeObject = Target.AddNew();
            }
            if (GUILayout.Button("Select Line Thin"))
            {
                Target.SelectLineThin();
            }
            if (Target.CanDuplicateLeft)
            {
                if (GUILayout.Button("Duplicate Left"))
                {
                    Target.DuplicateLeft();
                }
            }
            if (Target.CanDuplicateRight)
            {
                if (GUILayout.Button("Duplicate Right"))
                {
                    Target.DuplicateRight();
                }
            }
            if (Target.CanReverse)
            {
                if (GUILayout.Button("Reverse"))
                {
                    Target.Reverse();
                }
            }
            if (GUILayout.Button("UpdateRenderer"))
            {
                Target.UpdateRenderer();
            }
            if (GUILayout.Button(Target.loop ? "Disable Loop" : "Enable Loop"))
            {
                Target.ToggleLoop();
            }

        }
    }
}