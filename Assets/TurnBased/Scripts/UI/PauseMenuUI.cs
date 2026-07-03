using System;
using UnityEngine;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    public Action OnResumeButtonClicked;

    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitButton;

    public void Init()
    {
        resumeButton.onClick.RemoveAllListeners();
        resumeButton.onClick.AddListener(HandleOnResumeButtonClicked);

        quitButton.onClick.RemoveAllListeners();
        quitButton.onClick.AddListener(HandleOnQuitButtonClicked);

        ShowMenu(false);
    }

    public void ShowMenu(bool isShow)
    {
        gameObject.SetActive(isShow);
    }

    private void HandleOnResumeButtonClicked()
    {
        OnResumeButtonClicked?.Invoke();
    }

    private void HandleOnQuitButtonClicked()
    {
        Application.Quit();
        Debug.LogError("APP QUITING");
    }
}
