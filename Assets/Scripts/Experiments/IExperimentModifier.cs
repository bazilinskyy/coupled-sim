using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IExperimentModifier {
    public void SetParameter(NetworkingManager.ExperimentParameter[] experimentParameters);
}
