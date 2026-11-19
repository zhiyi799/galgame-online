using UnityEngine;
using UnityEngine.UI;

public class CharacterProfileCloseButton : MonoBehaviour
{
    public GameObject profileRoot;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(Close);
    }

    public void Close()
    {
        if (profileRoot != null)
            profileRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(Close);
    }
}
