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

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Packages.MapToolbox
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Relation))]
    public class CargoArea : MonoBehaviour
    {
        public enum SubType
        {
            loading,
            unloading,
            loading_and_unloading,
        }
        public SubType subType = SubType.loading;
        public float duration = 300;
        public VehicleParkingArea vehicle_parking;
        public OperationArea operation;
        public Relation Relation => GetComponent<Relation>() ?? gameObject.AddComponent<Relation>();
        Lanelet2Map Lanelet2Map => GetComponentInParent<Lanelet2Map>();
        internal bool Valide => vehicle_parking != null && vehicle_parking.Way.Valide;
        internal bool drawGizmos = false;

        private void Start()
        {
            if (vehicle_parking)
            {
                vehicle_parking.cargoArea = this;
                vehicle_parking.Way.Ref.TryAdd(Relation);
                Relation.Members.TryAdd(vehicle_parking.Way);
            }
            if (operation)
            {
                operation.cargoArea = this;
                operation.Way.Ref.TryAdd(Relation);
                Relation.Members.TryAdd(operation.Way);
            }
            RefreshColors();
        }
        internal static CargoArea AddNew(Lanelet2Map map, SubType subType)
        {
            var ret = map.AddChildGameObject<CargoArea>(map.transform.ChildMapId());
            ret.gameObject.RecordUndoCreateGo();
            ret.subType = subType;
            ret.vehicle_parking = VehicleParkingArea.AddNew(map);
            ret.operation = OperationArea.AddNew(map);
            ret.vehicle_parking.cargoArea = ret;
            ret.operation.cargoArea = ret;
            ret.vehicle_parking.Way.Ref.Add(ret.Relation);
            ret.operation.Way.Ref.Add(ret.Relation);
            ret.Relation.Members.Add(ret.vehicle_parking.Way);
            ret.Relation.Members.Add(ret.operation.Way);
            ret.PopulateDefaultCorners();
            ret.RefreshColors();
            return ret;
        }
        internal void RefreshColors()
        {
            if (vehicle_parking)
            {
                vehicle_parking.RefreshColor(subType);
            }
        }
        internal void SelectVehicleParking()
        {
            var list = new List<GameObject> { gameObject };
            if (vehicle_parking)
            {
                list.Add(vehicle_parking.gameObject);
                if (vehicle_parking.Way != null)
                    foreach (var n in vehicle_parking.Way.Nodes)
                        if (n != null) list.Add(n.gameObject);
            }
            Selection.objects = list.Distinct().ToArray();
        }
        internal void SelectOperation()
        {
            var list = new List<GameObject> { gameObject };
            if (operation)
            {
                list.Add(operation.gameObject);
                if (operation.Way != null)
                    foreach (var n in operation.Way.Nodes)
                        if (n != null) list.Add(n.gameObject);
            }
            Selection.objects = list.Distinct().ToArray();
        }
        internal void AttachToLanelet(Lanelet lanelet)
        {
            if (lanelet != null)
            {
                lanelet.Relation.Members.TryAdd(Relation);
            }
        }
        internal void DetachFromLanelet(Lanelet lanelet)
        {
            if (lanelet != null)
            {
                lanelet.Relation.Members.Remove(Relation);
            }
        }
        internal bool IsAttachedTo(Lanelet lanelet) => lanelet != null && lanelet.Relation.Members.Contains(Relation);
        internal void PopulateDefaultCorners()
        {
            Vector3 center = Lanelet2Map != null ? Lanelet2Map.transform.position : Vector3.zero;
            if (SceneView.lastActiveSceneView != null)
            {
                center = SceneView.lastActiveSceneView.pivot;
            }
            center.y = Utils.GetHeight(center);
            float w = vehicle_parking.width;
            float l = vehicle_parking.length;
            float hl = l * 0.5f;
            Vector3 heading = Vector3.forward;
            Vector3 perp = Vector3.right;
            vehicle_parking.Way.InsertNode(center + heading * (-hl) + perp * (-w), 0);
            vehicle_parking.Way.InsertNode(center + heading * (+hl) + perp * (-w), 1);
            vehicle_parking.Way.InsertNode(center + heading * (+hl), 2);
            vehicle_parking.Way.InsertNode(center + heading * (-hl), 3);
            vehicle_parking.Way.Nodes.Add(vehicle_parking.Way.Nodes.First());
            vehicle_parking.UpdateRenderer();
            operation.Way.InsertNode(center + heading * (-hl), 0);
            operation.Way.InsertNode(center + heading * (+hl), 1);
            operation.Way.InsertNode(center + heading * (+hl) + perp * (+w), 2);
            operation.Way.InsertNode(center + heading * (-hl) + perp * (+w), 3);
            operation.Way.Nodes.Add(operation.Way.Nodes.First());
            operation.UpdateRenderer();
        }
        internal void RegenerateAll()
        {
            if (vehicle_parking == null || operation == null) return;
            var vpNodes = vehicle_parking.Way.Nodes.Where(_ => _ != null).ToList();
            if (vpNodes.Count > 1 && vpNodes.First().Equals(vpNodes.Last()))
                vpNodes.RemoveAt(vpNodes.Count - 1);
            if (vpNodes.Count < 4) return;
            var opNodes = operation.Way.Nodes.Where(_ => _ != null).ToList();
            if (opNodes.Count > 1 && opNodes.First().Equals(opNodes.Last()))
                opNodes.RemoveAt(opNodes.Count - 1);
            if (opNodes.Count < 4) return;
            Vector3 sharedMid = (vpNodes[2].Position + vpNodes[3].Position) * 0.5f;
            Vector3 heading = vpNodes[1].Position - vpNodes[0].Position;
            heading.y = 0;
            if (heading.sqrMagnitude < 1e-5f) return;
            heading.Normalize();
            Vector3 perp = Vector3.Cross(heading, Vector3.up);
            if (perp.sqrMagnitude < 1e-5f) return;
            perp.Normalize();
            vehicle_parking.RegenerateFromSharedEdge(sharedMid, heading, perp);
            operation.RegenerateFromSharedEdge(sharedMid, heading, perp);
        }
        private void OnDrawGizmos()
        {
            if (!drawGizmos || vehicle_parking == null || vehicle_parking.Way == null)
            {
                return;
            }
            var nodes = vehicle_parking.Way.Nodes.Where(_ => _ != null).ToList();
            if (nodes.Count > 1 && nodes.First().Equals(nodes.Last()))
            {
                nodes.RemoveAt(nodes.Count - 1);
            }
            if (nodes.Count < 2)
            {
                return;
            }
            Vector3 center = Vector3.zero;
            foreach (var n in nodes)
            {
                center += n.Position;
            }
            center /= nodes.Count;
            Vector3 dir = nodes[1].Position - nodes[0].Position;
            dir.y = 0;
            if (dir.sqrMagnitude < 1e-5f)
            {
                return;
            }
            dir.Normalize();
            float len = 3f;
            Vector3 tip = center + dir * len;
            Gizmos.color = Color.white;
            Gizmos.DrawLine(center, tip);
            Vector3 perp = Vector3.Cross(dir, Vector3.up);
            if (perp.sqrMagnitude > 1e-5f)
            {
                perp.Normalize();
                Vector3 back = tip - dir * 0.8f;
                Gizmos.DrawLine(tip, back + perp * 0.4f);
                Gizmos.DrawLine(tip, back - perp * 0.4f);
            }
            if (operation != null && operation.Way != null)
            {
                var opNodes = operation.Way.Nodes.Where(_ => _ != null).ToList();
                if (opNodes.Count > 1 && opNodes.First().Equals(opNodes.Last()))
                {
                    opNodes.RemoveAt(opNodes.Count - 1);
                }
                if (opNodes.Count > 0)
                {
                    Vector3 opCenter = Vector3.zero;
                    foreach (var n in opNodes)
                    {
                        opCenter += n.Position;
                    }
                    opCenter /= opNodes.Count;
                    Color c = Gizmos.color;
                    c.a = 0.5f;
                    Gizmos.color = c;
                    Gizmos.DrawLine(center, opCenter);
                }
            }
        }
    }
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CargoArea))]
    class CargoAreaEditor : Editor
    {
        CargoArea Target => target as CargoArea;
        private void OnEnable()
        {
            foreach (CargoArea item in targets)
            {
                item.drawGizmos = true;
            }
        }
        private void OnDisable()
        {
            foreach (CargoArea item in targets)
            {
                item.drawGizmos = false;
            }
        }
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            Target.RefreshColors();
            if (GUILayout.Button("Select vehicle_parking"))
            {
                Target.SelectVehicleParking();
            }
            if (GUILayout.Button("Select operation"))
            {
                Target.SelectOperation();
            }
            var lanelet = Selection.gameObjects
                .Select(_ => _.GetComponent<Lanelet>())
                .FirstOrDefault(_ => _ != null);
            if (lanelet != null && !lanelet.Equals(Target))
            {
                if (Target.IsAttachedTo(lanelet))
                {
                    if (GUILayout.Button("Detach from Lanelet"))
                    {
                        Target.DetachFromLanelet(lanelet);
                    }
                }
                else
                {
                    if (GUILayout.Button("Attach to Lanelet"))
                    {
                        Target.AttachToLanelet(lanelet);
                    }
                }
            }
        }
    }
}