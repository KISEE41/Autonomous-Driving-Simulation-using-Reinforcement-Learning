using UnityEngine;
using System.Collections;
using System.Linq;

public class ActivateOnCollision : MonoBehaviour {
	
	public LayerMask ignoreLayers;//Used to only allow Desired Layers to Collide wit us. (Player Layer should not be in this list)
	public Rigidbody rBody;
	public Collider triggerCol;
	public float triggerRadius = 1f;
	public AudioClip[] hitClips;
	private AudioSource aSource;
	public GameObject holder;
	private bool active = false;

	void Awake () {
		//Grab Audio Clips
		hitClips = Resources.LoadAll("HitClips", typeof(AudioClip)).Cast<AudioClip>().ToArray();
		if(hitClips.Length > 0){
			aSource = gameObject.AddComponent<AudioSource> ();
		}
		if(rBody == null){
			rBody = gameObject.GetComponent<Rigidbody> ();
			if (rBody == null) {
				rBody = gameObject.AddComponent<Rigidbody>();
			}
		}
		rBody.useGravity = false;
		rBody.isKinematic = false;
		rBody.Sleep();
	}

	void OnCollisionEnter(Collision other){
		if (!active)
		{
			active = true;
			//Debug.Log("Hit: " + name + " By: " + other.gameObject.name);
			//Update Rigidbody
			rBody.isKinematic = false;
			rBody.useGravity = true;
			if (hitClips.Length > 0)
			{
				//Play Sound
				int randomHit = Mathf.RoundToInt(Random.Range(0, hitClips.Length));
				aSource.PlayOneShot(hitClips[randomHit]);
			}
			StartCoroutine(SelfDestruct());
		}
	}

	IEnumerator SelfDestruct(){
		yield return new WaitForSeconds(5);
		if(holder){
			DestroyImmediate (holder);
		} else {
			DestroyImmediate(transform.gameObject);
		}
	}
}