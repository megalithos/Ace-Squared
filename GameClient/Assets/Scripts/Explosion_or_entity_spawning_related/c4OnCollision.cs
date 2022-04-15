using System.Collections;
using UnityEngine;

public class c4OnCollision : MonoBehaviour
{
    Rigidbody rb;
    private Vector3 posCur;
    private Quaternion rotCur;
    bool grounded = false;
    private Light light;

    [HideInInspector]
    public bool thrown = false;
    bool collisionHappened = false;
    public AudioClip beepSound;
    AudioSource audioSrc;
    float dist = 0f;
    public GameObject c4ExplosionPrefab;
    public AudioClip explosionSound;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        light = GetComponentInChildren<Light>();
        light.enabled = false;
        audioSrc = GetComponent<AudioSource>();
        StartCoroutine(DestroyIfHitNothingIn20s());
    }

    IEnumerator DestroyIfHitNothingIn20s()
    {
        yield return new WaitForSeconds(20f);
        if (gameObject != null)
            Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.transform.CompareTag("worldterrain"))
        {
            Collide();
        }
    }

    void Collide()
    {
        if (collisionHappened)
            return;

        rb.isKinematic = true;
        collisionHappened = true;

        transform.rotation = rotCur;
        //transform.position += dist * -transform.up * 0.35f;
        RaycastHit hit;
        if (Physics.Raycast(new Ray(transform.position, -transform.up), out hit, .5f) == true && !hit.transform.CompareTag("LocalPlayer") && !hit.transform.CompareTag("Player"))
        {
            transform.position += hit.distance * 0.7f * -transform.up;
        }
        gameObject.layer = 0;
        StartCoroutine(Beep());
    }

    void SetCorrectRotation()
    {
        if (!thrown)
            return;

        //declare a new Ray. It will start at this object's position and it's direction will be straight down from the object (in local space, that is)

        Ray[] raysToCast = new Ray[]
        {
            new Ray(transform.position, -transform.up),
            new Ray(transform.position, transform.up),
            new Ray(transform.position, transform.forward),
        };

        //decalre a RaycastHit. This is neccessary so it can get "filled" with information when casting the ray below.
        RaycastHit hit;
        //cast the ray. Note the "out hit" which makes the Raycast "fill" the hit variable with information. The maximum distance the ray will go is 1.5
        if (Physics.Raycast(raysToCast[0], out hit, .5f) == true && !hit.transform.CompareTag("LocalPlayer") && !hit.transform.CompareTag("Player"))
        {
            //draw a Debug Line so we can see the ray in the scene view. Good to check if it actually does what we want. Make sure that it uses the same values as the actual Raycast. In this case, it starts at the same position, but only goes up to the point that we hit.
            //Debug.DrawLine(transform.position, hit.point, Color.green);
            //store the roation and position as they would be aligned on the surface
            rotCur = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
            posCur = new Vector3(transform.position.x, hit.point.y, transform.position.z);
        }
    }

    private void Update()
    {
        SetCorrectRotation();
    }

    public void Throw()
    {
        thrown = true;
        Debug.Log("throwed c4");
    }

    int totalBeeps = 4;
    float timeBetweenBeeps = 1f; // in seconds
    float lightFlashTime = 0.1f;
    int explosionsCount = 5;
    IEnumerator Beep()
    {
        for (int i = 0; i < totalBeeps; i++)
        {
            audioSrc.PlayOneShot(beepSound);
            light.enabled = true;
            yield return new WaitForSeconds(lightFlashTime);
            light.enabled = false;

            yield return new WaitForSeconds(timeBetweenBeeps);
        }

        // ready to explode and will receive explosion data from server soon, so
        // instantiate explosions in their correct positions.
        Vector3 vectorInWhichDirectionToSpawnExplosions = new Vector3(Mathf.Round(-transform.up.normalized.x), Mathf.Round(-transform.up.normalized.y), Mathf.Round(-transform.up.normalized.z));

        // currently explosions will delete 20 blocks in a row.
        // instantiate 4 explosions with 5 blocks in between

        gameObject.GetComponent<MeshRenderer>().enabled = false;
        for (int i = 0; i < explosionsCount; i++)
        {
            GameObject instantiated = Instantiate(c4ExplosionPrefab, transform.position + vectorInWhichDirectionToSpawnExplosions * i * 4, Quaternion.identity);
            instantiated.GetComponent<AudioSource>().PlayOneShot(explosionSound);
            yield return new WaitForSeconds(0.2f);
        }
        Destroy(gameObject);
    }
}
