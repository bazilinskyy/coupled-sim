
using System;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent( typeof( Camera ) )]
[AddComponentMenu( "Image Effects/FXAA" )]
public class FXAA : MonoBehaviour
{
	[SerializeField] private Shader shader;
	private Material mat;

	void CreateMaterialIfNeeded()
	{
		if ( shader != null && shader.isSupported )
		{
			mat = ( mat != null ) ? mat : new Material( shader );
		}
		else
		{
			Debug.LogError( "FXAA is not supported on this device." );
			enabled = false;
			return;
		}
	}

	void Start()
	{
		CreateMaterialIfNeeded();
	}

	public void OnRenderImage( RenderTexture source, RenderTexture destination )
	{
		float rcpWidth = 1.0f / Screen.width;
		float rcpHeight = 1.0f / Screen.height;

		mat.SetVector( "_rcpFrame", new Vector4( rcpWidth, rcpHeight, 0, 0 ) );
		mat.SetVector( "_rcpFrameOpt", new Vector4( rcpWidth * 2, rcpHeight * 2, rcpWidth * 0.5f, rcpHeight * 0.5f ) );

		Graphics.Blit( source, destination, mat );
	}
}



