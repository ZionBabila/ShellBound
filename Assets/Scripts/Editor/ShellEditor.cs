#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Shell), true)]
public class ShellEditor : Editor
{
    private void OnSceneGUI()
    {
        Shell shell = (Shell)target;
        Transform shellTransform = shell.transform;

        Vector3 anchorWorldPos = shellTransform.TransformPoint(shell.anchorOffset);
        float handleSize = HandleUtility.GetHandleSize(anchorWorldPos) * 0.12f;

        Handles.color = Color.magenta;
        Handles.DrawWireDisc(anchorWorldPos, Vector3.forward, handleSize * 1.3f);
        Handles.DrawLine(anchorWorldPos + Vector3.left * handleSize, anchorWorldPos + Vector3.right * handleSize);
        Handles.DrawLine(anchorWorldPos + Vector3.up * handleSize, anchorWorldPos + Vector3.down * handleSize);
        Handles.Label(anchorWorldPos + Vector3.up * handleSize * 1.5f, "Shell Anchor");

        EditorGUI.BeginChangeCheck();
        var fmh_23_73_639143810217684421 = Quaternion.identity; Vector3 newAnchorWorld = Handles.FreeMoveHandle(anchorWorldPos, handleSize, Vector3.one * 0.1f, Handles.CircleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(shell, "Move Shell Anchor");
            shell.anchorOffset = shellTransform.InverseTransformPoint(newAnchorWorld);
            EditorUtility.SetDirty(shell);
        }

        if (shell is TunaCan tunaCan)
        {
            Vector3 canWorldPos = shellTransform.TransformPoint(tunaCan.anchorOffset);
            Handles.color = Color.cyan;
            Handles.DrawWireDisc(canWorldPos, Vector3.forward, handleSize * 1.1f);
            Handles.DrawLine(canWorldPos + Vector3.left * handleSize, canWorldPos + Vector3.right * handleSize);
            Handles.DrawLine(canWorldPos + Vector3.up * handleSize, canWorldPos + Vector3.down * handleSize);
            Handles.Label(canWorldPos + Vector3.up * handleSize * 1.5f, "Tuna Can Attach");

            EditorGUI.BeginChangeCheck();
            var fmh_41_71_639143810217694689 = Quaternion.identity; Vector3 newCanWorld = Handles.FreeMoveHandle(canWorldPos, handleSize, Vector3.one * 0.1f, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(tunaCan, "Move TunaCan Attach Offset");
                tunaCan.anchorOffset = shellTransform.InverseTransformPoint(newCanWorld);
                EditorUtility.SetDirty(tunaCan);
            }
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Use the scene handles to move the shell anchor point and (for TunaCan) the can attach offset. Works in edit mode without Play.", MessageType.Info);
    }
}
#endif