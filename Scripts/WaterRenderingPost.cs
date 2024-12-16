//Unity2DFluidSim by David Westberg https://github.com/Zombie1111/Unity2DFluidSim
using UnityEngine;

namespace Zomb2DPhysics
{
    public class WaterRenderingPost : MonoBehaviour
    {
        [SerializeField] private Shader waterPostShader = null;
        [System.NonSerialized] public Camera waterCam;
        private Material waterPostMat;

        private void Awake()
        {
            if (waterPostShader == null) throw new System.Exception(transform.name + " waterPostShader has not been assigned");
            if (TryGetComponent(out waterCam) == false) throw new System.Exception("There is no camera attatched to " + transform.name);

            waterPostMat = new Material(waterPostShader);
        }

        [ImageEffectOpaque]
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Graphics.Blit(source, destination, waterPostMat);
        }
    }
}