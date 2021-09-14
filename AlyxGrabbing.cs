using System;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

[RequireComponent(typeof(LineRenderer))]
public class AlyxGrabbing : MonoBehaviour
{
    public SteamVR_Action_Boolean pinchAction;
    public SteamVR_Action_Boolean grabAction;

    [Tooltip("The size of the box after the raycast hits.")]
    public float boxSize = 0.5f;
    [Tooltip("At which angle the raycast will ignore any object")]
    public float angle = 20;
    [Tooltip("How far the raycast checks.")]
    public float rayLength = 4f;
    [Tooltip("How curved the line appears. Currently a min/max limit of 2")]
    public int amountLinePoints = 2;
    [Tooltip("How long we are going to affect the object")]
    public float letGo = 1f;

    public GameObject debugobject;
    private Hand hand;
    private Interactable lastItem;
    private Interactable currentItem;
    private LineRenderer line;
    private LayerMask layerMask;

    //Related to grabbing the item
    private Rigidbody rb;
    private float startTime;
    private bool inRange;
    private bool attractingObject;
    // Start is called before the first frame update
    void Start()
    {
        //The layer the hand is on
        layerMask = gameObject.layer;
        hand = gameObject.GetComponent<Hand>();
        line = gameObject.GetComponent<LineRenderer>();
        line.positionCount = amountLinePoints;
        startTime = Time.time;
    }

    private void Update()
    {
        Vector3 pos = transform.position;
        //Check if button is pressed, if we have detected an item, and if we are not attracting something or holding something
        if (!attractingObject && hand.currentAttachedObject is null && IsPinchButtonPressed() && !(lastItem is null))
        {
            //Set up lines from the hand to lastItem with a curve
            Quaternion rot = transform.localRotation;
            Vector3 itemPosition = lastItem.transform.position;
            Quaternion direction = Quaternion.LookRotation(itemPosition - pos);
            
            Vector3 middlePoint = Vector3.Lerp(pos, itemPosition, 0.5f);

            middlePoint += direction * new Vector3(rot.y, -rot.x, 0);
            
            DrawCurve(pos,middlePoint,itemPosition);
            line.enabled = true;
            //Check if we are flicking our hand
            if (hand.GetTrackedObjectAngularVelocity().x < -4)
            {
                rb = lastItem.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    return;
                }
                Debug.Log("Flick");
                attractingObject = true;
                startTime = Time.time;
                //Initial velocity to get it going
                rb.velocity = transform.position - rb.position + Vector3.up * 4;
                SendMessageToObjects(true, false);
            }
        }
        else
        {
            line.enabled = false;
        }
        
        //Exit out of the update function if we are not attracting something
        if (!attractingObject && rb == null)
            return;
        
        float timeDelta = Time.time - startTime;

        //Are we close to the hand?
        if (Vector3.Distance(pos, rb.position) < 0.5 && !inRange)
        {
            Debug.Log("In Range");
            hand.HoverLock(lastItem);
            inRange = true;
            return;
        }
        //Check if the letGo duration has expired or if we catch something   
        if (timeDelta >= letGo || !(hand.currentAttachedObject is null))
        {
            Debug.Log("Letting go");
            hand.HoverUnlock(lastItem);
            attractingObject = false;
            rb = null;
            inRange = false;
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {

        //Keep moving the object to the hand
        if (attractingObject && rb != null)
        {
            if(Vector3.Distance(transform.position, rb.position) > 1 && !inRange)
            {
                rb.AddForce(transform.position - rb.position, ForceMode.Acceleration);
            }
        }
        
        
        //Check if we are not holding something, if the button is pressed, and we are not attracting something
        if (!attractingObject && hand.currentAttachedObject is null && !IsPinchButtonPressed())
        {
            //Set up raycast stuff
            RaycastHit[] hit;
            Ray ray = new Ray(transform.position, transform.forward);
            //Check if we hit a collider
            //Possibly change this to a box raycast
            hit = Physics.SphereCastAll(ray, boxSize,rayLength, layerMask, QueryTriggerInteraction.UseGlobal);

            if (hit.Length > 0)
            {
                //Get the closest gameObject and set that in the currentItem variable
                GetClosestGameObject(hit);

                //If nothing is found
                if (currentItem is null)
                {
                    SendMessageToObjects(true, false);
                    lastItem = null;
                    return;
                }
                //If something is found and it's not the same as the last item
                if (currentItem != lastItem)
                {
                    SendMessageToObjects(true, true);
                    lastItem = currentItem;
                    
                    currentItem = null;
                }
            }
            else
            {
                //Set everything back to null if we didn't raycast anything
                SendMessageToObjects(true, false);
                currentItem = null;
                lastItem = null;
            }
        }
        
    }


    private void GetClosestGameObject(RaycastHit[] hit)
    {
        //Set up variables
        Collider bestTarget = null;
        float closestDistance = Mathf.Infinity;
        Vector3 currentPosition = transform.position;
        //Loop over every Collider we detected and see which one is closer
        foreach (var collision in hit)
        {
            //First check the angle of the object, ignore if it's too far angled from the hand
            Vector3 directionToTarget = collision.collider.transform.position - currentPosition;
            if (Vector3.Angle(directionToTarget, transform.forward) > angle)
            {
                Debug.Log("Object outside of hand influence " + collision.collider.name);
            }
            else
            {
                float distance = directionToTarget.sqrMagnitude;
                if (!(distance < closestDistance)) continue;
                closestDistance = distance;
                bestTarget = collision.collider;
            }
        }

        try
        {
            currentItem = bestTarget.GetComponent<Interactable>();
        }
        catch (NullReferenceException e)
        {
            return;
        }
        
        //We didn't find an Interactable on the object, so check the parent for an interactable
        //TODO keep looping up until we find a parent with a possible upper limit to prevent lag
        if (currentItem is null)
        {
            currentItem = bestTarget.transform.parent.GetComponent<Interactable>();
        }
    }

    private bool IsPinchButtonPressed()
    {
        //Returns true when pressed, returns false when not pressed or not defined
        return pinchAction != null && pinchAction.GetState(hand.handType) || grabAction != null && grabAction.GetState(hand.handType);
    }

    void SendMessageToObjects(bool sendToLastItem, bool sendToCurrentItem)
    {
        //Taken from hand.cs at line 164
        //Send messages to the gameObjects so that they can do their own events
        //Used in this case to add an outline to objects we detected
        if (lastItem != null && sendToLastItem)
        {
            lastItem.SendMessage("OnHandHoverEnd", hand, SendMessageOptions.DontRequireReceiver);
            if (lastItem != null)
            {
                this.BroadcastMessage("OnParentHandHoverEnd", lastItem, SendMessageOptions.DontRequireReceiver);
            }
        }

        if (currentItem != null && sendToCurrentItem)
        {
            currentItem.SendMessage("OnHandHoverBegin", hand, SendMessageOptions.DontRequireReceiver);
            if (currentItem != null)
            {
                this.BroadcastMessage("OnParentHandHoverBegin", currentItem, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    
    private void DrawCurve(Vector3 handObject, Vector3 middlePoint, Vector3 endObject)
    {
        //Taken from https://www.codinblack.com/how-to-draw-lines-circles-or-anything-else-using-linerenderer/
        float t = 0f;
        Vector3 point = new Vector3(0, 0, 0);
        for (int i = 0; i < line.positionCount; i++)
        {
            point = (1 - t) * (1 - t) * handObject + 2 * (1 - t) * t * middlePoint + t * t * endObject;
            line.SetPosition(i, point);
            t += 1 / (float)line.positionCount;
        }
    }
}
