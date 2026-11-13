using NUnit.Framework;
using UnityEngine;

// RightHandDrive is in the game's own assembly (Assembly-CSharp), which test assemblies cannot reference: called by reflection
public class RightHandDriveTests
{
    static bool Apply(GameObject car) =>
        (bool)System.Type.GetType("RightHandDrive, Assembly-CSharp", true).GetMethod("Apply").Invoke(null, new object[] { car });

    static Transform Child(Transform parent, string path, Vector3 pos)
    {
        foreach (var name in path.Split('/'))
        {
            var t = parent.Find(name);
            if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
            parent = t;
        }
        parent.localPosition = pos;
        return parent;
    }

    [Test]
    public void MovesDriverSideToTheRightAndMirrorsTheModel()
    {
        var car = new GameObject("Car");
        try
        {
            var model = Child(car.transform, "CarModel", Vector3.zero);
            var wheel = Child(car.transform, "CarLogic/ModelElements/steering wheel", new Vector3(-0.31f, 1f, 0.27f));
            wheel.localRotation = Quaternion.Euler(20, 0, 0);
            var cam = Child(car.transform, "DriverLogic/CameraPositionSet", new Vector3(-0.3f, 1.34f, -0.27f));
            var mirror = Child(car.transform, "CarLogic/ModelElements/RearMirrors/LeftMirror", new Vector3(-0.79f, -0.14f, 0.87f));
            mirror.localRotation = Quaternion.Euler(7, 352, 359);
            var passenger = Child(car.transform, "PassengerLogic/CameraPositionSet", new Vector3(0.31f, 1.27f, -0.37f));

            Assert.IsTrue(Apply(car));
            Assert.AreEqual(-1f, model.localScale.x, 1e-6f);
            Assert.AreEqual(0.31f, wheel.localPosition.x, 1e-5f);
            Assert.Less(Quaternion.Angle(wheel.localRotation, Quaternion.Euler(20, 0, 0)), 0.01f);
            Assert.AreEqual(0.3f, cam.localPosition.x, 1e-5f);
            Assert.AreEqual(-0.31f, passenger.localPosition.x, 1e-5f);
            Assert.AreEqual(0.79f, mirror.localPosition.x, 1e-5f);
            // the left mirror, turned 8 degrees towards a driver on the left, is now turned towards the right
            Assert.Less(Quaternion.Angle(mirror.localRotation, Quaternion.Euler(7, 8, 1)), 0.01f);
        }
        finally { Object.DestroyImmediate(car); }
    }

    [Test]
    public void LeavesOtherVehiclesAlone()
    {
        var bike = new GameObject("Bicycle");
        try
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("stays left-hand drive"));
            Assert.IsFalse(Apply(bike));
        }
        finally { Object.DestroyImmediate(bike); }
    }
}
