using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//allows controlling HMI state with keyboard buttons
public class eHMIShowJohn : MonoBehaviour
{
    public bool ShowDisabled = false;
    public bool ShowStop = false;
    public bool ShowWalk = false;

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
        if (ShowDisabled == true)
        {
            ChangeToState(HMIState.DISABLED);
        }
        else if (ShowStop == true)
        {
            ChangeToState(HMIState.STOP);
        }
        else if (ShowWalk == true)
        {
            ChangeToState(HMIState.WALK);
        }
    }
}
