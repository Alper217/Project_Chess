using UnityEngine;

namespace ProjectChess.UI
{
    public class CameraBreathing : MonoBehaviour
    {
        [Header("Position Breathing")]
        [SerializeField] private Vector3 positionAmplitude = new Vector3(0.02f, 0.05f, 0.02f);
        [SerializeField] private Vector3 positionFrequency = new Vector3(0.3f, 0.4f, 0.35f);

        [Header("Rotation Breathing")]
        [SerializeField] private Vector3 rotationAmplitude = new Vector3(0.2f, 0.3f, 0.1f);
        [SerializeField] private Vector3 rotationFrequency = new Vector3(0.25f, 0.35f, 0.3f);

        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private float seed;

        private void Start()
        {
            initialPosition = transform.localPosition;
            initialRotation = transform.localRotation;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float time = Time.time + seed;

            // Position Offset
            float posX = Mathf.PerlinNoise(time * positionFrequency.x, 0) * 2f - 1f;
            float posY = Mathf.PerlinNoise(0, time * positionFrequency.y) * 2f - 1f;
            float posZ = Mathf.PerlinNoise(time * positionFrequency.z, time * positionFrequency.z) * 2f - 1f;

            transform.localPosition = initialPosition + new Vector3(
                posX * positionAmplitude.x,
                posY * positionAmplitude.y,
                posZ * positionAmplitude.z
            );

            // Rotation Offset
            float rotX = Mathf.PerlinNoise(time * rotationFrequency.x + 10, 0) * 2f - 1f;
            float rotY = Mathf.PerlinNoise(0, time * rotationFrequency.y + 10) * 2f - 1f;
            float rotZ = Mathf.PerlinNoise(time * rotationFrequency.z + 10, time * rotationFrequency.z + 10) * 2f - 1f;

            transform.localRotation = initialRotation * Quaternion.Euler(
                rotX * rotationAmplitude.x,
                rotY * rotationAmplitude.y,
                rotZ * rotationAmplitude.z
            );
        }
    }
}
