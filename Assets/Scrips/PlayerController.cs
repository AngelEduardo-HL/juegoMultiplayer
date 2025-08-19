using Unity.Netcode;
using UnityEngine;
using System.Globalization;
using TMPro;



public class PlayerController : NetworkBehaviour
{
    Camera playerCamera;

    [Header("Weapon")]
    public GameObject ProjectilePrefab;
    //Donde aparece o se spawnea el prefab del arma
    public Transform WeaponSocket;
    public float FireRate = 2;
    float lastShootTimer = 0;
    public bool FullAuto;

    [Header("Hats")]
    public GameObject[] Hats; // Lista de accesorios de cabeza disponibles
    private GameObject currentHat; // Accesorio de cabeza actual   

    [Header("Player Settings")]
    public float speed = 2f; // velocidad de movimiento del jugador
    public Vector3 CameraOffset = new Vector3(0, 2.5f, -2);
    public Vector3 desiredDirection; // direccion deseada del jugador

    [Header("Animator")]
    public Animator animator; // Referencia al Animator del jugador

    //La salud es replicada
    NetworkVariable<int> Health = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    //Id del nombre que escogio el jugador
    NetworkVariable<int> nameId = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    //Id del accesorio de la cabeza
    NetworkVariable<int> hatId = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Sounds an FX")]
    public AudioClip DeathSound;
    public AudioClip DamageSound;
    public AudioClip AttackSound;

    [Header("HUD")]
    public MenuManager menuManager;
    private TMP_Text PlayerLabel;

    void Start()
    {
        playerCamera = Camera.main;
        menuManager = GameObject.Find("GameManager").GetComponent<MenuManager>();

        if (IsOwner)
        {
            menuManager.HUD.gameObject.SetActive(true);

            //Establecer nombre y accesorio seleccionado
            SetNameIDRpc(menuManager.selectedNameIndex); // Por defecto, el primer nombre
        }

        if (IsClient)
        {
            //copia el nombre del jugador al menu
            PlayerLabel = Instantiate(menuManager.TemplatePlayerLabel, menuManager.HUD).GetComponent<TMP_Text>();
            PlayerLabel.gameObject.SetActive(true);


        }
    }

    //Notifica al servidor que el jugador ha seleccionado un nombre
    [Rpc(SendTo.Server)]
    public void SetNameIDRpc(int idx)
    {
        nameId.Value = idx;
    }

    public void SetHatIDRpc(int idx)
    {
        hatId.Value = idx;
    }

    void Update()
    {
        if (IsOwner)
        {
            desiredDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            desiredDirection.Normalize();

            if (isAlive())
            {
                if (Input.GetKey(KeyCode.W))
                {
                    transform.position += new Vector3(0, 0, 1 * Time.deltaTime);
                }
                if (Input.GetKey(KeyCode.S))
                {
                    transform.position += new Vector3(0, 0, -1 * Time.deltaTime);
                }
                if (Input.GetKey(KeyCode.A))
                {
                    transform.position += new Vector3(-1 * Time.deltaTime, 0, 0);
                }
                if (Input.GetKey(KeyCode.D))
                {
                    transform.position += new Vector3(1 * Time.deltaTime, 0, 0);
                }

                float mag = desiredDirection.magnitude;
                //en el animator debe existir un parametro isWalking para activar la animacion de movimiento
                animator.SetBool("isWalking", mag > 0);
                if (mag > 0)
                {
                    //interpolar entre la rotacion actual y la deseada
                    Quaternion q = Quaternion.LookRotation(desiredDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, q, Time.deltaTime * 10);
                    //hay que declarar public float speed=2 mas arriba
                    transform.Translate(0, 0, speed * Time.deltaTime);
                }
            }
            if(FullAuto)
            {
                if (Input.GetButton("Fire1"))
                {
                    FireWeaponRpc();
                }
            }
            else
            {
                if (Input.GetButtonDown("Fire1"))
                {
                    FireWeaponRpc();
                }
            }

            playerCamera.transform.position = transform.position + CameraOffset;
            playerCamera.transform.LookAt(transform.position);
        }

        menuManager.labelHealth.text = "" + Health.Value;

        if (IsServer)
        {
            lastShootTimer += Time.deltaTime;
        }

        if (IsClient)
        {
            PlayerLabel.text = menuManager.allowedNames[nameId.Value];
            //Posicion de la etiqueta cerca del jugador
            PlayerLabel.transform.position = playerCamera.WorldToScreenPoint(transform.position
                + new Vector3(0, 0.8f, 0));

            //Actualiza el accesorio de la cabeza
            UpdateHat(hatId.Value);
        }
    }

