using System;
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

    public int uid;
    
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
        InitializeForRound(dropMainGun: false);
    }

    /// <summary>
    /// 回合开始时重置武器状态。
    /// 副武器永远刷新到默认手枪（id=2）+ 满弹。
    /// 主武器：dropMainGun=true → 丢失（清空槽位）；false → 保留并补满弹药。
    /// 切回手枪槽位作为本回合默认握持，避免上回合死时持枪状态被带过来。
    /// </summary>
    public void InitializeForRound(bool dropMainGun)
    {
        // 副武器永远满血复活
        weapons[0] = new WeaponInfo(2, 12, 24);

        if (dropMainGun)
        {
            weapons[1] = null;
        }
        else if (weapons[1] != null)
        {
            // 保留主武器，补满弹药
            WeaponConfig cfg = WeaponDic.instance.weaponDic[weapons[1].id];
            weapons[1] = new WeaponInfo(cfg.id, cfg.magazineCapacity, cfg.magazineCapacity * 2);
        }

        // 默认握持槽位：有主武器持主武器，否则持副武器
        activeWeaponIndex = weapons[1] != null ? 1 : 0;
        reloading = false;
        firingTime = 0;
        upTime = 0;
        upAngle = 0;
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

            PlayerStateInfo state = NetworkManager.playerStateInfos[GetComponent<PlayerState>().uid.ToString()];

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

            // 子弹最远落点（命中链结束后用于广播 PlayerFire 的 hitPoint）
            Vector3 hitPoint = center + fireDirection * 100f;

            // 命中链处理：按距离顺序遍历
            //  - 是自己的 BodyCollider → 跳过（不更新 hitPoint，子弹假装没被自己挡住）
            //  - 是敌人的 BodyCollider → 扣血并停止（FPS 标准：子弹不穿透敌人）
            //  - 其它（墙、地） → 落点定在该 hit，停止
            foreach (RaycastHit hit in hits)
            {
                BodyCollider bodyCollider = hit.collider.GetComponent<BodyCollider>();
                if (bodyCollider != null)
                {
                    // 跳过射手自己身上的 BodyCollider，不更新 hitPoint
                    if (bodyCollider.character.GetComponent<PlayerState>().uid == uid)
                        continue;

                    // 命中敌人：结算伤害 + 通知被击中者播 hit indicator
                    int damage = 0;
                    if (bodyCollider.part == BodyPart.Head) damage = WeaponDic.instance.weaponDic[weaponId].damage_head;
                    if (bodyCollider.part == BodyPart.Torso) damage = WeaponDic.instance.weaponDic[weaponId].damage_torso;
                    if (bodyCollider.part == BodyPart.Legs) damage = WeaponDic.instance.weaponDic[weaponId].damage_legs;
                    bodyCollider.GetDamaged(uid, damage, weaponId);
                    bodyCollider.character.GetComponent<PlayerController>().GetHit(transform.position);

                    hitPoint = hit.point;
                    break;        // FPS 默认：命中第一个敌人就停（不穿透）
                }
                else
                {
                    // 撞到墙 / 地等场景物：落点定在这里，停止
                    hitPoint = hit.point;
                    break;
                }
            }
            var playerFire = new PlayerFire(state.uid, hitPoint);
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
