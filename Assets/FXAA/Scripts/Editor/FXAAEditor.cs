using UnityEngine;
using System.Collections;
using System;
using UnityEditor;
using AseFxaa;

namespace AseFxaa
{
	internal class AboutAffiliate : EditorWindow
	{
		public static void Init()
		{
			AboutAffiliate window = (AboutAffiliate)GetWindow( typeof( AboutAffiliate ), true, "Affiliate Link Information" );
			window.minSize = new Vector2( 400, 130 );
			window.maxSize = new Vector2( 400, 130 );
			window.Show();
		}

		public void OnGUI()
		{
			GUIStyle lableStyle = new GUIStyle( GUI.skin.label );
			lableStyle.wordWrap = true;
			lableStyle.alignment = TextAnchor.MiddleCenter;
			lableStyle.richText = true;

			GUIStyle textLink = new GUIStyle( GUI.skin.label );
			textLink.normal.textColor = EditorGUIUtility.isProSkin ? FXAAEditor.LinkColorInPro : FXAAEditor.LinkColorInPersonal;
			GUILayout.Space( 16 );
			GUILayout.Label( "By using our Publisher Affiliate link we receive a small commission for all Asset Store purchases done in the following 7 days. If you want to support free content such as the one you are using, please consider using our unique Publisher link. Thank You!", lableStyle );
			GUILayout.Space( 16 );
			GUILayout.BeginHorizontal();
			if( GUILayout.Button( "Learn more about the Unity Affiliate program <color=#4C7DFFFF>here</color>", lableStyle ) )
			{
				Help.BrowseURL( "https://unity3d.com/affiliates" );
			}
			GUILayout.EndHorizontal();
		}
	}

	[Serializable]
	internal class BannerInfo
	{
		public string imageURL;
		public string message;
		public string affiliateLink;

		public static BannerInfo CreateFromJSON( string jsonString )
		{
			return JsonUtility.FromJson<BannerInfo>( jsonString );
		}

		public BannerInfo( string i, string m, string a )
		{
			imageURL = i;
			message = m;
			affiliateLink = a;
		}
	}
}

[CustomEditor( typeof( FXAA ) )]
public class FXAAEditor : Editor
{
	private GUIStyle m_linkStyle;
	private GUIStyle m_lableStyle;
	private GUIStyle m_textLink;
	private GUIContent m_button = new GUIContent( "" );

	private bool m_initialized = false;
	private bool m_imageLoaded = false;

	private int m_maxheight = 105;
	private float m_currheight = 105;
	private float m_imageRatio = 0.23863f;

	public static Color LinkColorInPro = new Color( 0.3f, 0.5f, 1 );
	public static Color LinkColorInPersonal = new Color( 0.1f, 0.3f, 0.8f );
	private Texture2D m_fetchedImage = null;
	private Texture2D m_defaultImage;

	private string m_jsonURL = "http://amplify.pt/Banner/FxaaInfo.json";
	private BannerInfo m_info = new BannerInfo(
		"http://amplify.pt/Banner/Banner_Fxaa.jpg",
		"Like FXAA? Check out Amplify Shader Editor, award-winning node-based shader editor for Unity!",
		"https://www.assetstore.unity3d.com/en/#!/content/68570?aid=1011lPwI&pubref=FXAA" );

	IEnumerator coroutine;
	IEnumerator StartRequest( string url )
	{
		WWW www = new WWW( url );
		yield return www;
	}

	public void EditorUpdateFetchInfo()
	{
		WWW www = (WWW)coroutine.Current;
		if( !coroutine.MoveNext() )
		{
			if( !www.isDone )
			{
				coroutine.MoveNext();
			}
			else
			{
				if( string.IsNullOrEmpty( www.error ) )
				{
					m_info = BannerInfo.CreateFromJSON( www.text );
					this.Repaint();

					EditorApplication.update += EditorUpdateFetchImage;
					coroutine = StartRequest( m_info.imageURL );
				}
				EditorApplication.update -= EditorUpdateFetchInfo;
			}
		}
	}

	public void EditorUpdateFetchImage()
	{
		WWW www = (WWW)coroutine.Current;
		if( !coroutine.MoveNext() )
		{
			if( !www.isDone )
			{
				coroutine.MoveNext();
			}
			else
			{
				if( string.IsNullOrEmpty( www.error ) )
				{
					m_fetchedImage = new Texture2D( www.texture.width, www.texture.height, TextureFormat.RGB24, false, true );
					m_fetchedImage.SetPixels( www.texture.GetPixels() );
					m_fetchedImage.Apply();
					this.Repaint();
				}
				EditorApplication.update -= EditorUpdateFetchImage;
			}
		}
	}

	public override void OnInspectorGUI()
	{
		if( !m_initialized )
		{
			m_initialized = true;
			m_linkStyle = new GUIStyle( GUIStyle.none );
			m_linkStyle.alignment = TextAnchor.UpperCenter;

			m_lableStyle = new GUIStyle( GUI.skin.label );

			m_lableStyle.wordWrap = true;
			m_lableStyle.alignment = TextAnchor.MiddleLeft;
			m_defaultImage = AssetDatabase.LoadAssetAtPath<Texture2D>( AssetDatabase.GUIDToAssetPath( "dd17fcfd0f1d6b540a22a6b89c9e8b06" ) );
			m_imageRatio = (float)m_defaultImage.height / (float)m_defaultImage.width;
			m_maxheight = m_defaultImage.height;

			m_textLink = new GUIStyle( GUI.skin.label );
			m_textLink.normal.textColor = Color.white;
			m_textLink.alignment = TextAnchor.MiddleCenter;
			m_textLink.fontSize = 9;
		}

		if( m_fetchedImage != null )
		{
			m_button.image = m_fetchedImage;
			m_imageRatio = (float)m_fetchedImage.height / (float)m_fetchedImage.width;
			m_maxheight = m_fetchedImage.height;
		}
		else
		{
			m_button.image = m_defaultImage;
			m_imageRatio = (float)m_defaultImage.height / (float)m_defaultImage.width;
			m_maxheight = m_defaultImage.height;

			if( !m_imageLoaded )
			{
				m_imageLoaded = true;

				EditorApplication.update += EditorUpdateFetchInfo;
				coroutine = StartRequest( m_jsonURL );
			}
		}

		EditorGUILayout.BeginVertical( "ObjectFieldThumb" );
		{
			m_currheight = Mathf.Min( m_maxheight, ( EditorGUIUtility.currentViewWidth - 30 ) * m_imageRatio );
			Rect buttonRect = EditorGUILayout.GetControlRect( false, m_currheight );
			EditorGUIUtility.AddCursorRect( buttonRect, MouseCursor.Link );
			if( GUI.Button( buttonRect, m_button, m_linkStyle ) )
			{
				Help.BrowseURL( m_info.affiliateLink );
			}
			GUILayout.BeginHorizontal();
			GUILayout.Label( m_info.message, m_lableStyle );
			GUILayout.FlexibleSpace();
			GUILayout.BeginVertical();
			if( GUILayout.Button( "Learn More", GUILayout.Height( 25 ) ) )
			{
				Help.BrowseURL( m_info.affiliateLink );
			}
			Color cache = GUI.color;
			GUI.color = EditorGUIUtility.isProSkin ? LinkColorInPro : LinkColorInPersonal;
			if( GUILayout.Button( "Affiliate Link", m_textLink ) )
			{
				AboutAffiliate.Init();
			}
			GUI.color = cache;
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
		}
		EditorGUILayout.EndVertical();
	}
}
