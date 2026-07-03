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
    public UnityAction<Interactables> OnPlayerInteractableUpdate;
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

    [Header("Sprite Animation")]
    [SerializeField] private SpriteLoopHandler spriteLoopHandler;
    [SerializeField] private float moveInputDeadzone = 0.1f;

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

    private Interactables _currentInteractable;

    public Interactables currentInteractable
    {
        get { return _currentInteractable; }
        private set
        {
            if (_currentInteractable != value) OnPlayerInteractableUpdate?.Invoke(value);
            _currentInteractable = value;
        }
    }
    private bool interactRequested;

    private bool isExternallyDriven;

    private bool isSpriteLooping;

    public Interactables CurrentInteractable => currentInteractable;
    public bool IsGrounded => isGrounded;

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

        if (!isExternallyDriven)
        {
            HandleJumpRequest();
        }

        HandleCurrentPlatform();
        if (!isExternallyDriven)
        {
            HandleDropRequest();
        }

        HandleInteractDetection();
        HandleInteractRequest();
        HandleSpriteAnimation();
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
        velocity.x = moveInput.x * moveSpeed;

        if (jumpRequested)
        {
            velocity.y = jumpForce;
            jumpRequested = false;
        }
        else if (isGrounded && velocity.y <= 0f)
        {

        }
        else if (velocity.y < 0f)
        {
            velocity += (fallMultiplier - 1f) * Physics.gravity.y * Time.fixedDeltaTime * Vector3.up;
        }
        else if (velocity.y > 0f && moveInput.y <= 0.1f)
        {
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
            if (!hit.TryGetComponent<Interactables>(out var interactable))
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
            TryInteract(currentInteractable);
            interactRequested = false;
        }
    }

    /// <summary>
    /// Drives the ground-loop sprite animation: loops while grounded and moving,
    /// stops on stop/jump/airborne, and flips facing based on horizontal input direction.
    /// </summary>
    private void HandleSpriteAnimation()
    {
        if (spriteLoopHandler == null) return;

        bool isMovingHorizontally = Mathf.Abs(moveInput.x) > moveInputDeadzone;
        bool shouldLoop = isGrounded && isMovingHorizontally && !jumpRequested;

        if (shouldLoop != isSpriteLooping)
        {
            spriteLoopHandler.StartLoping(shouldLoop);
            isSpriteLooping = shouldLoop;
        }

        if (isMovingHorizontally)
        {
            spriteLoopHandler.SetXFlip(moveInput.x < 0f);
        }
    }

    /// <summary>
    /// Attempts to start an interaction with the given target. Shared by the player's own
    /// input-driven interact request and any externally driven interaction (e.g. cutscenes).
    /// </summary>
    public bool TryInteract(Interactables target)
    {
        if (target == null) return false;

        bool isInteracting = target.Interact(HandleOnInteractionEnd);
        if (isInteracting)
        {
            // LogMessage($"OnPlayerInteracted {true}");
            target.OnTriggerCombatAction = HandleOnCombatTriggered;
            OnPlayerInteracted?.Invoke(true);
        }

        return isInteracting;
    }

    private void HandleOnInteractionEnd()
    {
        // LogMessage($"OnPlayerInteracted {false}");
        OnPlayerInteracted?.Invoke(false);
    }

    private void HandleOnCombatTriggered(CombatPartyHandler enemy)
    {
        OnPlayerTriggerCombat?.Invoke(enemy);
    }
    #endregion

    #region Cutscene Control
    /// <summary>
    /// Toggles between normal player input and external (cutscene-driven) control.
    /// Reuses the same movement/jump physics either way - only the source of intent changes.
    /// </summary>
    public void SetCutsceneControl(bool active)
    {
        isExternallyDriven = active;
        SubscribeInput(!active);

        if (active)
        {
            moveInput = Vector2.zero;
            jumpHeldLastFrame = false;
        }
    }

    public void DriveExternalMove(float horizontal)
    {
        moveInput.x = horizontal;
    }

    public void RequestExternalJump()
    {
        if (isGrounded)
        {
            jumpRequested = true;
        }
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
        Debug.Log($"[PlayerExploration] {msg}");
    }
}