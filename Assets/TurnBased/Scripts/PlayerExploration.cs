using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PlayerExploration : MonoBehaviour
{
    public UnityAction<bool> OnPlayerInteracted;
    public Action<CombatPartyHandler> OnPlayerTriggerCombat;

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

    [Header("Interaction")]
    [SerializeField] private Transform interactCheck;
    [SerializeField] private float interactRadius = 1.5f;
    [SerializeField] private LayerMask interactableLayers; // set this to the "Interactables" layer in the Inspector

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

    private Interactables currentInteractable;
    private bool interactRequested;

    public Interactables CurrentInteractable => currentInteractable;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        playerLayer = gameObject.layer;
        oneWayPlatformLayer = LayerMask.NameToLayer(oneWayPlatformLayerName);
    }

    private void OnEnable()
    {
        SubscribeInput(true);
    }

    private void OnDisable()
    {
        SubscribeInput(false);
    }

    public void Init(PlayerInputAction controls)
    {
        this.controls = controls;
        SubscribeInput(true);
    }

    public void SubscribeInput(bool isSubscribe)
    {
        if (controls == null) return;

        controls.Exploration.Move.performed -= OnMove;
        controls.Exploration.Move.canceled -= OnMove;
        controls.Exploration.Interact.performed -= OnInteract;

        if (isSubscribe)
        {
            controls.Exploration.Move.performed += OnMove;
            controls.Exploration.Move.canceled += OnMove;
            controls.Exploration.Interact.performed += OnInteract;
        }
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        interactRequested = true;
    }

    private void Update()
    {
        HandleIsGrounded();

        HandleJumpRequest();

        HandleCurrentPlatform();
        HandleDropRequest();

        HandleInteractDetection();
        HandleInteractRequest();
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

    private void HandleInteractDetection()
    {
        if (interactCheck == null)
        {
            currentInteractable = null;
            return;
        }

        Collider[] hits = Physics.OverlapSphere(
            interactCheck.position,
            interactRadius,
            interactableLayers);

        Interactables closest = null;
        float closestSqrDist = float.MaxValue;

        foreach (Collider hit in hits)
        {
            // Layer mask narrows the candidate set; component check confirms it's a real interactable
            Interactables interactable = hit.GetComponent<Interactables>();
            if (interactable == null)
                continue;

            float sqrDist = (hit.transform.position - interactCheck.position).sqrMagnitude;
            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = interactable;
            }
        }

        currentInteractable = closest;
    }

    private void HandleInteractRequest()
    {
        if (interactRequested)
        {
            if (currentInteractable != null)
            {
                bool isInteracting = currentInteractable.Interact(HandleOnInteractionEnd);
                if (isInteracting)
                {
                    LogMessage($"OnPlayerInteracted {true}");
                    currentInteractable.OnTriggerCombat = HandleOnCombatTriggered;
                    OnPlayerInteracted?.Invoke(true);
                }
            }

            interactRequested = false;
        }
    }

    private void HandleOnInteractionEnd()
    {
        LogMessage($"OnPlayerInteracted {false}");
        OnPlayerInteracted?.Invoke(false);
    }

    private void HandleOnCombatTriggered(CombatPartyHandler enemy)
    {
        OnPlayerTriggerCombat?.Invoke(enemy);
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
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(
                groundCheck.position,
                groundCheckRadius);
        }

        if (interactCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(
                interactCheck.position,
                interactRadius);
        }
    }

    private void LogMessage(string msg)
    {
        // Debug.Log($"[PlayerExploration] {msg}");
    }
}