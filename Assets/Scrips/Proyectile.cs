using UnityEngine;
using Unity.Netcode;

public class Proyectile : NetworkBehaviour
{
    public float lifeTime = 5f; // Tiempo de vida del proyectil
    public float speed = 10f; // Velocidad del proyectil
    public float damage = 35;

    //Quien disparo
    public PlayerController instigator;
    public Vector3 direction;

    //Efecto de disparo
    public GameObject ImpactPrefab;


    void Start()
    {
        
    }

    void Update()
    {
        lifeTime -= Time.deltaTime;

        //Desaparece el proyectil despues de cierto Tiempo
        if (lifeTime <= 0)
        {
            //Despawnea en las instancias conectadas
            GetComponent<NetworkObject>().Despawn(true);
        }

        if(IsServer)
        {
            transform.position += direction * speed * Time.deltaTime;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Collision with " + other.name);
        if (IsServer)
        {
            PlayerController pc = other.GetComponent<PlayerController>();

            if(pc != null) // instigator != pc
            {
                pc.TakeDamageRpc((int)damage);
                OnImpactRpc(); // Llama al RPC para mostrar el efecto de impacto
                GetComponent<NetworkObject>().Despawn(true); // Despawnea el proyectil
            }
        }
    }


    //Efectos de contanto
    [Rpc(SendTo.ClientsAndHost)]

    public void OnImpactRpc()
    {
        if(ImpactPrefab != null)
        {
            GameObject impact = Instantiate(ImpactPrefab, transform.position, Quaternion.identity);
            Destroy(impact, 2); // Destruye el efecto de impacto después de 2 segundos
        }
    }
}
