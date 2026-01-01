#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Transform))]
public class GhostTouchGizmo : Editor
{
    static readonly float radius = 0.09f;
    static readonly Color gizmoColor = new Color(1f, 0.4f, 0.7f, 0.5f);

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    static void DrawTouchGizmo(Transform transform, GizmoType gizmoType)
    {
        if (!transform.name.StartsWith("GhostTouch_")) return;
        
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.DrawSphere(transform.position, radius * 0.3f);
    }
}
#endif
