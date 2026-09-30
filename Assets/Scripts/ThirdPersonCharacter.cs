using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class ThirdPersonCharacter : MonoBehaviour
{
    private enum MovementStyle { Run, RunFast, SwaggerWalk }
    private enum CharacterAction { None, Jump, Roll }

    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator characterAnimator;
    [SerializeField, Min(0f)] private float runSpeed = 4.5f;
    [SerializeField, Min(0f)] private float fastRunSpeed = 7f;
    [SerializeField, Min(0f)] private float swaggerWalkSpeed = 1.4f;
    [SerializeField, Min(0f)] private float turnSpeed = 12f;
    [SerializeField] private float gravity = -20f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
    [SerializeField, Min(0f)] private float jumpForwardMultiplier = 0.7f;
    [SerializeField, Min(0.1f)] private float jumpActionDuration = 1.18f;
    [SerializeField, Min(0.1f)] private float jumpAnimationSpeed = 2f;
    [SerializeField, Min(0f)] private float rollSpeed = 3.4f;
    [SerializeField, Min(0.1f)] private float rollActionDuration = 1.17f;

    private CharacterController characterController;
    private float verticalSpeed;
    private float actionElapsed;
    private float actionForwardSpeed;
    private Vector3 actionDirection;
    private MovementStyle movementStyle = MovementStyle.Run;
    private CharacterAction action = CharacterAction.None;
    private string currentAnimation;
    private bool hasMoved;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (characterAnimator == null)
            characterAnimator = GetComponentInChildren<Animator>();
        if (characterAnimator != null)
            characterAnimator.applyRootMotion = false;
    }

    private void Start()
    {
        PlayAnimation("StandingIdle", true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            movementStyle = MovementStyle.Run;
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            movementStyle = MovementStyle.RunFast;
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            movementStyle = MovementStyle.SwaggerWalk;

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

        if (action == CharacterAction.None && characterController.isGrounded)
        {
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                BeginRoll(isMoving ? inputDirection : transform.forward);
            else if (Input.GetKeyDown(KeyCode.Space))
                BeginJump(isMoving ? inputDirection : Vector3.zero, moveSpeed);
        }

        Vector3 horizontalVelocity;
        if (action == CharacterAction.Jump)
        {
            actionElapsed += Time.deltaTime;
            horizontalVelocity = characterController.isGrounded && actionElapsed > 0.1f
                ? Vector3.zero : actionDirection * actionForwardSpeed;
        }
        else if (action == CharacterAction.Roll)
        {
            actionElapsed += Time.deltaTime;
            horizontalVelocity = actionDirection * rollSpeed;
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

        if ((action == CharacterAction.Jump && landed && actionElapsed >= jumpActionDuration) ||
            (action == CharacterAction.Roll && actionElapsed >= rollActionDuration))
        {
            action = CharacterAction.None;
            if (characterAnimator != null)
                characterAnimator.speed = 1f;
            PlayAnimation(isMoving ? MovementAnimation() : hasMoved ? "WalkIdle" : "StandingIdle");
        }
    }

    private void BeginJump(Vector3 direction, float moveSpeed)
    {
        action = CharacterAction.Jump;
        actionElapsed = 0f;
        actionDirection = direction;
        actionForwardSpeed = moveSpeed * jumpForwardMultiplier;
        if (direction.sqrMagnitude > 0.001f)
            hasMoved = true;
        verticalSpeed = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
        PlayAnimation("Jump", true);
        if (characterAnimator != null)
            characterAnimator.speed = jumpAnimationSpeed;
    }

    private void BeginRoll(Vector3 direction)
    {
        action = CharacterAction.Roll;
        actionElapsed = 0f;
        actionDirection = direction;
        hasMoved = true;
        PlayAnimation("Roll", true);
    }

    private string MovementAnimation()
    {
        return movementStyle == MovementStyle.RunFast ? "RunFast"
             : movementStyle == MovementStyle.SwaggerWalk ? "SwaggerWalk" : "Run";
    }

    private void PlayAnimation(string stateName, bool immediate = false)
    {
        if (characterAnimator == null || currentAnimation == stateName && !immediate)
            return;

        if (immediate)
            characterAnimator.Play(stateName, 0, 0f);
        else
            characterAnimator.CrossFadeInFixedTime(stateName, 0.12f, 0);
        currentAnimation = stateName;
    }
}
