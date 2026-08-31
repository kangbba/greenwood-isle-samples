using System.Collections.Generic;
using UnityEngine;

public class InteractionInfo
{
    public InteractionType Type;
    public List<Element> Elements;
    public Vector2 AnchoredPos;
    public bool UseFocus;

    public InteractionInfo(InteractionType type, Vector2 anchoredPos, bool useFocus = false, params Element[] elements)
    {
        Type = type;
        AnchoredPos = anchoredPos;
        UseFocus = useFocus;
        Elements = new List<Element>(elements ?? new Element[0]);
    }
}
