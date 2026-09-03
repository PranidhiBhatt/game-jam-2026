using System;
using UnityEngine;

namespace DangerousArena.Player
{
    /// <summary>
    /// Reusable 3D Player Controller for Dangerous Arena.
    /// Handles responsive movement, jump, gravity, and camera-relative orientation
    /// using Unity's CharacterController.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Maximum movement speed in units per second.")]
        [SerializeField] private float moveSpeed = 7.5f;

        [Tooltip("Acceleration rate when speeding up.")]
        [SerializeField] private float acceleration = 25.0f;

        [Tooltip("Deceleration rate when slowing down or stopping.")]
        [SerializeField] private float deceleration = 30.0f;

        [Tooltip("Turn smoothing speed in degrees per second.")]
        [SerializeField] private float rotationSpeed = 720.0f;

        [Header("Jump & Gravity")]
        [Tooltip("Upward impulse velocity applied on jump.")]
        [SerializeField] private float jumpForce = 7.0f;

        [Tooltip("Downward acceleration applied when airborne (negative value).")]
        [SerializeField] private float gravity = -20.0f;

        [Tooltip("Small downward force applied while grounded to prevent ledge/step jitter.")]
        [SerializeField] private float groundedDownwardForce = -2.5f;

        [Tooltip("Maximum falling speed.")]
        [SerializeField] private float terminalVelocity = -35.0f;

        [Header("Ground Detection")]
        [Tooltip("Extra ray/sphere check offset below the character controller.")]
        [SerializeField] private float groundCheckOffset = 0.15f;

        [Tooltip("Radius of the ground check sphere.")]
        [SerializeField] private float groundCheckRadius = 0.25f;

        [Tooltip("Layers considered solid ground.")]
        [SerializeField] private LayerMask groundLayer = ~0;

        [Header("Camera & Orientation")]
        [Tooltip("Camera used to align movement directions. If unassigned, Camera.main is used.")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("If true, movement is relative to the camera orientation. If false, relative to world axes.")]
        [SerializeField] private bool alignMovementWithCamera = true;

        [Header("Visual Model Reference (Optional)")]
        [Tooltip("Optional child transform holding the player mesh/model for independent rotation.")]
        [SerializeField] private Transform visualTransform;

        // Public Events for other systems (Audio, VFX, Animation) to subscribe to
        public event Action OnJumped;
        public event Action OnLanded;

        // References & State
        private CharacterController characterController;
        private Vector3 currentHorizontalVelocity;
        private float verticalVelocity;
        private bool isGrounded;
        private bool wasGroundedLastFrame;
        private bool isMovementEnabled = true;

        // Public Properties
        public bool IsMovementEnabled => isMovementEnabled;
        public bool IsGrounded => isGrounded;
        public Vector3 Velocity => characterController != null ? characterController.velocity : Vector3.zero;
        public Vector3 HorizontalVelocity => currentHorizontalVelocity;
        public float MoveSpeed => moveSpeed;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (visualTransform == null)
            {
                visualTransform = transform;
            }
        }

        private void Update()
        {
            UpdateGroundCheck();

            if (isMovementEnabled)
            {
                Vector2 inputVector = ReadInputVector();
                bool jumpInput = ReadJumpInput();

                HandleHorizontalMovement(inputVector);
                HandleVerticalMovement(jumpInput);
            }
            else
            {
                // When movement is disabled (e.g. death or pause), stop horizontal drift but preserve gravity
                currentHorizontalVelocity = Vector3.MoveTowards(
                    currentHorizontalVelocity, 
                    Vector3.zero, 
                    deceleration * Time.deltaTime
                );
                HandleVerticalMovement(jumpInput: false);
            }

            // Combine velocities and apply to CharacterController
            Vector3 finalMovement = currentHorizontalVelocity + (Vector3.up * verticalVelocity);
            characterController.Move(finalMovement * Time.deltaTime);

            // Notify landing transition
            if (isGrounded && !wasGroundedLastFrame)
            {
                OnLanded?.Invoke();
            }

            wasGroundedLastFrame = isGrounded;
        }

        /// <summary>
        /// Dual ground check: queries CharacterController.isGrounded as well as a small sphere cast
        /// to ensure stability on edges and moving platforms.
        /// </summary>
        private void UpdateGroundCheck()
        {
            if (characterController.isGrounded)
            {
                isGrounded = true;
                return;
            }

            Vector3 sphereOrigin = transform.position + (Vector3.up * (characterController.radius + groundCheckOffset));
            isGrounded = Physics.CheckSphere(sphereOrigin, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Reads directional input from WASD and Arrow keys.
        /// Supports both new Input System and legacy Input Manager seamlessly.
        /// </summary>
        private Vector2 ReadInputVector()
        {
            Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Mathf.Approximately(input.sqrMagnitude, 0f))
            {
                input.x = Input.GetAxisRaw("Horizontal");
                input.y = Input.GetAxisRaw("Vertical");
            }
#endif

            return Vector2.ClampMagnitude(input, 1f);
        }

        /// <summary>
        /// Reads jump input (Space key).
        /// Supports both new Input System and legacy Input Manager seamlessly.
        /// </summary>
        private bool ReadJumpInput()
        {
            bool jumpPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                jumpPressed = UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!jumpPressed)
            {
                jumpPressed = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space);
            }
