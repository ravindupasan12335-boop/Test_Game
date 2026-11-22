
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class controller : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 2f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float jumpForce = 5f;

    [Header("Crouching")]
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float standingHeight = 2f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.15f;

    [Header("Animation")]
    [SerializeField] private string moveParameter = "MoveAmount";
    [SerializeField] private string crouchParameter = "IsCrouching";

    private Rigidbody rb;
    private BoxCollider boxCollider;

    private float horizontalInput;
    private float verticalInput;

    private bool isCrouching;
    private bool isSprinting;
    private bool isGrounded;

    private float originalColliderHeight;
    private Vector3 originalColliderCenter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        originalColliderHeight = boxCollider.size.y;
        originalColliderCenter = boxCollider.center;

        rb.freezeRotation = true;

        if (animator != null)
            animator.applyRootMotion = false;
    }

    private void Update()
    {
        ReadInput();
        HandleCrouch();
        HandleJump();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        CheckGrounded();
        MovePlayer();
    }

    private void ReadInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching;
    }

    private void MovePlayer()
    {
        Vector3 movement =
            transform.right * horizontalInput +
            transform.forward * verticalInput;

        movement = Vector3.ClampMagnitude(movement, 1f);

        float currentSpeed = moveSpeed;

        if (isCrouching)
            currentSpeed = crouchSpeed;
        else if (isSprinting)
            currentSpeed *= sprintMultiplier;

        Vector3 velocity = rb.linearVelocity;

        velocity.x = movement.x * currentSpeed;
        velocity.z = movement.z * currentSpeed;

        rb.linearVelocity = velocity;
    }

    private void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.C) ||
            Input.GetKeyDown(KeyCode.LeftControl))
        {
            isCrouching = !isCrouching;
            UpdateCrouchCollider();
        }
    }

    private void UpdateCrouchCollider()
    {
        float newHeight = isCrouching
            ? crouchHeight
            : originalColliderHeight;

        Vector3 newSize = boxCollider.size;
        Vector3 newCenter = boxCollider.center;

        float heightDifference = originalColliderHeight - newHeight;

        newSize.y = newHeight;
        newCenter.y = originalColliderCenter.y - heightDifference / 2f;

        boxCollider.size = newSize;
        boxCollider.center = newCenter;
    }

    private void HandleJump()
    {
        if (Input.GetKeyDown(KeyCode.Space) &&
            isGrounded &&
            !isCrouching)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.y = 0f;
            rb.linearVelocity = velocity;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    private void CheckGrounded()
    {
        Bounds bounds = boxCollider.bounds;

        Vector3 origin = new Vector3(
            bounds.center.x,
            bounds.min.y + 0.05f,
            bounds.center.z
        );

        isGrounded = Physics.Raycast(
            origin,
            Vector3.down,
            groundCheckDistance + 0.05f
        );
    }

    private void UpdateAnimation()
    {
        if (animator == null)
            return;

        float moveAmount = new Vector2(
            horizontalInput,
            verticalInput
        ).magnitude;

        animator.SetFloat(moveParameter, Mathf.Clamp01(moveAmount), 0.1f, Time.deltaTime);
        animator.SetBool(crouchParameter, isCrouching);
    }

    public void ApplyConveyorPush(Vector3 pushDirection, float pushForce)
    {
        rb.AddForce(
            pushDirection.normalized * pushForce,
            ForceMode.Force
        );
    }
}
