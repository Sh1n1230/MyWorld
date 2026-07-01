using UnityEngine;
using UnityEngine.InputSystem;

public class MenuController : MonoBehaviour
{
    public GameObject menuPanel;

    private InputCharacter inputCharacter;
    private InputAction menuAction;

    void Awake()
    {
        inputCharacter = new InputCharacter();
    }

    void OnEnable()
    {
        menuAction = inputCharacter.UI.Menu;
        menuAction.Enable();
        menuAction.performed += OnMenuPerformed;
    }

    void OnDisable()
    {
        menuAction.performed -= OnMenuPerformed;
        menuAction.Disable();
    }

    void OnDestroy()
    {
        inputCharacter.Dispose();
    }

    private void OnMenuPerformed(InputAction.CallbackContext context)
    {
        ToggleMenu();
    }

    public void ToggleMenu()
    {
        menuPanel.SetActive(!menuPanel.activeSelf);
    }

    public void CloseMenu()
    {
        menuPanel.SetActive(false);
    }

    public void OnSettingClicked()
    {
        // Settingメニューの中身は今後実装
    }
}
