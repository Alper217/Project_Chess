using UnityEditor;
using UnityEngine;

namespace AlperKocasalih.Chess.Grid.Editor
{
    [CustomEditor(typeof(MovementPattern))]
    public class MovementPatternEditor : UnityEditor.Editor
    {
        private MovementPattern pattern;
        private const int GRID_SIZE = 7;
        private const float HEX_SIZE = 25f;
        
        private void OnEnable()
        {
            pattern = (MovementPattern)target;
            if (pattern.gridData == null || pattern.gridData.Length != GRID_SIZE * GRID_SIZE)
            {
                pattern.gridData = new bool[GRID_SIZE * GRID_SIZE];
                EditorUtility.SetDirty(pattern);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            pattern.patternName = EditorGUILayout.TextField("Pattern Name", pattern.patternName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Movement Shape (Click to toggle)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Center (3,3) is the pawn position.", MessageType.Info);
            
            DrawHexGrid();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(pattern);
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawHexGrid()
        {
            // Center GUI layout
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            Rect rect = GUILayoutUtility.GetRect(GRID_SIZE * HEX_SIZE * 1.5f, GRID_SIZE * HEX_SIZE * 1.5f + HEX_SIZE);
            if (Event.current.type == EventType.Repaint || Event.current.type == EventType.MouseDown)
            {
                float xSpacing = HEX_SIZE * 0.9f;
                float ySpacing = HEX_SIZE * 1.0f; // vertical spacing
                
                for (int x = 0; x < GRID_SIZE; x++)
                {
                    for (int y = 0; y < GRID_SIZE; y++)
                    {
                        int index = y * GRID_SIZE + x;
                        
                        // Flat top odd-q style stagger
                        float xPos = rect.x + x * xSpacing;
                        float yPos = rect.y + y * ySpacing;
                        
                        // Offset odd columns downwards
                        if (x % 2 != 0)
                        {
                            yPos -= ySpacing * 0.5f;
                        }

                        Rect hexRect = new Rect(xPos, yPos, HEX_SIZE, HEX_SIZE);

                        // Colors
                        bool isCenter = (x == 3 && y == 3);
                        bool isMove = pattern.gridData[index];

                        Color drawColor = Color.white;
                        if (isCenter && !isMove) drawColor = new Color(0.7f, 0.7f, 1f); // Light blue center
                        if (isMove) drawColor = isCenter ? new Color(0f, 0.3f, 0.7f) : new Color(0.2f, 0.2f, 0.2f);

                        // Draw simplistic Hex shape via GUI color
                        Color oldColor = GUI.color;
                        GUI.color = drawColor;
                        
                        // Using Box style for ease, though we could draw a custom poly.
                        GUI.Box(hexRect, isCenter ? "C" : "", EditorStyles.helpBox);
                        
                        GUI.color = oldColor;

                        // Click handle
                        if (Event.current.type == EventType.MouseDown && hexRect.Contains(Event.current.mousePosition))
                        {
                            pattern.gridData[index] = !pattern.gridData[index];
                            GUI.changed = true;
                            Event.current.Use();
                        }
                    }
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}
