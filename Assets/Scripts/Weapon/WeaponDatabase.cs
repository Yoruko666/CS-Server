using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponDatabase", menuName = "SO/Weapon/WeaponDatabase")]

public class WeaponDatabase : ScriptableObject
{
    public List<WeaponConfig> weaponDatabase = new List<WeaponConfig>();
}
