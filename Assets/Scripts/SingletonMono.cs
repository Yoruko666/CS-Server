using UnityEngine;

public abstract class SingletonMono<T> : MonoBehaviour where T : SingletonMono<T>
{
    public static T instance;

    protected virtual bool DontDestroyOnSceneLoad => true;

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = (T)this;
            if (DontDestroyOnSceneLoad) DontDestroyOnLoad(gameObject);
            OnSingletonAwake();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnSingletonAwake() { }
}
