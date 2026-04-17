using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public int id;

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

    public void Initialize(int id, string playerName)
    {
        this.id = id;
        playerInfo.playerName = playerName;
        Reborn();
    }

    public void Reborn()
    {
        characterController.enabled = false;
        rotationX = 0;
        rotationY = (id / 3) * 180;
        transform.position = MatchManager.instance.mapConfig.bornPoints[id];
        transform.rotation = Quaternion.Euler(0, ((id / 3) * 180), 0);
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
    }

    public void ApplyInput(PlayerInputInfo inputInfo)
    {
        lookInputX += inputInfo.lookInputX;
        lookInputY += inputInfo.lookInputY;
        moveInputX = inputInfo.moveInputX;
        moveInputY = inputInfo.moveInputY;
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
        PlayerInputInfo inputInfo = new PlayerInputInfo("Server", moveInputX, moveInputY, lookInputX, lookInputY, jump, isWalk, isCrouch);
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

        int acceleration = isGrounded ? 50: 15;
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
            velocity = -0.5f;
            if (isInAir)
            {
                isInAir = false;
                speed = 0;
            }
        }
        else
        {
            isInAir = true;
            velocity -= GRAVITY * TICK_INTERVAL;
        }

        if (isCrouch) height = Mathf.MoveTowards(height, 1.2f, 4 * TICK_INTERVAL);
        else height = Mathf.MoveTowards(height, 1.6f, 4 * TICK_INTERVAL);
        characterController.height = height;
        characterController.center = new Vector3(0, height / 2, 0);
        center.localPosition = new Vector3(0, height, 0);
    }

    public void GetHit(Vector3 position)
    {
        var hit = new Hit(playerInfo.playerName, position);
        NetworkManager.SendMessage(playerInfo.playerName, MessageType.Hit, hit);
    }
}