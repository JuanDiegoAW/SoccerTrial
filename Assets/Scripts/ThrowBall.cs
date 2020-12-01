using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ThrowBall : MonoBehaviour
{
    private struct QuadraticCurveData
    {
        public Vector2 startVector;
        public Vector2 middleVector;
        public Vector2 endVector;
    }

    private Vector3 ballSpawnPosition;

    private Vector2 swipeStartPosition, swipeEndPosition, overallSwipeDirection;
    private Vector2 swipeCurveRight, swipeCurveLeft;

    private Rigidbody ballRigidBody;

    private bool isBallInMovement = false;
    private bool isBallThrowCurved = false;
    private bool isLastForceApplied = false;

    private QuadraticCurveData curveData;

    private float swipeStartTime, swipeIntervalTime, movementStartTime;
    private float throwForceXandY = 0.3f;
    private float throwForceZ = 42f;

    public Text startSwipeText;
    public Text endSwipeText;
    public Text leftSwipeText;
    public Text rightSwipeText;
    public Text directionText;
    public Text timeText;

    // Start is called before the first frame update
    void Start()
    {
        ballSpawnPosition = gameObject.transform.position;
        ballRigidBody = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.touchCount > 0 && !isBallInMovement)
        {
            Touch actualTouch = Input.GetTouch(0);
            if (actualTouch.phase == TouchPhase.Began)
            {
                this.GetTouchStartData(actualTouch);
            }
            else if (actualTouch.phase == TouchPhase.Ended)
            {
                isBallInMovement = true;
                this.CompareActualTouchToHighestCurves(actualTouch);
                this.GetTouchEndData(actualTouch);
                this.CalculateBallShotDirection();               
                this.StartCoroutine(spawnBall());
            }
            else
            {
                this.CompareActualTouchToHighestCurves(actualTouch);
            }
        }
        else if (isBallInMovement && isBallThrowCurved)
        {
            float timeElapsed = (Time.time - movementStartTime)/ swipeIntervalTime;
            timeText.text = timeElapsed.ToString();
            if (timeElapsed <= 0.9f)
            {
                this.CurveQuadraticBall(timeElapsed, curveData);
            }
            else if (!isLastForceApplied)
            {
                isLastForceApplied = true;
                ballRigidBody.AddForce(-(curveData.middleVector - swipeEndPosition).x / 10, 0 ,0);
            }
        }
    }

    private void CompareActualTouchToHighestCurves(Touch touch)
    {
        if (touch.position.x > swipeCurveRight.x)
        {
            swipeCurveRight = touch.position;
        }
        else if (touch.position.x < swipeCurveLeft.x)
        {
            swipeCurveLeft = touch.position;
        }
    }

    private void GetTouchStartData(Touch touch)
    {
        swipeStartTime = Time.time;
        swipeStartPosition = touch.position;
        swipeCurveRight = touch.position;
        swipeCurveLeft = touch.position;

        startSwipeText.text = $"S: x = {touch.position.x} y = {touch.position.y}";
    }

    private void GetTouchEndData(Touch touch)
    {
        swipeIntervalTime = Time.time - this.swipeStartTime;
        swipeEndPosition = touch.position;
        overallSwipeDirection = swipeStartPosition - swipeEndPosition;
        endSwipeText.text = $"E: x = {touch.position.x} y = {touch.position.y}";
    }

    private void CalculateBallShotDirection()
    {
        isBallInMovement = true;     
        ballRigidBody.isKinematic = false;

        leftSwipeText.text = $"L: x = {swipeCurveLeft.x} y = {swipeCurveLeft.y}";
        rightSwipeText.text = $"R: x = {swipeCurveRight.x} y = {swipeCurveRight.y}";

        if (Math.Abs(swipeStartPosition.x - swipeCurveLeft.x) <= Math.Abs(swipeCurveRight.x - swipeStartPosition.x))
        {
            if (swipeCurveRight.y >= swipeEndPosition.y)
            {
                this.ShootStraightBall();
                directionText.text = "Straight. Right";
            }
            else
            {
                swipeCurveRight.x += (swipeCurveRight.x - swipeStartPosition.x) / 1.5f;
                this.ShootAndSetQuadraticData(swipeStartPosition, swipeCurveRight, swipeEndPosition);
                directionText.text = "Curved. Right";
            }
        }
        else
        {
            if (swipeCurveLeft.y >= swipeEndPosition.y)
            {
                this.ShootStraightBall();
                directionText.text = "Straight. Left";
            }
            else
            {
                directionText.text = "Curved. Left";
                swipeCurveLeft.x -= (swipeStartPosition.x - swipeCurveLeft.x) / 1.5f;
                this.ShootAndSetQuadraticData(swipeStartPosition, swipeCurveLeft, swipeEndPosition);
            }
        }
        movementStartTime = Time.time;
    }

    private void ShootStraightBall()
    {
        ballRigidBody.AddForce(-overallSwipeDirection.x * throwForceXandY, -overallSwipeDirection.y * throwForceXandY, throwForceZ / swipeIntervalTime);
    }

    private void ShootAndSetQuadraticData(Vector2 startingVector, Vector2 mVector, Vector2 endingVector)
    {
        isBallThrowCurved = true;
        ballRigidBody.AddForce(0f, -overallSwipeDirection.y * throwForceXandY, throwForceZ / swipeIntervalTime);
        curveData = new QuadraticCurveData()
        {
            startVector = startingVector,
            middleVector = mVector,
            endVector = endingVector
        };
    }

    private void CurveQuadraticBall(float time, QuadraticCurveData curveData)
    {
        Vector3 curve = this.CalculateCubicBezierCurve(
            time,
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.startVector.x, curveData.startVector.y, 1)),
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.middleVector.x, curveData.middleVector.y, 1)),
            Camera.main.ScreenToWorldPoint(new Vector3(curveData.endVector.x, curveData.endVector.y, 1))
        );
        gameObject.transform.position += new Vector3(curve.x - gameObject.transform.position.x, 0f, 0f);
    }

    private Vector3 CalculateCubicBezierCurve(float time, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        // B(t) = (1-t)^2 * P0 + 2(1-t)tP1 + t^2 * P2 , 0 < t < 1
        //              a      +    b      +     c 
        float timeMinusOne = 1 - time;
        Vector3 a = timeMinusOne * timeMinusOne * p0;
        Vector3 b = 2 * timeMinusOne * time * p1;
        Vector3 c = time * time * p2;

        return a + b + c;
    }

    private IEnumerator spawnBall()
    {
        yield return new WaitForSeconds(7);
        isBallInMovement = false;
        isLastForceApplied = false;
        isBallThrowCurved = false;
        ballRigidBody.velocity = Vector3.zero;
        ballRigidBody.angularVelocity = Vector3.zero;
        ballRigidBody.AddForce(0f, 0f, 0f);
        ballRigidBody.isKinematic = true;       
        gameObject.transform.position = ballSpawnPosition;
    }
}
