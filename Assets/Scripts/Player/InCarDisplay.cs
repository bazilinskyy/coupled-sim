using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// A display inside the participant's car, described like a designed eHMI (DesignedEhmi) in
/// StreamingAssets/InCar/{Design}.json: a head-up display (HUD) on the windshield, seen about 2.5 m
/// ahead, or a centre screen on the dashboard. Its states follow the automation (AutomatedDriving):
/// MANUAL, AUTOMATED, TAKEOVER (a take-over request); elements of the state ANY are always shown.
/// Text elements can show live values: {speed} (km/h), {mode}, {time_left} (seconds left to take
/// over), {reaction} (last take-over reaction time, s), {slider} (the participant's response slider).
/// Set per role (ExperimentRoleDefinition.HudDesign, CentreScreenDesign) or with -hud / -centreScreen.
/// </summary>
public class InCarDisplay : DesignedEhmi
{
    public enum Kind { Hud, CentreScreen }
    public Kind Placement = Kind.Hud;

    AutomatedDriving _automation;
    Rigidbody _rb;
    PlayerAvatar _car;
    bool _placed;

    protected override string Folder => "InCar";

    protected override string StateKey
    {
        get
        {
            if (_automation == null && _car != null) _automation = _car.GetComponentInChildren<AutomatedDriving>(true);
            if (_automation == null || !_automation.enabled) return "MANUAL";
            switch (_automation.State)
            {
                case AutomatedDriving.Control.Automated: return "AUTOMATED";
                case AutomatedDriving.Control.TakeOverRequested: return "TAKEOVER";
                default: return "MANUAL";
            }
        }
    }

    protected override string FillText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
        var ci = CultureInfo.InvariantCulture;
        float speed = _rb != null ? Mathf.Abs(Vector3.Dot(_rb.linearVelocity, _rb.transform.forward)) * 3.6f : 0f;
        float left = _automation != null && _automation.State == AutomatedDriving.Control.TakeOverRequested
            ? Mathf.Max(0f, _automation.TimeBudget - (Time.time - _automation.RequestTime)) : 0f;
        string mode = StateKey == "AUTOMATED" ? "Automated" : StateKey == "TAKEOVER" ? "Take over" : "Manual";
        return text.Replace("{speed}", Mathf.RoundToInt(speed).ToString(ci))
                   .Replace("{mode}", mode)
                   .Replace("{time_left}", left.ToString("F1", ci))
                   .Replace("{reaction}", _automation != null && _automation.LastReactionTime >= 0 ? _automation.LastReactionTime.ToString("F2", ci) : "-")
                   .Replace("{slider}", ResponseInput.Local != null ? ResponseInput.Local.SliderValue.ToString("F0", ci) : "-");
    }

    /// <summary>Adds a display to the participant's car (it places itself once the car's camera is known).</summary>
    public static InCarDisplay Add(PlayerAvatar car, string design, Kind kind)
    {
        var go = new GameObject($"In-car display {kind} {design}");
        go.transform.SetParent(car.transform, false);
        go.layer = car.gameObject.layer;
        var d = go.AddComponent<InCarDisplay>();
        d.Design = design;
        d.Placement = kind;
        d._car = car;
        return d;
    }

    protected override void Start()
    {
        if (_car == null) _car = GetComponentInParent<PlayerAvatar>();
        if (_car != null)
        {
            _automation = _car.GetComponentInChildren<AutomatedDriving>(true);
            _rb = _car.GetComponent<Rigidbody>();
        }
        base.Start();
    }

    protected override void Update()
    {
        if (!_placed) Place();
        base.Update();
    }

    void Place()
    {
        Camera cam = null;
        foreach (var c in _car != null ? _car.GetComponentsInChildren<Camera>() : new Camera[0])
            if (c.isActiveAndEnabled && c.targetTexture == null && (cam == null || c.depth > cam.depth)) cam = c;
        if (cam == null) return;
        var car = _car.transform;
        var eye = car.InverseTransformPoint(cam.transform.position);
        Vector3 local = Placement == Kind.Hud
            ? new Vector3(eye.x, eye.y - 0.02f, eye.z + 2.5f)      // seen through the windshield, just below the horizon
            : new Vector3(0f, eye.y - 0.16f, eye.z + 0.62f);      // middle of the dashboard
        transform.localPosition = local;
        // face the driver's eyes (the panel is seen from its +z side)
        transform.rotation = Quaternion.LookRotation(cam.transform.position - transform.position, car.up);
        _placed = true;
    }
}
