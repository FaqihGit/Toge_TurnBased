using Unity.VisualScripting;
using UnityEngine;

public class WorldCanvasManager : MonoBehaviour
{
    [SerializeField] private RectTransform interactablePrompt;

    private Camera mainCam;
    private Transform targetInteractable;

    void LateUpdate()
    {
        PositionPrompt();
    }

    public void Init(Camera camera)
    {
        mainCam = camera;
        ShowInteractablePrompt(false);
    }

    public void ShowInteractablePrompt(bool isShow, Transform target = null)
    {
        // Debug.Log($"ShowInteractablePrompt {isShow} target {target}");
        if (isShow && target)
        {
            targetInteractable = target;
            interactablePrompt.gameObject.SetActive(true);
        }
        else
        {
            targetInteractable = null;
            interactablePrompt.gameObject.SetActive(false);
        }
    }

    private void PositionPrompt()
    {
        if (interactablePrompt == null || targetInteractable == null || !targetInteractable.gameObject.activeSelf) return;

        Vector3 worldPos = targetInteractable.position;
        Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);
        interactablePrompt.position = screenPos; // match whatever space conversion CombatUnitUI.Bind uses (Overlay vs Camera-space canvas)
    }
}
