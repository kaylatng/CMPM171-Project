using UnityEngine;
using TMPro;

public class CardVisual : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    public int CardID;

    public void Initialize(int id) {
        CardID = id;
        if (id >= 60) {
            nameText.text = "Modifier";
            GetComponent<SpriteRenderer>().color = Color.yellow;
        } else {
            nameText.text = "Spell " + id;
            GetComponent<SpriteRenderer>().color = Color.white;
        }
    }
}
