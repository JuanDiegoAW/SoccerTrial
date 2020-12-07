using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GoalScript : MonoBehaviour
{
    // TEMPORARY text to indicate a Goal occurred
    [SerializeField] private Text goalText;

    // The GameObject that represents the soccer ball
    private GameObject soccerBall;
    // The GameObject thet represents the referee on the field
    private GameObject referee;

    // Start is called before the first frame update
    void Start()
    {
        goalText.text = "";
        soccerBall = GameObject.FindGameObjectWithTag(TagsEnum.GameObjectTags.Player.ToString());
        referee = GameObject.FindGameObjectWithTag(TagsEnum.GameObjectTags.Referee.ToString());
    }

    // Method that will be triggered when an object enters the goal area
    private void OnTriggerEnter(Collider collider)
    {
        // If the object entering was the ball of the Player
        if (collider.gameObject.tag == TagsEnum.GameObjectTags.Player.ToString())
        {
            soccerBall.GetComponent<BallScript>().SetIsGoalScored(true);
            referee.GetComponent<RefereeScript>().SetStopCountdown(true);
            goalText.text = "GOL";
            //stopCountdown = true;           
        }
    }

    // Method that will be triggered once an object leaves the goal area
    private void OnTriggerExit(Collider collider)
    {
        if (collider.gameObject.tag == TagsEnum.GameObjectTags.Player.ToString())
        {
            goalText.text = "";
        }
    }
}