    bool insideDamageVolume;
    float insideDVCounter = 0;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("collision con " + other.name);
        DamageVolume dv = other.GetComponent<DamageVolume>();
        if(dv != null)
        {
            TakeDamage(dv.damagePerSec);
            insideDamageVolume = true;
            insideDVCounter = 0; // reinicio el contador
        }
    }

    void UpdateHat(int idx)
    {
        if(Hats.Length == 0) return;

        if (currentHat != null)
        {
            Destroy(currentHat);
        }

        if (idx >= 0 && idx < Hats.Length)
        {
            currentHat = Instantiate(Hats[idx], transform.position, Quaternion.identity, transform);
            currentHat.transform.localPosition = new Vector3(0, 0.65f, 0); // Ajusta la posición del sombrero
            currentHat.transform.localRotation = Quaternion.identity; // Asegura que el sombrero esté orientado correctamente
        }   
    }

    private void OnTriggerStay(Collider other)
    {
        DamageVolume dv = other.GetComponent<DamageVolume>();
        if (dv != null)
        {
            insideDVCounter += Time.deltaTime;
            if(insideDVCounter > 1) // si ya paso un segundo
            {
                insideDVCounter = 0;
                TakeDamage(dv.damagePerSec);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("salgo de " + other.name);
        DamageVolume dv = other.GetComponent<DamageVolume>();
        if (dv != null)
        {
            insideDamageVolume = false;
        }
    }

    //Dispara el arma
    [Rpc(SendTo.Server)]
    public void FireWeaponRpc()
    {
        if (lastShootTimer < (60/FireRate)) return;

        if(ProjectilePrefab != null)
        {
            GameObject go = Instantiate(ProjectilePrefab, WeaponSocket.position, WeaponSocket.rotation);
            Proyectile proj = go.GetComponent<Proyectile>();
            proj.direction = transform.forward;
            proj.instigator = this;
            //Notifica la aparicion a los clientes
            proj.GetComponent<NetworkObject>().Spawn(true);
            lastShootTimer = 0;

            Debug.Log("Disparo en servidor");
        }
    }


    //Llamada a procedimiento remoto para que el servidor calcule la nueva salud en base al daño
    [Rpc(SendTo.Server)]

    public void TakeDamageRpc(int amount)
    {
        Debug.Log("TakeDamage en servidor");
        TakeDamage(amount);
    }

    void TakeDamage(int damage)
    {
        if(isAlive())
        {
            if(!IsServer)
            {
                //si no es lamada en un servidor, notificarlo
                TakeDamageRpc(damage);
                return;
            }

            Health.Value -= damage;
            if (Health.Value <= 0)
            {
                //pos me mori
                OnDeath();
                //Animacion de muerte
                animator.SetBool("Die", true);
            }
            Debug.Log("health: " + Health.Value);
        }
    }

    void OnDeath()
    {

        // particle.play
        //sound.play
        Debug.Log("me muero Xdxdxd");
        GetComponent<AudioSource>().clip = DeathSound;
        GetComponent<AudioSource>().Play();
    }

    public bool isAlive()
    {
        return Health.Value > 0;
    }

    public bool IsDead()
    {
        return !isAlive();
    }
}
