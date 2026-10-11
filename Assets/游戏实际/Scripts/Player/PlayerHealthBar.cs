using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBar : MonoBehaviour
{
    public Image healthBarImage;

    private PlayerCharacterControler _playerCharacterController;

    private void Start()
    {
        _playerCharacterController=FindObjectOfType<PlayerCharacterControler>();
    }

    private void Update()
    {
        if (_playerCharacterController)
        {
            healthBarImage.fillAmount = _playerCharacterController.CurrentHealth / _playerCharacterController.maxHealth;
        }
    }
}
