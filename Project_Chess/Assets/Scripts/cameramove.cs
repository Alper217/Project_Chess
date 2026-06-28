using UnityEngine;

[DisallowMultipleComponent]
public class cameramove : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float heightSpeed = 5f;

    [Header("Height Limits")]
    [SerializeField] private bool clampHeight = true;
    [SerializeField] private float minHeight = 4f;
    [SerializeField] private float maxHeight = 20f;

    [Header("Rotation")]
    [SerializeField] private float keyboardRotationSpeed = 90f;
    [SerializeField] private float mouseRotationSpeed = 3f;
    [SerializeField] private bool enableMouseRotation = true;
    [SerializeField] private float minPitch = 25f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Keys")]
    [SerializeField] private KeyCode heightUpKey = KeyCode.E;
    [SerializeField] private KeyCode heightDownKey = KeyCode.Q;
    [SerializeField] private KeyCode rotateLeftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rotateRightKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode pitchUpKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode pitchDownKey = KeyCode.DownArrow;

    private float yaw;
    private float pitch;

    private void Awake()
    {
        Vector3 eulerAngles = transform.eulerAngles;
        yaw = eulerAngles.y;
        pitch = Mathf.Clamp(NormalizeAngle(eulerAngles.x), minPitch, maxPitch);

        ApplyRotation();
    }

    private void Update()
    {
        HandleMovement();
        HandleHeight();
        HandleRotation();
    }

    private void HandleMovement()
    {
        Vector3 moveInput = GetMoveInput();
        if (moveInput.sqrMagnitude <= 0f)
        {
            return;
        }

        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = yawRotation * Vector3.forward;
        Vector3 right = yawRotation * Vector3.right;
        Vector3 moveDirection = (forward * moveInput.z + right * moveInput.x).normalized;

        transform.position += moveSpeed * Time.deltaTime * moveDirection;
    }

    private void HandleHeight()
    {
        float heightInput = 0f;

        if (Input.GetKey(heightUpKey))
        {
            heightInput += 1f;
        }

        if (Input.GetKey(heightDownKey))
        {
            heightInput -= 1f;
        }

        if (Mathf.Approximately(heightInput, 0f))
        {
            return;
        }

        Vector3 position = transform.position;
        position.y += heightInput * heightSpeed * Time.deltaTime;

        if (clampHeight)
        {
            position.y = Mathf.Clamp(position.y, minHeight, maxHeight);
        }

        transform.position = position;
    }

    private void HandleRotation()
    {
        float yawInput = 0f;
        float pitchInput = 0f;

        if (Input.GetKey(rotateLeftKey))
        {
            yawInput -= 1f;
        }

        if (Input.GetKey(rotateRightKey))
        {
            yawInput += 1f;
        }

        if (Input.GetKey(pitchUpKey))
        {
            pitchInput -= 1f;
        }

        if (Input.GetKey(pitchDownKey))
        {
            pitchInput += 1f;
        }

        yaw += yawInput * keyboardRotationSpeed * Time.deltaTime;
        pitch += pitchInput * keyboardRotationSpeed * Time.deltaTime;

        if (enableMouseRotation && Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * mouseRotationSpeed;
            pitch -= Input.GetAxis("Mouse Y") * mouseRotationSpeed;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        ApplyRotation();
    }

    private static Vector3 GetMoveInput()
    {
        Vector3 input = Vector3.zero;

        if (Input.GetKey(KeyCode.W))
        {
            input.z += 1f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            input.z -= 1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            input.x += 1f;
        }

        if (Input.GetKey(KeyCode.A))
        {
            input.x -= 1f;
        }

        return input;
    }

    private void ApplyRotation()
    {
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;

        if (angle < 0f)
        {
            angle += 360f;
        }

        return angle;
    }

    private void OnValidate()
    {
        if (minHeight > maxHeight)
        {
            (minHeight, maxHeight) = (maxHeight, minHeight);
        }

        if (minPitch > maxPitch)
        {
            (minPitch, maxPitch) = (maxPitch, minPitch);
        }
    }
}
