using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChatManager : MonoBehaviour
{
    public static ChatManager instance;
    int maxMessages = 25;
    public GameObject chatPanel, textObject;
    public TMPro.TMP_InputField inputField;
    List<ChatMessage> messageList = new List<ChatMessage>();
    float messageSendCooldown = 1;
    bool canSendMessage = true;

    private delegate void CommandMethod(List<string> args);
    static List<Command> commands = new List<Command>();
    static readonly List<string> adminCommandsThatWeNeedToSendToServer = new List<string> { "test", "kick", "stop", "ban", "god", "vanish", "ping" };
    [SerializeField] private HUDManager hudManager;

    [SerializeField] private List<GameObject> listOfChatObjectsThatWeNeedToToggle = new List<GameObject>();
    public void SetChatBoxActive(bool myBool)
    {
        foreach (GameObject _obj in listOfChatObjectsThatWeNeedToToggle)
        {
            _obj.SetActive(myBool);
        }
        if (GameManager.localPlayer != null)
            GameManager.localPlayer.menuOpen = myBool;

        ToggleAllChatItems(myBool);
    }

    private void ToggleAllChatItems(bool boolean)
    {
        foreach (ChatMessage msg in messageList)
            msg.textObject.SetActive(boolean);
    }

    public bool GetChatBoxActive()
    {
        return listOfChatObjectsThatWeNeedToToggle[1].gameObject.activeSelf;
    }

    /// <summary>
    /// Clears the chat
    /// </summary>
    public void Clear()
    {
        foreach (ChatMessage message in messageList)
        {
            Destroy(message.textObject);
        }
        for (int i = 0; i < messageList.Count; i++)
        {
            Destroy(messageList[i].textObject);
            messageList.RemoveAt(1);
        }
    }

    class Command
    {
        public string stringToInvokeCommand;
        public readonly CommandMethod method;
        public readonly string usageTip;
        public readonly int parameterCount;
        public readonly bool adminOnlyCmd;
        public readonly bool pipeToServer;

        // pipeToServer will handle if we want to pipe the cmd to server for further handling
        // useful for /ban /kick /god, ...
        public Command(string _stringToInvokeCommand, CommandMethod _method, int _parametercount, string _usageTip = "", bool _adminOnlyCmd = false, bool _pipeToServer = false)
        {
            stringToInvokeCommand = _stringToInvokeCommand;
            method = _method;
            commands.Add(this);
            usageTip = _usageTip;
            parameterCount = _parametercount;
            adminOnlyCmd = _adminOnlyCmd;
            pipeToServer = _pipeToServer;
        }
    }

    /// <summary>
    /// The character user needs to start their chat message with in order to issue commands to the server.
    /// </summary>
    char commandChar = '/';

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else Destroy(this);
    }

    private void Start()
    {
        GameManager.instance.OnPlayerChatReceived_ += OnChatMessageReceivedFromServer;
        GameManager.instance.OnServerMessageReceivedEvent += OnServerMessage;
        Command command = new Command("team", SwitchTeam, 1, "/team [id], for example: /team 1");
        Command command1 = new Command("color", SwitchSelectedBlockColor, 3, "/color [r] [g] [b], (all must be in the 0-255 range) for example /color 120 200 122");
        new Command("fly", Fly, 0, "/fly to fly and noclip", true);
        new Command("god", null, 0, "/god to enable godmode", true);
        new Command("vanish", null, 0, "/vanish to vanish completely from other people's sight", true);
        new Command("kick", null, 1, "/...", true);
        new Command("ban", null, 1, "/...", true);
        new Command("ping", null, 0, "/ping", true);
        SetChatBoxActive(false);
    }

    // if chat is closed and enter is pressed -> open chat
    // if chat is open and enter is pressed -> if inputField is empty close chat, else send the msg and close chat
    private void Update()
    {
        if (!GetChatBoxActive() && Input.GetKeyDown(KeyCode.Return)) // if chat is closed
        {
            Debug.Log("Opening chat interface");
            Cursor.lockState = CursorLockMode.None;
            SetChatBoxActive(true);
            StartCoroutine(WaitForInputActivation());
        }
        else if (GetChatBoxActive() && Input.GetKeyDown(KeyCode.Return))
        {
            inputField.ReleaseSelection();
            Cursor.lockState = CursorLockMode.Locked;
            if (inputField.text != "" && canSendMessage)
            {
                // code here
                canSendMessage = false;
                StartCoroutine(ResetSendCooldown());

                // parsing here, if it is a normal message, send it with clientsend
                // for now, just treat every message as a normal chat message
                string messageToSend = inputField.text;

                if (messageToSend.StartsWith(commandChar.ToString()))
                {
                    // first remove the first character from the string
                    messageToSend = messageToSend.Remove(0, 1);

                    // split the message by whitespace
                    string[] args = messageToSend.Split(null);
                    List<string> argsList = new List<string>();

                    for (int i = 0; i < args.Length; i++)
                    {
                        if (!System.String.IsNullOrWhiteSpace(args[i]))
                            argsList.Add(args[i]); // do not add empty
                    }

                    // for the input, check if there is a corresponding keyValuePair in keywordMethod
                    bool foundCommand = false;
                    foreach (Command item in commands)
                    {
                        // check if we have the priviledges for the command
                        if (item.adminOnlyCmd && !Client.instance.isAdmin)
                            continue;

                        if (item.stringToInvokeCommand == argsList[0].ToLower())
                        {
                            foundCommand = true;
                            try
                            {
                                if (argsList.Count - 1 == item.parameterCount)
                                {
                                    if (item.adminOnlyCmd && Client.instance.isAdmin || !item.adminOnlyCmd)
                                        item.method?.Invoke(argsList);

                                    if (adminCommandsThatWeNeedToSendToServer.Contains(item.stringToInvokeCommand))
                                    {
                                        Debug.Log("piping to server this cmd");
                                        ClientSend.CommandWithArgs(messageToSend); // send raw
                                    }
                                }
                                else
                                    SendChatMessage("Error.		" + item.usageTip);
                            }
                            catch (Exception e)
                            {
                                SendChatMessage("Error.		" + item.usageTip);
                                Debug.Log(e.Message);
                            }
                        }

                    }

                    // if we didn't find any commands with the correct string, print the invalid command to the chat
                    if (!foundCommand)
                    {
                        Debug.Log("command does not exist.");
                        SendChatMessage("Error, invalid command.");
                    }
                }
                else
                {
                    // encode the string to byte array
                    byte[] bytesToSend = Encoder.EncodeStringToBytes(messageToSend);

                    // send to server
                    ClientSend.PlayerChatMessage(bytesToSend);
                }
            }
            inputField.text = ""; SetChatBoxActive(false);
        }
    }

    // there is something wrong when activating it on the same frame
    // so better wait for one frame before activating the input field
    // since it works
    public IEnumerator WaitForInputActivation()
    {
        yield return 0;
        inputField.ActivateInputField();
        yield break;
    }


    void SwitchTeam(List<string> args)
    {
        int teamIDToChangeTo = System.Convert.ToInt32(args[1]);
        Debug.Log("switching team to..." + teamIDToChangeTo);

        ClientSend.TeamChangeRequest(teamIDToChangeTo);
    }

    /* args[1]:r
	 * args[2]:g
	 * args[3]:b
	 * args[4]:a
	 * */
    void SwitchSelectedBlockColor(List<string> args)
    {
        // we need to put the args in int32, and send to server
        byte[] bytes = new byte[4];

        bytes[0] = Convert.ToByte(args[3]);
        bytes[1] = Convert.ToByte(args[2]);
        bytes[2] = Convert.ToByte(args[1]);

        int color = BitConverter.ToInt32(bytes, 0);

        ClientSend.PlayerSelectedBlockColorChange(color);
    }

    IEnumerator ResetSendCooldown()
    {
        yield return new WaitForSeconds(messageSendCooldown);
        canSendMessage = true;
    }

    // format the string properly and then pass to SendChatMessage method
    void OnChatMessageReceivedFromServer(string senderUsername, string message)
    {
        string formatString = "<{0}>: {1}";

        SendChatMessage(string.Format(formatString, senderUsername, message));
    }

    /// <summary>
    /// Get the appropriate string to send from the serverMessages dictionary and call SendChatMessage() to
    /// output the message to the user.
    /// </summary>
    /// <param name="messageID"></param>
    /// <param name="message"></param>
    void OnServerMessage(string message)
    {
        SendChatMessage(message);
    }

    public void SendChatMessage(string text)
    {
        if (!chatPanel.activeSelf)
            return;

        if (messageList.Count >= maxMessages)
        {
            Destroy(messageList[0].textObject);
            messageList.RemoveAt(0);
        }

        GameObject instantiatedText = Instantiate(textObject, chatPanel.transform);
        ChatMessage newMessage = new ChatMessage(text, instantiatedText);

        newMessage.textObject.GetComponent<TMPro.TextMeshProUGUI>().text = newMessage.text;
        messageList.Add(newMessage);
    }

    void Fly(List<string> args)
    {
        if (GameManager.localPlayer.debugMovement)
        {
            GameManager.localPlayer.DisableDebugMovement();
        }
        else
            GameManager.localPlayer.EnableDebugMovement();
    }

    void PipeCmdToServer(List<string> args)
    {

    }
}

[System.Serializable]
public class ChatMessage
{
    public string text;
    public GameObject textObject;

    public ChatMessage(string _text, GameObject _textObject)
    {
        text = _text;
        textObject = _textObject;
    }
}