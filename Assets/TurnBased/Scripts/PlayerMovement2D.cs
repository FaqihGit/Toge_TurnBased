using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
[RequireComponent(typeof(Rigidbody))]

[RequireComponent(typeof(Collider))]
public class PlayerMovement2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 2f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayers;

    [Header("Platform Drop")]
    [SerializeField] private string oneWayPlatformLayerName = "OneWayPlatform";

    private Rigidbody rb;

    private PlayerInputAction controls;

    private Collider currentPlatform;
    private Vector2 moveInput;
    private bool jumpRequested;
    private bool jumpHeldLastFrame;

    private bool isGrounded;
    private bool isDropping;

    private int playerLayer;
    private int oneWayPlatformLayer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        controls = new PlayerInputAction();

        rb.constraints =
            RigidbodyConstraints.FreezePositionZ |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;

        rb.useGravity = true;

        playerLayer = gameObject.layer;
        oneWayPlatformLayer = LayerMask.NameToLayer(oneWayPlatformLayerName);
    }

    private void OnEnable()
    {
        controls.Enable();

        controls.Movement.Move.performed += OnMove;
        controls.Movement.Move.canceled += OnMove;
    }

    private void OnDisable()
    {
        controls.Movement.Move.performed -= OnMove;
        controls.Movement.Move.canceled -= OnMove;

        controls.Disable();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void Update()
    {
        HandleIsGrounded();

        HandleJumpRequest();

        HandleCurrentPlatform();
        HandleDropRequest();
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    #region Action Handlers
    private void HandleIsGrounded()
    {
        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundLayers
        );
    }

    private void HandleJumpRequest()
    {
        bool jumpHeld = moveInput.y > 0.5f;

        // Only trigger on the transition from not pressed -> pressed
        if (jumpHeld && !jumpHeldLastFrame && isGrounded)
        {
            jumpRequested = true;
        }

        jumpHeldLastFrame = jumpHeld;
    }

    private void HandleCurrentPlatform()
    {
        if (isGrounded)
        {
            Collider[] hits = Physics.OverlapSphere(
                groundCheck.position,
                groundCheckRadius,
                groundLayers);

            currentPlatform = null;

            foreach (Collider hit in hits)
            {
                // Only store platforms on the OneWayPlatform layer
                if (hit.gameObject.layer == oneWayPlatformLayer)
                {
                    currentPlatform = hit;
                    break;
                }
            }
        }
        else if (!isDropping)
        {
            currentPlatform = null;
        }
    }

    private void HandleDropRequest()
    {
        if (moveInput.y < -0.5f &&
            isGrounded &&
            !isDropping)
        {
            StartCoroutine(DropThroughPlatform());
        }
    }

    private void HandleMovement()
    {
        Vector3 velocity = rb.linearVelocity;

        // Horizontal movement
        velocity.x = moveInput.x * moveSpeed;

        // Instant jump
        if (jumpRequested)
        {
            velocity.y = jumpForce;
            jumpRequested = false;
        }

        // Better jump physics
        if (velocity.y < 0f)
        {
            // Falling -> stronger gravity
            velocity += (fallMultiplier - 1f) * Physics.gravity.y * Time.fixedDeltaTime * Vector3.up;
        }
        else if (velocity.y > 0f && moveInput.y <= 0.1f)
        {
            // Released jump early -> shorter jump
            velocity += (lowJumpMultiplier - 1f) * Physics.gravity.y * Time.fixedDeltaTime * Vector3.up;
        }

        rb.linearVelocity = velocity;
    }
    #endregion

    private IEnumerator DropThroughPlatform()
    {
        if (currentPlatform == null)
            yield break;

        isDropping = true;

        Collider playerCollider = GetComponent<Collider>();

        Physics.IgnoreCollision(
            playerCollider,
            currentPlatform,
            true);

        // Give the player a little downward velocity
        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x,
            -2f,
            rb.linearVelocity.z);

        yield return new WaitUntil(() => !playerCollider.bounds.Intersects(currentPlatform.bounds));

        Physics.IgnoreCollision(
            playerCollider,
            currentPlatform,
            false);

        isDropping = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius);
    }
}