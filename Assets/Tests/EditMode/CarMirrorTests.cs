using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// RearMirrorsReflection is in the game's own assembly (Assembly-CSharp), which test assemblies cannot reference: called by reflection
public class CarMirrorTests
{
    const string SmartCar = "Assets/Prefabs/Cars/DrivableSmartCommon.prefab";

    static void SetLive(GameObject car, Transform eye, bool on) =>
        System.Type.GetType("RearMirrorsReflection, Assembly-CSharp", true).GetMethod("SetLive").Invoke(null, new object[] { car, eye, on });

    static Transform Mirrors(GameObject car) => car.transform.Find("CarLogic/ModelElements/RearMirrors");

    [Test]
    public void ReplayTurnsTheMirrorsOfTheCarSeenFromInsideOnAndOff()
    {
        var car = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SmartCar));
        var eye = new GameObject("Replay camera").transform;
        try
        {
            var mirrors = Mirrors(car);
            Assert.IsNotNull(mirrors, "the smart car has CarLogic/ModelElements/RearMirrors");
            // off as in the prefab (only a participant's own car turns them on): a replayed car's mirrors showed stale pictures
            Assert.IsFalse(mirrors.gameObject.activeSelf);

            SetLive(car, eye, true);
            Assert.IsTrue(mirrors.gameObject.activeInHierarchy);
            var cams = mirrors.GetComponentsInChildren<Camera>(true);
            Assert.AreEqual(3, cams.Length, "left, right and middle mirror cameras");
            foreach (var c in cams) { Assert.IsTrue(c.enabled); Assert.IsNotNull(c.targetTexture); }

            SetLive(car, null, false);
            Assert.IsFalse(mirrors.gameObject.activeSelf);
            foreach (var c in cams) Assert.IsFalse(c.enabled);
        }
        finally { Object.DestroyImmediate(car); Object.DestroyImmediate(eye.gameObject); }
    }

    [Test]
    public void VehiclesWithoutMirrorsAreLeftAlone()
    {
        var bike = new GameObject("Bicycle");
        try { Assert.DoesNotThrow(() => SetLive(bike, null, true)); }
        finally { Object.DestroyImmediate(bike); }
    }
}
