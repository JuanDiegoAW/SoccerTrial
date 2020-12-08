using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RefereeScript : MonoBehaviour
{
    // Current time left that the player has to take a shot
    [SerializeField] private int countdownTime;
    // Starting number that will indicate how much time the player has to take a shot
    private int countdownStartTime;
    // Integer that will register the position of the current lives the player has
    private int currentCard;

    // Text that will display the amount of seconds left that a player has to kick the ball
    [SerializeField] private Text countdownDisplay;
    // TEMPORARY text to indicate a Goal occurred
    [SerializeField] private Text goalText;

    // Boolean that indicates if it is necessary to stop the timer countdown
    private bool stopCountdown = false;
    // Boolean to indicate if the countdown will be active or not
    [SerializeField] private bool isCountdownActive;

    // Images that represent the yellow cards or red cards (the lives) of the player
    [SerializeField] private RawImage[] cards;

    // Texture of the cards
    [SerializeField] private Texture cardTexture;

    // The GameObject that represents the soccer ball
    private GameObject soccerBall;
    // The Game Controller
    private GameObject gameController;

    // The animation used when a card spawns
    [SerializeField] private AnimationClip cardSpawnAnimation;

    // Start is called before the first frame update
    void Start()
    {
        soccerBall = GameObject.FindGameObjectWithTag(TagsEnum.GameObjectTags.Player.ToString());
        gameController = GameObject.FindGameObjectWithTag(TagsEnum.GameObjectTags.GameController.ToString());
        countdownDisplay.text = "";
        if (isCountdownActive)
        {
            countdownStartTime = countdownTime;
            countdownDisplay.text = countdownTime.ToString();
            StartCoroutine(LevelCountdown());
        }// Set the texture, size and animation of the cards
        this.InitializeCards(cardTexture);
        // Make yellow cards not visible at the beginning      
        this.DisableAllCards();
    }

    // Method to set the texture, animation and size of the cards
    public void InitializeCards(Texture texture)
    {
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].texture = texture;
            cards[i].GetComponent<RectTransform>().sizeDelta = new Vector2(110, 110);
            cards[i].GetComponent<Animation>().AddClip(cardSpawnAnimation, CardsAnimationEnums.AnimationName.SpawnCard.ToString());
        }
    }

    // Method to "make invisible" all the cards
    public void DisableAllCards()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].enabled = false;
        }
        currentCard = 0;
    }

    // Method to restart the countdown timer, and make it start ticking again
    public void RestartTimer()
    {
        if (isCountdownActive)
        {
            countdownTime = countdownStartTime;
            countdownDisplay.text = countdownTime.ToString();
            stopCountdown = false;
            StartCoroutine(LevelCountdown());
        }     
    }

    // Corutine that will make the timer tick down and respond accordingly
    private IEnumerator LevelCountdown()
    {
        // If there is still time left in the timer
        while (countdownTime > 0 && !stopCountdown)
        {
            yield return new WaitForSeconds(1f);
            countdownTime--;
            countdownDisplay.text = countdownTime.ToString();
        }
        // if the timer is at zero
        if (countdownTime == 0 && !stopCountdown)
        {
            this.ZeroCountAction();
        }
    }

    // Method to stop the countdown
    public void SetStopCountdown(bool isCountdownStopped)
    {
        this.stopCountdown = isCountdownStopped;
    }

    // Method that decides what happens when the timer 
    private void ZeroCountAction()
    {
        soccerBall.GetComponent<BallScript>().RespawnBall();
        this.SpawnCard(currentCard);
        currentCard++;
        // We substract life from the player, and if it still has a life left...
        if (soccerBall.GetComponent<BallScript>().SubstractLife(1) > 0)
        {
            if (isCountdownActive)
            {
                this.RestartTimer();
                soccerBall.GetComponent<BallScript>().CancelRespawnCorrutineIfActive();
            }
        }
        // If the player doesn't have any more lives left...
        else
        {
            //Game over
            gameController.GetComponent<GameManagerScript>().GameOver();
        }
    }

    public bool GetIsCountdownActive()
    {
        return isCountdownActive;
    }

    private void SpawnCard(int i)
    {
        cards[i].GetComponent<Animation>().Play(CardsAnimationEnums.AnimationName.SpawnCard.ToString(), PlayMode.StopAll);
        cards[currentCard].enabled = true;
    }
}
