using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

public class OptionsItem : MonoBehaviour
{
    public Text optionNameText;
    public Text outputText;
    public Slider slider;
    public delegate void ChangeValue(float amount);
    public event ChangeValue ChangeValueEvent;
    public delegate void ChangeToggleState();
    public event ChangeToggleState ChangeToggleStateEvent;
    public Toggle toggleObject;

    Dictionary<OptionsManager.OptionType, GameObject> dict;

    /// <summary>
    /// If the type is slider, then we enable only slider and disable all others.
    /// </summary>
    /// <param name="type">Type of this spawned options item.</param>
    public void UpdateThis(OptionsManager.OptionType type)
    {
        dict = new Dictionary<OptionsManager.OptionType, GameObject>
        {
        { OptionsManager.OptionType.SLIDER, slider.gameObject},
        { OptionsManager.OptionType.BOOLEAN, toggleObject.gameObject },
        };
        foreach (KeyValuePair<OptionsManager.OptionType, GameObject> kvp in dict)
        {
            // if the type is correct, then just ignore that and disable all others
            if (kvp.Key == type)
            {
                kvp.Value.SetActive(true);
                continue;
            }

            kvp.Value.SetActive(false);
        }
    }
}
