using UnityEngine;
using UnityEngine.UI;
using Varjo;

public class VarjoDistortedColorStreamView : MonoBehaviour {

    public RawImage leftView;
    public RawImage rightView;

    public void UpdateViews(VarjoDistortedColorStream.VarjoDistortedColorFrame frame)
    {
        leftView.texture = frame.leftTexture;
        rightView.texture = frame.rightTexture;
    }
}