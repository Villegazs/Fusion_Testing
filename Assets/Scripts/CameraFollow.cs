using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Singleton
    {
        get => _singleton;

        set
        {
            if (value == null)
                _singleton = null;
            else if (_singleton == null)
                _singleton = value;
            else if(_singleton != value)
            {
                Destroy(value);
                Debug.LogError($"There should only ever be one instance of {nameof(CameraFollow)}");
            }
        }
    }

    public static CameraFollow _singleton;
    [SerializeField] private Highlighter highlighter;

    private Player player;

    private Transform target;

    private void Awake()
    {
        Singleton = this;
    }

    private void OnDestroy()
    {
        if (Singleton == this)
            Singleton = null;

    }

    private void LateUpdate()
    {
        if (target != null)
        {
            transform.SetPositionAndRotation(target.position, target.rotation);
            highlighter.UpdateHighlightable(transform.position, transform.forward, player);
        }
    }

    public void SetTarget(Transform newTarget, Player player)
    {
        target = newTarget;
        this.player = player;
    }

}
