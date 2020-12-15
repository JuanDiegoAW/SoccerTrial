using System;
using System.Collections;
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
    // Boolean that indicates if the first frame outside the reposition area has been accounted or not
    private bool isOutsideRepositionArea = false;
    // Boolean that indicates if the swipe movement started from the reposition area
    private bool touchStartedOnRepositionArea = false;
    // Boolean that indicates if the swipe movement of a shot is in process or not
    private bool isShotInProgress = false;

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
    // Time necessary to respawn the ball after it was shot
    [SerializeField] private float respawnTime;
    // Variable that serves as a threshold to know when a shot is curved in the x axis or not
    private float curveAllowedHorizontalPercentage = 10;
    // Variable that serves as a threshold to know when a shot is curved or not in the y axis
    private float curveAllowedVerticalPercentage = 5;
    // Variable that stablishes what percentage of the screen will be used to reposition the ball
    private float screenRepositionPercentage = 20f;
    // Variable that keeps track of how much force is applied in the Z axis when the ball is shot
    private float zForceApplied = 0f;

    // Starting Lives of the player
    [SerializeField] private int startingLives;
    // Frames used to decide if a user 
    [SerializeField] private int frameThreshold;
    private int frameCount = 0;
    // The actual number of lies of the player
    private int currentLives;

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
    [SerializeField] private Text leftCurveText;
    [SerializeField] private Text RightCurveText;
    [SerializeField] private Text endVectorText;
    [SerializeField] private Text directionText;

    [SerializeField] private Text leftCurvePercentage;
    [SerializeField] private Text rightCurvePercentage;

    [SerializeField] private Image startPositionIndicator;
    [SerializeField] private Image middlePositionIndicator;
    [SerializeField] private Image endPositionIndicator;

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
                Touch actualTouch = Input.GetTouch(0);
                // If the ball is within the reposition area
                if (actualTouch.position.y * 100 / Camera.main.pixelHeight < screenRepositionPercentage)
                    this.RepositionBallBasedOnPixels(actualTouch.position);
                else
                {
                    if (!isTouchPhaseEnded)
                    {
                        frameCount++;
                        startVectorText.text = frameCount.ToString();
                    }


                    if (!isOutsideRepositionArea && touchStartedOnRepositionArea)
                        this.ShotBegan(actualTouch);
                    else if (!isTouchPhaseEnded)
                    {
                        if (isShotInProgress)
                            this.ShotInProgress(actualTouch);
                        if (actualTouch.phase == TouchPhase.Ended)
                        {
                            frameCount = 0;
                            this.ShotEnded(actualTouch);
                        }                           
                    }
                }
            }
            // If the ball is in movement and the throw is curved
            else if (isBallInMovement && isBallThrowCurved)
            {
                float timeElapsed = (Time.time - movementStartTime) / swipeIntervalTime * 0.4f;
                // If the time elapsed since the ball was shot is still in range, we apply the curved effect to the ball
                if (timeElapsed <= 0.9f)
                    this.CurveQuadraticBall(timeElapsed, curveData);
                // If enough time has passed to fully curve the ball, we see if it is necessary to apply the last force in the X axis to it
                else if (!isLastForceApplied)
                {
                    isLastForceApplied = true;
                    if (zForceApplied > 700f)
                        this.AddForceToBall(-(curveData.middleVector - swipeEndPosition).x / 35, 0f, 0.8f);
                    else if (zForceApplied > 500f)
                        this.AddForceToBall(-(curveData.middleVector - swipeEndPosition).x / 50, 0f, 0.8f);
                    else if (zForceApplied > 300f)
                        this.AddForceToBall(-(curveData.middleVector - swipeEndPosition).x / 65, 0f, 0.8f);
                    else if (zForceApplied > 100f)
                        this.AddForceToBall(-(curveData.middleVector - swipeEndPosition).x / 80, 0f, 0.8f);
                    else
                        this.AddForceToBall(-(curveData.middleVector - swipeEndPosition).x / 125, 0f, 0.8f);
                }
            }
        }
    }

    // Method that defines what happens when a user is starting to touch the screen
    private void ShotBegan(Touch actualTouch)
    {
        isOutsideRepositionArea = true;
        isTouchPhaseEnded = false;
        isShotInProgress = true;
        this.RepositionBallBasedOnPixels(actualTouch.position);
        this.GetTouchStartData(actualTouch);
    }

    // Method that defines what happens when a user is ending to touch the screen
    private void ShotEnded(Touch actualTouch)
    {
        touchStartedOnRepositionArea = false;
        isTouchPhaseEnded = true;
        float yDistancePercentage = Math.Abs(actualTouch.position.y - swipeStartPosition.y) * 100 / Camera.main.pixelHeight;
        if (isOutsideRepositionArea
            && actualTouch.position.y > swipeStartPosition.y
            && yDistancePercentage > 8)
        {
            this.CompareActualTouchToHighestCurves(actualTouch);
            this.GetTouchEndData(actualTouch);
            this.CalculateBallDirectionAndShoot();
            this.StartCoroutine(AwaitToSpawnBall());
        }
        isOutsideRepositionArea = false;
        isShotInProgress = false;
    }

    // Method that defines what happens when a user is in the middle of touching the screen
    private void ShotInProgress(Touch actualTouch)
    {
        this.CompareActualTouchToHighestCurves(actualTouch);
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

    // Method to reposition the ball in the X Axis based on a pixel position
    private void RepositionBallBasedOnPixels(Vector2 touch)
    {
        touchStartedOnRepositionArea = true;
        Vector2 newPosition = Camera.main.ScreenToWorldPoint(new Vector3(touch.x, touch.y, gameObject.transform.localPosition.z));
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

    // Method to compare the current position of the user's touch and see if it surpasses the previously registerd highest point of the swipe curve.
    private void CompareActualTouchToHighestCurves(Touch touch)
    {
        if (touch.position.x > swipeCurveRight.x)
            swipeCurveRight = touch.position;
        else if (touch.position.x < swipeCurveLeft.x)
            swipeCurveLeft = touch.position;
    }

    // Method to save the initial data when the user starts the swipe movement
    private void GetTouchStartData(Touch touch)
    {
        swipeStartTime = Time.time;
        swipeStartPosition = touch.position;
        swipeCurveRight = touch.position;
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
        if (this.CheckIfShotIsCurved(out bool isShotCurvedRight))
        {
            if (isShotCurvedRight)
            {
                startPositionIndicator.transform.position = new Vector3(swipeStartPosition.x, swipeStartPosition.y, 0f);
                middlePositionIndicator.transform.position = new Vector3(swipeCurveRight.x, swipeCurveRight.y, 0f);
                endPositionIndicator.transform.position = new Vector3(swipeEndPosition.x, swipeEndPosition.y, 0f);

                swipeCurveRight.x += (swipeCurveRight.x - swipeStartPosition.x) * 2f;
                this.ShootCurvedBall(swipeCurveRight);
            }
            else
            {
                startPositionIndicator.transform.position = new Vector3(swipeStartPosition.x, swipeStartPosition.y, 0f);
                middlePositionIndicator.transform.position = new Vector3(swipeCurveLeft.x, swipeCurveLeft.y, 0f);
                endPositionIndicator.transform.position = new Vector3(swipeEndPosition.x, swipeEndPosition.y, 0f);

                swipeCurveLeft.x -= (swipeStartPosition.x - swipeCurveLeft.x) * 2f;
                this.ShootCurvedBall(swipeCurveLeft);
            }
            rightCurvePercentage.text = $"Z force: {(-overallSwipeDirection.y * throwForceZ / swipeIntervalTime)}";
        }
        else
        {
            startPositionIndicator.transform.position = new Vector3(swipeStartPosition.x, swipeStartPosition.y, 0f);
            middlePositionIndicator.transform.position = new Vector3(0f, 0f, 0f);
            endPositionIndicator.transform.position = new Vector3(swipeEndPosition.x, swipeEndPosition.y, 0f);

            rightCurvePercentage.text = $"Z force: {(-overallSwipeDirection.y * throwForceZ / swipeIntervalTime)}";
            this.ShootStraightBall();
        }
    }

    // Method that evaluates if the shot has the necessary conditions to count as curved
    private bool CheckIfShotIsCurved(out bool isShotCurvedRight)
    {
        // If the curve is to the right
        if (Math.Abs(swipeStartPosition.x - swipeCurveLeft.x) <= Math.Abs(swipeCurveRight.x - swipeStartPosition.x))
        {
            isShotCurvedRight = true;

            float curveHorizontalEndPercentage = (swipeCurveRight.x - swipeEndPosition.x) * 100f / Camera.main.pixelWidth;
            float curveHorizontalStartPercentage = (swipeCurveRight.x - swipeStartPosition.x) * 100f / Camera.main.pixelWidth;
            float curveVerticalEndPercentage = (swipeEndPosition.y - swipeCurveRight.y) * 100f / Camera.main.pixelHeight;
            float curveVerticalStartPercentage = (swipeCurveRight.y - swipeStartPosition.y) * 100f / Camera.main.pixelHeight;

            if ((curveHorizontalEndPercentage > curveAllowedHorizontalPercentage && curveHorizontalStartPercentage > curveAllowedHorizontalPercentage)
                && (curveVerticalEndPercentage > curveAllowedVerticalPercentage && curveVerticalStartPercentage > curveAllowedVerticalPercentage))
            {
                return true;
            }
        }
        else
        {
            isShotCurvedRight = false;

            float curveHorizontalEndPercentage = (swipeEndPosition.x - swipeCurveLeft.x) * 100f / Camera.main.pixelWidth;
            float curveHorizontalStartPercentage = (swipeStartPosition.x - swipeCurveLeft.x) * 100f / Camera.main.pixelWidth;
            float curveVerticalEndPercentage = (swipeEndPosition.y - swipeCurveLeft.y) * 100f / Camera.main.pixelHeight;
            float curveVerticalStartPercentage = (swipeCurveLeft.y - swipeStartPosition.y) * 100f / Camera.main.pixelHeight;

            if ((curveHorizontalEndPercentage > curveAllowedHorizontalPercentage && curveHorizontalStartPercentage > curveAllowedHorizontalPercentage)
                && (curveVerticalEndPercentage > curveAllowedVerticalPercentage && curveVerticalStartPercentage > curveAllowedVerticalPercentage))
            {
                return true;
            }
        }

        isShotCurvedRight = default;
        return false;
    }

    // Method to do a straight shoot based on the overallSwipeDirection
    private void ShootStraightBall()
    {
        // We set that the ball is in movement and remove its kinematic property
        isBallInMovement = true;
        ballRigidBody.isKinematic = false;

        float xOffset = (swipeStartPosition.x - Camera.main.pixelWidth / 2) * throwForceX;
        float normalForceX = (-overallSwipeDirection.x * throwForceX);
        zForceApplied = (-overallSwipeDirection.y * throwForceZ / swipeIntervalTime);

        this.AddForceToBall(xOffset + normalForceX, (-overallSwipeDirection.y * throwForceY), zForceApplied);

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

        zForceApplied = (-overallSwipeDirection.y * throwForceZ / swipeIntervalTime);

        this.AddForceToBall(0f, (-overallSwipeDirection.y * throwForceY), zForceApplied);

        // We register the time when the ball was shot
        movementStartTime = Time.time;
        // We set the curve data
        this.SetQuadraticCuvedBallData(swipeStartPosition, curveVector, swipeEndPosition);
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
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.startVector.x, curveData.startVector.y, 1)),
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.middleVector.x, curveData.middleVector.y, 1)),
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.endVector.x, curveData.endVector.y, 1))
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

    // Method to find the distance between two points
    private double findDistanceBetweenTwoPoints(Vector2 startingPonint, Vector2 endingPoint)
    {
        //d=√((x2-x1)²+(y2-y1)²)
        //d=     a    +   b
        double a = startingPonint.x - endingPoint.x;
        a = a * a;
        double b = startingPonint.y - endingPoint.y;
        b = b * b;
        double d = Math.Sqrt(a + b);

        return d;
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
        isBallInMovement = false;
        isLastForceApplied = false;
        isBallThrowCurved = false;
        ballRigidBody.velocity = Vector3.zero;
        ballRigidBody.angularVelocity = Vector3.zero;
        ballRigidBody.AddForce(0f, 0f, 0f);
        ballRigidBody.isKinematic = true;
        gameObject.transform.localPosition = ballSpawnPosition;
        trailRenderer.Clear();
        if (isGoalScored)
        {
            referee.GetComponent<RefereeScript>().DisableAllCards();
            referee.GetComponent<RefereeScript>().RestartTimer();
        }
        isGoalScored = false;
    }
}
