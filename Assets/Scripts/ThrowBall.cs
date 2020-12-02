﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ThrowBall : MonoBehaviour
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

    // Variable that will contain the data of the curved shot
    private QuadraticCurveData curveData;

    // Variables that will store time variables. swipeStartTime = when the swipe started. swipeIntervalTime = how much time does the swipe lasted
    // movementStartTime = at what time does the ball started moving
    private float swipeStartTime, swipeIntervalTime, movementStartTime;
    // Variable that will indicate how strong the shot is in the X and Y axis
    private float throwForceXandY = 0.3f;
    // Variable that indicates how strong the shot is in the Z axis
    private float throwForceZ = 42f;

    // Start is called before the first frame update
    void Start()
    {
        ballSpawnPosition = gameObject.transform.position;
        ballRigidBody = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        // If the user is touching the screen and if the ball is NOT in movement
        if (Input.touchCount > 0 && !isBallInMovement)
        {
            Touch actualTouch = Input.GetTouch(0);
            // We see if the user is starting to touch the screen
            if (actualTouch.phase == TouchPhase.Began)
            {
                this.GetTouchStartData(actualTouch);
            }
            // Else, we see if the user is ending to touch the screen
            else if (actualTouch.phase == TouchPhase.Ended)
            {
                isBallInMovement = true;
                this.CompareActualTouchToHighestCurves(actualTouch);
                this.GetTouchEndData(actualTouch);
                this.CalculateBallDirectionAndShoot();               
                this.StartCoroutine(spawnBall());
            }
            // Else, the user is in the middle of the touch
            else
            {
                this.CompareActualTouchToHighestCurves(actualTouch);
            }
        }
        // If the ball is in movement 
        else if (isBallInMovement)
        {
            // If the ball trajectory still needs to be curved
            if (isBallThrowCurved)
            {
                float timeElapsed = (Time.time - movementStartTime)/ swipeIntervalTime;
                // If the time elapsed since the ball was shot is still in range, we apply the curved effect to the ball
                if (timeElapsed <= 0.9f)
                {
                    this.CurveQuadraticBall(timeElapsed, curveData);
                }
                // If enough time has passed to fully curve the ball, we see if it is necessary to apply the last force in the X axis to it
                else if (!isLastForceApplied)
                {
                    isLastForceApplied = true;
                    ballRigidBody.AddForce(-(curveData.middleVector - swipeEndPosition).x / 10, 0 ,0);
                }
            }
        }
    }

    // Method to compare the current position of the user's touch and see if it surpasses the previously registerd highest point of the swipe curve.
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
        swipeIntervalTime = Time.time - this.swipeStartTime;
        swipeEndPosition = touch.position;
        overallSwipeDirection = swipeStartPosition - swipeEndPosition;
    }

    // Mehotd to calculate if a shot needs to be curved, and it what direction, or if the shot needs to be straight (and its direction too)
    private void CalculateBallDirectionAndShoot()
    {
        // We set that the ball is in movement and remove its kinematic property
        isBallInMovement = true;     
        ballRigidBody.isKinematic = false;

        // If the ball went to the right
        if (Math.Abs(swipeStartPosition.x - swipeCurveLeft.x) <= Math.Abs(swipeCurveRight.x - swipeStartPosition.x))
        {
            // We see if it was a straight shot
            if (swipeCurveRight.y >= swipeEndPosition.y)
            {
                this.ShootStraightBall();
            }
            // Or if the shot needs to be curved
            else
            {
                swipeCurveRight.x += (swipeCurveRight.x - swipeStartPosition.x) / 1.5f;
                ballRigidBody.AddForce(0f, -overallSwipeDirection.y * throwForceXandY, throwForceZ / swipeIntervalTime);
                this.SetQuadraticCuvedBallData(swipeStartPosition, swipeCurveRight, swipeEndPosition);
            }
        }
        // Else, the ball went to the left
        else
        {
            // We see if it was a straight shot
            if (swipeCurveLeft.y >= swipeEndPosition.y)
            {
                this.ShootStraightBall();
            }
            // Or if the shot needs to be curved
            else
            {
                swipeCurveLeft.x -= (swipeStartPosition.x - swipeCurveLeft.x) / 1.5f;
                ballRigidBody.AddForce(0f, -overallSwipeDirection.y * throwForceXandY, throwForceZ / swipeIntervalTime);
                this.SetQuadraticCuvedBallData(swipeStartPosition, swipeCurveLeft, swipeEndPosition);
            }
        }
        // We register the time when the ball was shot
        movementStartTime = Time.time;
    }

    // Method to do a straight shot based on the overallSwipeDirection
    private void ShootStraightBall()
    {
        ballRigidBody.AddForce(-overallSwipeDirection.x * throwForceXandY, -overallSwipeDirection.y * throwForceXandY, throwForceZ / swipeIntervalTime);
    }

    // Method to set specific vectors to shoot a curved ball
    private void SetQuadraticCuvedBallData(Vector2 startingVector, Vector2 mVector, Vector2 endingVector)
    {
        isBallThrowCurved = true;
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

    // Method to await x amount of secons and spawn the ball without any velocity of force in it
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
