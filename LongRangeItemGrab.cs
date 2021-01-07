using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

public class LongRangeItemGrab : MonoBehaviour
{
    public SteamVR_Action_Boolean teleportAction;
    
    private Hand hand;
    private LineRenderer line;

    private Interactable lastItem;
    private Interactable currentItem;

    public bool enableDebug;
    public Material debugMaterial;
    public float boxSize = 4f;
    public float rayLength = 4f;
    
    private LayerMask layerMask;

    private void Start()
    {
        layerMask = gameObject.layer;
        hand = gameObject.GetComponent<Hand>();
        
        //Add the LineRenderer component
        if (enableDebug)
        { 
            line = gameObject.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.03f;
            line.material = debugMaterial;
        }
    }

    private void FixedUpdate()
    {
        //Because for some items we can't teleport while hovering over it, we need to check if we are pressing the teleport button
        if (hand.currentAttachedObject is null && !IsTeleportButtonPressed())
        {
            //Set up raycast stuff
            Vector3 pos = transform.position;
            RaycastHit hit;
            Ray ray = new Ray(pos, transform.TransformDirection(Vector3.forward));
        
            //Send out raycast
            if (Physics.Raycast(ray, out hit, rayLength, layerMask))
            {
                Collider[] col = Physics.OverlapBox(hit.point, new Vector3(boxSize, boxSize, boxSize));
                //Debug stuff if you want to see the ray
                if (enableDebug)
                {
                    line.enabled = true;
                    var points = new Vector3[2];
                    points[0] = ray.origin;
                    points[1] = hit.point;
                    line.SetPositions(points);
                }

                GetClosestGameObject(col, hit.point);
                
                //If there is no Interactable set
                if (currentItem is null)
                {
                    hand.HoverUnlock(lastItem);
                    lastItem = null;
                    return;
                }
                
                //Hoverlock over the Interactable if the lastitem was not null or a different Interactable
                if (currentItem != lastItem)
                { 
                    hand.HoverLock(currentItem);
                    lastItem = currentItem;
                }
                return;
            }
            //Check if we are still hovering over the Interactable after no longer seeing any other Interactables
            if (hand.IsStillHovering(lastItem))
            {
                hand.HoverUnlock(lastItem);
                lastItem = null;
                currentItem = null;
            }
        }
        
    }
    
    private void GetClosestGameObject(Collider[] col, Vector3 position)
    {
        Collider bestTarget = null;
        float closestDistance = Mathf.Infinity;
        Vector3 currentPosition = position;
        //Loop over every Collider we detected and see which one is closer
        foreach (var collision in col)
        {
            Vector3 directionToTarget = collision.gameObject.transform.position - currentPosition;
            float distance = directionToTarget.sqrMagnitude;

            if (!(distance < closestDistance)) continue;
            closestDistance = distance;
            bestTarget = collision;
        }
        
        currentItem = bestTarget.GetComponent<Interactable>();
        
        //We didn't find an Interactable on the object, so check the parent for an interactable
        //TODO keep looping up until we find a parent with a possible upper limit to prevent lag
        if (currentItem is null)
        {
            currentItem = bestTarget.transform.parent.GetComponent<Interactable>();
        }
    }

    private bool IsTeleportButtonPressed()
    {
        //Returns true when pressed, returns false when not pressed or not defined
        return teleportAction != null && teleportAction.GetState(hand.handType);
    }
}
