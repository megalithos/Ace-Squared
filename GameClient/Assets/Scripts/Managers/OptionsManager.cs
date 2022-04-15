using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using Newtonsoft.Json;
using System.IO;
using System;

public class OptionsManager : MonoBehaviour
{
	public static OptionsManager instance;
	delegate void myDelegate(bool boo);

	// this list holds data about parents in all sub panels (graphics setting or keybinds setting menu for examplpe)
	// so when we instantiate the options they get instantiated into the correct panels
	[SerializeField] GameObject inputPanel;
	[SerializeField] GameObject optionItemPrefab;

	enum OptionMenu
	{
		MISC,
		GRAPHICS,
		INPUT,
		COUNT,
	}

	public enum OptionType
	{
		SLIDER,
		BOOLEAN,
		INPUT,
	}

	class Button
	{
		OptionType type;
		Text nameText;
		GameObject instantiatedObject;
		public string name;
		public float value;

		static List<Button> buttons = new List<Button>();
		public Button(OptionMenu panel, OptionType _type, string _name, float min = 0, float max = 199, myDelegate del = null, bool wholeNumbers = false)
		{
			type = _type;
			name = _name;

			// instantiate the object
			instantiatedObject = Instantiate(instance.optionItemPrefab, instance.associatedOptionPanels[panel].transform);
			OptionsItem item = instantiatedObject.GetComponent<OptionsItem>();
            item.outputText.text = Config.settings[_name].ToString();

			// set the text to correct one
			float defaultValue = Config.settings[_name];
			switch (_type)
			{
				case OptionType.SLIDER:
					Slider refSlider = item.slider;
					refSlider.onValueChanged.AddListener((float amount) => {
						Config.settings[_name] = amount;
                        item.outputText.text = Mathf.Round(amount).ToString();
                        if (name == "chunk_view_distance" && World.instance.mapLoaded && World.instance.player != null)
                            World.instance.ResetViewDistanceChunks();
					});
                    refSlider.wholeNumbers = wholeNumbers;
					refSlider.minValue = min;
					refSlider.maxValue = max;
					refSlider.value = defaultValue;
					item.optionNameText.SetText(_name);
                    
					break;
				case OptionType.BOOLEAN:
					Toggle toggle = instantiatedObject.GetComponentInChildren<Toggle>();
					toggle.isOn = System.Convert.ToBoolean(Config.settings[_name]);
					toggle.onValueChanged.AddListener((bool bol)=> { Config.settings[_name] = (float)System.Convert.ToDouble(bol); if (del != null) del(bol); });
					item.optionNameText.SetText(_name);
					item.outputText.SetText("");
					break;
			}
			item.UpdateThis(_type);
			buttons.Add(this);
		}
		
		public void ChangeValue(float amount)
		{
			value = amount;
		}

		public static List<Button> GetListOfAllButtons()
		{
			return buttons;
		}
	}

	// associate certain option menus with specific options
	Dictionary<OptionMenu, GameObject> associatedOptionPanels;

	// initialize options button data
	void Awake()
	{
		if (instance == null)
			instance = this;
		else
			Destroy(this);

		associatedOptionPanels = new Dictionary<OptionMenu, GameObject> {
			{OptionMenu.INPUT, inputPanel},
		};


		new Button(OptionMenu.INPUT, OptionType.SLIDER, "sensitivity", 1, 5000);
		new Button(OptionMenu.INPUT, OptionType.SLIDER, "ads_sensitivity", 100, 1000);
		new Button(OptionMenu.INPUT, OptionType.SLIDER, "scoped_sensitivity", 1, 1000);
		new Button(OptionMenu.INPUT, OptionType.BOOLEAN, "fullscreen_enabled", 0, 0, ((bool bol)=> { Screen.fullScreen = bol; }));
		new Button(OptionMenu.INPUT, OptionType.BOOLEAN, "fps_text_enabled", 0, 0, ((bool bol)=>
		{
			if (HUDManager.instance.fpsTextObj != null)
				HUDManager.instance.fpsTextObj.SetActive(bol);
		}
		));
		new Button(OptionMenu.INPUT, OptionType.BOOLEAN, "debug_info_enabled", 0, 0, ((bool bol)=>
		{
			if (HUDManager.instance.fpsTextObj != null)
				HUDManager.instance.debugInfoHolder.SetActive(bol);
		}
		));
        new Button(OptionMenu.INPUT, OptionType.SLIDER, "max_decals", 0, 30, null, true);
        new Button(OptionMenu.INPUT, OptionType.SLIDER, "chunk_view_distance", 5, 100, null 
            
            , true);
       Debug.Log(JsonConvert.SerializeObject(Config.settings));

	}

	// for back button
	public void SaveUserSettings()
	{
		Config.SaveUserSettings();
	}
}
