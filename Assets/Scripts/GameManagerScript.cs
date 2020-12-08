using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class GameManagerScript : MonoBehaviour
{
    private GameObject deathMenu;

    void Start()
    {
        deathMenu = GameObject.FindGameObjectWithTag(TagsEnum.GameObjectTags.DeathCanvas.ToString());
        if (deathMenu.activeSelf)
        {
            deathMenu.SetActive(false);
        }
    }

    public void GameOver()
    {
        //SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        deathMenu.SetActive(true);
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
