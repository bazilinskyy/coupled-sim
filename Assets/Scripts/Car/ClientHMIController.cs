using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//allows controlling HMI state with keyboard buttons
public class ClientHMIController : MonoBehaviour
{
    PlayerAvatar _avatar;
    HMIManager _manager;
    private void Awake()
    {
        _avatar = GetComponent<PlayerAvatar>();
    }

    public void Init(HMIManager manager)
    {
        _manager = manager;
    }

    void ChangeToState(HMIState state)
    {
        var hmis = _avatar.HMISlots;
        if (hmis.TopHMI != null)
        {
            _manager.RequestHMIChange(hmis.TopHMI, state);
        }
        if (hmis.HoodHMI != null)
        {
            _manager.RequestHMIChange(hmis.HoodHMI, state);
        }
        if (hmis.WindshieldHMI != null)
        {
            _manager.RequestHMIChange(hmis.WindshieldHMI, state);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ChangeToState(HMIState.DISABLED);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ChangeToState(HMIState.STOP);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ChangeToState(HMIState.WALK);
        }
    }
}
