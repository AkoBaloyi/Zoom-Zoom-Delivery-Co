using NUnit.Framework;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class CarrAI : MonoBehaviour
{
    [SerializeField]
    private List<Vector3> path = null;

    [SerializeField]
    private float arriveDistance = 3f, lastpointArriveDistance = .1f;

    [SerializeField]
    private float turningAngleOffset = 5;

    [SerializeField]
    private Vector3 currentTargetPosition;

    private int index = 0;


    private bool myVar;
    public bool stop
    {



        get { return myVar; }
        set { stop = value; }
    }

    [field: SerializeField ]
    public UnityEvent <Vector2> onDerieve  { get; set; }


    private void Start()
    {
        if (path == null || path.Count == 0)
        {
            stop = true;
        }

        else
        {
            currentTargetPosition = path[index];
        }
    }
}
