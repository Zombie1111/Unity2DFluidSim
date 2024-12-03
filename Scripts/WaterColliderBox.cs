//Unity2DFluidSim by David Westberg https://github.com/Zombie1111/Unity2DFluidSim
using UnityEditor;
using UnityEngine;

namespace Zomb2DPhysics
{
    public class WaterColliderBox : MonoBehaviour
    {
        [System.NonSerialized] public int wColIndex = -1;
        [Tooltip("The width of the water collider (Transform up axis)")] public float colWidth = 1.0f;
        [Tooltip("The Lenght of the water collider (Transform right axis)")] public float colLenght = 1.0f;
        private WaterPhy wPhy;

        private void Awake()
        {
            //Get components
            wPhy = GameObject.FindAnyObjectByType<WaterPhy>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            wPhy.AddOrUpdateWaterCollider(this);
        }

        private void OnDisable()
        {
            if (wPhy == null) return;//If waterPhy script was destroyed first when closing scene
            wPhy.RemoveWaterCollider(this);
        }

        /// <summary>
        /// Returns a new WaterPhy.WaterCol object that is used by the fluid simulation
        /// </summary>
        public WaterPhy.WaterCol ToWaterCol()
        {
            Vector2 forward = (Vector2)transform.right * colLenght;
            Vector2 side = (Vector2)transform.up * colWidth;

            return new()
            {
                dirForwardLenght = forward,
                dirSideLenght = side,
                pos = (Vector2)transform.position - (forward * 0.5f),
                isActive = true
            };
        }

        /// <summary>
        /// Tells the fluid simulation that the waterCollider has moved
        /// </summary>
        public void UpdateWaterColliderLocation()
        {
            wPhy.AddOrUpdateWaterCollider(this);
        }

#if UNITY_EDITOR
        [Tooltip("If true, the transform local scale will match water collider width * 2.0f and lenght (Editmode Only)")]
        [SerializeField] private bool editorSyncTransScaleAndColWithLenght = true;
        private Vector3 editorPrevScale = Vector3.one;

        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying == true)
            {
                UpdateWaterColliderLocation();
            }

            if (editorSyncTransScaleAndColWithLenght == true)
            {
                Vector3 scale = transform.localScale;
                if (scale != editorPrevScale)
                {
                    colLenght = scale.x;
                    colWidth = scale.y / 2.0f;
                    editorPrevScale = scale;
                }
                else transform.localScale = new(colLenght, colWidth * 2.0f, scale.z);
            }

            //Draw collider
            var wCol = ToWaterCol();
            Gizmos.color = Color.green;

            //Top
            Vector2 lineStart = wCol.pos + wCol.dirSideLenght;
            Vector2 line = wCol.dirForwardLenght;
            DrawLine();

            //Side Start
            line = -wCol.dirSideLenght * 2.0f;
            DrawLine();

            //Top
            lineStart = (wCol.pos + wCol.dirForwardLenght) - wCol.dirSideLenght;
            line = -wCol.dirForwardLenght;
            DrawLine();

            //Side End
            line = wCol.dirSideLenght * 2.0f;
            DrawLine();

            void DrawLine()
            {
                Gizmos.DrawLine(lineStart, lineStart + line);
            }
        }

        //########################Custom Editor######################################
        [CustomEditor(typeof(WaterColliderBox))]
        public class YourScriptEditor : Editor
        {
            private static readonly string[] hiddenFields = new string[]
            {
                "m_Script"
            };

            public override void OnInspectorGUI()
            {
                serializedObject.Update();


                DrawPropertiesExcluding(serializedObject, hiddenFields);
                EditorGUILayout.HelpBox("UpdateWaterColliderLocation() should be called after changing the" +
                    " waterCollider transform at runtime by script", MessageType.Info);

                //Apply changes
                serializedObject.ApplyModifiedProperties();
            }
        }
#endif
    }
}

