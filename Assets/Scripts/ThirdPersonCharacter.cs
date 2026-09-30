using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class ThirdPersonCharacter : MonoBehaviour
{
    private const float JumpInputBuffer = 0.15f;
    private enum MovementStyle { Run, RunFast, SwaggerWalk }
    private enum CharacterAction { None, Jump, Roll, BigFrontJump }

    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator characterAnimator;
    [SerializeField, Min(0f)] private float runSpeed = 4.5f;
    [SerializeField, Min(0f)] private float fastRunSpeed = 7f;
    [SerializeField, Min(0f)] private float swaggerWalkSpeed = 2.2f;
    [SerializeField, Min(0f)] private float turnSpeed = 12f;
    [SerializeField] private float gravity = -20f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
    [SerializeField, Min(0f)] private float jumpForwardMultiplier = 0.7f;
    [SerializeField, Min(0.1f)] private float jumpActionDuration = 1.18f;
    [SerializeField, Min(0.1f)] private float jumpAnimationSpeed = 2f;
    [SerializeField, Min(0.1f)] private float walkJumpAnimationSpeed = 1.2f;
    [SerializeField, Min(0f)] private float walkJumpHeight = 1.85f;
    [SerializeField, Min(0.1f)] private float bigFrontJumpAnimationSpeed = 1.1f;
    [SerializeField, Min(0.1f)] private float rollActionDuration = 1.17f;

    private CharacterController characterController;
    private float verticalSpeed;
    private float actionElapsed;
    private float actionForwardSpeed;
    private bool walkJumpActive;
    private Vector3 actionDirection;
    private MovementStyle movementStyle = MovementStyle.Run;
    private CharacterAction action = CharacterAction.None;
    private string currentAnimation;
    private bool hasMoved;
    private bool walkMode;
    private bool jumpQueued;
    private float jumpQueuedUntil;
    private Vector3 jumpQueuedDirection;
    private float jumpQueuedMoveSpeed;
    private bool queuedWalkJump;
    private bool queuedBigFrontJump;
    private Vector3 modelLocalPosition;
    private Quaternion modelLocalRotation;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (characterAnimator == null)
            characterAnimator = GetComponentInChildren<Animator>();
        if (characterAnimator != null)
        {
            modelLocalPosition = characterAnimator.transform.localPosition;
            modelLocalRotation = characterAnimator.transform.localRotation;
            characterAnimator.applyRootMotion = false;
        }
    }

    private void Start()
    {
        PlayAnimation("StandingIdle", true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
            walkMode = !walkMode;

        movementStyle = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
            ? MovementStyle.RunFast : walkMode ? MovementStyle.SwaggerWalk : MovementStyle.Run;

        if (action == CharacterAction.Roll || action == CharacterAction.BigFrontJump)
        {
            if (action == CharacterAction.Roll)
            {
                actionElapsed += Time.deltaTime * (characterAnimator != null ? characterAnimator.speed : 1f);
                if (actionElapsed >= rollActionDuration)
                    EndRootMotionAction();
            }
            return;
        }

        if (cameraTransform == null)
            return;

        // The project's Horizontal/Vertical axes contain arrow keys only.
        float horizontal = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                         - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float vertical = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                       - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        Vector2 input = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward);
        Vector3 inputDirection = cameraRight * input.x + cameraForward * input.y;
        bool isMoving = input.sqrMagnitude > 0.001f;
        float moveSpeed = movementStyle == MovementStyle.RunFast ? fastRunSpeed
                        : movementStyle == MovementStyle.SwaggerWalk ? swaggerWalkSpeed : runSpeed;

        // Capture the choice on the Space key-down frame. Later direction inputs cannot replace it.
        if (action == CharacterAction.None && Input.GetKeyDown(KeyCode.Space))
        {
            jumpQueued = true;
            jumpQueuedUntil = Time.time + JumpInputBuffer;
            jumpQueuedDirection = isMoving ? inputDirection : Vector3.zero;
            jumpQueuedMoveSpeed = moveSpeed;
            queuedWalkJump = hasMoved || isMoving;
            queuedBigFrontJump = isMoving && movementStyle == MovementStyle.RunFast &&
                                 characterAnimator != null;
        }

        if (jumpQueued && Time.time > jumpQueuedUntil)
            jumpQueued = false;

        if (action == CharacterAction.None && characterController.isGrounded)
        {
            if (jumpQueued)
            {
                jumpQueued = false;
                if (queuedBigFrontJump)
                {
                    BeginBigFrontJump(jumpQueuedDirection);
                    return;
                }
                BeginJump(jumpQueuedDirection, jumpQueuedMoveSpeed, queuedWalkJump);
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                BeginRoll(isMoving ? inputDirection : transform.forward);
                return;
            }
        }

        Vector3 horizontalVelocity;
        if (action == CharacterAction.Jump)
        {
            actionElapsed += Time.deltaTime;
            horizontalVelocity = characterController.isGrounded && actionElapsed > 0.1f
                ? Vector3.zero : actionDirection * actionForwardSpeed;
        }
        else
        {
            horizontalVelocity = inputDirection * moveSpeed;
            if (isMoving)
            {
                hasMoved = true;
                PlayAnimation(MovementAnimation());
            }
            else
            {
                PlayAnimation(hasMoved ? "WalkIdle" : "StandingIdle");
            }
        }

        if (horizontalVelocity.sqrMagnitude > 0.001f)
        {
            Quaternion facing = Quaternion.LookRotation(horizontalVelocity, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, facing,
                1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        if (characterController.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;
        verticalSpeed += gravity * Time.deltaTime;
        CollisionFlags flags = characterController.Move(
            (horizontalVelocity + Vector3.up * verticalSpeed) * Time.deltaTime);
        bool landed = (flags & CollisionFlags.Below) != 0;
        if (landed && verticalSpeed < 0f)
            verticalSpeed = -2f;

        if (action == CharacterAction.Jump && landed &&
            (characterAnimator == null ? actionElapsed >= jumpActionDuration
                                       : !characterAnimator.IsInTransition(0) &&
                                         characterAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >=
                                         (walkJumpActive ? 0.85f : 1f)))
        {
            action = CharacterAction.None;
            if (characterAnimator != null)
                characterAnimator.speed = 1f;
            PlayAnimation(isMoving ? MovementAnimation() : hasMoved ? "WalkIdle" : "StandingIdle");
        }
    }

    private void LateUpdate()
    {
        if ((action != CharacterAction.Roll && action != CharacterAction.BigFrontJump) || characterAnimator == null)
            return;

        Vector3 animationMotion = characterAnimator.deltaPosition;
        if (action == CharacterAction.Roll)
            animationMotion.y = -2f * Time.deltaTime;
        else
        {
            // A trimmed clip can report its initial root offset as a single-frame delta.
            float forward = Mathf.Max(0f, Vector3.Dot(animationMotion, transform.forward));
            animationMotion = transform.forward * forward + Vector3.up * animationMotion.y;
            animationMotion = Vector3.ClampMagnitude(animationMotion, 10f * Time.deltaTime);
        }
        characterController.Move(animationMotion);
        characterAnimator.transform.localPosition = modelLocalPosition;

        if (action == CharacterAction.BigFrontJump && !characterAnimator.IsInTransition(0) &&
            characterAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
            EndRootMotionAction();
    }

    private void BeginJump(Vector3 direction, float moveSpeed, bool useWalkJump)
    {
        action = CharacterAction.Jump;
        actionElapsed = 0f;
        actionDirection = direction;
        actionForwardSpeed = moveSpeed * jumpForwardMultiplier;
        walkJumpActive = useWalkJump;
        if (useWalkJump)
            hasMoved = true;
        verticalSpeed = Mathf.Sqrt(2f * Mathf.Abs(gravity) *
            (useWalkJump ? walkJumpHeight : jumpHeight));
        if (characterAnimator != null)
            characterAnimator.speed = useWalkJump ? walkJumpAnimationSpeed : jumpAnimationSpeed;
        PlayAnimation(useWalkJump ? "WalkJump" : "Jump", false, useWalkJump ? 0.06f : 0.12f);
    }

    private void BeginRoll(Vector3 direction)
    {
        action = CharacterAction.Roll;
        actionElapsed = 0f;
        hasMoved = true;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        if (characterAnimator != null)
        {
            characterAnimator.speed = movementStyle == MovementStyle.RunFast ? 1.3f
                                    : movementStyle == MovementStyle.SwaggerWalk ? 0.8f : 1f;
            characterAnimator.applyRootMotion = true;
        }
        PlayAnimation("Roll", true);
    }

    private void BeginBigFrontJump(Vector3 direction)
    {
        if (characterAnimator == null)
            return;

        action = CharacterAction.BigFrontJump;
        hasMoved = true;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        characterAnimator.speed = bigFrontJumpAnimationSpeed;
        characterAnimator.applyRootMotion = true;
        PlayAnimation("BigFrontJump", false, 0.06f);
    }

    private void EndRootMotionAction()
    {
        action = CharacterAction.None;
        verticalSpeed = -2f;
        if (characterAnimator != null)
        {
            characterAnimator.speed = 1f;
            characterAnimator.applyRootMotion = false;
            characterAnimator.transform.SetLocalPositionAndRotation(modelLocalPosition, modelLocalRotation);
        }
        PlayAnimation(IsMovingInputHeld() ? MovementAnimation()
            : hasMoved ? "WalkIdle" : "StandingIdle");
    }

    private static bool IsMovingInputHeld()
    {
        return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
               Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
               Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.LeftArrow) ||
               Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.RightArrow);
    }

    private string MovementAnimation()
    {
        return movementStyle == MovementStyle.RunFast ? "RunFast"
             : movementStyle == MovementStyle.SwaggerWalk ? "SwaggerWalk" : "Run";
    }

    private void PlayAnimation(string stateName, bool immediate = false, float transitionDuration = 0.12f)
    {
        if (characterAnimator == null || currentAnimation == stateName && !immediate)
            return;

        if (immediate)
            characterAnimator.Play(stateName, 0, 0f);
        else
            characterAnimator.CrossFadeInFixedTime(stateName, transitionDuration, 0);
        currentAnimation = stateName;
    }
}
