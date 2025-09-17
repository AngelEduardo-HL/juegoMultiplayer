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

    [Header("Hats")]
    public GameObject[] Hats;     // Asignar 3 prefabs en el Inspector
    private GameObject currentHat;
    private int _lastHat = -1;

    // Para no repetir SFX/acciones al morir
    private bool hasPlayedDeath = false;

    void Start()
    {
        playerCamera = Camera.main;
        menuManager = GameObject.Find("GameManager").GetComponent<MenuManager>();

        if (IsOwner)
        {
            menuManager.HUD.gameObject.SetActive(true);

            //Establecer nombre y sombrero iniciales desde menú
            SetNameIDRpc(menuManager.selectedNameIndex);
            SetHatIDRpc(menuManager.selectedSombrero);
        }

        if (IsClient)
        {
            // Etiqueta de nombre sobre el jugador
            PlayerLabel = Instantiate(menuManager.TemplatePlayerLabel, menuManager.HUD).GetComponent<TMP_Text>();
            PlayerLabel.gameObject.SetActive(true);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Reaccionar a cambios replicados (sombrero y vida)
        hatId.OnValueChanged += (prev, nextVal) => UpdateHat(nextVal);
        Health.OnValueChanged += OnHealthChanged;

        // Aplicar estado actual al entrar
        UpdateHat(hatId.Value);
        OnHealthChanged(Health.Value, Health.Value); // forzar refresco de HUD/estado visual
    }

    //Notifica al servidor que el jugador ha seleccionado un nombre
    [Rpc(SendTo.Server)]
    public void SetNameIDRpc(int idx)
    {
        nameId.Value = idx;
    }

    // Setter del sombrero DEBE ser Server RPC
    [Rpc(SendTo.Server)]
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
                    transform.position += new Vector3(0, 0, 1 * Time.deltaTime);
                if (Input.GetKey(KeyCode.S))
                    transform.position += new Vector3(0, 0, -1 * Time.deltaTime);
                if (Input.GetKey(KeyCode.A))
                    transform.position += new Vector3(-1 * Time.deltaTime, 0, 0);
                if (Input.GetKey(KeyCode.D))
                    transform.position += new Vector3(1 * Time.deltaTime, 0, 0);

                float mag = desiredDirection.magnitude;
                animator.SetBool("isWalking", mag > 0);
                if (mag > 0)
                {
                    Quaternion q = Quaternion.LookRotation(desiredDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, q, Time.deltaTime * 10);
                    transform.Translate(0, 0, speed * Time.deltaTime);
                }
            }

            // Disparo: Fire1 (Mouse0). Semi/Auto correctos
            bool wantsToShoot = FullAuto ? Input.GetButton("Fire1") : Input.GetButtonDown("Fire1");
            if (wantsToShoot)
            {
                FireWeaponRpc(); // Server
            }

            playerCamera.transform.position = transform.position + CameraOffset;
            playerCamera.transform.LookAt(transform.position);
        }

        // Avanza el temporizador SOLO en server (cooldown correcto)
        if (IsServer)
        {
            lastShootTimer += Time.deltaTime;
        }

        // UI de etiqueta (nombre) para todos los clientes
        if (IsClient && PlayerLabel != null)
        {
            if (menuManager.allowedNames != null && nameId.Value >= 0 && nameId.Value < menuManager.allowedNames.Count)
                PlayerLabel.text = menuManager.allowedNames[nameId.Value];

            PlayerLabel.transform.position = playerCamera.WorldToScreenPoint(transform.position + new Vector3(0, 0.8f, 0));
        }
    }

    private void OnHealthChanged(int previous, int current)
    {
        // Actualiza HUD SOLO del owner (evita que el remoto pise tu HUD)
        if (IsOwner && menuManager != null && menuManager.labelHealth != null)
        {
            menuManager.labelHealth.text = current.ToString();
        }

        // Estado visual/animación en TODOS los clientes
        if (current <= 0)
        {
            if (animator != null)
                animator.SetBool("Die", true);

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Evitar que “siga caminando”
            speed = 0f;

            // SFX una sola vez por cliente
            if (!hasPlayedDeath)
            {
                hasPlayedDeath = true;
                var au = GetComponent<AudioSource>();
                if (au != null && DeathSound != null)
                {
                    au.clip = DeathSound;
                    au.Play();
                }
            }
        }
    }

    void UpdateHat(int idx)
    {
        if (Hats == null || Hats.Length == 0) return;
        if (idx == _lastHat) return;

        _lastHat = idx;

        if (currentHat != null)
        {
            Destroy(currentHat);
            currentHat = null;
        }

        if (idx >= 0 && idx < Hats.Length && Hats[idx] != null)
        {

            Transform parent = transform;
            currentHat = Instantiate(Hats[idx], parent);
            currentHat.transform.localPosition = new Vector3(0f, 0.65f, 0f); // ajusta altura a tu rig
            currentHat.transform.localRotation = Quaternion.identity;
        }
    }

    bool insideDamageVolume;
    float insideDVCounter = 0;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("collision con " + other.name);
        DamageVolume dv = other.GetComponent<DamageVolume>();
        if (dv != null)
        {
            TakeDamage(dv.damagePerSec);
            insideDamageVolume = true;
            insideDVCounter = 0; // reinicio el contador
        }
    }

    private void OnTriggerStay(Collider other)
    {
        DamageVolume dv = other.GetComponent<DamageVolume>();
        if (dv != null)
        {
            insideDVCounter += Time.deltaTime;
            if (insideDVCounter > 1) // si ya paso un segundo
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
        // Si FireRate = disparos/segundo, tiempo mínimo entre disparos es 1/FireRate
        if (lastShootTimer < (1f / Mathf.Max(FireRate, 0.0001f))) return;

        if (ProjectilePrefab != null && WeaponSocket != null)
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
        if (isAlive())
        {
            if (!IsServer)
            {
                // Notificar al servidor si se llamó desde cliente
                TakeDamageRpc(damage);
                return;
            }

            Health.Value -= damage;

            // Ya no disparamos animación aquí; la dispara OnHealthChanged en todos
            if (Health.Value <= 0)
            {
                OnDeath();
            }

            Debug.Log("health: " + Health.Value);
        }
    }

    void OnDeath()
    {
        Debug.Log("me muero Xdxdxd");
        // El SFX visual lo maneja OnHealthChanged para todos los clientes
        var au = GetComponent<AudioSource>();
        if (au != null && DeathSound != null)
        {
            au.clip = DeathSound;
            au.Play();
        }
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
