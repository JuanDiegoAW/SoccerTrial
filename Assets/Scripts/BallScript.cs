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

    private Vector3 curve;

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
    // Boolean that indicates if user started is swipe interactoin over the ball
    private bool isTouchOriginatedFromBallPosition = false;
    // Boolean that indicates if the shot is directed to the right. If not, it is directed to the left
    private bool isShotDirectionToTheRight = false;

    // Variable that will contain the data of the curved shot
    private QuadraticCurveData curveData;

    // Variables that will store time variables. swipeStartTime = when the swipe started. swipeIntervalTime = how much time does the swipe lasted
    // movementStartTime = at what time does the ball started moving
    private float swipeStartTime, swipeIntervalTime, movementStartTime, swipeCurveTime;
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
    // Variable that keeps track of how much force is applied in the Z axis when the ball is shot
    private float zForceApplied = 0f;
    // Variable used to verify if a circle was registered or not
    float gestureLength = 0;
    // The greater the curve factor, the bigger the curve in the throw
    float curveFactor = 0;

    // Starting Lives of the player
    [SerializeField] private int startingLives;
    // Frames used to decide if a user 
    [SerializeField] private int frameThreshold;
    // The actual number of lies of the player
    private int currentLives;
    // Maximum force in the Z axis on a normal throw
    private int maxZForce = 610;
    // Maximum factor added to a curved shot
    private int maxCurveFactor = 50;

    // Queue that registers the inputs from the user from the last 17 frames
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

    Vector2 gestureSum = Vector2.zero;

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
        if (this.IsPlayerAlive())
        {
            // If the user is touching the screen and if the ball is NOT in movement
            if (Input.touchCount > 0 && !isBallInMovement)
            {
                // If the user hasn't stopped touching the screen
                if (!isTouchPhaseEnded)
                {
                    // We store the current touch
                    Touch actualTouch = Input.GetTouch(0);
                    
                    // If the user is starting to touch the screen
                    if (actualTouch.phase.Equals(TouchPhase.Began))
                    {
                        // We need to verify the touch is over the ball
                        if (this.IsTouchOverBall(actualTouch))
                            isTouchOriginatedFromBallPosition = true;
                    }
                    // Else, if the user is currently making a touch gesture (that originated over the ball's position)
                    if (isTouchOriginatedFromBallPosition)
                    { 
                        // We reposition the ball to were the user is touching
                        RepositionBallBasedOnTouch(actualTouch);
                        // If the user is ending the touch gesture
                        if (actualTouch.phase == TouchPhase.Ended)
                        {
                            this.SaveFinalTouchFrameData(actualTouch);
                            this.ShotEnded();
                        }
                        // If the user is in the middle of the touch gesture
                        else
                        {
                            // We store the current position of the touch, and it's registered time
                            this.AddElementsToPositionQueue(new PositionInTime()
                            {
                                vectorPosition = actualTouch.position,
                                positionTime = Time.time
                            });
                            // And we verify if a circle gesture was registered
                            this.isCircleRegistered();
                        }
                    }
                }
            }
            // If the ball is in movement and the throw is curved
            else if (isBallInMovement && isBallThrowCurved)
            {
                float timeElapsed = (Time.time - movementStartTime) / swipeCurveTime * 0.45f;
                // If the time elapsed since the ball was shot is still in range, we apply the curved effect to the ball
                if (timeElapsed <= 0.85f)
                    this.CurveQuadraticBall(timeElapsed, curveData);
                // If enough time has passed to fully curve the ball, we see if it is necessary to apply the last force in the X axis to it
                else if (!isLastForceApplied)
                {
                    isLastForceApplied = true;
                    //endVectorText.text = "overallx: " + overallSwipeDirection.x;
                    float curveOffset = (curveFactor / maxCurveFactor);
                    startVectorText.text = curveOffset.ToString();

                    if (isShotDirectionToTheRight)
                        this.AddForceToBall(overallSwipeDirection.x / (10f / curveOffset), 0f, 0.5f);
                    else
                        this.AddForceToBall(overallSwipeDirection.x / (10f / curveOffset), 0f, 0.5f);
                }
            }
        }
    }

    // Method that evaluates the last inputs from the user to verify if a circle gesture was made or not
    private void isCircleRegistered()
    {
        rightVectorText.text = "" + curveFactor;

        endVectorText.text = "" + positionInTimeQueue.Count;
        PositionInTime[] touchPositions = new PositionInTime[positionInTimeQueue.Count];
        positionInTimeQueue.CopyTo(touchPositions,0);

        gestureSum = Vector2.zero;
        gestureLength = 0;
        Vector2 prevDelta = Vector2.zero;
        for (int i = 0; i < touchPositions.Length - 2; i++)
        {
            Vector2 delta = touchPositions[i + 1].vectorPosition - touchPositions[i].vectorPosition;
            float deltaLength = delta.magnitude;
            gestureSum += delta;
            gestureLength += deltaLength;
            if (Vector2.Dot(delta, prevDelta) < 0f)
            {
                startVectorText.text = "Cleared dot";
                positionInTimeQueue.Clear();
                //gestureCount = 0;
            }
            prevDelta = delta;
        }
        int gestureBase = (Screen.width + Screen.height) / 4;

        // If a circle was made, we augment the curve factor variable
        if (gestureLength > gestureBase && gestureSum.magnitude < gestureBase / 2)
            curveFactor+=1;

        // If a circle is not being made, we decrement the throw factor
        else if (curveFactor > 0)
            curveFactor -= 0.2f;

        else
            curveFactor = 0;
    }

    // Method to find the slope of two points and also return the Y offset
    private float FindSlopeOfTwoPoints(Vector2 firstPoint, Vector2 secondPoint, out float yOffset)
    {
        float slope = (firstPoint.y - secondPoint.y) / (firstPoint.x - secondPoint.x);
        yOffset = firstPoint.y - (slope * firstPoint.x);
        //yOffset = default;
        return slope;
    }

    // Method that defines the behaviour of the ball once the user's input for a shot ended. 
    private void ShotEnded()
    {
        //this.AnalyzePositionQueue();
        this.CalculateBallDirectionAndShoot();
        this.StartCoroutine(AwaitToSpawnBall());
    }

    // Method to store the necessary information once the user stopped touching the screen
    private void SaveFinalTouchFrameData(Touch finalTouch)
    {
        // The touch fase ended
        isTouchPhaseEnded = true;
        // We store the last swipe position registered
        swipeEndPosition = finalTouch.position;

        // We get the first position saved in the PositionInTimeQueue (the position the ball was in 17 frames ago)
        PositionInTime firstSavedPosition = positionInTimeQueue.Dequeue();
        swipeStartPosition = firstSavedPosition.vectorPosition;

        // We get the interval of time that transcurred between the first swipe position and the last swipe position
        swipeIntervalTime = (Time.time - firstSavedPosition.positionTime);
        // That interval gets duplicated for the swipeCurveTime, as to make the curve effect last longer
        swipeCurveTime = swipeIntervalTime * 2f;

        // We get the OverallSwipeDirection (the difference between the first swipe position and the last swipe position). This vector will determine the direction of the shot
        overallSwipeDirection = firstSavedPosition.vectorPosition - swipeEndPosition;

        // And we verify if the swipe direction is towards the right or not
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
        Vector2 newPosition = Camera.main.ScreenToWorldPoint(new Vector3(touch.position.x, touch.position.y, gameObject.transform.localPosition.z));
        gameObject.transform.position = new Vector3(
            newPosition.x,
            newPosition.y,
            gameObject.transform.position.z
        );
        // We do not register a tail renderer for the reposition of the ball
        trailRenderer.Clear();
    }

    // Method to reposition the ball based on a touch position with a small delay
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
    private bool IsPlayerAlive()
    {
        return currentLives > 0;
    }

    // Method to add elements to the PositionInTime Queue, verifying it does not surpass the maximum amount of elements
    private void AddElementsToPositionQueue(PositionInTime positionInTime)
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

    // Mehotd to calculate if a shot needs to be curved, and it what direction, or if the shot needs to be straight (and its direction too)
    private void CalculateBallDirectionAndShoot()
    {
        // We check if the ball throw needs to be curved
        if (this.CheckIfThrowIsCurved())
        {
            // If it the shot is curved, we first stablish the curve it will form
            this.SetCurveDirection();
            // And then we make the throw
            this.ShootStraightBall(false);
        }
        else
        {
            startPositionIndicator.transform.position = new Vector3(swipeStartPosition.x, swipeStartPosition.y, 0f);
            middlePositionIndicator.transform.position = new Vector3(0f, 0f, 0f);
            endPositionIndicator.transform.position = new Vector3(swipeEndPosition.x, swipeEndPosition.y, 0f);

            // Else, the throw is straight
            this.ShootStraightBall(true);
        }
    }

    // Method to know if a throw is curved
    private bool CheckIfThrowIsCurved()
    {
        // We see if the curve factor is greater than 20
        isBallThrowCurved = curveFactor >= 20;
        return isBallThrowCurved;
    }

    // Method to establish how the curve will be formed on a curved shot
    private void SetCurveDirection()
    {
        //We need three vectors to form the bezier curve. The starting point (which is where the ball was at the end of the user's touch)
        Vector2 curveStartVector = swipeEndPosition;
        // The end of the curve, which will be the same as the starting point but highter in the Y axis 
        Vector2 curveEndVector = new Vector2(swipeEndPosition.x, swipeEndPosition.y + (swipeEndPosition.y - swipeStartPosition.y) * 34f);
        // And the middle vector (wich defines the curve)
        Vector2 middleVector = Vector2.zero;

        // We decrement the curve factor so that the curve is not exagerated
        curveFactor /= 1.8f;

        // And we make sure the curve factor does not go above the maximum value allowed (to not make an exagerated curve, also)
        float curveFactorOffset = curveFactor <= maxCurveFactor ? curveFactor : maxCurveFactor;

        // If the shot direction is to the right, we curve the ball torwards the right too
        if (isShotDirectionToTheRight)
            middleVector = new Vector2(
                curveStartVector.x + (curveStartVector.x - swipeStartPosition.x) * (curveFactorOffset / 10f), 
                curveStartVector.y + (curveStartVector.y - swipeStartPosition.y) / 32.5f
            );
        // If the shot direction is to the left, we curve the ball torwards the left too
        else
            middleVector = new Vector2(
                curveStartVector.x - (swipeStartPosition.x - curveStartVector.x) * (curveFactorOffset / 10f), 
                curveStartVector.y + (curveStartVector.y - swipeStartPosition.y) / 32.5f
            );

        this.SetQuadraticCuvedBallData(
            curveStartVector,
            middleVector,
            curveEndVector
        );
    }

    // Method to do a shot based on the overallSwipeDirection
    private void ShootStraightBall(bool addXForce)
    {
        // We set that the ball is in movement and remove its kinematic property
        isBallInMovement = true;
        ballRigidBody.isKinematic = false;

        // We find the force that will be applied in the Z axis
        zForceApplied = (-overallSwipeDirection.y * throwForceZ / swipeIntervalTime);
        zForceApplied = zForceApplied > 0f ? zForceApplied : 0f;
        zForceApplied = zForceApplied > maxZForce ? maxZForce : zForceApplied;

        rectPositionIndicator.transform.position = swipeStartPosition;

        // We find the force that will be applied in the X axis only if needed
        float normalForceX = 0f;
        float xOffset = 0f;
        if (addXForce)
        {
            normalForceX = (-overallSwipeDirection.x * throwForceX);
            xOffset = (swipeStartPosition.x - Camera.main.pixelWidth / 2) * throwForceX * (zForceApplied / 770f);
        }
              
        // We find the force that will be applied in the Y axis
        float normalForceY = (-overallSwipeDirection.y * throwForceY);
        float yEndSwipePercentage = (swipeEndPosition.y / Camera.main.pixelHeight);
        float yOffset = yEndSwipePercentage * normalForceY;
        yOffset = yEndSwipePercentage > 0.5f ? yOffset *= 3f : yOffset *= 1.5f;

        // And we add the force to the ball
        this.AddForceToBall(normalForceX + xOffset, normalForceY + yOffset, zForceApplied);

        rightCurvePercentage.text = zForceApplied.ToString();

        // We register the time when the ball was shot
        movementStartTime = Time.time;
    }

    // Method that adds a specified force to the ball's rigid body
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
        curve = this.CalculateQuadraticBezierCurve(
            time,
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.startVector.x, curveData.startVector.y, ballSpawnPosition.z)),
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.middleVector.x, curveData.middleVector.y, ballSpawnPosition.z)),
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.endVector.x, curveData.endVector.y, ballSpawnPosition.z))
        );
        // We curve the ball in the X axis based on the time elapsed since the ball was shot'
        //Vector3 newPosition = gameObject.transform.position + new Vector3(curve.x - gameObject.transform.position.x, 0f, 0f);
        //gameObject.transform.position += new Vector3(curve.x - gameObject.transform.position.x, 0f, 0f);
        //this.AddForceToBall( -(curve.x - gameObject.transform.position.x)/Math.Abs(time - 0.5f), 0f, 0f);
        //gameObject.transform.position = Vector3.MoveTowards(previousPosition, newPosition, 0.1f);
        gameObject.transform.position += new Vector3((curve.x - gameObject.transform.position.x), 0f, 0f);
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

    //Method to reset the values of all variables
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
        curveFactor = 0;

        startVectorText.text = "";
        leftVectorText.text = "";
        rightVectorText.text = "";
        endVectorText.text = "";
        directionText.text = "";
        leftCurvePercentage.text = "";
        rightCurvePercentage.text = "";
    }
}
