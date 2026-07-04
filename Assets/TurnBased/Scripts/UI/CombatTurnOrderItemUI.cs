using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatTurnOrderItemUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image unitImage;
    [SerializeField] private Color playerColor;
    [SerializeField] private Color enemiesColor;
    [SerializeField] private TMP_Text unitName;

    public void Init(CombatUnit combatUnit)
    {
        var unitData = combatUnit.source;

        unitImage.gameObject.SetActive(true);

        bool hasSprite = unitData.sprite != null;
        if (hasSprite)
        {
            unitImage.overrideSprite = unitData.sprite;
            unitImage.color = Color.white;
            unitName.text = string.Empty;
        }
        else
        {
            unitImage.overrideSprite = null;
            unitImage.color = combatUnit.faction == UnitFactionEnum.Player ? playerColor : enemiesColor;
            unitName.text = unitData.name;
        }
    }

    public void SetAlpha(float alpha)
    {
        canvasGroup.alpha = alpha;
    }

    public void SetEmpty()
    {
        unitImage.gameObject.SetActive(false);
        unitName.text = ". . .";
    }

}
