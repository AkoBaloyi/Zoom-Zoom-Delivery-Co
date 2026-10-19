using UnityEngine;

public class taxiscriptRunner : MonoBehaviour
{
    public TaxiScript taxiScript;
    public CarAIController carAIController;

    private bool routeStarted = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        taxiScript.ComputeRoute();
    }

    // Update is called once per frame
    void Update()
    {
     if (!routeStarted)
        {
            if (taxiScript.coroutines > 0)
            {
                carAIController.isCarControlledByAI = false;
            }
            else
            {
                carAIController.isCarControlledByAI = true;
                carAIController.nextCheckpoint = taxiScript.bestRoute[0];
                routeStarted = true;
            }
        }
    }
}
