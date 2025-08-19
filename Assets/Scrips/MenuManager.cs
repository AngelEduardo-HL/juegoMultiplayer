using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Networking;

public struct NameData
{
    public string[] names;
}

public class MenuManager : MonoBehaviour
{
    public RectTransform mainMenu;
    public RectTransform HUD;
    public TextMeshProUGUI labelHealth;
    public TMP_Text TemplatePlayerLabel;

    public TMP_Dropdown dropdownNames;
    public List<string> allowedNames = new List<string>();

    public string API_URL = "http://monsterballgo.com/api/"; // Replace with your actual API URL
    public string ENDPOINT_NAMES = "names";

    public int selectedSombrero;

    //Devuelve el nombre seleccionado por el jugador
    public int selectedNameIndex
    {
        get
        {
            return dropdownNames.value;
        }
    }

    void Start()
    {

        TemplatePlayerLabel.gameObject.SetActive(false);
        HUD.gameObject.SetActive(false);
        mainMenu.gameObject.SetActive(true);

        GetNames();

    }

    public void OnButtonCreate()
    {
        //oculta el menu
        mainMenu.gameObject.SetActive(false);
        NetworkManager.Singleton.StartHost();
    }

    public void OnButtonJoin()
    {
        mainMenu.gameObject.SetActive(false);
        NetworkManager.Singleton.StartClient();
    }

    void GetNames()
    {
        //allowedNames.Add("NiggaSuker");
        //allowedNames.Add("P.DiddyLover");
        //allowedNames.Add("The Fhurer Slayer");

        dropdownNames.ClearOptions();
        dropdownNames.AddOptions(allowedNames);

        StartCoroutine(GetNamesFromServer());
    }

    public void SelectHat(int idx)
    {
        selectedSombrero = idx;
        Debug.Log("Sombrero seleccionado: " + idx);

        // Replacing the deprecated method with the recommended one
        var localPlayer = Object.FindFirstObjectByType<PlayerController>();
        if (localPlayer != null && localPlayer.IsOwner)
        {
            localPlayer.SetHatIDRpc(idx);
        }
    }

    IEnumerator GetNamesFromServer()
    {
        //Hacer una peticion de tipo GET a la API para obtener los nombres
        UnityWebRequest request = UnityWebRequest.Get(API_URL + ENDPOINT_NAMES);

        yield return request.SendWebRequest();

        if(request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Nombres obtenidos correctamente: " + request.downloadHandler.text);
            string json = request.downloadHandler.text;
            NameData nameData = JsonUtility.FromJson<NameData>(json);
            allowedNames.AddRange(nameData.names);
            dropdownNames.ClearOptions();
            dropdownNames.AddOptions(allowedNames);

        }
        else
        {
            Debug.Log("Error al obtener los nombres: " + request);
        }
    }
}
