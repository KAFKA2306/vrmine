using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class SyncButton : UdonSharpBehaviour
{
    [SerializeField] private MeshRenderer _renderer;
    [SerializeField] private Material _onMaterial;
    [SerializeField] private Material _offMaterial;

    [UdonSynced, FieldChangeCallback(nameof(IsOn))]
    private bool _isOn;

    public bool IsOn
    {
        get => _isOn;
        set
        {
            _isOn = value;
            UpdateVisuals();
        }
    }

    public override void Interact()
    {
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        IsOn = !IsOn;
        RequestSerialization();
    }

    private void UpdateVisuals()
    {
        if (_renderer == null) return;
        _renderer.sharedMaterial = _isOn ? _onMaterial : _offMaterial;
    }
}
