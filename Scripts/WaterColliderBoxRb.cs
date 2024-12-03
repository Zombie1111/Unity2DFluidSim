#if !FLUID_NORBBUOYANCY
using UnityEngine;

namespace Zomb2DPhysics
{
    public class WaterColliderBoxRb : MonoBehaviour
    {
        private WaterColliderBox wCol = null;
        private Rigidbody2D rb = null;

        private void Awake()
        {
            if (TryGetComponent(out wCol) == false)
                throw new System.Exception(transform.name + " does not have a WaterColliderBox script");

            rb = wCol.TryGetWaterColRb();
            if (rb == null) throw new System.Exception(transform.name + " does not have a Rigidbody2D");
        }

        private void FixedUpdate()
        {
            if (rb.IsSleeping() == true) return;//Really worth it?? Rbs in water will never sleep
            wCol.UpdateWaterColliderLocation();
        }
    }
}
#endif

