using UnityEngine;
using UnityEngine.UI;

public class CharacterProfileTabButton : MonoBehaviour
{
    public CharacterProfileController controller;

    public CharacterProfileTab tab;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(OpenTab);
    }

    private void OpenTab()
    {
        if (controller != null)
            controller.OpenTab(tab);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OpenTab);
    }
}
