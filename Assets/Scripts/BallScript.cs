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
        public Vector2 vectorPosition;  // The position of the user's touch 
        public float positionTime;      // The exact time in which the touch was over that specific position
    }

    // The initial position where the ball spawns (relative to the camera)
    private Vector3 ballSpawnPosition;

    // Vector that indicates the starting position of the swipe that will shoot the ball
    private Vector2 swipeStartPosition;
    // Vector that indicates the endind position of the swipe that will shoot the ball
    private Vector2 swipeEndPosition;
    // Vector resultant of substracting the swipeStartPosition and the swipeEndPosition to let us know the overall swipe direction
    private Vector2 overallSwipeDirection;
    // Vectors that will save in real time what is the maximum curve position, either to the right or to the left
    private Vector2 swipeCurveRight, swipeCurveLeft;
    // Vector that keeps track of the sum of a user's gestures to know if a circle was made or not
    Vector2 gestureSum;

    // RigidBody of the ball prefab
    private Rigidbody ballRigidBody;

    // Boolean that indicates if the ball is in movement or not
    private bool isBallInMovement;
    // Boolean that indicates if the throw will curve the ball, or if the shot is straight
    private bool isBallThrowCurved;
    // Boolean that will be used only on curved shots. It indicates if the very last force is applied to the ball after giving it the curved effect
    private bool isLastForceApplied;
    // Boolean that defines if a goal was scored
    private bool isGoalScored;
    // Boolean that keeps track if a corrutine is currently active or not
    private bool isRespawnCorrutineActive;
    // Boolean that indicates if the ball respawn has been canceled or not
    private bool isRespawnCancelled;
    // Boolean that indicates the user has already finished touching the screen
    private bool isTouchPhaseEnded;
    // Boolean that indicates if user started is swipe interactoin over the ball
    private bool isTouchOriginatedFromBallPosition;
    // Boolean that indicates if the shot is directed to the right. If not, it is directed to the left
    private bool isShotDirectionToTheRight;
    // Boolean that indicates if the curved effect of the ball is towards the right. If not, it is towards the left
    private bool isCurveEffectToTheRight;
    // Boolean that indicates if the ball needs to be repositioned towards the respawn area
    private bool returnBallToRespawnPosition;
    // Boolean that indicates if the ball needs to be repositioned towards above the goal
    private bool returnBallToRespawnPositionInTwoSteps;

    // Variable that will contain the data of the curved shot
    private QuadraticCurveData curveData;

    // Variables that will store time variables. swipeStartTime = When the swipe started // swipeIntervalTime = How much time does the swipe lasted
    // movementStartTime = At what time does the ball started moving 
    private float swipeStartTime, swipeIntervalTime, movementStartTime;
    // Time necessary to respawn the ball after it was shot
    [SerializeField] private float respawnTime;
    // Variable that keeps track of how much force is applied in the Z axis when the ball is shot
    private float zForceApplied;
    // Variable used to verify if a circle was registered or not
    private float gestureLength;
    // The number of circles registered as part of the user's input
    private float circlesRegistered;
    // Value that indicates how much force in the X axis must be applied at the end of a curved shot
    private float endForceToApply;

    private float lastRegisteredCircleTime;

    private float elapsedTime;
    // Variable that divides the last force applied in a curved shot. The greater the value, the less force will be applied
    private float lastCurveForceDividend;

    // Starting Lives of the player
    [SerializeField] private int startingLives;
    // Frames used to capture a user's input, that will later be analized to determine if the user made a circle gesture or not
    [SerializeField] private int circleGestureThreshold;
    // Frams used to capture a user's input, that will later determine the direction of the user's shot
    [SerializeField] private int throwGestureThreshold;
    // The actual number of lies of the player
    private int currentLives;
    // Counter that will be used to determine if a curved shot effect is towards the right
    private int rightCurveCount;
    // Counter that will be used to determine if a curved shot effect is towards the left
    private int leftCurveCount;

    // Queue that registers the inputs from the user for a specified amount of frames. It will determine if a circle gesture was made. 
    private Queue<PositionInTime> circleGestureQueue = new Queue<PositionInTime>();
    // Queue that registers the inputs from the user for a specified amount of frames. It will determine the direction of a shot. 
    private Queue<PositionInTime> throwGestureQueue = new Queue<PositionInTime>();

    // GameObject with the behaviour of the referee
    private GameObject referee;
    // GameObject with the behaviour of the goal
    private GameObject goal;

    // Trail renderer of the gameobject
    private TrailRenderer trailRenderer;

    // Constat that helps make a curved effect last longer. The bigger the value, the less the curved effect will last
    private const float CURVE_TIME_MODIFIER = 0.3f;
    // A bezier curve's time can (idealy) be only from 0 to 1. This constant specifies the delimitation for said curve
    private const float BEZIER_CURVE_TIME_LIMIT = 1.3f;
    // Const that serves as a dividend for the count of circles drawn. The bigger the value, the less curved the effect will be per circle drawn
    private const float CIRCLE_COUNT_DIVIDEND = 13f;
    // Constant that will indicate how strong the shot is in the X axis. The greater the value, the strongest the shot
    private const float THROW_FORCE_X = 0.38f;
    // Constant that will indicate how strong the shot is in the Y axis. The greater the value, the strongest the shot
    private const float THROW_FORCE_Y = 0.05f;
    // Constant that indicates how strong the shot is in the Z axis. The greater the value, the strongest the shot
    private const float THROW_FORCE_Z = 0.125f;
    // Constant that spreads the ball's middle curve effect in the Z axis. The greater the value, the longer the middle of the curve will be spread in the Z axis
    private const float MIDDLE_CURVE_Z_FACTOR = 65725.55f;
    // Constant that spreads the ball's ending curve effect in the Z axis. The greater the value, the longer the end of the curve will be spread in the Z axis
    private const float END_CURVE_Z_FACTOR = 113091.33f;
    // Time in seconds since the last circle registered from user's input to decrement the amount of circles counted
    private const float CIRCLE_DERECEMENT_TIME = 1f;
    // Constant that determines how long does it take to reposition the ball towards the respawn position. The greater the value, the longer it takes.
    private const float REPOSITION_SPEED = 10f;

    // Maximum force in the Z axis allows on a shot
    private const int MAX_Z_FORCE_ALLOWED = 700;
    // Maximum factor added to a curved shot
    private const int MAX_CURVE_FACTOR = 5;
    // Threshold that specifies how many circles are needed to be registered from the user's input to make a curved throw
    private const int No_CIRLES_FOR_CURVED_THROW = 2;
    // Constant that establishes how much offset is applied in the X axis when doing a curved throw based on the ball's position.
    private const int X_OFFSET_CURVE_MULTIPLER = 2850; //The greater the value, the greater the offset.

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
        // We save the ball's spawn position
        ballSpawnPosition = gameObject.transform.localPosition;
        ballRigidBody = GetComponent<Rigidbody>();
        trailRenderer = gameObject.GetComponent<TrailRenderer>();
        referee = GameObject.FindGameObjectWithTag(TagsEnum.GameObjectTags.Referee.ToString());
        goal = GameObject.FindGameObjectWithTag(TagsEnum.GameObjectTags.Goal.ToString());
        currentLives = startingLives;

        // We initialize variables
        rightCurveCount = 0;
        leftCurveCount = 0;
        gestureLength = 0;
        endForceToApply = 0;
        circlesRegistered = 0;
        zForceApplied = 0;
        elapsedTime = 0;
        gestureSum = Vector2.zero;

        // We initialize booleans
        isBallInMovement = false;
        isBallThrowCurved = false;
        isLastForceApplied = false;
        isGoalScored = false;
        isRespawnCorrutineActive = false;
        isRespawnCancelled = false;
        isTouchPhaseEnded = false;
        isTouchOriginatedFromBallPosition = false;
        isShotDirectionToTheRight = false;
        isCurveEffectToTheRight = false;
        returnBallToRespawnPosition = false;
        returnBallToRespawnPositionInTwoSteps = false;
    }

    // Update is called once per frame
    void Update()
    {
        // The player will only be allowed to perform an action if it is alive
        if (this.IsPlayerAlive())
        {
            // If the user is touching the screen and if the ball is NOT in movement
            if (Input.touchCount > 0 && !isBallInMovement)
            {
                // If the user hasn't stopped touching the screen
                if (!isTouchPhaseEnded)
                {
                    // We save the current touch
                    Touch actualTouch = Input.GetTouch(0);
                    // If the user is starting to touch the screen
                    if (actualTouch.phase.Equals(TouchPhase.Began))
                    {
                        // We need to verify the touch is over the ball
                        if (this.IsTouchOverBall(actualTouch))
                            isTouchOriginatedFromBallPosition = true;
                    }
                    // Else, if the user is currently making a touch gesture that originated over the ball's position
                    if (isTouchOriginatedFromBallPosition)
                    {
                        this.HandleUserGesture(actualTouch);
                    }
                }
            }
            // If the ball is in movement 
            else if (isBallInMovement)
            {
                //If the the throw is curved, we need to apply the curve effect
                if (isBallInMovement && isBallThrowCurved)
                    this.GraduallyApplyCurvedEffectToShot();

                // If the user touches the screen mid-throw, we need to verify the ball is not returning to the respawn position 
                if (Input.touchCount > 0 && !returnBallToRespawnPosition && !returnBallToRespawnPositionInTwoSteps)
                {
                    // If the user double taps the screen, we return the ball to the respawn position
                    if (this.VerifyIfUserDoubleTaps(Input.GetTouch(0)))
                    {
                        if (gameObject.transform.position.z > goal.transform.position.z)
                            returnBallToRespawnPositionInTwoSteps = true;
                        else
                            returnBallToRespawnPosition = true;
                    }
                }

                if (returnBallToRespawnPosition)
                    this.TransitionBallToRespawnPosition();
                else if (returnBallToRespawnPositionInTwoSteps)
                    this.TransitionBallToAboveGoal();
            }          
        }
    }

    // Method that gradually applies the curved effect to a shot based on the elapsed time since the ball was "kicked"
    private void GraduallyApplyCurvedEffectToShot()
    {
        float timeElapsed = (Time.time - movementStartTime) / swipeIntervalTime * CURVE_TIME_MODIFIER;
        // If the time elapsed since the ball was shot is still in range, we apply the curved effect to the ball
        if (timeElapsed <= BEZIER_CURVE_TIME_LIMIT)
            this.CurveQuadraticBall(timeElapsed, curveData);

        // If enough time has passed to fully curve the ball, we see if it is necessary to apply the last force in the X axis to it
        else if (!isLastForceApplied)
        {
            directionText.text = "FINISHED  z: " + (-endForceToApply / lastCurveForceDividend);
            // We let the code know the last force will be applied
            isLastForceApplied = true;

            //// And we add the last force to the ball, so it's curved trajectory doesn't end abruptly
            if (isShotDirectionToTheRight)
                this.AddForceToBall(-endForceToApply / lastCurveForceDividend, 0f, 0.2f);
            else
                this.AddForceToBall(-endForceToApply / lastCurveForceDividend, 0f, 0.2f);
        }
    }

    // Method that handles a user's touch that interacts with the ball
    private void HandleUserGesture(Touch actualTouch)
    {
        // We reposition the ball to were the user is touching
        this.RepositionBallBasedOnTouch(actualTouch);
        // If the user is ending the touch gesture
        if (actualTouch.phase == TouchPhase.Ended)
        {
            this.SaveFinalTouchFrameData(actualTouch);
            this.ShotEnded();
        }
        // Else, if the user is in the middle of the touch gesture
        else
        {
            // We save the current position of the touch and the actual time in which it occurs
            PositionInTime latestPositionInTime = new PositionInTime()
            {
                vectorPosition = actualTouch.position,
                positionTime = Time.time
            };
            // We store the current position of the touch, and it's registered time
            this.AddElementsToPositionInTimeQueue(latestPositionInTime, circleGestureQueue, circleGestureThreshold);
            this.AddElementsToPositionInTimeQueue(latestPositionInTime, throwGestureQueue, throwGestureThreshold);
            // And we verify if a circle gesture was registered
            this.isCircleRegistered();
        }
        
        if (Time.time - lastRegisteredCircleTime >= CIRCLE_DERECEMENT_TIME && circlesRegistered > 0)
        {
            circlesRegistered--;
            //leftVectorText.text = "Decrement: " + circlesRegistered;
            lastRegisteredCircleTime = Time.time;
        }
        else if (circlesRegistered == 0)
        {
            rightCurveCount = 0;
            leftCurveCount = 0;
        }
    }

    // Method to verify if a user double taps
    private bool VerifyIfUserDoubleTaps(Touch touch)
    {
        return touch.tapCount >= 2;
    }

    // Method to transition from the current ball's position, to the respawn position
    private void TransitionBallToRespawnPosition()
    {
        this.StopAllCoroutines();
        elapsedTime += Time.deltaTime;
        transform.position = Vector3.Lerp(
            gameObject.transform.position,
            Camera.main.transform.TransformPoint(ballSpawnPosition),
            elapsedTime / 2
        );
        if (Vector3.Distance(gameObject.transform.localPosition, ballSpawnPosition) < 0.93f)
        {
            this.RespawnBall();
        }
    }

    // Method to transition from the current ball's position, to the position above the goal
    private void TransitionBallToAboveGoal()
    {
        this.StopAllCoroutines();
        elapsedTime += Time.deltaTime;
        Vector3 aboveGoalPosition = Camera.main.transform.TransformPoint(goal.transform.position) +
            new Vector3(-Camera.main.transform.position.x, 3f, 3f);

        transform.position = Vector3.Lerp(
            gameObject.transform.position,
            aboveGoalPosition,
            elapsedTime
        );
        if (Vector3.Distance(gameObject.transform.position, aboveGoalPosition) < 0.9f)
        {
            returnBallToRespawnPosition = true;
            returnBallToRespawnPositionInTwoSteps = false;
        }
    }

    // Method that evaluates the last inputs from the user to verify if a circle gesture was made or not
    private void isCircleRegistered()
    {
        if (circleGestureQueue.Count > 5)
        { 
            PositionInTime[] touchPositions = new PositionInTime[circleGestureQueue.Count];
            circleGestureQueue.CopyTo(touchPositions,0);

            gestureSum = Vector2.zero;
            gestureLength = 0;
            Vector2 prevDelta = Vector2.zero;
            float lowestYPoint = Camera.main.pixelHeight;
            int lowestYPointPosition = 0;

            for (int i = 0; i < touchPositions.Length - 2; i++)
            {
                if (touchPositions[i].vectorPosition.y < lowestYPoint)
                {
                    lowestYPoint = touchPositions[i].vectorPosition.y;
                    lowestYPointPosition = i;
                }

                Vector2 delta = touchPositions[i + 1].vectorPosition - touchPositions[i].vectorPosition;
                float deltaLength = delta.magnitude;
                gestureSum += delta;
                gestureLength += deltaLength;
                if (Vector2.Dot(delta, prevDelta) < 0f)
                    circleGestureQueue.Clear();
                
                prevDelta = delta;
                //rightVectorText.text = "" + gestureSum;
            }
            int gestureBase = (Screen.width + Screen.height) / 4;

            // If a circle was made, we augment the curve factor variable
            if (gestureLength > gestureBase && gestureSum.magnitude < gestureBase / 2)
            {
                if (lowestYPointPosition > 0 && 
                    touchPositions[lowestYPointPosition].vectorPosition.x >= touchPositions[lowestYPointPosition - 1].vectorPosition.x)
                    leftCurveCount++;
                else
                    rightCurveCount++;

                circlesRegistered++;
                lastRegisteredCircleTime = Time.time;
                //leftVectorText.text = "" + circlesRegistered;
                circleGestureQueue.Clear();
            }
        }
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
        PositionInTime firstSavedPosition = throwGestureQueue.Dequeue();
        swipeStartPosition = firstSavedPosition.vectorPosition;

        // We get the interval of time that transcurred between the first swipe position and the last swipe position
        swipeIntervalTime = (Time.time - firstSavedPosition.positionTime);

        // We get the OverallSwipeDirection (the difference between the first swipe position and the last swipe position). This vector will determine the direction of the shot
        overallSwipeDirection = firstSavedPosition.vectorPosition - swipeEndPosition;

        // And we verify if the swipe direction is towards the right or not
        isShotDirectionToTheRight = swipeEndPosition.x > swipeStartPosition.x;
    }

    // Method that defines if a user's touch is over the ball or not
    private bool IsTouchOverBall(Touch touch)
    {
        Ray touchRay = Camera.main.ScreenPointToRay(touch.position);
        if (Physics.Raycast(touchRay, out RaycastHit objectCollided))
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

    // Method to add elements to a PositionInTime Queue, verifying it does not surpass the maximum amount of elements
    private void AddElementsToPositionInTimeQueue(PositionInTime positionInTime, Queue<PositionInTime> queue, int frameThreshold)
    {
        if (queue.Count == frameThreshold)
        {
            queue.Dequeue();        
        }
        else if (queue.Count > frameThreshold)
        {
            for (int i = 0; i <= (queue.Count - frameThreshold); i++)
            {
                queue.Dequeue();
            }
        }
        queue.Enqueue(positionInTime);
    }

    // Mehotd to calculate if a shot needs to be curved, and it what direction, or if the shot needs to be straight (and its direction too)
    private void CalculateBallDirectionAndShoot()
    {
        // We check if the ball throw needs to be curved
        if (this.CheckIfThrowIsCurved())
        {
            // And then we make the throw
            this.ShootStraightBall(false);
            // If it the shot is curved, we first stablish the curve it will form
            this.SetCurveDirection();
            

            startPositionIndicator.transform.position = new Vector3(swipeStartPosition.x, swipeStartPosition.y, 0f);
            middlePositionIndicator.transform.position = new Vector3(0f, 0f, 0f);
            endPositionIndicator.transform.position = new Vector3(swipeEndPosition.x, swipeEndPosition.y, 0f);
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
        if (leftCurveCount > 0 || rightCurveCount > 0)
        { 
            // We see if the user has made at least 2 circles
            if (leftCurveCount >= rightCurveCount)
            {
                startVectorText.text = "Comba hacia la izquierda";
                isCurveEffectToTheRight = false;
            }
            else
            {
                startVectorText.text = "Comba hacia la derecha";
                isCurveEffectToTheRight = true;
            }
        }
        isBallThrowCurved = circlesRegistered >= No_CIRLES_FOR_CURVED_THROW;
        return isBallThrowCurved;
    }

    // Method to establish how the curve will be formed on a curved shot
    private void SetCurveDirection()
    {
        if (zForceApplied > 25f)
        {
            //We need three vectors to form the bezier curve. The starting point (which is where the ball was at the end of the user's touch)
            Vector2 curveStartVector = swipeEndPosition;
            // The end of the curve, which will be the same as the starting point but highter in the Y axis 
            Vector2 curveEndVector = Vector2.zero;
            // And the middle vector (wich defines the curve)
            Vector2 curveMiddleVector = Vector2.zero;

            // And we make sure the curve factor does not go above the maximum value allowed (to not make an exagerated curve)
            circlesRegistered = circlesRegistered <= MAX_CURVE_FACTOR ? circlesRegistered : MAX_CURVE_FACTOR;
            float curveFactorOffset = circlesRegistered / CIRCLE_COUNT_DIVIDEND;
            float xAxisDifference = Math.Abs(swipeStartPosition.x - curveStartVector.x);
            float distanceBetweenSwipeAndCameraCenter = (swipeEndPosition.x - Camera.main.pixelWidth / 2) / Camera.main.pixelWidth;

            float xOffset = distanceBetweenSwipeAndCameraCenter * X_OFFSET_CURVE_MULTIPLER;
            lastCurveForceDividend = Math.Abs(distanceBetweenSwipeAndCameraCenter) <= 0.35f ? 5f : 0.3f;
            float curveEndVectorFactor = 0.08f;
            float curveMiddleVectorFactor = 8;
            bool isMiddleValueLimited = true;

            // If the shot direction is to the right, we set the middle of the curve torwards the right
            if (isShotDirectionToTheRight)
            {
                if (isCurveEffectToTheRight)
                {
                    curveEndVectorFactor = 20/2;
                    isMiddleValueLimited = false;
                    curveMiddleVectorFactor /= 2;
                    lastCurveForceDividend *= -1.25f;
                }
                float xMiddleValue = curveStartVector.x + (xOffset + (xAxisDifference * curveMiddleVectorFactor * curveFactorOffset));
                curveMiddleVector = new Vector2(
                    xMiddleValue < curveStartVector.x && isMiddleValueLimited ? curveStartVector.x : xMiddleValue,
                    curveStartVector.y + ((curveStartVector.y - swipeStartPosition.y) * MIDDLE_CURVE_Z_FACTOR)
                );
            }
            // If the shot direction is to the left, we set the middle of the curve torwards the left
            else
            {
                if (!isCurveEffectToTheRight)
                {
                    curveEndVectorFactor = 20 / 2;
                    isMiddleValueLimited = false;
                    curveMiddleVectorFactor /= 2;
                    lastCurveForceDividend *= -1.25f;
                }
                float xMiddleValue = curveStartVector.x + (xOffset - (xAxisDifference * curveMiddleVectorFactor * curveFactorOffset));
                curveMiddleVector = new Vector2(
                    xMiddleValue > curveStartVector.x && isMiddleValueLimited ? curveStartVector.x : xMiddleValue,
                    curveStartVector.y + ((curveStartVector.y - swipeStartPosition.y) * MIDDLE_CURVE_Z_FACTOR)
                );
            }

            if (isCurveEffectToTheRight)
            {
                curveEndVector = new Vector2(
                    curveMiddleVector.x + (xAxisDifference * curveEndVectorFactor * curveFactorOffset /*+ Math.Abs(xOffset)*/),
                    curveStartVector.y + ((curveStartVector.y - swipeStartPosition.y) * END_CURVE_Z_FACTOR)
                );
            }
            else
            {
                curveEndVector = new Vector2(
                    curveMiddleVector.x - (xAxisDifference * curveEndVectorFactor * curveFactorOffset /*- Math.Abs(xOffset)*/),
                    curveStartVector.y + ((curveStartVector.y - swipeStartPosition.y) * END_CURVE_Z_FACTOR)
                );
            }
            startVectorText.text = "X ax diff: " + xAxisDifference;
            leftCurvePercentage.text = "xOffset: " + xOffset;
            rightCurvePercentage.text = "curveFactor offset: " + curveFactorOffset;

            leftVectorText.text = "curveStart: " + curveStartVector.ToString();
            rightVectorText.text = "curveMiddle: " + curveMiddleVector.ToString();
            endVectorText.text = "curveEnd: " + curveEndVector.ToString();
            

            endForceToApply = curveEndVector.x - curveMiddleVector.x;

            //directionText.text = "last force: " + endForceToApply / LAST_CURVE_FORCE_DIVIDEND;

            this.SetQuadraticCuvedBallData(
                curveStartVector,
                curveMiddleVector,
                curveEndVector
            );
        }
        else
            isBallThrowCurved = false;
    }

    // Method to do a shot based on the overallSwipeDirection
    private void ShootStraightBall(bool addXForce)
    {
        // We set that the ball is in movement and remove its kinematic property
        isBallInMovement = true;
        ballRigidBody.isKinematic = false;

        // We find the force that will be applied in the Z axis
        zForceApplied = (-overallSwipeDirection.y * THROW_FORCE_Z / swipeIntervalTime);
        zForceApplied = zForceApplied > 0f ? zForceApplied : 0f;
        zForceApplied = zForceApplied > MAX_Z_FORCE_ALLOWED ? MAX_Z_FORCE_ALLOWED : zForceApplied;

        rectPositionIndicator.transform.position = swipeStartPosition;

        // We find the force that will be applied in the X axis only if needed
        float normalForceX = 0f;
        float xOffset = 0f;
        if (addXForce)
        {
            normalForceX = (-overallSwipeDirection.x * THROW_FORCE_X);
            xOffset = (swipeStartPosition.x - Camera.main.pixelWidth / 2) * (zForceApplied * 0.25f / 765f);
        }
              
        // We find the force that will be applied in the Y axis
        float normalForceY = (-overallSwipeDirection.y * THROW_FORCE_Y);
        float yEndSwipePercentage = (swipeEndPosition.y / Camera.main.pixelHeight);
        float yOffset = yEndSwipePercentage * normalForceY;

        if (yOffset > 75)
        {
            yOffset *= 80f;
        }
        else if (yOffset > 50)
        {
            yOffset *= 6f;
        }
        else if (yOffset > 25)
        {
            yOffset *= 1.35f;
        }
        //yOffset = yEndSwipePercentage > 0.6f ? yOffset *= 6f : yOffset *= 1.25f;

        

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
        Vector3 curve = this.CalculateQuadraticBezierCurve(
            time,
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.startVector.x, curveData.startVector.y, ballSpawnPosition.z)),
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.middleVector.x, curveData.middleVector.y, ballSpawnPosition.z)),
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.endVector.x, curveData.endVector.y, ballSpawnPosition.z))
        );
        //// We curve the ball in the X axis based on the time elapsed since the ball was shot
        //this.AddForceToBall(-(curve.x - gameObject.transform.position.x) / 1.5f, 0f,0f);
        gameObject.transform.position += new Vector3((curve.x - gameObject.transform.position.x), 0f, 0f);
        //ballRigidBody.velocity = Vector3.right * curve.x;
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
        StopAllCoroutines();
        this.ResartValues();
        if (isGoalScored)
        {
            referee.GetComponent<RefereeScript>().DisableAllCards();
            referee.GetComponent<RefereeScript>().RestartTimer();
        }       
    }

    //Method to reset the values of all variables
    private void ResartValues()
    {
        
        circleGestureQueue.Clear();
        ballRigidBody.velocity = Vector3.zero;
        ballRigidBody.angularVelocity = Vector3.zero;
        ballRigidBody.AddForce(0f, 0f, 0f);
        ballRigidBody.isKinematic = true;
        isGoalScored = false;
        gameObject.transform.localPosition = ballSpawnPosition;

        rightCurveCount = 0;
        leftCurveCount = 0;
        gestureLength = 0;
        endForceToApply = 0;
        circlesRegistered = 0;
        zForceApplied = 0;
        elapsedTime = 0;
        gestureSum = Vector2.zero;

        isBallInMovement = false;
        isBallThrowCurved = false;
        isLastForceApplied = false;
        isGoalScored = false;
        isRespawnCorrutineActive = false;
        isRespawnCancelled = false;
        isTouchPhaseEnded = false;
        isTouchOriginatedFromBallPosition = false;
        isShotDirectionToTheRight = false;
        isCurveEffectToTheRight = false;
        returnBallToRespawnPosition = false;
        returnBallToRespawnPositionInTwoSteps = false;

        startVectorText.text = "";
        leftVectorText.text = "";
        rightVectorText.text = "";
        endVectorText.text = "";
        directionText.text = "";
        leftCurvePercentage.text = "";
        rightCurvePercentage.text = "";

        trailRenderer.Clear();
    }
}
