using chadiik.devUtils;
using chadiik.algorithms;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class TestSSDT : MonoBehaviour {

	public int radius = 3;
	public float skew = 1.15f;
	public Texture2D maskTexture, ssdtTexture;

	protected void Start () {

		maskTexture = GetComponent<Renderer> ().sharedMaterial.mainTexture as Texture2D;

		ExecTime.Start ( "SSDT8" );
		StartCoroutine ( GenerationCoroutine () );
	}

	private IEnumerator GenerationCoroutine () {

		bool[] mask = SSDT8.MaskFromRedThreshold ( maskTexture );

		System.Func<SSDT8> generation = new SSDT8( mask, maskTexture.width ).Generate ( radius, skew );

		SSDT8 ssdt8;
		do {
			ssdt8 = generation ();
			yield return null;
		}
		while ( ssdt8 == null );

		ssdtTexture = ssdt8.GenerateTexture();
		GetComponent<Renderer> ().material.mainTexture = ssdtTexture;

		//SSDT8.GenerateContrours ( texture, 64 );

		ExecTime.End ( "SSDT8" );
	}

}
