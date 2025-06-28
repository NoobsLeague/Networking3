using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
/**
 * The AvatarView class is a wrapper around both a skin and speechbubble with a couple of simple methods:
 * 
 *  Move        -   moves anywhere you tell it do 
 *  SetSkin     -   takes an id, automatically mods/clamps it to a valid index in a list of skin prefabs and instantiates it
 *  Say         -   passes your text on the SpeechBubble, you can safely 'say' all incoming messages, 
 *                  the SpeechBubble auto queues and displays it.
 * 
 * For instantaneous positioning, just set the worldposition directly (probably only needed on spawning).
 * 
 * @author J.C. Wichman
 */
public class AvatarView : MonoBehaviour
{
    [Tooltip("How fast does this avatar move to the given target location")]
    public float moveSpeed = 0.05f;

    [Tooltip("This list of skin prefabs this avatar can use.")]
    [SerializeField] private List<GameObject> prefabs = null;

    [Tooltip("Optional: a small ring prefab to highlight your own avatar.")]
    [SerializeField] private GameObject ringPrefab = null;

    private GameObject _skin = null;
    private int _skinId = -1;

    private bool _moving = false;
    private Vector3 _target;

    private SpeechBubble _speechBubble;
    private Animator _animator = null;

    private GameObject _ringInstance;

    private void Awake()
    {
        //this should always be present
        _speechBubble = 
        GetComponentInChildren<SpeechBubble>();
        //this needs to be retrieved on a per skin basis
        //_animator = ...
        if (ringPrefab != null)
        {
            _ringInstance = Instantiate(ringPrefab, transform);
            _ringInstance.SetActive(false);
        }
    }

    private void Update()
    {
        //if we are moving, rotate towards the target and move towards it
        if (_moving)
        {
            Quaternion rot = Quaternion.LookRotation(_target - transform.position, Vector3.up);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, rot, 6f * Time.deltaTime);
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, _target, moveSpeed * Time.deltaTime);
            _moving = Vector3.Distance(transform.localPosition, _target) > 0.01f;
            if (!_moving) updateAnimator();
        }
    }
    /**
     * Initiate move the avatar towards the endposition with the default movement speed.
     */
    public void Move(Vector3 pEndPosition)
    {
        _target = pEndPosition;
        _moving = true;
        updateAnimator();
    }

    /**
     * Changes the current skin by replacing the current skin gameobject with a new one.
     * At this point for simplicity the AvatarView expects a certain prefab height,
     * a skin pivot point aligned at the feet of the skin and 
     * an animator with an IsWalking parameter.
     * Please check one of the provided skin prefabs.
     */
    public void SetSkin(int pSkin)
    {
        //'normalize' the skin id so we will never crash
        if (pSkin % prefabs.Count == _skinId) return;
        _skinId = Mathf.Clamp(pSkin % prefabs.Count, 0, prefabs.Count - 1);

        //bye bye current one if one exists
        if (_skin != null) Destroy(_skin);

        //create the new one and get its animator       
        _skin = Instantiate(prefabs[_skinId], transform);
        _animator = _skin.GetComponent<Animator>();
        updateAnimator();

        //throw some scaling effect in there
        _skin.transform.DOScale(1, 1).From(0.01f).SetEase(Ease.OutElastic);
    }

    /**
     * Queue the given text into the speechbubble.
     */
    public void Say(string pText) => _speechBubble.Say(pText);

    public void Remove()
    {
        _skin.transform.DOScale(0, 0.5f).SetEase(Ease.InBack);
        _speechBubble.Clear();
        Destroy(gameObject, 0.6f);
        enabled = false;
    }
	/**
     * Set the animator to walking and update its speed if required.
     */
    private void updateAnimator()
    {
        if (_animator == null) return;
        _animator.SetBool("IsWalking", _moving);
        _animator.speed = _moving ? moveSpeed : 1;
    }
    
    public void WhereMe(bool show)
    {
        if (_ringInstance != null)
        {
            _ringInstance.SetActive(show);
            Debug.Log($"[ShowRing] avatar {gameObject.name} → ring {(show ? "ON" : "OFF")}");
        }
    }
}
