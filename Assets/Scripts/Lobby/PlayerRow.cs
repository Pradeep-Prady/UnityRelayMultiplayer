using TMPro;
using UnityEngine;

public class PlayerRow : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text readyStatusText;

    private void Awake()
    {
        if (playerNameText == null || readyStatusText == null)
        {
            TMP_Text[] textFields = GetComponentsInChildren<TMP_Text>(true);

            foreach (TMP_Text textField in textFields)
            {
                if (playerNameText == null && textField.name.Contains("Name"))
                {
                    playerNameText = textField;
                }
                else if (readyStatusText == null && textField.name.Contains("Ready"))
                {
                    readyStatusText = textField;
                }
            }
        }
    }

    public void SetData(string playerName, bool isReady, bool isLocalPlayer)
    {
        if (playerNameText != null)
        {
            playerNameText.text = isLocalPlayer ? $"{playerName} (You)" : playerName;
        }

        if (readyStatusText != null)
        {
            readyStatusText.text = isReady ? "Ready" : "Not Ready";
            readyStatusText.color = isReady ? new Color(0.1f, 0.65f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
        }
    }
}
