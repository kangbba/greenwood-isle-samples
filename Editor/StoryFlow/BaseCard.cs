/*───────────────────────── BaseCard<T>.cs ─────────────────────*/
#if UNITY_EDITOR
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Shared behaviour for every graph node, generic over its data type so each
/// card keeps a strongly typed Data reference instead of casting from a base.
/// </summary>
public abstract class BaseCard<TData> : Node where TData : BaseFlowData
{
    public TData Data { get; protected set; }

    public Port InputPort  { get; protected set; }
    public Port OutputPort { get; protected set; }

    private System.Action _onSelect;
    private System.Action _onDelete;

    protected void Init(
        TData          data,
        bool           useInput,
        bool           useOutput,
        Vector2        size,
        System.Action  onSelect,
        System.Action  onDelete)
    {
        Data = data;

        if (useInput)
        {
            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input,
                                        Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "In";
            inputContainer.Add(InputPort);
        }

        if (useOutput)
        {
            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output,
                                         Port.Capacity.Multi, typeof(bool));
            OutputPort.portName = "Out";
            outputContainer.Add(OutputPort);
        }

        SetPosition(new Rect(Vector2.zero, size));

        _onSelect = onSelect;
        _onDelete = onDelete;

        // Selection is bound to the title bar only, so clicks on the card's own
        // controls still reach them. The event is deliberately not stopped, or
        // GraphView would lose the drag.
        if (titleContainer != null)
        {
            titleContainer.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button == 0)
                {
                    _onSelect?.Invoke();
                }
            });
        }
    }

    public void InvokeDelete() => _onDelete?.Invoke();

    public abstract void LoadFromData();
    public abstract void SaveToData();
}
#endif
