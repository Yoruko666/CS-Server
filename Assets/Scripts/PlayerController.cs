using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public int slot;

    private Transform center;
    public Transform spine;

    private int latestTick;
    private float moveInputX, moveInputY;
    private float lookInputX, lookInputY;

    private readonly float mouseSensitive = 0.2f;
    private float speed, targetSpeed, velocity, height;
    private float rotationX, rotationY;
    private Vector3 movement;

    private bool jump;
    private bool isWalk;
    private bool isCrouch;
    private bool isGrounded;
    private bool isInAir;

    private PlayerStateInfo playerInfo = new PlayerStateInfo();
    private CharacterController characterController;

    private readonly float SPEED_WALK = 3;
    private readonly float SPEED_RUN = 6;
    private readonly float SPEED_CROUCH = 2;
    private readonly float GRAVITY = 9.8f;

    // ============ 落地踉跄（必须与客户端 PlayerController 严格一致）============
    private const float STAGGER_FALL_THRESHOLD = 4f;
    private const float STAGGER_DURATION_PER_SPEED = 0.06f;
    private const float STAGGER_DURATION_MAX = 0.6f;
    private const float STAGGER_ACCELERATION = 8f;
    private float staggerTimer = 0f;

    private readonly static float TICK_INTERVAL = NetworkManager.TICK_INTERVAL;

    private void Awake()
    {
        center = transform.Find("Center");
        characterController = GetComponent<CharacterController>();
    }

    private void LateUpdate()
    {
        spine.Rotate(0, 0, rotationX, Space.Self);
    }

    public void Initialize(int slot, int uid)
    {
        this.slot = slot;
        playerInfo.uid = uid;
        Reborn();
    }

    public void Reborn()
    {
        characterController.enabled = false;
        rotationX = 0;
        rotationY = (slot / 3) * 180;
        transform.position = MatchManager.instance.mapConfig.bornPoints[slot];
        transform.rotation = Quaternion.Euler(0, ((slot / 3) * 180), 0);
        characterController.enabled = true;

        moveInputX = moveInputY = 0;
        lookInputX = lookInputY = 0;
        speed = targetSpeed = 0;
        movement = Vector3.zero;
        velocity = 0; 
        isGrounded = true; 
        isInAir = false; 
        jump = false; 
        isCrouch = false; 
        isWalk = false; 
        staggerTimer = 0f;       // 重生清空踉跄
    }

    // 服务端权威：限制单 tick 输入合理范围，防止异常客户端瞬移视角 / 加速移动
    // 预算：1 个 tick 间隔 ~7.8ms (128Hz)，正常游戏最快约 720°/s -> 单 tick ~5.6°
    // 这里给到 30° 作为宽松上限（考虑灵敏度乘子与抖动），超过的 input 视为可疑
    private const float MAX_LOOK_DELTA_PER_TICK = 30f / 0.2f;   // 角度 / mouseSensitive

    public void ApplyInput(PlayerInputInfo inputInfo)
    {
        // 范围 clamp：移动方向的归一向量分量必须在 [-1, 1]
        float clampedLookX = Mathf.Clamp(inputInfo.lookInputX, -MAX_LOOK_DELTA_PER_TICK, MAX_LOOK_DELTA_PER_TICK);
        float clampedLookY = Mathf.Clamp(inputInfo.lookInputY, -MAX_LOOK_DELTA_PER_TICK, MAX_LOOK_DELTA_PER_TICK);

        lookInputX += clampedLookX;
        lookInputY += clampedLookY;
        moveInputX = Mathf.Clamp(inputInfo.moveInputX, -1f, 1f);
        moveInputY = Mathf.Clamp(inputInfo.moveInputY, -1f, 1f);
        latestTick = inputInfo.tick;
        isWalk = inputInfo.isWalk;
        isCrouch = inputInfo.isCrouch;
        if (inputInfo.jump) jump = true;
    }

    public PlayerStateInfo GetPlayerStateInfo()
    {
        playerInfo.tick = latestTick;
        playerInfo.positionX = transform.position.x;
        playerInfo.positionY = transform.position.y;
        playerInfo.positionZ = transform.position.z;
        playerInfo.rotationY = rotationY;
        playerInfo.rotationX = rotationX;
        playerInfo.speed = speed;
        playerInfo.velocity = velocity;
        playerInfo.isCrouch = isCrouch;
        return playerInfo;
    }

    public void UpdateStateInfo(PlayerStateInfo playerStateInfo)
    {
        playerStateInfo.tick = latestTick;
        playerStateInfo.positionX = transform.position.x;
        playerStateInfo.positionY = transform.position.y;
        playerStateInfo.positionZ = transform.position.z;
        playerStateInfo.rotationY = rotationY;
        playerStateInfo.rotationX = rotationX;
        playerStateInfo.speed = speed;
        playerStateInfo.velocity = velocity;
        playerStateInfo.height = height;
        playerStateInfo.isCrouch = isCrouch;
    }

    public PlayerInputInfo GetInputInfo()
    {
        PlayerInputInfo inputInfo = new PlayerInputInfo(-1, moveInputX, moveInputY, lookInputX, lookInputY, jump, isWalk, isCrouch);
        lookInputX = 0;
        lookInputY = 0;
        jump = false;
        return inputInfo;
    }

    public void ProcessInput(PlayerInputInfo inputInfo)
    {
        float moveInputX = inputInfo.moveInputX, moveInputY = inputInfo.moveInputY;
        float lookInputX = inputInfo.lookInputX, lookInputY = inputInfo.lookInputY;
        bool jump = inputInfo.jump, isWalk = inputInfo.isWalk, isCrouch = inputInfo.isCrouch;

        rotationY += lookInputX * mouseSensitive;
        rotationX -= lookInputY * mouseSensitive;
        rotationX = Mathf.Clamp(rotationX, -60, 60);
        transform.rotation = Quaternion.Euler(0, rotationY, 0);
        center.localRotation = Quaternion.Euler(rotationX, 0, 0);

        Vector3 direction = transform.rotation * new Vector3(moveInputX, 0, moveInputY);
        direction.y = 0;
        direction = direction.normalized;

        if (moveInputX != 0 || moveInputY != 0)
        {
            if (isCrouch) targetSpeed = SPEED_CROUCH;
            else if (isWalk) targetSpeed = SPEED_WALK;
            else targetSpeed = SPEED_RUN;
        }
        else targetSpeed = 0;

        // 加速度：地面正常 50，空中 15，踉跄期降到 STAGGER_ACCELERATION
        // （与客户端 PlayerController 严格一致，避免触发回滚）
        float acceleration;
        if (isGrounded)
            acceleration = staggerTimer > 0f ? STAGGER_ACCELERATION : 50f;
        else
            acceleration = 15f;
        speed = Mathf.MoveTowards(speed, targetSpeed, acceleration * TICK_INTERVAL);
        movement = Vector3.MoveTowards(movement, direction * targetSpeed, acceleration * TICK_INTERVAL);

        if (jump && isGrounded && !isCrouch)
        {
            isGrounded = false;
            velocity = 4;
        }
        characterController.Move((movement + new Vector3(0, velocity, 0)) * TICK_INTERVAL);

        if (characterController.isGrounded)
        {
            isGrounded = true;
            // 落地：根据下落速度触发踉跄（同客户端逻辑）
            if (isInAir)
            {
                isInAir = false;
                float fallSpeed = -velocity;
                if (fallSpeed > STAGGER_FALL_THRESHOLD)
                {
                    float over = fallSpeed - STAGGER_FALL_THRESHOLD;
                    float duration = Mathf.Min(over * STAGGER_DURATION_PER_SPEED, STAGGER_DURATION_MAX);
                    if (duration > staggerTimer) staggerTimer = duration;
                }
            }
            velocity = -0.5f;
        }
        else
        {
            isInAir = true;
            velocity -= GRAVITY * TICK_INTERVAL;
        }

        if (staggerTimer > 0f) staggerTimer = Mathf.Max(0f, staggerTimer - TICK_INTERVAL);

        if (isCrouch) height = Mathf.MoveTowards(height, 1.2f, 4 * TICK_INTERVAL);
        else height = Mathf.MoveTowards(height, 1.6f, 4 * TICK_INTERVAL);
        characterController.height = height;
        characterController.center = new Vector3(0, height / 2, 0);
        center.localPosition = new Vector3(0, height, 0);
    }

    public void GetHit(Vector3 position)
    {
        var hit = new Hit(playerInfo.uid, position);
        NetworkManager.Send(playerInfo.uid.ToString(), MessageType.Hit, hit);
    }
}