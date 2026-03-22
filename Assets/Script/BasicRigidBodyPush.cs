using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class BasicRigidBodyPush : MonoBehaviour
{
    public LayerMask pushLayers;
    public bool canPush;
    [Range(0.5f, 5f)] public float strength = 1.1f;
    [Range(0.1f, 5f)] public float minimumPushSpeed = 1.0f;
    public bool onlyPushWhileGrounded = true;

    private CharacterController _controller;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!canPush)
        {
            return;
        }

        PushRigidBodies(hit);
    }

    private void PushRigidBodies(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic)
        {
            return;
        }

        if (_controller == null)
        {
            return;
        }

        if (onlyPushWhileGrounded && !_controller.isGrounded)
        {
            return;
        }

        int bodyLayerMask = 1 << body.gameObject.layer;
        if ((bodyLayerMask & pushLayers.value) == 0)
        {
            return;
        }

        if (hit.moveDirection.y < -0.3f)
        {
            return;
        }

        Vector3 playerVelocity = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z);
        if (playerVelocity.sqrMagnitude < minimumPushSpeed * minimumPushSpeed)
        {
            return;
        }

        Vector3 pushDirection = playerVelocity.normalized;
        float pushForce = strength * Mathf.Clamp(playerVelocity.magnitude, 0.5f, 3.0f);
        body.AddForceAtPosition(pushDirection * pushForce, hit.point, ForceMode.Impulse);
    }
}
