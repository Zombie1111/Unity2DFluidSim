#if !FLUID_NORBBUOYANCY
using UnityEngine;

namespace Zomb2DPhysics
{
    public class WaterColliderBoxRb : MonoBehaviour
    {
        [System.NonSerialized] public WaterColliderBox wCol = null;
        [System.NonSerialized] public Rigidbody2D rb = null;
        /// <summary>
        /// Number of water particles this WaterColliderBoxRb is currently touching
        /// </summary>
        [System.NonSerialized] public int waterPatCountTouching = 0;
        //private Rigidbody2D rb = null;

        private void Awake()
        {
            if (TryGetComponent(out wCol) == false)
                throw new System.Exception(transform.name + " does not have a WaterColliderBox script");

            if (wCol.TryGetComponent(out rb) == false) throw new System.Exception(transform.name + " does not have a Rigidbody2D");
        }

        private void FixedUpdate()
        {
            //if (rb.IsSleeping() == true) return;//Really worth it?? Rbs in water will never sleep
            wCol.UpdateWaterColliderLocation();
        }
    }
}
#endif