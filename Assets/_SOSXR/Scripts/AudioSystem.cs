using UnityEngine;


[RequireComponent(typeof(AudioSource))]
public class AudioSystem : MonoBehaviour
{
    [SerializeField] private AudioClip m_onOurWayClip;

    [SerializeField] private AudioClip m_ranRedLightClip;
    [SerializeField] private AudioClip m_nearlyThereClip;
    [SerializeField] private AudioClip m_haveArrivedClip;
    private AudioSource _source;


    private void Awake()
    {
        _source = GetComponent<AudioSource>();
    }


    private void OnEnable()
    {
        EventsSystem.OnOurWay += PlayOnOurWay;
        EventsSystem.RanRedLight += PlayRanRedLight;
        EventsSystem.NearlyThere += PlayNearlyThere;
        EventsSystem.HaveArrived += PlayHaveArrived;
    }


    private void PlayOnOurWay()
    {
        _source.PlayClip(m_onOurWayClip);
    }


    private void PlayRanRedLight()
    {
        _source.PlayClip(m_ranRedLightClip);
    }


    private void PlayNearlyThere()
    {
        _source.PlayClip(m_nearlyThereClip);
    }


    private void PlayHaveArrived()
    {
        _source.PlayClip(m_haveArrivedClip);
    }


    private void OnDisable()
    {
        EventsSystem.OnOurWay -= PlayOnOurWay;
        EventsSystem.RanRedLight -= PlayRanRedLight;
        EventsSystem.NearlyThere -= PlayNearlyThere;
        EventsSystem.HaveArrived -= PlayHaveArrived;
    }
}