#endif

            return jumpPressed;
        }

        /// <summary>
        /// Smoothly accelerates, decelerates, and steers the character horizontally.
        /// </summary>
        private void HandleHorizontalMovement(Vector2 input)
        {
            Vector3 targetDirection = CalculateMoveDirection(input);
            Vector3 targetVelocity = targetDirection * moveSpeed;

            float rate = targetDirection.sqrMagnitude > 0.01f ? acceleration : deceleration;
            currentHorizontalVelocity = Vector3.MoveTowards(
                currentHorizontalVelocity, 
                targetVelocity, 
                rate * Time.deltaTime
            );

            // Rotate character visual towards movement direction
            if (currentHorizontalVelocity.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(currentHorizontalVelocity, Vector3.up);
                visualTransform.rotation = Quaternion.RotateTowards(
                    visualTransform.rotation, 
                    targetRotation, 
                    rotationSpeed * Time.deltaTime
                );
            }
        }

        /// <summary>
        /// Computes 3D move direction relative to camera or world axes.
        /// </summary>
        private Vector3 CalculateMoveDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.001f)
            {
                return Vector3.zero;
            }

            if (alignMovementWithCamera && playerCamera != null)
            {
                Vector3 camForward = playerCamera.transform.forward;
                Vector3 camRight = playerCamera.transform.right;
                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();

                return (camForward * input.y + camRight * input.x).normalized;
            }

            return new Vector3(input.x, 0f, input.y).normalized;
        }

        /// <summary>
        /// Applies gravity and handles single-jump physics.
        /// </summary>
        private void HandleVerticalMovement(bool jumpInput)
        {
            if (isGrounded)
            {
                if (jumpInput)
                {
                    verticalVelocity = jumpForce;
                    isGrounded = false;
                    OnJumped?.Invoke();
                }
                else
                {
                    // Small downward force to stay glued to ground/steps without bouncing
                    verticalVelocity = groundedDownwardForce;
                }
            }
            else
            {
                // In air: accumulate gravity
                verticalVelocity += gravity * Time.deltaTime;
                if (verticalVelocity < terminalVelocity)
                {
                    verticalVelocity = terminalVelocity;
                }
            }
        }

        // --- PUBLIC API FOR GAME SYSTEMS (Death, Respawn, Teleport) ---

        /// <summary>
        /// Enables or disables player input and movement (e.g., when the player dies, game pauses, or level ends).
        /// </summary>
        public void SetMovementEnabled(bool isEnabled)
        {
            isMovementEnabled = isEnabled;
            if (!isEnabled)
            {
                currentHorizontalVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Teleports the player to a target position and optional rotation, cleanly resetting internal physics.
        /// </summary>
        public void Teleport(Vector3 targetPosition, Quaternion? targetRotation = null)
        {
            bool wasActive = characterController.enabled;
            if (wasActive)
            {
                characterController.enabled = false;
            }

            transform.position = targetPosition;
            if (targetRotation.HasValue)
            {
                transform.rotation = targetRotation.Value;
                if (visualTransform != null && visualTransform != transform)
                {
                    visualTransform.rotation = targetRotation.Value;
                }
            }

            ResetVelocity();

            if (wasActive)
            {
                characterController.enabled = true;
            }
        }

        /// <summary>
        /// Immediately clears all horizontal and vertical velocity.
        /// </summary>
        public void ResetVelocity()
        {
            currentHorizontalVelocity = Vector3.zero;
            verticalVelocity = groundedDownwardForce;
        }

        /// <summary>
        /// Assigns an active camera reference dynamically at runtime.
        /// </summary>
        public void SetCamera(Camera newCamera)
        {
            playerCamera = newCamera;
        }
    }
}
