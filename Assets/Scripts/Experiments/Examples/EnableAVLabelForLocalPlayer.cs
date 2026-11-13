using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnableAVLabelForLocalPlayer : MonoBehaviour, IExperimentModifier
{
    public void SetParameter(NetworkingManager.ExperimentParameter[] experimentParameters)
    {
        foreach (var param in experimentParameters) {
            if (param.name == "ego_labeled")
            {
                NetworkingManager.Instance.PlayerSystem.LocalPlayer.GetComponent<CarConfigurator>().Label.SetActive(param.value.Equals("true"));
            }
        }
    }
}
