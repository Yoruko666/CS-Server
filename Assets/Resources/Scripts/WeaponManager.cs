using Newtonsoft.Json;
using System;
using System.Collections;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    private WeaponInfo[] weapons = new WeaponInfo[2];
    private int activeWeaponIndex = 0;

    private float firingTime = 0;
    private float upTime = 0;
    private float upAngle = 0;

    private bool reloading = false;
    private float reloadTimer;

    public string playerName;
    
    void Start()
    {
        weapons[0] = new WeaponInfo(2, 12, 24);
    }

    void Update()
    {
        if (reloading)
        {
            reloadTimer -= Time.deltaTime;
            if (reloadTimer <= 0)
            {
                ReloadDone();
            }
        }
    }

    public void HandleTick()
    {
        if (firingTime >= 0)
        {
            firingTime -= NetworkManager.TICK_INTERVAL;
            upTime = Mathf.MoveTowards(upTime, 0.8f, NetworkManager.TICK_INTERVAL);
            upAngle = Mathf.Pow(upTime, 2) * 10;
        }
        else
        {
            upTime = Mathf.MoveTowards(upTime, 0f, 2 * NetworkManager.TICK_INTERVAL);
            upAngle = Mathf.Pow(upTime, 2) * 10;
        }
        upAngle = Mathf.Clamp(upAngle, 0, 6);
    }

    public void Initialize()
    {
        weapons[0] = new WeaponInfo(2, 12, 24);
        if (weapons[1] != null)
        {
            WeaponConfig weaponConfig = WeaponDic.instance.weaponDic[weapons[1].id];
            AcquireWeapon(weaponConfig.id, weaponConfig.magazineCapacity, weaponConfig.magazineCapacity * 2);
            SwitchWeapon(1);
        }
    }

    public void UpdateStateInfo(PlayerStateInfo playerStateInfo)
    {
        playerStateInfo.weapons = weapons;
        playerStateInfo.activeWeaponIndex = activeWeaponIndex;
    }

    public void Fire(int seed)
    {
        if (weapons[activeWeaponIndex].ammoNum > 0)
        {
            weapons[activeWeaponIndex].ammoNum--;

            int weaponId = weapons[activeWeaponIndex].id;
            firingTime = Mathf.Min(1 / WeaponDic.instance.weaponDic[weaponId].shootSpeed, 1);

            PlayerStateInfo state = NetworkManager.playerStateInfos[GetComponent<PlayerState>().playerName];

            Quaternion playerRotation = Quaternion.Euler(0, state.rotationY, 0);
            Quaternion cameraRotation = Quaternion.Euler(state.rotationX, 0, 0);
            Vector3 center = state.GetPosition() + new Vector3(0, state.height, 0);
            Vector3 fireDirection = playerRotation * cameraRotation * Vector3.forward;

            System.Random rand = new(seed);

            float speed = state.speed;
            float max = upTime * 2 + speed;
            float min = -max;

            float verticalOffset = (float)rand.NextDouble() * (max - min) + min - upAngle;
            float horizontalOffset = (float)rand.NextDouble() * (max - min) + min;

            fireDirection = Quaternion.AngleAxis(verticalOffset, playerRotation * Vector3.right) * fireDirection;
            fireDirection = Quaternion.AngleAxis(horizontalOffset, playerRotation * Vector3.up) * fireDirection;

            int layer = LayerMask.NameToLayer("CharacterController");

            RaycastHit[] hits = Physics.RaycastAll(center, fireDirection, 100f, ~(1 << layer));
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Vector3 hitPoint = center + fireDirection * 100f;

            foreach (RaycastHit hit in hits)
            {
                BodyCollider bodyCollider = hit.collider.GetComponent<BodyCollider>();
                hitPoint = hit.point;
                if (bodyCollider != null)
                {
                    if (bodyCollider.character.GetComponent<PlayerState>().playerName == playerName)
                        continue;

                    int damage = 0;
                    if (bodyCollider.part == BodyPart.Head) damage = WeaponDic.instance.weaponDic[weaponId].damage_head;
                    if (bodyCollider.part == BodyPart.Torso) damage = WeaponDic.instance.weaponDic[weaponId].damage_torso;
                    if (bodyCollider.part == BodyPart.Legs) damage = WeaponDic.instance.weaponDic[weaponId].damage_legs;
                    bodyCollider.GetDamaged(playerName, damage, weaponId);
                    bodyCollider.character.GetComponent<PlayerController>().GetHit(transform.position);
                }
                else break;
            }
            var playerFire = new PlayerFire(state.playerName, hitPoint);
            NetworkManager.Broadcast(MessageType.Fire, playerFire);
        }
    }

    public void StartReload()
    {
        reloading = true;
        WeaponInfo weapon = weapons[activeWeaponIndex];
        reloadTimer = WeaponDic.instance.weaponDic[weapon.id].reloadTime;
    }

    public void ReloadDone()
    {
        WeaponInfo weapon = weapons[activeWeaponIndex];
        int capacity = WeaponDic.instance.weaponDic[weapon.id].magazineCapacity;
        if (weapon.ammoReserve >= capacity - weapon.ammoNum)
        {
            weapon.ammoReserve -= capacity - weapon.ammoNum;
            weapon.ammoNum = capacity;
        }
        else
        {
            weapon.ammoNum += weapon.ammoReserve;
            weapon.ammoReserve = 0;
        }
    }

    public void AcquireWeapon(int id, int ammoNum, int ammoReserve)
    {
        WeaponConfig weaponConfig = WeaponDic.instance.weaponDic[id];
        if (weaponConfig.weaponType == WeaponType.Handgun)
        {
            weapons[0] = new WeaponInfo(id, ammoNum, ammoReserve);
        }
        else
        {
            weapons[1] = new WeaponInfo(id, ammoNum, ammoReserve);
            SwitchWeapon(1);
        }
    }

    public void SwitchWeapon(int index)
    {
        reloading = false;
        activeWeaponIndex = index;
    }
}
