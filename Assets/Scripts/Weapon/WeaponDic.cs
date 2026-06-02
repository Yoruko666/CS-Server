using System.Collections.Generic;
using UnityEngine;

public class WeaponDic : SingletonMono<WeaponDic>
{
    public WeaponDatabase weaponDatabase;
    [HideInInspector] public List<WeaponConfig> weaponDic = new List<WeaponConfig>();

    protected override void OnSingletonAwake()
    {
        // 与原 Start 行为一致；改放到 OnSingletonAwake 是为了在依赖它的其他单例
        // Awake 中也能立即取到 weaponDic（Awake 早于所有 Start）。
        weaponDic = weaponDatabase.weaponDatabase;
    }
}
