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
        set { myVar = value; }
    }

    [field: SerializeField ]
    public UnityEvent <Vector2> onDrive  { get; set; }


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

    public void SetPath(List<Vector3> path)
    {
        if (path.Count == 0)

        {
            Destroy(gameObject);
                return;
        }
        this.path = path;
            index = 0;
        currentTargetPosition = this.path[index];

        Vector3 relativepoint = transform.InverseTransformPoint(this.path[index + 1]);
        float angle = Mathf.Atan2(relativepoint.x, relativepoint.z) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0, angle, 0);
        stop = false;
    }

    private void Update()
    {
        CheckIfArrived();
        Drive();
    }

    private void Drive()
    {
        if (stop)
        {
            onDrive?.Invoke(Vector2.zero);
        }
        else
        {
            Vector3 relativepoint = transform.InverseTransformPoint(currentTargetPosition);
            float angle = Mathf.Atan2(relativepoint.x, relativepoint.z) * Mathf.Rad2Deg;
            var rotateCar = 0;

            if (angle > turningAngleOffset)
            {
                rotateCar = 1;
            }

            else if (angle < -turningAngleOffset)

            {  rotateCar = -1;}

            onDrive?.Invoke( new Vector2(rotateCar,1));
        }
    }

    private void CheckIfArrived()
    {
        if (stop == false)

        {
            var distanceToCheck = arriveDistance;
            if (index == path.Count -1)
            {
                distanceToCheck = lastpointArriveDistance;
            }

            if (Vector3.Distance(currentTargetPosition, transform.position) < distanceToCheck)

            {
                SetNextTargetIndex();
            }
        }
    }

    private void SetNextTargetIndex()
    {
        index++;

        if (index >=path.Count)
        {
            stop = true;
            Destroy(gameObject);
        }
        else
        {
            currentTargetPosition = path[index];
        }
    }

}
