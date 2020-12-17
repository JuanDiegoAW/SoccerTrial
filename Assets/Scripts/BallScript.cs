using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BallScript : MonoBehaviour
{
    // Structure that will contain the necessary data to shoot a curved ball
    private struct QuadraticCurveData
    {
        public Vector2 startVector;     //The starting positition of the curved swipe
        public Vector2 middleVector;    //The maximum curve point of the swipe
        public Vector2 endVector;       //The ending position of the curved swipe
    }

    private struct PositionInTime
    {
        public Vector2 vectorPosition;
        public float positionTime;
    }

    // The initial position where the ball spawns (relative to the camera)
    private Vector3 ballSpawnPosition;

    // Vector that indicates the starting position of the swipe that will shoot the ball
    private Vector2 swipeStartPosition;
    // Vector that indicates the endind position of the swipe that will shoot the ball
    private Vector2 swipeEndPosition;
    // Vector resultant of substracting the swipeStartPosition and the swipeEndPosition to let us know the overall swipe direction
    private Vector2 overallSwipeDirection;
    // Vectores that will save in real time what is the maximum curve position, either to the right or to the left
    private Vector2 swipeCurveRight, swipeCurveLeft;

    // RigidBody of the ball prefab
    private Rigidbody ballRigidBody;

    // Boolean that indicates if the ball is in movement or not
    private bool isBallInMovement = false;
    // Boolean that indicates if the throw will curve the ball, or if the shot is straight
    private bool isBallThrowCurved = false;
    // Boolean that will be used only on curved shots. It indicates if the very last force is applied to the ball after giving it the curved effect
    private bool isLastForceApplied = false;
    // Boolean that defines if a goal was scored
    private bool isGoalScored = false;
    // Boolean that keeps track if a corrutine is currently active or not
    private bool isRespawnCorrutineActive = false;
    // Boolean that indicates if the ball respawn has been canceled or not
    private bool isRespawnCancelled = false;
    // Boolean that indicates the user has already finished touching the screen
    private bool isTouchPhaseEnded = false;

    private bool isTouchOriginatedFromBallPosition = false;

    private bool isShotDirectionToTheRight = false;

    // Variable that will contain the data of the curved shot
    private QuadraticCurveData curveData;

    // Variables that will store time variables. swipeStartTime = when the swipe started. swipeIntervalTime = how much time does the swipe lasted
    // movementStartTime = at what time does the ball started moving
    private float swipeStartTime, swipeIntervalTime, movementStartTime;
    // Variable that will indicate how strong the shot is in the X axis
    [SerializeField] private float throwForceX;
    // Variable that will indicate how strong the shot is in the Y axis
    [SerializeField] private float throwForceY;
    // Variable that indicates how strong the shot is in the Z axis
    [SerializeField] private float throwForceZ;
    // Variable that indicates how strong the shot is in the Z axis
    [SerializeField] private float throwForceZCurved;
    // Time necessary to respawn the ball after it was shot
    [SerializeField] private float respawnTime;
    // Variable that serves as a threshold to know when a shot is curved in the x axis or not
    private float curveAllowedHorizontalPercentage = 10;
    // Variable that serves as a threshold to know when a shot is curved or not in the y axis
    private float curveAllowedVerticalPercentage = 5;
    // Variable that keeps track of how much force is applied in the Z axis when the ball is shot
    private float zForceApplied = 0f;

    // Starting Lives of the player
    [SerializeField] private int startingLives;
    // Frames used to decide if a user 
    [SerializeField] private int frameThreshold;
    // The actual number of lies of the player
    private int currentLives;

    private int maxZForce = 670;

    private Queue<PositionInTime> positionInTimeQueue = new Queue<PositionInTime>();

    // GameObject with the behaviour of the referee
    private GameObject referee;
    // GameObject with the behaviour of the goal
    private GameObject goal;

    // Trail renderer of the gameobject
    private TrailRenderer trailRenderer;

    // Object hit by the raycast
    RaycastHit objectCollided;
    // Ray that determines if the user taps the ball or not
    Ray touchRay;

    [SerializeField] private Text startVectorText;
    [SerializeField] private Text leftVectorText;
    [SerializeField] private Text rightVectorText;
    [SerializeField] private Text endVectorText;
    [SerializeField] private Text directionText;

    [SerializeField] private Text leftCurvePercentage;
    [SerializeField] private Text rightCurvePercentage;

    [SerializeField] private Image startPositionIndicator;
    [SerializeField] private Image middlePositionIndicator;
    [SerializeField] private Image endPositionIndicator;
    [SerializeField] private Image rectPositionIndicator;

    // Start is called before the first frame update
    void Start()
    {
        ballSpawnPosition = gameObject.transform.localPosition;
        ballRigidBody = GetComponent<Rigidbody>();
        trailRenderer = gameObject.GetComponent<TrailRenderer>();
        referee = GameObject.FindGameObjectWithTag(TagsEnum.GameObjectTags.Referee.ToString());
        goal = GameObject.FindGameObjectWithTag(TagsEnum.GameObjectTags.Goal.ToString());
        currentLives = startingLives;
    }

    // Update is called once per frame
    void Update()
    {
        // The player will only be allowed to throw the ball if there are enough lives left
        if (this.isPlayerAlive())
        {
            // If the user is touching the screen and if the ball is NOT in movement
            if (Input.touchCount > 0 && !isBallInMovement)
            {
                if (!isTouchPhaseEnded)
                {
                    Touch actualTouch = Input.GetTouch(0);
                    
                    if (actualTouch.phase.Equals(TouchPhase.Began))
                    {
                        if (this.IsTouchOverBall(actualTouch))
                            isTouchOriginatedFromBallPosition = true;
                    }
                    if (isTouchOriginatedFromBallPosition)
                    { 
                        RepositionBallBasedOnTouch(actualTouch);
                        if (actualTouch.phase == TouchPhase.Ended)
                        {
                            this.SaveFinalTouchFrameData(actualTouch);
                            this.ShotEnded();
                        }
                        else
                        {
                            this.addElementsToPositionQueue(new PositionInTime()
                            {
                                vectorPosition = actualTouch.position,
                                positionTime = Time.time
                            });
                        }
                    }
                }
            }
            // If the ball is in movement and the throw is curved
            //else if (isBallInMovement && isBallThrowCurved)
            //{
            //    float timeElapsed = (Time.time - movementStartTime) / swipeIntervalTime * 0.4f;
            //    // If the time elapsed since the ball was shot is still in range, we apply the curved effect to the ball
            //    if (timeElapsed <= 1f)
            //        this.CurveQuadraticBall(timeElapsed, curveData);
            //    // If enough time has passed to fully curve the ball, we see if it is necessary to apply the last force in the X axis to it
            //    else if (!isLastForceApplied)
            //    {
            //        isLastForceApplied = true;
            //        if (zForceApplied > 700f)
            //            this.AddForceToBall(overallSwipeDirection.x / 5, 0f, 0.8f);
            //        else if (zForceApplied > 500f)
            //            this.AddForceToBall(overallSwipeDirection.x / 15, 0f, 0.8f);
            //        else if (zForceApplied > 300f)
            //            this.AddForceToBall(overallSwipeDirection.x / 20, 0f, 0.8f);
            //        else if (zForceApplied > 200f)
            //            this.AddForceToBall(overallSwipeDirection.x / 23, 0f, 0.8f);
            //        else if (zForceApplied > 100f)
            //            this.AddForceToBall(overallSwipeDirection.x / 30, 0f, 0.8f);
            //        else
            //            this.AddForceToBall(overallSwipeDirection.x / 70, 0f, 0.8f);
            //    }
            //}
        }
    }


    private float FindSlopeOfTwoPoints(Vector2 firstPoint, Vector2 secondPoint, out float yOffset)
    {
        float slope = (firstPoint.y - secondPoint.y) / (firstPoint.x - secondPoint.x);
        yOffset = firstPoint.y - (slope * firstPoint.x);
        //yOffset = default;
        return slope;
    }

    private void ShotEnded()
    {      
        //this.AnalyzePositionQueue();
        this.CalculateBallDirectionAndShoot();
        this.StartCoroutine(AwaitToSpawnBall());
    }

    private void SaveFinalTouchFrameData(Touch finalTouch)
    {
        isTouchPhaseEnded = true;
        swipeEndPosition = finalTouch.position;

        PositionInTime firstSavedPosition = positionInTimeQueue.Dequeue();
        swipeStartPosition = firstSavedPosition.vectorPosition;
        swipeIntervalTime = Time.time - firstSavedPosition.positionTime;
        overallSwipeDirection = firstSavedPosition.vectorPosition - swipeEndPosition;

        isShotDirectionToTheRight = swipeEndPosition.x > swipeStartPosition.x;
    }

    // Method that defines if a user's touch is over the ball or not
    private bool IsTouchOverBall(Touch touch)
    {
        touchRay = Camera.main.ScreenPointToRay(touch.position);
        if (Physics.Raycast(touchRay, out objectCollided))
        {
            if (objectCollided.collider.gameObject.tag == TagsEnum.GameObjectTags.Player.ToString())
                return true;
        }
        return false;
    }

    // Method to reposition the ball based on a touch position
    private void RepositionBallBasedOnTouch(Touch touch)
    {
        //touchStartedOnRepositionArea = true;
        Vector2 newPosition = Camera.main.ScreenToWorldPoint(new Vector3(touch.position.x, touch.position.y, gameObject.transform.localPosition.z));
        gameObject.transform.position = new Vector3(
            newPosition.x,
            newPosition.y,
            gameObject.transform.position.z
        );
        trailRenderer.Clear();
    }

    private IEnumerator RepositionBallBasedOnTouchWithDelay(Touch touch, float time)
    {
        yield return new WaitForSeconds(time);
        Vector2 newPosition = Camera.main.ScreenToWorldPoint(new Vector3(touch.position.x, touch.position.y, gameObject.transform.localPosition.z));
        gameObject.transform.position = new Vector3(
            newPosition.x,
            newPosition.y,
            gameObject.transform.position.z
        );
        trailRenderer.Clear();
    }

    // Method to verify a player is alive
    private bool isPlayerAlive()
    {
        return currentLives > 0;
    }

    private void addElementsToPositionQueue(PositionInTime positionInTime)
    {
        if (positionInTimeQueue.Count == frameThreshold)
        {
            positionInTimeQueue.Dequeue();        
        }
        else if (positionInTimeQueue.Count > frameThreshold)
        {
            for (int i = 0; i <= (positionInTimeQueue.Count - frameThreshold); i++)
            {
                positionInTimeQueue.Dequeue();
            }
        }
        positionInTimeQueue.Enqueue(positionInTime);
    }

    // Method to compare the current position of the user's touch and see if it surpasses the previously registerd highest point of the swipe curve.
    private void CompareActualTouchToHighestCurves(Touch touch)
    {
        if (touch.position.x > swipeCurveRight.x)
            swipeCurveRight = touch.position;
        else if (touch.position.x < swipeCurveLeft.x)
            swipeCurveLeft = touch.position;
    }

    // Method to save the final thata when the user ends the swipe movement
    private void GetTouchEndData(Touch touch)
    {
        swipeIntervalTime = Time.time - swipeStartTime;
        swipeEndPosition = touch.position;
        overallSwipeDirection = swipeStartPosition - swipeEndPosition;
    }

    // Mehotd to calculate if a shot needs to be curved, and it what direction, or if the shot needs to be straight (and its direction too)
    private void CalculateBallDirectionAndShoot()
    {
        if (isBallThrowCurved)
        {
            this.ShootCurvedBall(swipeCurveRight);
            startPositionIndicator.transform.position = new Vector3(swipeStartPosition.x, swipeStartPosition.y, 0f);
            //middlePositionIndicator.transform.position = new Vector3(swipeCurveRight.x, swipeCurveRight.y, 0f);
            endPositionIndicator.transform.position = new Vector3(swipeEndPosition.x, swipeEndPosition.y, 0f);
        }
        else
        {
            startPositionIndicator.transform.position = new Vector3(swipeStartPosition.x, swipeStartPosition.y, 0f);
            middlePositionIndicator.transform.position = new Vector3(0f, 0f, 0f);
            endPositionIndicator.transform.position = new Vector3(swipeEndPosition.x, swipeEndPosition.y, 0f);

            this.ShootStraightBall();
        }
    }

    // Method to do a straight shoot based on the overallSwipeDirection
    private void ShootStraightBall()
    {
        // We set that the ball is in movement and remove its kinematic property
        isBallInMovement = true;
        ballRigidBody.isKinematic = false;

        zForceApplied = (-overallSwipeDirection.y * throwForceZ / swipeIntervalTime);
        zForceApplied = zForceApplied > 0f ? zForceApplied : 0f;
        zForceApplied = zForceApplied > maxZForce ? maxZForce : zForceApplied;

        float normalForceX = (-overallSwipeDirection.x * throwForceX);
        float xOffset = (swipeStartPosition.x - Camera.main.pixelWidth / 2) * throwForceX * (zForceApplied / 810f);
        
        float normalForceY = (-overallSwipeDirection.y * throwForceY);
        float yEndSwipePercentage = (swipeEndPosition.y / Camera.main.pixelHeight);
        float yOffset = yEndSwipePercentage * normalForceY;
        yOffset = yEndSwipePercentage > 0.5f ? yOffset *= 3f : yOffset *= 1.5f;

        this.AddForceToBall(normalForceX + xOffset, normalForceY + yOffset, zForceApplied);

        rightCurvePercentage.text = zForceApplied.ToString();

        // We register the time when the ball was shot
        movementStartTime = Time.time;
    }

    // Method to shoot a curved ball based on the curveVector specified
    private void ShootCurvedBall(Vector2 curveVector)
    {
        // We set that the ball is in movement, that is a curved throw and remove its kinematic property
        isBallInMovement = true;
        ballRigidBody.isKinematic = false;
        isBallThrowCurved = true;

        zForceApplied = (-overallSwipeDirection.y * throwForceZCurved / swipeIntervalTime);
        zForceApplied = zForceApplied > 0f ? zForceApplied : 0f;
        zForceApplied = zForceApplied > maxZForce ? maxZForce : zForceApplied;

        float normalForceY = (-overallSwipeDirection.y * throwForceY);
        float yEndSwipePercentage = (swipeEndPosition.y / Camera.main.pixelHeight);
        float yOffset = yEndSwipePercentage * normalForceY;
        yOffset = yEndSwipePercentage > 0.5f ? yOffset *= 3f : yOffset *= 1.5f;

        this.AddForceToBall(0f, normalForceY + yOffset, zForceApplied);

        // We register the time when the ball was shot
        movementStartTime = Time.time;
        // We set the curve data
        if (isShotDirectionToTheRight)
        { 
            this.SetQuadraticCuvedBallData(
                new Vector2(swipeEndPosition.x + (swipeEndPosition.x * 0.5f), swipeEndPosition.y), 
                /*curveVector*/new Vector2(curveVector.x, swipeEndPosition.y + (swipeEndPosition.y - curveVector.y)),
                /*swipeStartPosition*/ new Vector2(swipeStartPosition.x, swipeEndPosition.y + (swipeEndPosition.y - swipeStartPosition.y))
            );
        }
        else
        {
            this.SetQuadraticCuvedBallData(
                new Vector2(swipeEndPosition.x - (swipeEndPosition.x * 0.5f), swipeEndPosition.y),
                /*curveVector*/new Vector2(curveVector.x, swipeEndPosition.y + (swipeEndPosition.y - curveVector.y)),
                /*swipeStartPosition*/ new Vector2(swipeStartPosition.x, swipeEndPosition.y + (swipeEndPosition.y - swipeStartPosition.y))
            );
        }

        rightCurvePercentage.text = zForceApplied.ToString();
    }

    private void AddForceToBall(float xForce, float yForce, float zForce)
    {
        ballRigidBody.AddForce(
            xForce,
            yForce,
            zForce
        );
    }

    // Method to set specific vectors to shoot a curved ball
    private void SetQuadraticCuvedBallData(Vector2 startingVector, Vector2 mVector, Vector2 endingVector)
    {     
        curveData = new QuadraticCurveData()
        {
            startVector = startingVector,
            middleVector = mVector,
            endVector = endingVector
        };
    }

    // Method to curve the ball in the X axis based on a Quadratic Bezier Curve 
    private void CurveQuadraticBall(float time, QuadraticCurveData curveData)
    {
        Vector3 curve = this.CalculateQuadraticBezierCurve(
            time,
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.startVector.x, curveData.startVector.y, 1f)),
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.middleVector.x, curveData.middleVector.y, 1f)),
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.endVector.x, curveData.endVector.y, 1f))
        );
        // We curve the ball in the X axis based on the time elapsed since the ball was shot
        gameObject.transform.position += new Vector3(curve.x - gameObject.transform.position.x, 0f, 0f);
    }

    // Method to calculate a Cuadratic Bezier Curve based on three vectors and the time elapsed
    // http://www.theappguruz.com/blog/bezier-curve-in-games
    private Vector3 CalculateQuadraticBezierCurve(float time, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        // B(t) = (1-t)^2 * P0 + 2(1-t)tP1 + t^2 * P2 , 0 < t < 1
        //return=       a      +    b      +     c 
        float timeMinusOne = 1 - time;
        Vector3 a = timeMinusOne * timeMinusOne * p0;
        Vector3 b = 2 * timeMinusOne * time * p1;
        Vector3 c = time * time * p2;

        return a + b + c;
    }

    // Method to substract a life from the player and return the number of lifes it has after that
    public int SubstractLife(int livesToSubstract)
    {
        if (livesToSubstract > currentLives)
            currentLives = 0;
        else
            currentLives -= livesToSubstract;
        return currentLives;
    }

    // Method to restart the number of lives of the player
    public void RestartLives()
    {
        currentLives = startingLives;
    }

    // Method to set if the spawn is canceled
    public void SetIsRespawnCanceled(bool isRespawnCancelled)
    {
        this.isRespawnCancelled = isRespawnCancelled;
    }

    // Method to cancel the respawn corrutine if it was activated
    public void CancelRespawnCorrutineIfActive()
    {
        if (isRespawnCorrutineActive)
            this.SetIsRespawnCanceled(true);
    }

    // Method to set if the ball scored a goal or not
    public void SetIsGoalScored(bool goalScored)
    {
        this.isGoalScored = goalScored;
        if (goalScored)
            this.RestartLives();
    }

    // Method to await x amount of secons and spawn the ball without any velocity of force in it
    private IEnumerator AwaitToSpawnBall()
    {
        isRespawnCorrutineActive = true;
        yield return new WaitForSeconds(respawnTime);

        if (!isRespawnCancelled)
            this.RespawnBall();
        else
            isRespawnCancelled = false;
        isRespawnCorrutineActive = false;
    }

    // Method to respown a ball
    public void RespawnBall()
    {
        this.ResartValues();
        trailRenderer.Clear();
        if (isGoalScored)
        {
            referee.GetComponent<RefereeScript>().DisableAllCards();
            referee.GetComponent<RefereeScript>().RestartTimer();
        }       
    }

    private void ResartValues()
    {
        positionInTimeQueue.Clear();
        isBallInMovement = false;
        isTouchOriginatedFromBallPosition = false;
        isTouchPhaseEnded = false;
        isLastForceApplied = false;
        isBallThrowCurved = false;
        ballRigidBody.velocity = Vector3.zero;
        ballRigidBody.angularVelocity = Vector3.zero;
        ballRigidBody.AddForce(0f, 0f, 0f);
        ballRigidBody.isKinematic = true;
        isGoalScored = false;
        gameObject.transform.localPosition = ballSpawnPosition;
    }
}
