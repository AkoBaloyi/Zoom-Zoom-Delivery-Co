using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace SimpleCity.AI
{
    public class Marker : MonoBehaviour
    {
        public Vector3 Position { get => transform.position;}

        public List<Marker> adjacentMarkers;

        [SerializeField]
        private bool openForConnections;

        public bool OpenForconnections
        {
            get { return openForConnections; }
        }

        public List<Vector3> GetAdjacentPositions()
        {
            return new List<Vector3>(adjacentMarkers.Select(x => x.Position).ToList());
        }

#if UNITY_EDITOR
        // Selection is editor-only. Without this guard a player build fails to compile.
        private void OnDrawGizmos()
        {
            if (Selection.activeObject == gameObject)
            {
                Gizmos.color = Color.red;
                if (adjacentMarkers.Count > 0)
                {
                    foreach (var item in adjacentMarkers)
                    {
                       
                        Gizmos.DrawLine(transform.position, item.Position);

                    }
                }
                Gizmos.color = Color.white;
            }
        }
#endif
    }

}
