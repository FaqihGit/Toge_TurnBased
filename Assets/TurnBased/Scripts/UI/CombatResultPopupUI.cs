using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatResultPopupUI : MonoBehaviour
{
    public Action OnConfirm;

    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text promptText;

    public void Init()
    {
        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(HandleOnConfirmButtonClicked);

        ShowPopup(false);
    }

    public void ShowPopup(bool isShow)
    {
        gameObject.SetActive(isShow);
    }

    public void SetPrompt(string prompt)
    {
        promptText.text = prompt;
    }

    private void HandleOnConfirmButtonClicked()
    {
        OnConfirm?.Invoke();
    }
}
